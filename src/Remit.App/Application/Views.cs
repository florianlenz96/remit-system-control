using Remit.Domain;

namespace Remit.App.Application;

public sealed record AreaView(string Id, string Name, AutonomyLevel Autonomy, TransparencyLevel Transparency);

public sealed record NeedSummaryView(string Id, string Title, NeedStatus Status, Priority Priority);

public sealed record EffectView(string Label, string Text);

public sealed record OptionView(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<EffectView> Effects,
    decimal CostMin,
    decimal CostMax,
    string Currency,
    IReadOnlyList<string> Risks,
    IReadOnlyList<PropertyType> RequiredProperties,
    bool Recommended);

public sealed record ProposalView(string Id, string Analysis, ProposalStatus Status, string? AcceptedOption, IReadOnlyList<OptionView> Options);

public sealed record PropertyView(
    string Id,
    PropertyType Type,
    string Label,
    string Rule,
    Bindingness Bindingness,
    string MeasuredBy,
    string? Expected,
    string? NeedId,
    DeviationKind? OpenDeviation,
    string? OpenDeviationText);

public sealed record ComponentView(string Id, string Name, ComponentKind Kind, ComponentStatus Status, string? NeedId, bool TakenOver);

public sealed record QuestionOptionView(string Key, string Title, string Description);

public sealed record DecisionView(
    string Id,
    DecisionKind Kind,
    string By,
    string? Chosen,
    string? Rationale,
    AutonomyLevel? Level,
    DateTimeOffset At,
    string? NeedId,
    string? NeedTitle,
    string? About,
    string? Question,
    IReadOnlyList<QuestionOptionView> Options,
    string? DefaultOption,
    DateTimeOffset? Deadline,
    QuestionStatus? QuestionStatus);

public sealed record OverviewView(
    string Version,
    string SystemName,
    AreaView Area,
    IReadOnlyList<NeedSummaryView> Needs,
    IReadOnlyList<PropertyView> Properties,
    IReadOnlyList<ComponentView> Components,
    IReadOnlyList<DecisionView> OpenQuestions,
    IReadOnlyList<DecisionView> Decisions,
    IReadOnlyList<DeviationView> Deviations);

public sealed record NeedView(
    string Id,
    string Title,
    string Description,
    NeedStatus Status,
    Priority Priority,
    string RequestedBy,
    ProposalView? Proposal,
    IReadOnlyList<PropertyView> Properties,
    IReadOnlyList<ComponentView> Components,
    IReadOnlyList<DecisionView> Decisions);
