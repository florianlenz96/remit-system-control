using static Remit.Domain.Tests.TestData;

namespace Remit.Domain.Tests;

public sealed class ChangeFeasibilityTests
{
    [Fact]
    public void An_option_whose_expected_effect_meets_the_target_has_no_unmet_properties()
    {
        Assert.Empty(ChangeFeasibility.UnmetTargets(CacheOption(), [ResponseTime(250)], T0));
    }

    [Fact]
    public void An_option_whose_expected_effect_misses_a_target_is_reported_with_that_effect()
    {
        var unmet = Assert.Single(ChangeFeasibility.UnmetTargets(CacheOption(), [ResponseTime(150)], T0));

        Assert.Equal(new SystemPropertyId("E1"), unmet.Property.Id);
        Assert.Equal(200, unmet.Effect.Value);
    }

    [Fact]
    public void Properties_of_other_areas_or_other_metrics_do_not_apply()
    {
        var otherArea = Property("E9", Threshold("p95_response_time", ComparisonOperator.LessOrEqual, 100, "ms", area: Payment));
        var otherMetric = Property("E8", Threshold("availability", ComparisonOperator.GreaterOrEqual, 99.9m, "%", type: PropertyType.Availability));

        Assert.Empty(ChangeFeasibility.UnmetTargets(CacheOption(), [otherArea, otherMetric], T0));
    }

    [Fact]
    public void Budgets_are_checked_against_the_running_cost_plus_the_upper_bound_of_the_option()
    {
        // Cache costs 150–200 EUR/month: a budget of 175 is not enough even though the minimum fits.
        Assert.Single(ChangeFeasibility.ExceededBudgets(CacheOption(), 0, [Budget(175)], T0));
        Assert.Empty(ChangeFeasibility.ExceededBudgets(CacheOption(), 0, [Budget(200)], T0));

        // 80 EUR already running in the area plus up to 200 EUR exceeds a 250 EUR budget.
        Assert.Single(ChangeFeasibility.ExceededBudgets(CacheOption(), 80, [Budget(250)], T0));
        Assert.Equal(280, ChangeFeasibility.ProjectedMonthlyCost(CacheOption(), 80));
    }

    [Fact]
    public void Retired_properties_do_not_apply()
    {
        var budget = Budget(100);
        budget.Retire(AcceptCache, Tpo, T0);

        Assert.Empty(ChangeFeasibility.ExceededBudgets(CacheOption(), 0, [budget], T0));
    }
}

public sealed class DescribedChangeTests
{
    private static readonly AreaId[] CatalogOnly = [TestData.Catalog];

    [Fact]
    public void A_change_that_keeps_all_targets_may_be_made_autonomously_at_level_3()
    {
        var properties = new[] { TestData.ResponseTime(250), TestData.Budget(250) };
        var unmet = ChangeFeasibility.UnmetTargets([new ExpectedEffect("p95_response_time", 180, "ms")], CatalogOnly, properties, TestData.T0);
        var budgets = ChangeFeasibility.ExceededBudgets(240, CatalogOnly, properties, TestData.T0);

        var change = ChangeFeasibility.ToIntendedChange(ChangeKind.TechnicalChange, [TestData.PolicyAt(3)], unmet, budgets, false);

        Assert.Equal(EscalationOutcome.Autonomous, EscalationRules.Evaluate(change).Outcome);
    }

    [Fact]
    public void Scaling_up_beyond_the_budget_goes_to_the_tpo_even_at_level_4()
    {
        var properties = new[] { TestData.Budget(250) };
        var budgets = ChangeFeasibility.ExceededBudgets(400, CatalogOnly, properties, TestData.T0);

        var change = ChangeFeasibility.ToIntendedChange(ChangeKind.TechnicalChange, [TestData.PolicyAt(4)], [], budgets, false);

        var result = EscalationRules.Evaluate(change);
        Assert.Equal(EscalationOutcome.TpoQuestion, result.Outcome);
        Assert.Contains("BudgetExceeded", result.Reason);
    }

    [Fact]
    public void Knowingly_missing_a_must_target_goes_to_the_tpo_but_a_should_target_does_not()
    {
        var effect = new ExpectedEffect("p95_response_time", 400, "ms");
        var must = ChangeFeasibility.UnmetTargets([effect], CatalogOnly, [TestData.ResponseTime(250)], TestData.T0);
        var should = ChangeFeasibility.UnmetTargets([effect], CatalogOnly, [TestData.ResponseTime(250, Bindingness.Should)], TestData.T0);

        Assert.Equal(EscalationOutcome.TpoQuestion, EscalationRules.Evaluate(
            ChangeFeasibility.ToIntendedChange(ChangeKind.TechnicalChange, [TestData.PolicyAt(3)], must, [], false)).Outcome);
        Assert.Equal(EscalationOutcome.Autonomous, EscalationRules.Evaluate(
            ChangeFeasibility.ToIntendedChange(ChangeKind.TechnicalChange, [TestData.PolicyAt(3)], should, [], false)).Outcome);
    }

    [Fact]
    public void Changing_the_data_location_always_goes_to_the_tpo()
    {
        var change = ChangeFeasibility.ToIntendedChange(ChangeKind.TechnicalChange, [TestData.PolicyAt(3)], [], [], changesDataLocation: true);

        Assert.Equal(EscalationOutcome.TpoQuestion, EscalationRules.Evaluate(change).Outcome);
    }
}
