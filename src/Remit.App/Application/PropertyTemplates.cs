using Remit.Domain;

namespace Remit.App.Application;

/// <summary>
/// How the TPO enters a property of a given type: either a number against a fixed metric, or a
/// sentence that is checked instead of measured. Each template carries the measurement spec, so a
/// property entered through it is always measurable (principle 3).
/// </summary>
public sealed record PropertyTemplate(
    PropertyType Type,
    string Label,
    string Question,
    string? Metric,
    ComparisonOperator? Operator,
    string? Unit,
    decimal? DefaultValue,
    string? DefaultStatement,
    string MeasuredBy)
{
    public bool IsNumeric => Metric is not null;
}

/// <summary>What the TPO entered for one property.</summary>
public sealed record PropertyInput(PropertyType Type, decimal? Value, string? Statement, Bindingness Bindingness);

public static class PropertyTemplates
{
    public static IReadOnlyList<PropertyTemplate> All { get; } =
    [
        new(PropertyType.Performance, "Response time", "How fast must 95 % of all requests be answered?",
            "p95_response_time", ComparisonOperator.LessOrEqual, "ms", 250, null, "Response time monitoring"),
        new(PropertyType.Availability, "Availability", "How much of the month must the system be available?",
            "availability", ComparisonOperator.GreaterOrEqual, "%", 99.9m, null, "Availability monitoring"),
        new(PropertyType.Freshness, "Freshness", "How old may data shown to users be at most?",
            "data_age", ComparisonOperator.LessOrEqual, "s", 60, null, "Age of the shown data"),
        new(PropertyType.Exclusion, "Exclusion", "What must never be served from a copy?",
            null, null, null, null, "Stock levels are never served from a copy", "Rule check before every change"),
        new(PropertyType.FailureBehavior, "Failure behavior", "What should happen if the new part fails?",
            null, null, null, null, "The system keeps working, just slower", "Regular failure experiment"),
        new(PropertyType.Recovery, "Recovery", "How quickly must the system be back after an outage?",
            "recovery_time", ComparisonOperator.LessOrEqual, "min", 15, null, "Recovery test"),
        new(PropertyType.Budget, "Budget", "How much may this cost per month at most?",
            "monthly_cost", ComparisonOperator.LessOrEqual, "EUR/month", 250, null, "Monthly cost report"),
        new(PropertyType.Location, "Data location", "Where may data be processed and stored?",
            null, null, null, null, "EU regions only", "Location check before every change"),
    ];

    public static PropertyTemplate For(PropertyType type) => All.Single(t => t.Type == type);

    public static PropertyDefinition ToDefinition(PropertyInput input, PropertyScope scope)
    {
        var template = For(input.Type);
        PropertyRule rule = template.IsNumeric
            ? new ThresholdRule(
                template.Metric!,
                template.Operator!.Value,
                input.Value ?? throw new DomainException($"{template.Label}: please enter a value."),
                template.Unit!)
            : new ConstraintRule(string.IsNullOrWhiteSpace(input.Statement)
                ? throw new DomainException($"{template.Label}: please describe the rule.")
                : input.Statement.Trim());

        return new PropertyDefinition(input.Type, scope, rule, new MeasurementSpec(template.MeasuredBy), input.Bindingness);
    }

    /// <summary>The rule in the TPO's words, e.g. "at most 250 ms".</summary>
    public static string Describe(PropertyRule rule) => rule switch
    {
        ThresholdRule t => $"{OperatorText(t.Operator)} {t.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} {t.Unit}",
        ConstraintRule c => c.Statement,
        _ => rule.ToString() ?? "",
    };

    private static string OperatorText(ComparisonOperator op) => op switch
    {
        ComparisonOperator.LessThan => "below",
        ComparisonOperator.LessOrEqual => "at most",
        ComparisonOperator.Equal => "exactly",
        ComparisonOperator.GreaterOrEqual => "at least",
        _ => "above",
    };
}
