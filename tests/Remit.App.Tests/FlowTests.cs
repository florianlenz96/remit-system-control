using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Remit.App.Application;
using Remit.App.Persistence;
using Remit.Domain;

namespace Remit.App.Tests;

public sealed class FlowTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static RemitService NewService()
    {
        var service = new RemitService(new InMemoryEventStore(), TimeProvider.System);
        service.SeedExample();
        return service;
    }

    private static PropertyInput Input(PropertyType type, decimal? value = null) =>
        PropertyTemplates.For(type) is var t && t.IsNumeric
            ? new(type, value ?? t.DefaultValue, null, Bindingness.Must)
            : new(type, null, t.DefaultStatement, Bindingness.Must);

    private static IReadOnlyList<PropertyInput> CacheInputs(decimal budget = 250) =>
    [
        Input(PropertyType.Performance),
        Input(PropertyType.Freshness),
        Input(PropertyType.Exclusion),
        Input(PropertyType.FailureBehavior),
        Input(PropertyType.Budget, budget),
    ];

    [Fact]
    public void A_new_need_gets_a_proposal_with_at_least_two_options()
    {
        var service = NewService();

        var id = service.RegisterNeed("Search is too slow", "Searching takes several seconds.", Priority.Medium);

        var need = service.GetNeed(id);
        Assert.Equal(NeedStatus.ProposalAvailable, need.Status);
        Assert.NotNull(need.Proposal);
        Assert.True(need.Proposal.Options.Count >= 2);
        Assert.Single(need.Proposal.Options, o => o.Recommended);
    }

    [Fact]
    public void Accepting_requires_exactly_the_properties_of_the_option()
    {
        var service = NewService();

        var error = Assert.Throws<DomainException>(() =>
            service.AcceptOption("N-001", "cache", [Input(PropertyType.Performance), Input(PropertyType.Budget)]));

        Assert.Contains("Freshness", error.Message);
        Assert.Equal(NeedStatus.ProposalAvailable, service.GetNeed("N-001").Status);
        Assert.Empty(service.GetOverview().Properties);
    }

    [Fact]
    public void Within_autonomy_3_the_ai_builds_and_logs_its_decision()
    {
        var service = NewService();

        service.AcceptOption("N-001", "cache", CacheInputs());

        var need = service.GetNeed("N-001");
        Assert.Equal(NeedStatus.InOperation, need.Status);
        Assert.Equal(5, need.Properties.Count);
        var component = Assert.Single(need.Components);
        Assert.Equal(ComponentStatus.Active, component.Status);
        Assert.Contains(need.Decisions, d => d.Kind == DecisionKind.TpoDecision);
        var ai = Assert.Single(need.Decisions, d => d.Kind == DecisionKind.AiDecision);
        Assert.Equal(AutonomyLevel.DecideWithinProperties, ai.Level);
        Assert.False(string.IsNullOrWhiteSpace(ai.Rationale));
    }

    [Fact]
    public void A_budget_below_the_cost_becomes_a_tpo_question_with_default()
    {
        var service = NewService();

        service.AcceptOption("N-001", "cache", CacheInputs(budget: 100));

        var question = Assert.Single(service.GetOverview().OpenQuestions);
        Assert.Equal("keep-budget", question.DefaultOption);
        Assert.True(question.Deadline > DateTimeOffset.UtcNow);
        Assert.Equal(NeedStatus.PropertiesDefined, service.GetNeed("N-001").Status);

        service.AnswerQuestion(question.Id, "raise-budget", "Worth it");

        var need = service.GetNeed("N-001");
        Assert.Equal(NeedStatus.InOperation, need.Status);
        Assert.Equal("at most 200 EUR/month", need.Properties.Single(p => p.Type == PropertyType.Budget).Rule);
        Assert.Empty(service.GetOverview().OpenQuestions);
    }

    [Fact]
    public void At_autonomy_2_the_ai_asks_before_it_builds()
    {
        var service = NewService();
        service.ChangePolicy(AutonomyLevel.ApproveEachStep, TransparencyLevel.Decisions, "Try it carefully");

        service.AcceptOption("N-001", "cache", CacheInputs());

        var question = Assert.Single(service.GetOverview().OpenQuestions);
        Assert.Empty(service.GetNeed("N-001").Components);

        service.AnswerQuestion(question.Id, "approve", "");

        Assert.Equal(NeedStatus.InOperation, service.GetNeed("N-001").Status);
    }

    [Fact]
    public void At_autonomy_1_nothing_is_built_until_the_autonomy_is_raised()
    {
        var service = NewService();
        service.ChangePolicy(AutonomyLevel.ProposeOnly, TransparencyLevel.Result, "Only proposals for now");

        service.AcceptOption("N-001", "cache", CacheInputs());
        Assert.Equal(NeedStatus.PropertiesDefined, service.GetNeed("N-001").Status);
        Assert.Empty(Built(service));

        service.ChangePolicy(AutonomyLevel.DecideWithinProperties, TransparencyLevel.Result, "Go ahead");

        Assert.Equal(NeedStatus.InOperation, service.GetNeed("N-001").Status);
    }

    [Fact]
    public void Must_targets_of_earlier_needs_apply_to_later_options_in_the_area()
    {
        var service = NewService();
        service.AcceptOption("N-001", "cache", CacheInputs());
        var second = service.RegisterNeed("Pages load slowly", "Load time is too long.", Priority.Low);

        var error = Assert.Throws<DomainException>(() =>
            service.AcceptOption(second, "cdn", [Input(PropertyType.Performance, 500), Input(PropertyType.Budget, 300)]));

        Assert.Contains("at most 250 ms", error.Message);
    }

    [Fact]
    public void Budgets_apply_to_the_whole_area_including_what_already_runs()
    {
        var service = NewService();
        service.AcceptOption("N-001", "cache", CacheInputs(budget: 250));
        var second = service.RegisterNeed("Wish list", "Customers want to save products for later.", Priority.Low);

        // Cache (up to 200) + new building block (up to 150) = 350 EUR/month in the area, above the 250 budget.
        service.AcceptOption(second, "new-part", [Input(PropertyType.Budget, 400), Input(PropertyType.FailureBehavior)]);

        var question = Assert.Single(service.GetOverview().OpenQuestions);
        Assert.Contains("350 EUR", question.Question);
        Assert.Contains("at most 250 EUR/month", question.Question);
        Assert.DoesNotContain("400", question.Question);

        service.AnswerQuestion(question.Id, "raise-budget", "");

        var overview = service.GetOverview();
        Assert.Equal(2, overview.Components.Count(c => !c.TakenOver));
        var budgets = overview.Properties.Where(p => p.Type == PropertyType.Budget).Select(p => p.Rule).Order().ToList();
        Assert.Equal(["at most 350 EUR/month", "at most 400 EUR/month"], budgets);
        Assert.All(overview.Properties, p => Assert.NotNull(p.NeedId));
    }

    [Fact]
    public void The_budget_is_checked_against_the_upper_end_of_the_cost()
    {
        var service = NewService();

        // Cache costs 150–200 EUR/month; 175 covers the minimum but not the maximum.
        service.AcceptOption("N-001", "cache", CacheInputs(budget: 175));

        Assert.Single(service.GetOverview().OpenQuestions);
        Assert.Empty(Built(service));
    }

    [Fact]
    public void An_option_that_misses_a_must_target_is_refused_and_a_should_target_is_noted()
    {
        var service = NewService();

        // Content delivery is expected to reach about 450 ms.
        var error = Assert.Throws<DomainException>(() =>
            service.AcceptOption("N-001", "cdn", [Input(PropertyType.Performance, 250), Input(PropertyType.Budget)]));
        Assert.Contains("must apply", error.Message);
        Assert.Empty(service.GetOverview().Properties);

        service.AcceptOption("N-001", "cdn",
            [new PropertyInput(PropertyType.Performance, 250, null, Bindingness.Should), Input(PropertyType.Budget)]);

        var ai = Assert.Single(service.GetNeed("N-001").Decisions, d => d.Kind == DecisionKind.AiDecision);
        Assert.Contains("will probably not be met", ai.Rationale);
    }

    [Fact]
    public void An_answer_after_the_deadline_is_refused_and_the_default_applies()
    {
        var clock = new ManualClock();
        var service = new RemitService(new InMemoryEventStore(), clock);
        service.SeedExample();
        service.ChangePolicy(AutonomyLevel.ApproveEachStep, TransparencyLevel.Result, "Carefully");
        service.AcceptOption("N-001", "cache", CacheInputs());
        var question = Assert.Single(service.GetOverview().OpenQuestions);

        clock.Advance(TimeSpan.FromDays(4));

        var error = Assert.Throws<DomainException>(() => service.AnswerQuestion(question.Id, "approve", ""));
        Assert.Contains("deadline has passed", error.Message);
        Assert.Empty(Built(service));
        Assert.Equal(QuestionStatus.DefaultApplied, service.GetOverview().Decisions.Single(d => d.Id == question.Id).QuestionStatus);
    }

    [Fact]
    public void A_deferred_need_is_not_reopened_by_a_policy_change()
    {
        var service = NewService();
        service.AcceptOption("N-001", "cache", CacheInputs(budget: 100));
        service.AnswerQuestion(service.GetOverview().OpenQuestions.Single().Id, "keep-budget", "Not now");

        service.ChangePolicy(AutonomyLevel.DecideWithinProperties, TransparencyLevel.Decisions, "More insight");
        service.ChangePolicy(AutonomyLevel.ProposeOnly, TransparencyLevel.Decisions, "Pause");
        service.ChangePolicy(AutonomyLevel.DecideWithinProperties, TransparencyLevel.Decisions, "Go on");

        Assert.Empty(service.GetOverview().OpenQuestions);
        Assert.Equal(NeedStatus.PropertiesDefined, service.GetNeed("N-001").Status);
    }

    [Fact]
    public void Policy_changes_and_rejections_appear_in_the_decision_log()
    {
        var service = NewService();

        service.ChangePolicy(AutonomyLevel.ApproveEachStep, TransparencyLevel.Decisions, "New team");
        service.ChangePolicy(AutonomyLevel.ApproveEachStep, TransparencyLevel.Decisions, "Unchanged");
        service.RejectProposal("N-001", "Not needed this year");

        var decisions = service.GetOverview().Decisions;
        var policy = Assert.Single(decisions, d => d.Chosen!.StartsWith("Policy", StringComparison.Ordinal));
        Assert.Contains("autonomy 3 → 2", policy.Chosen);
        Assert.Equal("New team", policy.Rationale);
        var rejection = Assert.Single(service.GetNeed("N-001").Decisions);
        Assert.Equal("Not needed this year", rejection.Rationale);
    }

    [Fact]
    public void Impossible_values_are_refused_and_exact_values_are_shown_unrounded()
    {
        var service = NewService();
        var id = service.RegisterNeed("Outages", "The shop is sometimes not available.", Priority.High);

        var error = Assert.Throws<DomainException>(() => service.AcceptOption(id, "self-healing",
            [new PropertyInput(PropertyType.Availability, 150, null, Bindingness.Must), Input(PropertyType.Budget)]));
        Assert.Contains("between 0 and 100 %", error.Message);

        service.AcceptOption(id, "self-healing",
            [new PropertyInput(PropertyType.Availability, 99.9000m, null, Bindingness.Should), Input(PropertyType.Budget),
             new PropertyInput(PropertyType.Recovery, 7.5m, null, Bindingness.Must)]);
        var rules = service.GetNeed(id).Properties.Select(p => p.Rule).ToList();
        Assert.Contains("at least 99.9 %", rules);
        Assert.Contains("at most 7.5 min", rules);
        Assert.Equal("99.9999", PropertyTemplates.Number(99.9999m));
    }

    [Fact]
    public void A_failed_append_leaves_the_store_unchanged()
    {
        var store = new InMemoryEventStore();
        var need = Need.Register(new NeedId("N-1"), new SystemId("s"), "t", "d", [], "TPO", Priority.Low, NeedSource.Manual, RemitService.Tpo, DateTimeOffset.UtcNow);

        Assert.Throws<ConcurrencyException>(() => store.Append("need-N-1", 3, need.UncommittedEvents));

        Assert.Empty(store.LoadAll("need-"));
    }

    [Fact]
    public async Task The_api_serves_the_flow_and_reports_rule_violations_as_bad_request()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var created = await client.PostAsJsonAsync("/api/needs",
            new { title = "Outages in the evening", description = "The shop is sometimes not available.", priority = "High" }, Json, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(Json, ct)).GetProperty("id").GetString();

        var invalid = await client.PostAsJsonAsync($"/api/needs/{id}/accept", new { optionId = "self-healing", properties = Array.Empty<object>() }, Json, ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var accepted = await client.PostAsJsonAsync($"/api/needs/{id}/accept", new
        {
            optionId = "self-healing",
            properties = new object[]
            {
                new { type = "Availability", value = 99.9, bindingness = "Must" },
                new { type = "Budget", value = 50, bindingness = "Must" },
            },
        }, Json, ct);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);

        var need = await client.GetFromJsonAsync<JsonElement>($"/api/needs/{id}", Json, ct);
        Assert.Equal("InOperation", need.GetProperty("status").GetString());

        var missing = await client.GetAsync("/api/needs/N-999", ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var unknownType = await client.PostAsJsonAsync("/api/needs/N-001/accept", new
        {
            optionId = "cdn",
            properties = new object[]
            {
                new { type = "Performance", value = 500, bindingness = "Must" },
                new { type = "Budget", value = 100, bindingness = "Must" },
                new { type = 999, value = 1, bindingness = "Must" },
            },
        }, Json, ct);
        Assert.Equal(HttpStatusCode.BadRequest, unknownType.StatusCode);
    }

    [Fact]
    public async Task Without_the_example_the_app_still_starts_with_its_area()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("Remit:SeedExample", "false"));
        using var client = factory.CreateClient();

        var overview = await client.GetFromJsonAsync<JsonElement>("/api/overview", Json, TestContext.Current.CancellationToken);

        Assert.Equal(0, overview.GetProperty("needs").GetArrayLength());
        Assert.Equal("Catalog and search", overview.GetProperty("area").GetProperty("name").GetString());
    }

    private static IEnumerable<ComponentView> Built(RemitService service) =>
        service.GetOverview().Components.Where(c => !c.TakenOver);

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
