using static Remit.Domain.Tests.TestData;

namespace Remit.Domain.Tests;

public class SystemPropertyTests
{
    [Fact]
    public void A_property_without_measurement_spec_is_invalid()
    {
        Assert.Throws<DomainException>(() => new PropertyDefinition(
            PropertyType.Performance,
            PropertyScope.ForArea(Catalog),
            new ThresholdRule("p95_response_time", ComparisonOperator.LessOrEqual, 250, "ms"),
            null!,
            Bindingness.Must));
    }

    [Fact]
    public void Agents_cannot_define_properties()
    {
        Assert.Throws<DomainException>(() => SystemProperty.Define(
            new SystemPropertyId("E1"), ResponseTime().Definition, AcceptCache, BuildAgent, T0));
    }

    [Fact]
    public void Agent_may_adjust_should_property_at_level_4()
    {
        var property = ResponseTime(250, Bindingness.Should);
        var relaxed = Threshold("p95_response_time", ComparisonOperator.LessOrEqual, 300, "ms", Bindingness.Should);

        property.Change(relaxed, new DecisionId("D-2"), OpsAgent, AutonomyLevel.AdjustShouldProperties, T0);

        Assert.Equal(relaxed, property.Definition);
        Assert.IsType<SystemPropertyChanged>(property.UncommittedEvents[^1]);
    }

    [Fact]
    public void Agent_may_not_adjust_should_property_below_level_4()
    {
        var property = ResponseTime(250, Bindingness.Should);
        var relaxed = Threshold("p95_response_time", ComparisonOperator.LessOrEqual, 300, "ms", Bindingness.Should);

        Assert.Throws<DomainException>(() =>
            property.Change(relaxed, new DecisionId("D-2"), OpsAgent, AutonomyLevel.DecideWithinProperties, T0));
    }

    [Fact]
    public void Agent_may_never_adjust_must_property()
    {
        var property = ResponseTime();
        var relaxed = Threshold("p95_response_time", ComparisonOperator.LessOrEqual, 300, "ms");

        Assert.Throws<DomainException>(() =>
            property.Change(relaxed, new DecisionId("D-2"), OpsAgent, AutonomyLevel.AdjustShouldProperties, T0));
    }

    [Fact]
    public void Agent_may_not_turn_should_into_must()
    {
        var property = ResponseTime(250, Bindingness.Should);
        var promoted = Threshold("p95_response_time", ComparisonOperator.LessOrEqual, 250, "ms");

        Assert.Throws<DomainException>(() =>
            property.Change(promoted, new DecisionId("D-2"), OpsAgent, AutonomyLevel.AdjustShouldProperties, T0));
    }

    [Fact]
    public void State_is_rebuilt_from_events()
    {
        var property = ResponseTime();
        property.Change(Threshold("p95_response_time", ComparisonOperator.LessOrEqual, 200, "ms"),
            new DecisionId("D-2"), Tpo, AutonomyLevel.ProposeOnly, T0.AddDays(1));

        var rebuilt = SystemProperty.FromHistory(property.UncommittedEvents);

        Assert.Equal(property.Definition, rebuilt.Definition);
        Assert.Equal(2, rebuilt.Version);
        Assert.Empty(rebuilt.UncommittedEvents);
    }
}

public class AreaTests
{
    [Fact]
    public void Only_the_tpo_may_change_a_policy()
    {
        var area = Area.Define(Catalog, Shop, "Catalog and search", ProtectionNeed.Low, PolicyAt(3), Tpo, T0);

        Assert.Throws<DomainException>(() => area.ChangePolicy(PolicyAt(4), "earned", OpsAgent, T0));

        area.ChangePolicy(PolicyAt(4), "Success rate above threshold", Tpo, T0);
        Assert.Equal(AutonomyLevel.AdjustShouldProperties, area.Policy.Autonomy);
    }

    [Fact]
    public void Undefined_levels_are_rejected()
    {
        Assert.Throws<DomainException>(() => PolicyAt(5));
        Assert.Throws<DomainException>(() => PolicyAt(0));
    }
}

public class NeedTests
{
    private static Need NewNeed() => Need.Register(
        new NeedId("B-042"),
        Shop,
        "Faster response times in the online shop",
        "Customers complain about slow catalog pages in the evening.",
        [Catalog],
        "E-commerce department",
        Priority.High,
        NeedSource.TicketSystem("KUNDE-318"),
        Tpo,
        T0);

    [Fact]
    public void Need_follows_its_lifecycle()
    {
        var need = NewNeed();
        need.StartAnalysis(BuildAgent, T0);
        need.MarkProposalAvailable(BuildAgent, T0);
        need.MarkPropertiesDefined(Tpo, T0);
        need.StartImplementation(BuildAgent, T0);
        need.StartOperation(BuildAgent, T0);
        need.Close(Tpo, T0);

        Assert.Equal(NeedStatus.Closed, need.Status);
        Assert.Equal(NeedStatus.Closed, Need.FromHistory(need.UncommittedEvents).Status);
    }

    [Fact]
    public void Status_cannot_skip_steps()
    {
        var need = NewNeed();

        Assert.Throws<DomainException>(() => need.StartImplementation(BuildAgent, T0));
    }

    [Fact]
    public void Only_the_tpo_may_reject_or_define_properties()
    {
        var need = NewNeed();
        need.StartAnalysis(BuildAgent, T0);
        need.MarkProposalAvailable(BuildAgent, T0);

        Assert.Throws<DomainException>(() => need.Reject("not needed", BuildAgent, T0));
        Assert.Throws<DomainException>(() => need.MarkPropertiesDefined(BuildAgent, T0));
    }

    [Fact]
    public void Ai_may_add_affected_areas_without_duplicates()
    {
        var need = NewNeed();
        need.AddAffectedAreas([Catalog, Payment, Payment], BuildAgent, T0);

        Assert.Equal([Catalog, Payment], need.AffectedAreas);
    }
}

public class ProposalTests
{
    [Fact]
    public void Accepting_requires_exactly_the_options_required_properties()
    {
        var proposal = CacheProposal();

        var missing = proposal.MissingProperties(new OptionId("cache"), [PropertyType.Budget]);
        Assert.Equal(
            [PropertyType.Freshness, PropertyType.Exclusion, PropertyType.FailureBehavior, PropertyType.Recovery],
            missing);

        Assert.Throws<DomainException>(() =>
            proposal.Accept(new OptionId("cache"), [PropertyType.Budget], Tpo, T0));
    }

    [Fact]
    public void Accepting_with_all_required_properties_succeeds()
    {
        var proposal = CacheProposal();

        proposal.Accept(new OptionId("cache"), CacheOption().RequiredProperties, Tpo, T0);

        Assert.Equal(ProposalStatus.Accepted, proposal.Status);
        Assert.Equal(new OptionId("cache"), proposal.AcceptedOption);
    }

    [Fact]
    public void Proposals_are_created_by_agents_and_accepted_by_the_tpo()
    {
        Assert.Throws<DomainException>(() => Proposal.Create(
            new ProposalId("P-2"), new NeedId("B-042"), "analysis", [CacheOption()], new OptionId("cache"), Tpo, T0));

        var proposal = CacheProposal();
        Assert.Throws<DomainException>(() =>
            proposal.Accept(new OptionId("cdn"), [PropertyType.Freshness], BuildAgent, T0));
    }

    [Fact]
    public void Recommended_option_must_be_part_of_the_proposal()
    {
        Assert.Throws<DomainException>(() => Proposal.Create(
            new ProposalId("P-2"), new NeedId("B-042"), "analysis", [CacheOption()], new OptionId("cdn"), BuildAgent, T0));
    }

    [Fact]
    public void A_decided_proposal_cannot_be_decided_again()
    {
        var proposal = CacheProposal();
        proposal.Reject("too expensive", Tpo, T0);

        Assert.Throws<DomainException>(() =>
            proposal.Accept(new OptionId("cdn"), [PropertyType.Freshness], Tpo, T0));
    }
}

public class DecisionTests
{
    private static readonly QuestionOption[] BlackFridayOptions =
    [
        new("raise-budget-once", "Raise budget once", "Allow up to 400 EUR for November."),
        new("relax-target", "Relax target", "p95 up to 400 ms during Black Friday."),
        new("simplify-search", "Simplify search", "Turn off facets temporarily."),
    ];

    private static Decision BlackFridayQuestion() => Decision.AskTpo(
        new DecisionId("D-7"),
        DecisionReference.To(new SystemPropertyId("E1")),
        "Black Friday: target and budget cannot both be held",
        BlackFridayOptions,
        "relax-target",
        T0.AddDays(4),
        AutonomyLevel.DecideWithinProperties,
        OpsAgent,
        T0);

    [Fact]
    public void Tpo_question_needs_options_deadline_and_default()
    {
        var reference = DecisionReference.To(new SystemPropertyId("E1"));

        Assert.Throws<DomainException>(() => Decision.AskTpo(new DecisionId("D-8"), reference, "q",
            BlackFridayOptions[..1], "relax-target", T0.AddDays(1), AutonomyLevel.DecideWithinProperties, OpsAgent, T0));
        Assert.Throws<DomainException>(() => Decision.AskTpo(new DecisionId("D-8"), reference, "q",
            BlackFridayOptions, "unknown", T0.AddDays(1), AutonomyLevel.DecideWithinProperties, OpsAgent, T0));
        Assert.Throws<DomainException>(() => Decision.AskTpo(new DecisionId("D-8"), reference, "q",
            BlackFridayOptions, "relax-target", T0, AutonomyLevel.DecideWithinProperties, OpsAgent, T0));
    }

    [Fact]
    public void Tpo_answers_a_question()
    {
        var question = BlackFridayQuestion();

        question.Answer("raise-budget-once", "Black Friday revenue outweighs the cost", Tpo, T0.AddDays(1));

        Assert.Equal(QuestionStatus.Answered, question.QuestionStatus);
        Assert.Equal("raise-budget-once", question.Chosen);
        Assert.Throws<DomainException>(() => question.Answer("relax-target", "", Tpo, T0.AddDays(2)));
    }

    [Fact]
    public void Agents_cannot_answer_questions()
    {
        Assert.Throws<DomainException>(() => BlackFridayQuestion().Answer("relax-target", "", OpsAgent, T0));
    }

    [Fact]
    public void Default_option_applies_only_after_the_deadline()
    {
        var question = BlackFridayQuestion();

        Assert.False(question.ApplyDefaultIfDue(T0.AddDays(3)));
        Assert.True(question.ApplyDefaultIfDue(T0.AddDays(4)));

        Assert.Equal(QuestionStatus.DefaultApplied, question.QuestionStatus);
        Assert.Equal("relax-target", question.Chosen);
        Assert.False(question.ApplyDefaultIfDue(T0.AddDays(5)));
    }

    [Fact]
    public void Ai_decision_requires_rationale_and_at_least_level_2()
    {
        var reference = DecisionReference.To(new NeedId("B-042"));

        Assert.Throws<DomainException>(() => Decision.RecordAiDecision(new DecisionId("D-3"), reference,
            "Azure Managed Redis, West Europe", "", DecisionEffects.None, AutonomyLevel.DecideWithinProperties,
            BuildAgent, Reversibility.By("delete cache"), [], T0));
        Assert.Throws<DomainException>(() => Decision.RecordAiDecision(new DecisionId("D-3"), reference,
            "Azure Managed Redis, West Europe", "Cheapest option meeting E1 and E6", DecisionEffects.None,
            AutonomyLevel.ProposeOnly, BuildAgent, Reversibility.By("delete cache"), [], T0));

        var decision = Decision.RecordAiDecision(new DecisionId("D-3"), reference,
            "Azure Managed Redis, West Europe", "Cheapest option meeting E1 and E6",
            new DecisionEffects([new ComponentId("cache-catalog")], []), AutonomyLevel.DecideWithinProperties,
            BuildAgent, Reversibility.By("delete cache"), ["pr:42"], T0);

        Assert.Equal(DecisionKind.AiDecision, decision.Kind);
        Assert.Equal(decision.Chosen, Decision.FromHistory(decision.UncommittedEvents).Chosen);
    }

    [Fact]
    public void Open_questions_cannot_be_rated()
    {
        Assert.Throws<DomainException>(() => BlackFridayQuestion().Rate(Rating.Good, Tpo, T0));
    }
}

public class ComponentTests
{
    [Fact]
    public void Component_traces_back_to_its_decision_and_moves_through_its_lifecycle()
    {
        var component = Component.Plan(new ComponentId("cache-catalog"), Catalog, "Catalog cache",
            ComponentKind.Cache, [new ComponentId("catalog-db")], new DecisionId("D-3"), BuildAgent, T0);

        Assert.Equal(new DecisionId("D-3"), component.CreatedFrom);
        Assert.Equal(ComponentStatus.Planned, component.Status);

        component.Activate(["/subscriptions/.../redis/catalog"], new DecisionId("D-3"), T0);
        Assert.Equal(ComponentStatus.Active, component.Status);
        Assert.Throws<DomainException>(() => component.Activate([], new DecisionId("D-3"), T0));

        component.Retire(new DecisionId("D-4"), T0);
        Assert.Equal(ComponentStatus.Retired, Component.FromHistory(component.UncommittedEvents).Status);
        Assert.Throws<DomainException>(() => component.Retire(new DecisionId("D-5"), T0));
    }

    [Fact]
    public void Component_cannot_depend_on_itself()
    {
        var id = new ComponentId("cache-catalog");

        Assert.Throws<DomainException>(() => Component.Plan(id, Catalog, "Catalog cache", ComponentKind.Cache,
            [id], new DecisionId("D-3"), BuildAgent, T0));
    }
}

public class PropertyStateEvaluatorTests
{
    private static readonly ThresholdRule Target = new("p95_response_time", ComparisonOperator.LessOrEqual, 250, "ms");

    [Theory]
    [InlineData(200, null, PropertyState.Fulfilled)]
    [InlineData(200, 240.0, PropertyState.Fulfilled)]
    [InlineData(200, 400.0, PropertyState.AtRisk)]
    [InlineData(820, null, PropertyState.Violated)]
    public void State_reflects_current_value_and_forecast(double current, double? forecast, PropertyState expected)
    {
        var state = PropertyStateEvaluator.Evaluate(Target, (decimal)current, (decimal?)forecast);

        Assert.Equal(expected, state);
    }

    [Fact]
    public void At_risk_and_violated_map_to_deviation_kinds()
    {
        Assert.Equal(DeviationKind.Forecast, PropertyStateEvaluator.ToDeviationKind(PropertyState.AtRisk));
        Assert.Equal(DeviationKind.Violated, PropertyStateEvaluator.ToDeviationKind(PropertyState.Violated));
        Assert.Null(PropertyStateEvaluator.ToDeviationKind(PropertyState.Fulfilled));
    }
}
