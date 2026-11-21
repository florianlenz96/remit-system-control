using static Remit.Domain.Tests.TestData;

namespace Remit.Domain.Tests;

public class PropertyContradictionDetectorTests
{
    private static SystemProperty P(string id, ComparisonOperator op, decimal value, Validity? validity = null,
        string unit = "ms", AreaId? area = null) =>
        Property(id, Threshold("p95_response_time", op, value, unit, validity: validity, area: area));

    [Fact]
    public void Disjoint_ranges_on_the_same_metric_are_a_contradiction()
    {
        var result = PropertyContradictionDetector.Find([
            P("E1", ComparisonOperator.LessOrEqual, 250),
            P("E2", ComparisonOperator.GreaterOrEqual, 300),
        ]);

        var contradiction = Assert.Single(result);
        Assert.Equal(new SystemPropertyId("E1"), contradiction.First);
        Assert.Equal(new SystemPropertyId("E2"), contradiction.Second);
    }

    [Theory]
    [InlineData(ComparisonOperator.LessOrEqual, ComparisonOperator.GreaterOrEqual, false)]
    [InlineData(ComparisonOperator.LessThan, ComparisonOperator.GreaterOrEqual, true)]
    [InlineData(ComparisonOperator.LessOrEqual, ComparisonOperator.GreaterThan, true)]
    [InlineData(ComparisonOperator.Equal, ComparisonOperator.LessThan, true)]
    [InlineData(ComparisonOperator.Equal, ComparisonOperator.Equal, false)]
    public void Touching_bounds_respect_inclusiveness(ComparisonOperator first, ComparisonOperator second, bool contradicts)
    {
        var result = PropertyContradictionDetector.Find([P("E1", first, 250), P("E2", second, 250)]);

        Assert.Equal(contradicts, result.Count == 1);
    }

    [Fact]
    public void Overlapping_ranges_are_compatible()
    {
        var result = PropertyContradictionDetector.Find([
            P("E1", ComparisonOperator.LessOrEqual, 250),
            P("E2", ComparisonOperator.GreaterOrEqual, 100),
            P("E3", ComparisonOperator.LessThan, 400),
        ]);

        Assert.Empty(result);
    }

    [Fact]
    public void Different_scope_unit_or_validity_is_not_compared()
    {
        var november = new Validity(T0, T0.AddDays(7));
        var december = new Validity(T0.AddDays(10), T0.AddDays(20));

        var result = PropertyContradictionDetector.Find([
            P("E1", ComparisonOperator.LessOrEqual, 250, november),
            P("E2", ComparisonOperator.GreaterOrEqual, 300, december),
            P("E3", ComparisonOperator.GreaterOrEqual, 300, unit: "s"),
            P("E4", ComparisonOperator.GreaterOrEqual, 300, area: Payment),
        ]);

        Assert.Empty(result);
    }

    [Fact]
    public void Retired_properties_are_ignored()
    {
        var retired = P("E2", ComparisonOperator.GreaterOrEqual, 300);
        retired.Retire(new DecisionId("D-9"), Tpo, T0);

        Assert.Empty(PropertyContradictionDetector.Find([P("E1", ComparisonOperator.LessOrEqual, 250), retired]));
    }
}

public class ConflictResolverTests
{
    [Fact]
    public void Two_colliding_must_properties_always_become_a_tpo_question()
    {
        // Black Friday: target response time and budget cannot both be held.
        var resolution = ConflictResolver.Resolve([ResponseTime(), Budget()], T0);

        Assert.Equal(ConflictResolutionKind.TpoQuestion, resolution.Kind);
        Assert.Empty(resolution.PropertiesToYield);

        var escalation = EscalationRules.Evaluate(ConflictResolver.ToIntendedChange(resolution, [PolicyAt(4)]));
        Assert.Equal(EscalationOutcome.TpoQuestion, escalation.Outcome);
    }

    [Fact]
    public void Should_property_yields_to_must_property()
    {
        var resolution = ConflictResolver.Resolve([ResponseTime(), Budget(bindingness: Bindingness.Should)], T0);

        Assert.Equal(ConflictResolutionKind.YieldShouldProperties, resolution.Kind);
        Assert.Equal([new SystemPropertyId("E5")], resolution.PropertiesToYield);
    }

    [Theory]
    [InlineData(3, EscalationOutcome.TpoQuestion)]
    [InlineData(4, EscalationOutcome.Autonomous)]
    public void Ai_may_relax_the_should_property_only_at_level_4(int level, EscalationOutcome expected)
    {
        var resolution = ConflictResolver.Resolve([ResponseTime(), Budget(bindingness: Bindingness.Should)], T0);

        var escalation = EscalationRules.Evaluate(ConflictResolver.ToIntendedChange(resolution, [PolicyAt(level)]));

        Assert.Equal(expected, escalation.Outcome);
    }

    [Fact]
    public void Properties_not_active_at_that_time_do_not_count()
    {
        var expired = Property("E7", Threshold("cost", ComparisonOperator.LessOrEqual, 400, "EUR/month",
            validity: new Validity(T0.AddDays(-10), T0.AddDays(-1))));

        Assert.Throws<DomainException>(() => ConflictResolver.Resolve([ResponseTime(), expired], T0));
    }
}
