using Remit.Domain;

namespace Remit.App.Application;

/// <summary>One log search alert on Application Insights, derived from a System property (ADR 0010).</summary>
/// <param name="Signal">"responseTimeP95" or "availability"; the Bicep module knows the query for each.</param>
/// <param name="Operator">When the alert fires, e.g. GreaterThan for "p95 at most 250 ms".</param>
public sealed record LogAlertSpec(
    string Name,
    string PropertyId,
    string Description,
    string Signal,
    string Operator,
    decimal Threshold,
    int WindowMinutes,
    int Severity);

public sealed record BudgetSpec(string Name, string PropertyId, decimal Amount);

public sealed record NotCovered(string PropertyId, string Label, string Rule, string CheckedBy);

public sealed record AlertPlan(IReadOnlyList<LogAlertSpec> LogAlerts, IReadOnlyList<BudgetSpec> Budgets, IReadOnlyList<NotCovered> NotCovered);

/// <summary>
/// Maps measurable properties to Azure Monitor signals. The mapping is product-specific and therefore lives
/// here, not in the domain core.
/// </summary>
public static class AlertRules
{
    private const int DefaultWindowMinutes = 15;

    public static AlertPlan Derive(IEnumerable<SystemProperty> properties)
    {
        var logAlerts = new List<LogAlertSpec>();
        var budgets = new List<BudgetSpec>();
        var notCovered = new List<NotCovered>();

        foreach (var p in properties)
        {
            var label = PropertyTemplates.For(p.Definition.Type).Label;
            var ruleText = PropertyTemplates.Describe(p.Definition.Rule);
            var name = $"remit-{p.Id.Value.ToLowerInvariant()}-{p.Definition.Type.ToString().ToLowerInvariant()}";
            var description =
                $"Remit property {p.Id}: {label} {ruleText} ({p.Definition.Bindingness.ToString().ToLowerInvariant()}). " +
                "Before acting, call get_rules and check_change on Remit's MCP server.";
            var window = p.Definition.Measurement.Window is { } w ? (int)Math.Max(5, w.TotalMinutes) : DefaultWindowMinutes;
            var severity = p.Definition.Bindingness == Bindingness.Must ? 1 : 3;

            switch (p.Definition.Type, p.Definition.Rule)
            {
                case (PropertyType.Performance, ThresholdRule { Metric: "p95_response_time", Unit: "ms" } rule):
                    logAlerts.Add(new(name, p.Id.Value, description, "responseTimeP95", Violation(rule.Operator), rule.Value, window, severity));
                    break;
                case (PropertyType.Availability, ThresholdRule { Metric: "availability", Unit: "%" } rule):
                    logAlerts.Add(new(name, p.Id.Value, description, "availability", Violation(rule.Operator), rule.Value, window, severity));
                    break;
                case (PropertyType.Budget, ThresholdRule { Operator: ComparisonOperator.LessOrEqual or ComparisonOperator.LessThan } rule):
                    budgets.Add(new(name, p.Id.Value, rule.Value));
                    break;
                default:
                    notCovered.Add(new(p.Id.Value, label, ruleText, p.Definition.Measurement.Source));
                    break;
            }
        }

        return new(logAlerts, budgets, notCovered);
    }

    /// <summary>ARM deployment parameters for <c>infra/modules/property-alerts.bicep</c>.</summary>
    public static object ToParametersFile(AlertPlan plan) => new Dictionary<string, object>
    {
        ["$schema"] = "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
        ["contentVersion"] = "1.0.0.0",
        ["parameters"] = new Dictionary<string, object>
        {
            ["logAlerts"] = new { value = plan.LogAlerts },
            ["budgets"] = new { value = plan.Budgets },
        },
    };

    /// <summary>The comparison at which the alert fires: the opposite of what the property allows.</summary>
    private static string Violation(ComparisonOperator allowed) => allowed switch
    {
        ComparisonOperator.LessOrEqual => "GreaterThan",
        ComparisonOperator.LessThan => "GreaterThanOrEqual",
        ComparisonOperator.GreaterOrEqual => "LessThan",
        ComparisonOperator.GreaterThan => "LessThanOrEqual",
        _ => "NotEquals",
    };
}
