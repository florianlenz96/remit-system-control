using Remit.Domain;

namespace Remit.App.Application;

/// <summary>A building block that existed before Remit, listed by setup (not by the TPO).</summary>
public sealed record ExistingComponent(string Name, ComponentKind Kind, string ResourceId);

public sealed record EffectInput(string Metric, decimal Value, string Unit);

public sealed record QuestionOptionInput(string Key, string Title, string Description);

/// <summary>A change an operations agent intends to make, described by its consequences.</summary>
public sealed record ChangeDescription(
    string? ResourceId,
    string Kind,
    decimal? ExtraMonthlyCost,
    decimal? CurrentMonthlyCost,
    IReadOnlyList<EffectInput>? ExpectedEffects,
    bool ChangesDataLocation);

// ---- Views for agents (MCP). They may contain resource references; the TPO never sees them. ----

public sealed record AgentPropertyView(
    string Id,
    PropertyType Type,
    string Label,
    string Rule,
    string Bindingness,
    string MeasuredBy,
    string? Metric,
    string? Operator,
    decimal? Value,
    string? Unit);

public sealed record AgentComponentView(string Id, string Name, ComponentKind Kind, bool TakenOver, IReadOnlyList<string> ResourceIds);

public sealed record AgentDeviationView(string Id, string PropertyId, DeviationKind Kind, string Description, DateTimeOffset DetectedAt, string? Measure);

public sealed record AgentQuestionView(string Id, string Question, string? DefaultOption, DateTimeOffset? Deadline);

public sealed record AgentPolicyView(int Autonomy, string AutonomyMeaning, int Transparency, string RunModeForOperationsAgent);

public sealed record RulesView(
    string AreaId,
    string AreaName,
    AgentPolicyView Policy,
    IReadOnlyList<AgentPropertyView> Properties,
    IReadOnlyList<AgentComponentView> Components,
    IReadOnlyList<AgentDeviationView> OpenDeviations,
    IReadOnlyList<AgentQuestionView> OpenQuestions,
    string HowToWork);

public sealed record BlockingProperty(string Id, string Label, string Rule, string Bindingness, string Why);

/// <summary>
/// Outcome of <c>check_change</c>: "act" (decide and report), "ask_approval" (ask the TPO to approve the step),
/// "propose_only" (do not change anything) or "ask_tpo" (the TPO must decide).
/// </summary>
public sealed record ChangeCheckView(
    string Outcome,
    int Autonomy,
    string Reason,
    decimal? ProjectedMonthlyCost,
    IReadOnlyList<BlockingProperty> Blocking,
    string Next);

public sealed record QuestionStatusView(
    string Id,
    string Status,
    string Question,
    string? ChosenOption,
    string? ChosenTitle,
    string? Rationale,
    string? DefaultOption,
    DateTimeOffset? Deadline);

public sealed record ReportedView(string Id, string Message);

// ---- Views for the TPO ----

public sealed record DeviationView(
    string Id,
    string PropertyId,
    string PropertyLabel,
    string PropertyRule,
    DeviationKind Kind,
    string Description,
    string DetectedBy,
    DateTimeOffset DetectedAt,
    bool Resolved,
    DateTimeOffset? ResolvedAt,
    string? MeasureId);
