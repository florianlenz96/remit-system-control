namespace Remit.Domain;

/// <summary>A property a change is expected to miss, with the effect that misses it.</summary>
public sealed record UnmetProperty(SystemProperty Property, ExpectedEffect Effect);

/// <summary>
/// Checks a change against the system properties that apply to the areas it touches, before anything
/// is built or changed: expected effects against numeric targets, and the projected monthly cost against
/// budgets. Works for an option of a proposal and for a change an operations agent describes.
/// </summary>
public static class ChangeFeasibility
{
    /// <summary>
    /// Active numeric properties on the same metric and unit that an expected effect does not satisfy.
    /// "Must" properties among them block the change; "should" properties give way first.
    /// </summary>
    public static IReadOnlyList<UnmetProperty> UnmetTargets(
        IEnumerable<ExpectedEffect> effects, IReadOnlyCollection<AreaId> areas, IEnumerable<SystemProperty> properties, DateTimeOffset at)
    {
        var effectList = effects.ToList();
        return Applicable(areas, properties, at)
            .Where(p => p.Definition.Type != PropertyType.Budget && p.Definition.Rule is ThresholdRule)
            .SelectMany(p =>
            {
                var rule = (ThresholdRule)p.Definition.Rule;
                return effectList
                    .Where(e => string.Equals(e.Metric, rule.Metric, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(e.Unit, rule.Unit, StringComparison.OrdinalIgnoreCase) &&
                                !rule.IsSatisfiedBy(e.Value))
                    .Select(e => new UnmetProperty(p, e));
            })
            .ToList();
    }

    public static IReadOnlyList<UnmetProperty> UnmetTargets(
        ProposalOption option, IEnumerable<SystemProperty> properties, DateTimeOffset at) =>
        UnmetTargets(option.ExpectedEffects, option.AffectedAreas, properties, at);

    /// <summary>Active budget properties of the touched areas that the projected monthly cost exceeds.</summary>
    public static IReadOnlyList<SystemProperty> ExceededBudgets(
        decimal projectedMonthlyCost, IReadOnlyCollection<AreaId> areas, IEnumerable<SystemProperty> properties, DateTimeOffset at)
    {
        if (projectedMonthlyCost < 0)
        {
            throw new DomainException("Cost must not be negative.");
        }

        return Applicable(areas, properties, at)
            .Where(p => p.Definition.Type == PropertyType.Budget &&
                        p.Definition.Rule is ThresholdRule rule &&
                        !rule.IsSatisfiedBy(projectedMonthlyCost))
            .ToList();
    }

    /// <summary>
    /// Budgets an option would exceed. The projection is the cost already running in the areas plus the
    /// upper bound of the option's cost.
    /// </summary>
    public static IReadOnlyList<SystemProperty> ExceededBudgets(
        ProposalOption option, decimal runningMonthlyCost, IEnumerable<SystemProperty> properties, DateTimeOffset at)
    {
        if (runningMonthlyCost < 0)
        {
            throw new DomainException("Running cost must not be negative.");
        }

        return ExceededBudgets(ProjectedMonthlyCost(option, runningMonthlyCost), option.AffectedAreas, properties, at);
    }

    public static decimal ProjectedMonthlyCost(ProposalOption option, decimal runningMonthlyCost) =>
        runningMonthlyCost + option.Cost.MaxPerMonth;

    /// <summary>
    /// The intended change for the escalation rules: knowingly missing a "must" target or exceeding a
    /// budget always goes to the TPO (principle 2).
    /// </summary>
    public static IntendedChange ToIntendedChange(
        ChangeKind kind,
        IReadOnlyCollection<Policy> policies,
        IEnumerable<UnmetProperty> unmet,
        IEnumerable<SystemProperty> exceededBudgets,
        bool changesDataLocation)
    {
        var triggers = new List<AlwaysAskTrigger>();
        if (unmet.Any(u => u.Property.Definition.Bindingness == Bindingness.Must))
        {
            triggers.Add(AlwaysAskTrigger.MustPropertyViolated);
        }

        if (exceededBudgets.Any())
        {
            triggers.Add(AlwaysAskTrigger.BudgetExceeded);
        }

        if (changesDataLocation)
        {
            triggers.Add(AlwaysAskTrigger.DataLocationChange);
        }

        return new IntendedChange(kind, policies, triggers);
    }

    private static IEnumerable<SystemProperty> Applicable(
        IReadOnlyCollection<AreaId> areas, IEnumerable<SystemProperty> properties, DateTimeOffset at) =>
        properties.Where(p =>
            p.IsActiveAt(at) &&
            (p.Definition.Scope.Kind == ScopeKind.System ||
             (p.Definition.Scope.Kind == ScopeKind.Area && areas.Any(a => a.Value == p.Definition.Scope.TargetId))));
}
