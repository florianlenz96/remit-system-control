namespace Remit.Domain;

/// <summary>Two properties that can never be fulfilled at the same time.</summary>
public sealed record PropertyContradiction(SystemPropertyId First, SystemPropertyId Second, string Reason);

/// <summary>
/// Static check when properties are defined: numeric rules on the same metric, scope and unit whose
/// validity overlaps must leave a non-empty range of allowed values.
/// </summary>
public static class PropertyContradictionDetector
{
    public static IReadOnlyList<PropertyContradiction> Find(IEnumerable<SystemProperty> properties)
    {
        var candidates = properties
            .Where(p => !p.IsRetired && p.Definition.Rule is ThresholdRule)
            .ToList();
        var contradictions = new List<PropertyContradiction>();

        // Each rule allows an interval of values. In one dimension, a set of intervals has a common
        // point exactly when every pair intersects (Helly), so checking pairs is sufficient.
        for (var i = 0; i < candidates.Count; i++)
        {
            for (var j = i + 1; j < candidates.Count; j++)
            {
                var a = candidates[i];
                var b = candidates[j];
                var ruleA = (ThresholdRule)a.Definition.Rule;
                var ruleB = (ThresholdRule)b.Definition.Rule;

                if (a.Definition.Scope != b.Definition.Scope ||
                    !string.Equals(ruleA.Metric, ruleB.Metric, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(ruleA.Unit, ruleB.Unit, StringComparison.OrdinalIgnoreCase) ||
                    !a.Definition.Validity.Overlaps(b.Definition.Validity))
                {
                    continue;
                }

                if (Interval.Of(ruleA).Intersect(Interval.Of(ruleB)).IsEmpty)
                {
                    contradictions.Add(new(a.Id, b.Id, $"\"{ruleA}\" and \"{ruleB}\" cannot both be fulfilled."));
                }
            }
        }

        return contradictions;
    }

    private readonly record struct Interval(decimal? Lower, bool LowerInclusive, decimal? Upper, bool UpperInclusive)
    {
        public bool IsEmpty =>
            Lower is { } lo && Upper is { } hi && (lo > hi || (lo == hi && !(LowerInclusive && UpperInclusive)));

        public static Interval Of(ThresholdRule rule) => rule.Operator switch
        {
            ComparisonOperator.LessThan => new(null, false, rule.Value, false),
            ComparisonOperator.LessOrEqual => new(null, false, rule.Value, true),
            ComparisonOperator.Equal => new(rule.Value, true, rule.Value, true),
            ComparisonOperator.GreaterOrEqual => new(rule.Value, true, null, false),
            ComparisonOperator.GreaterThan => new(rule.Value, false, null, false),
            _ => throw new InvalidOperationException($"Unknown operator {rule.Operator}."),
        };

        public Interval Intersect(Interval other)
        {
            var (lower, lowerInclusive) = TighterLower(Lower, LowerInclusive, other.Lower, other.LowerInclusive);
            var (upper, upperInclusive) = TighterUpper(Upper, UpperInclusive, other.Upper, other.UpperInclusive);
            return new(lower, lowerInclusive, upper, upperInclusive);
        }

        private static (decimal?, bool) TighterLower(decimal? a, bool aInc, decimal? b, bool bInc) =>
            (a, b) switch
            {
                (null, _) => (b, bInc),
                (_, null) => (a, aInc),
                _ when a > b => (a, aInc),
                _ when b > a => (b, bInc),
                _ => (a, aInc && bInc),
            };

        private static (decimal?, bool) TighterUpper(decimal? a, bool aInc, decimal? b, bool bInc) =>
            (a, b) switch
            {
                (null, _) => (b, bInc),
                (_, null) => (a, aInc),
                _ when a < b => (a, aInc),
                _ when b < a => (b, bInc),
                _ => (a, aInc && bInc),
            };
    }
}

public enum ConflictResolutionKind
{
    /// <summary>All conflicting properties are "must": the TPO decides.</summary>
    TpoQuestion,

    /// <summary>The "should" properties give way. Whether the AI may adjust them itself is decided by <see cref="EscalationRules"/>.</summary>
    YieldShouldProperties,
}

public sealed record ConflictResolution(
    ConflictResolutionKind Kind,
    IReadOnlyList<SystemPropertyId> PropertiesToYield,
    string Reason);

/// <summary>
/// Resolves a conflict reported at runtime, e.g. by the operations agent: "target response time and
/// budget cannot both be held on Black Friday".
/// </summary>
public static class ConflictResolver
{
    public static ConflictResolution Resolve(IEnumerable<SystemProperty> conflicting, DateTimeOffset at)
    {
        var properties = conflicting.Where(p => p.IsActiveAt(at)).DistinctBy(p => p.Id).ToList();
        if (properties.Count < 2)
        {
            throw new DomainException("A conflict needs at least two properties that are active at that time.");
        }

        var should = properties.Where(p => p.Definition.Bindingness == Bindingness.Should).Select(p => p.Id).ToList();
        if (should.Count == 0)
        {
            return new(
                ConflictResolutionKind.TpoQuestion,
                [],
                "Conflicting \"must\" properties are never resolved by the AI.");
        }

        return new(
            ConflictResolutionKind.YieldShouldProperties,
            should,
            "\"Should\" properties give way first.");
    }

    /// <summary>The intended change that follows from a resolution, to be checked with <see cref="EscalationRules"/>.</summary>
    public static IntendedChange ToIntendedChange(ConflictResolution resolution, IReadOnlyCollection<Policy> affectedPolicies) =>
        resolution.Kind == ConflictResolutionKind.TpoQuestion
            ? new IntendedChange(ChangeKind.AdjustMustProperty, affectedPolicies, [AlwaysAskTrigger.MustPropertyConflict])
            : new IntendedChange(ChangeKind.AdjustShouldProperty, affectedPolicies, []);
}
