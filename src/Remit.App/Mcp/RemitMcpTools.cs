using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Remit.App.Application;
using Remit.Domain;

namespace Remit.App.Mcp;

/// <summary>
/// Remit's MCP server for operations agents (ADR 0006, ADR 0009). Tool-neutral: any agent that speaks MCP
/// can use it. The rules come from the domain core; this class only translates.
/// </summary>
[McpServerToolType]
public sealed class RemitMcpTools(RemitService remit, OperationsAgent agent)
{
    [McpServerTool(Name = "get_rules", ReadOnly = true, Idempotent = true)]
    [Description("Returns what applies to a resource or to the area: the Policy (autonomy level and what it allows you), all active System properties with their rules, the known components with their resource ids, open deviations and open TPO questions. Call this first when you investigate an incident.")]
    public RulesView GetRules(
        [Description("Azure resource id of the affected resource (optional). Sub-resources are matched to their component.")] string? resourceId = null) =>
        Run(() => remit.GetRules(resourceId));

    [McpServerTool(Name = "check_change", ReadOnly = true, Idempotent = true)]
    [Description("Checks a change BEFORE you make it. Describe its consequences; Remit answers with outcome 'act' (you may do it, then report_decision), 'ask_approval' or 'ask_tpo' (use ask_tpo and wait), or 'propose_only' (do not change anything), plus the properties that block it.")]
    public ChangeCheckView CheckChange(
        [Description("Azure resource id the change is about (optional).")] string? resourceId,
        [Description("technical_change (scale, restart, configure …), adjust_should_property, adjust_must_property or change_policy.")] string kind,
        [Description("Additional cost per month in EUR the change causes; negative for savings.")] decimal? extraMonthlyCost = null,
        [Description("Current cost per month in EUR of the area, e.g. from Cost Management. If omitted, Remit uses its own estimate.")] decimal? currentMonthlyCost = null,
        [Description("Expected values after the change, e.g. metric 'p95_response_time', value 180, unit 'ms'. Use the metric and unit of the properties from get_rules.")] EffectInput[]? expectedEffects = null,
        [Description("True if data would be stored or processed in another region.")] bool changesDataLocation = false) =>
        Run(() => remit.CheckChange(new(resourceId, kind, extraMonthlyCost, currentMonthlyCost, expectedEffects, changesDataLocation)));

    [McpServerTool(Name = "report_deviation")]
    [Description("Reports that a System property is violated (Violated), will soon be violated (Forecast) or that the system no longer matches the recorded decisions (Drift). Returns the deviation id.")]
    public ReportedView ReportDeviation(
        [Description("Id of the property from get_rules, e.g. SP-001.")] string propertyId,
        [Description("Violated, Forecast or Drift.")] DeviationKind kind,
        [Description("What you observed, with the measured value, in plain language.")] string description) =>
        Run(() => remit.ReportDeviation(propertyId, kind, description, agent.Actor));

    [McpServerTool(Name = "resolve_deviation")]
    [Description("Reports that the property of a deviation is fulfilled again.")]
    public ReportedView ResolveDeviation(
        [Description("Deviation id, e.g. DV-001.")] string deviationId,
        [Description("How you know it is fulfilled again, e.g. the current measured value.")] string note) =>
        Run(() => remit.ResolveDeviation(deviationId, note, agent.Actor));

    [McpServerTool(Name = "report_decision")]
    [Description("Records a decision you took (e.g. a mitigation you applied), with rationale. Describe the change like in check_change; Remit refuses it if you were not allowed to act on your own and no answered TPO question backs it.")]
    public ReportedView ReportDecision(
        [Description("What you decided, in one sentence, e.g. 'Scaled the catalog cache to the next size'.")] string chosen,
        [Description("Why, in plain language: which property it protects and which alternatives you rejected.")] string rationale,
        [Description("technical_change, adjust_should_property, adjust_must_property or change_policy.")] string kind,
        [Description("Azure resource id the change was made on (optional).")] string? resourceId = null,
        [Description("Additional cost per month in EUR.")] decimal? extraMonthlyCost = null,
        [Description("Current cost per month in EUR of the area, if known.")] decimal? currentMonthlyCost = null,
        [Description("Expected values after the change.")] EffectInput[]? expectedEffects = null,
        [Description("True if data is now stored or processed in another region.")] bool changesDataLocation = false,
        [Description("The deviation this decision reacts to, if any.")] string? deviationId = null,
        [Description("The answered TPO question this decision is based on, if the change needed the TPO.")] string? questionId = null,
        [Description("How the change can be undone, if it can.")] string? howToUndo = null,
        [Description("Links or ids in your own tools, e.g. the incident or thread. Never shown to the TPO.")] string[]? references = null) =>
        Run(() => remit.ReportDecision(
            new(resourceId, kind, extraMonthlyCost, currentMonthlyCost, expectedEffects, changesDataLocation),
            chosen, rationale, deviationId, questionId, howToUndo, references, agent.Actor));

    [McpServerTool(Name = "ask_tpo")]
    [Description("Asks the TPO a question you must not decide yourself, e.g. when two 'must' properties conflict or a budget would be exceeded. Write in plain domain language without technical terms or resource ids. Give at least two options, a default option that applies when the deadline passes, and a deadline.")]
    public ReportedView AskTpo(
        [Description("The question, e.g. 'Black Friday: response time and budget cannot both be met'.")] string question,
        [Description("At least two options with key, title and a description of the consequence.")] QuestionOptionInput[] options,
        [Description("Key of the option that applies if the TPO does not answer in time.")] string defaultOption,
        [Description("Hours until the default option applies (1 to 720).")] int deadlineHours,
        [Description("Azure resource id the question is about (optional).")] string? resourceId = null,
        [Description("The deviation this question is about, if any.")] string? deviationId = null) =>
        Run(() => remit.AskTpoFromAgent(resourceId, question, options, defaultOption, deadlineHours, deviationId, agent.Actor));

    [McpServerTool(Name = "get_question", ReadOnly = true, Idempotent = true)]
    [Description("Returns the status of a TPO question: open, answered (with the chosen option) or default_applied.")]
    public QuestionStatusView GetQuestion([Description("Question id, e.g. D-012.")] string questionId) =>
        Run(() => remit.GetQuestion(questionId));

    /// <summary>Rule violations become readable tool errors instead of a generic failure.</summary>
    private static T Run<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (DomainException e)
        {
            throw new McpException(e.Message);
        }
        catch (NotFoundException e)
        {
            throw new McpException(e.Message);
        }
    }
}

/// <summary>The identity the connected operations agent acts as.</summary>
public sealed class OperationsAgent(string name)
{
    public Actor Actor { get; } = Actor.Agent(name);
}
