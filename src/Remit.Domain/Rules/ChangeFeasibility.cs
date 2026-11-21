namespace Remit.Domain;

/// <summary>A property an option is expected to miss, with the effect that misses it.</summary>
public sealed record UnmetProperty(SystemProperty Property, ExpectedEffect Effect);

/// <summary>
/// Checks an option against the system properties that apply to the areas it touches, before anything
/// is built: expected effects against numeric targets, and the projected monthly cost against budgets.
/// </summary>
public static class ChangeFeasibility
{
    /// <summary>
    /// Active numeric properties on the same metric and unit that the option's expected effect does not
    /// satisfy. "Must" properties among them block the option; "should" properties give way first.
    /// </summary>
    public static IReadOnlyList<UnmetProperty> UnmetTargets(
        ProposalOption option, IEnumerable<SystemProperty> properties, DateTimeOffset at) =>
        Applicable(option, properties, at)
            .Where(p => p.Definition.Type != PropertyType.Budget && p.Definition.Rule is ThresholdRule)
            .SelectMany(p =>
            {
                var rule = (ThresholdRule)p.Definition.Rule;
                return option.ExpectedEffects
                    .Where(e => string.Equals(e.Metric, rule.Metric, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(e.Unit, rule.Unit, StringComparison.OrdinalIgnoreCase) &&
                                !rule.IsSatisfiedBy(e.Value))
                    .Select(e => new UnmetProperty(p, e));
            })
            .ToList();

    /// <summary>
    /// Active budget properties of the touched areas that the projected monthly cost would exceed. The
    /// projection is the cost already running in those areas plus the upper bound of the option's cost.
    /// </summary>
    public static IReadOnlyList<SystemProperty> ExceededBudgets(
        ProposalOption option, decimal runningMonthlyCost, IEnumerable<SystemProperty> properties, DateTimeOffset at)
    {
        if (runningMonthlyCost < 0)
        {
            throw new DomainException("Running cost must not be negative.");
        }

        var projected = ProjectedMonthlyCost(option, runningMonthlyCost);
        return Applicable(option, properties, at)
            .Where(p => p.Definition.Type == PropertyType.Budget &&
                        p.Definition.Rule is ThresholdRule rule &&
                        !rule.IsSatisfiedBy(projected))
            .ToList();
    }

    public static decimal ProjectedMonthlyCost(ProposalOption option, decimal runningMonthlyCost) =>
        runningMonthlyCost + option.Cost.MaxPerMonth;

    private static IEnumerable<SystemProperty> Applicable(
        ProposalOption option, IEnumerable<SystemProperty> properties, DateTimeOffset at) =>
        properties.Where(p =>
            p.IsActiveAt(at) &&
            (p.Definition.Scope.Kind == ScopeKind.System ||
             (p.Definition.Scope.Kind == ScopeKind.Area && option.AffectedAreas.Any(a => a.Value == p.Definition.Scope.TargetId))));
}
