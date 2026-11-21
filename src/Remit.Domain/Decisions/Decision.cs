namespace Remit.Domain;

public enum DecisionKind
{
    /// <summary>Taken by an agent within its autonomy level.</summary>
    AiDecision,

    /// <summary>Taken by the TPO directly, e.g. accepting a proposal.</summary>
    TpoDecision,

    /// <summary>A decision the AI must not take itself; the TPO answers it.</summary>
    TpoQuestion,
}

public enum ReferenceKind
{
    Need,
    Proposal,
    SystemProperty,
    Component,
    Deviation,
}

/// <summary>What a decision is about. Every component traces back to a need or deviation through decisions.</summary>
public sealed record DecisionReference(ReferenceKind Kind, string Id)
{
    public ReferenceKind Kind { get; } = Guard.Defined(Kind, nameof(Kind));

    public string Id { get; } = Guard.NotBlank(Id, nameof(Id));

    public static DecisionReference To(NeedId id) => new(ReferenceKind.Need, id.Value);

    public static DecisionReference To(ProposalId id) => new(ReferenceKind.Proposal, id.Value);

    public static DecisionReference To(SystemPropertyId id) => new(ReferenceKind.SystemProperty, id.Value);

    public static DecisionReference To(ComponentId id) => new(ReferenceKind.Component, id.Value);

    public static DecisionReference To(DeviationId id) => new(ReferenceKind.Deviation, id.Value);
}

/// <summary>Components and properties changed by a decision.</summary>
public sealed record DecisionEffects(IReadOnlyList<ComponentId> Components, IReadOnlyList<SystemPropertyId> Properties)
{
    public static DecisionEffects None { get; } = new([], []);
}

public sealed record Reversibility(bool IsReversible, string? How = null)
{
    public static Reversibility Irreversible { get; } = new(false);

    public static Reversibility By(string how) => new(true, Guard.NotBlank(how, nameof(how)));
}

public sealed record QuestionOption(string Key, string Title, string Description)
{
    public string Key { get; } = Guard.NotBlank(Key, nameof(Key));

    public string Title { get; } = Guard.NotBlank(Title, nameof(Title));
}

public enum QuestionStatus
{
    Open,
    Answered,

    /// <summary>The deadline passed without an answer; the default option was applied.</summary>
    DefaultApplied,
}

public enum Rating
{
    Good,
    Neutral,
    Bad,
}

public sealed record DecisionRecorded(
    DecisionId DecisionId,
    DecisionKind Kind,
    DecisionReference Reference,
    string Chosen,
    string Rationale,
    DecisionEffects Effects,
    AutonomyLevel? Level,
    Actor DecidedBy,
    Reversibility Reversibility,
    IReadOnlyList<string> ImplementationRefs,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record TpoQuestionRaised(
    DecisionId DecisionId,
    DecisionReference Reference,
    string Question,
    IReadOnlyList<QuestionOption> Options,
    string DefaultOptionKey,
    DateTimeOffset Deadline,
    AutonomyLevel Level,
    Actor RaisedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record TpoQuestionAnswered(
    DecisionId DecisionId,
    string OptionKey,
    string Rationale,
    Actor AnsweredBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record TpoQuestionDefaultApplied(
    DecisionId DecisionId,
    string OptionKey,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DecisionRated(
    DecisionId DecisionId,
    Rating Rating,
    Actor RatedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// Every change to the system, by the AI or the TPO, with its rationale. A TPO question is a decision
/// whose answer is pending; it always has options, a deadline and a default option.
/// </summary>
public sealed class Decision : AggregateRoot<DecisionId>
{
    private readonly List<QuestionOption> _options = [];
    private readonly List<string> _implementationRefs = [];

    private Decision()
    {
    }

    public DecisionKind Kind { get; private set; }

    public DecisionReference Reference { get; private set; } = null!;

    public string? Question { get; private set; }

    public IReadOnlyList<QuestionOption> Options => _options;

    public string? Chosen { get; private set; }

    public string? Rationale { get; private set; }

    public DecisionEffects Effects { get; private set; } = DecisionEffects.None;

    public AutonomyLevel? Level { get; private set; }

    public Actor DecidedBy { get; private set; } = null!;

    public Reversibility Reversibility { get; private set; } = Reversibility.Irreversible;

    public IReadOnlyList<string> ImplementationRefs => _implementationRefs;

    public DateTimeOffset DecidedAt { get; private set; }

    public DateTimeOffset? Deadline { get; private set; }

    public string? DefaultOptionKey { get; private set; }

    public QuestionStatus? QuestionStatus { get; private set; }

    public Rating? Rating { get; private set; }

    /// <summary>An agent records a decision it took within its autonomy level.</summary>
    public static Decision RecordAiDecision(
        DecisionId id,
        DecisionReference reference,
        string chosen,
        string rationale,
        DecisionEffects effects,
        AutonomyLevel level,
        Actor agent,
        Reversibility reversibility,
        IEnumerable<string> implementationRefs,
        DateTimeOffset at)
    {
        Guard.RequireAgent(agent, "record an AI decision");
        if (Guard.Defined(level, nameof(level)) == AutonomyLevel.ProposeOnly)
        {
            throw new DomainException("At autonomy level 1 the AI only proposes; it cannot record decisions.");
        }

        var decision = new Decision();
        decision.Raise(new DecisionRecorded(
            id,
            DecisionKind.AiDecision,
            reference,
            Guard.NotBlank(chosen, nameof(chosen)),
            Guard.NotBlank(rationale, nameof(rationale)),
            effects,
            level,
            agent,
            reversibility,
            implementationRefs.ToList(),
            at));
        return decision;
    }

    /// <summary>The TPO records a decision directly, e.g. accepting an option and defining properties.</summary>
    public static Decision RecordTpoDecision(
        DecisionId id,
        DecisionReference reference,
        string chosen,
        string rationale,
        DecisionEffects effects,
        Actor tpo,
        DateTimeOffset at)
    {
        Guard.RequirePerson(tpo, "record a TPO decision");
        var decision = new Decision();
        decision.Raise(new DecisionRecorded(
            id,
            DecisionKind.TpoDecision,
            reference,
            Guard.NotBlank(chosen, nameof(chosen)),
            rationale ?? "",
            effects,
            null,
            tpo,
            Reversibility.Irreversible,
            [],
            at));
        return decision;
    }

    /// <summary>An agent asks the TPO a question it must not decide itself.</summary>
    public static Decision AskTpo(
        DecisionId id,
        DecisionReference reference,
        string question,
        IEnumerable<QuestionOption> options,
        string defaultOptionKey,
        DateTimeOffset deadline,
        AutonomyLevel level,
        Actor raisedBy,
        DateTimeOffset at)
    {
        var list = options.ToList();
        if (list.Count < 2)
        {
            throw new DomainException("A TPO question needs at least two options.");
        }

        if (list.Select(o => o.Key).Distinct().Count() != list.Count)
        {
            throw new DomainException("Option keys of a TPO question must be unique.");
        }

        if (list.All(o => o.Key != defaultOptionKey))
        {
            throw new DomainException($"Default option '{defaultOptionKey}' is not one of the options.");
        }

        if (deadline <= at)
        {
            throw new DomainException("The deadline of a TPO question must be in the future.");
        }

        var decision = new Decision();
        decision.Raise(new TpoQuestionRaised(
            id,
            reference,
            Guard.NotBlank(question, nameof(question)),
            list,
            defaultOptionKey,
            deadline,
            Guard.Defined(level, nameof(level)),
            raisedBy,
            at));
        return decision;
    }

    public static Decision FromHistory(IEnumerable<IDomainEvent> history)
    {
        var decision = new Decision();
        decision.Rehydrate(history);
        return decision;
    }

    public void Answer(string optionKey, string rationale, Actor tpo, DateTimeOffset at)
    {
        Guard.RequirePerson(tpo, "answer a TPO question");
        EnsureOpenQuestion();
        EnsureOption(optionKey);
        Raise(new TpoQuestionAnswered(Id, optionKey, rationale ?? "", tpo, at));
    }

    /// <summary>
    /// Applies the default option if the deadline has passed without an answer.
    /// Returns true if the default was applied.
    /// </summary>
    public bool ApplyDefaultIfDue(DateTimeOffset now)
    {
        if (Kind != DecisionKind.TpoQuestion || QuestionStatus != Domain.QuestionStatus.Open || now < Deadline)
        {
            return false;
        }

        Raise(new TpoQuestionDefaultApplied(Id, DefaultOptionKey!, now));
        return true;
    }

    /// <summary>Retrospective rating; feeds the success rate used for earned autonomy.</summary>
    public void Rate(Rating rating, Actor ratedBy, DateTimeOffset at)
    {
        Guard.RequirePerson(ratedBy, "rate a decision");
        if (Chosen is null)
        {
            throw new DomainException($"Decision {Id} has not been taken yet.");
        }

        Raise(new DecisionRated(Id, Guard.Defined(rating, nameof(rating)), ratedBy, at));
    }

    protected override void When(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case DecisionRecorded e:
                Id = e.DecisionId;
                Kind = e.Kind;
                Reference = e.Reference;
                Chosen = e.Chosen;
                Rationale = e.Rationale;
                Effects = e.Effects;
                Level = e.Level;
                DecidedBy = e.DecidedBy;
                Reversibility = e.Reversibility;
                _implementationRefs.AddRange(e.ImplementationRefs);
                DecidedAt = e.OccurredAt;
                break;
            case TpoQuestionRaised e:
                Id = e.DecisionId;
                Kind = DecisionKind.TpoQuestion;
                Reference = e.Reference;
                Question = e.Question;
                _options.AddRange(e.Options);
                DefaultOptionKey = e.DefaultOptionKey;
                Deadline = e.Deadline;
                Level = e.Level;
                DecidedBy = e.RaisedBy;
                QuestionStatus = Domain.QuestionStatus.Open;
                break;
            case TpoQuestionAnswered e:
                Chosen = e.OptionKey;
                Rationale = e.Rationale;
                DecidedBy = e.AnsweredBy;
                DecidedAt = e.OccurredAt;
                QuestionStatus = Domain.QuestionStatus.Answered;
                break;
            case TpoQuestionDefaultApplied e:
                Chosen = e.OptionKey;
                Rationale = "No answer before the deadline; default option applied.";
                DecidedAt = e.OccurredAt;
                QuestionStatus = Domain.QuestionStatus.DefaultApplied;
                break;
            case DecisionRated e:
                Rating = e.Rating;
                break;
            default:
                throw new InvalidOperationException($"Unknown event {domainEvent.GetType().Name} for {nameof(Decision)}.");
        }
    }

    private void EnsureOpenQuestion()
    {
        if (Kind != DecisionKind.TpoQuestion || QuestionStatus != Domain.QuestionStatus.Open)
        {
            throw new DomainException($"Decision {Id} is not an open TPO question.");
        }
    }

    private void EnsureOption(string optionKey)
    {
        if (_options.All(o => o.Key != optionKey))
        {
            throw new DomainException($"'{optionKey}' is not an option of TPO question {Id}.");
        }
    }
}
