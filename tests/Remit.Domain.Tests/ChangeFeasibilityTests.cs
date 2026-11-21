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
