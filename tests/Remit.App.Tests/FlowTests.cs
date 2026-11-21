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
        Assert.Empty(service.GetOverview().Components);

        service.ChangePolicy(AutonomyLevel.DecideWithinProperties, TransparencyLevel.Result, "Go ahead");

        Assert.Equal(NeedStatus.InOperation, service.GetNeed("N-001").Status);
    }

    [Fact]
    public void Properties_of_a_second_need_apply_alongside_the_first()
    {
        var service = NewService();
        service.AcceptOption("N-001", "cache", CacheInputs());
        var second = service.RegisterNeed("Pages load slowly", "Load time is too long.", Priority.Low);

        service.AcceptOption(second, "cdn", [Input(PropertyType.Performance, 200), Input(PropertyType.Budget, 100)]);

        var overview = service.GetOverview();
        Assert.Equal(7, overview.Properties.Count);
        Assert.Equal(2, overview.Components.Count);
        Assert.All(overview.Properties, p => Assert.NotNull(p.NeedId));
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
    }
}
