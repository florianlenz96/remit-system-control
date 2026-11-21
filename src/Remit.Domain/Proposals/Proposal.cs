namespace Remit.Domain;

public enum ProposalStatus
{
    Open,
    Accepted,
    Rejected,

    /// <summary>The TPO asked the AI a question back; a revised proposal follows.</summary>
    Questioned,
}

public sealed record ExpectedEffect(string Metric, decimal Value, string Unit)
{
    public string Metric { get; } = Guard.NotBlank(Metric, nameof(Metric));

    public string Unit { get; } = Guard.NotBlank(Unit, nameof(Unit));
}

/// <summary>Cost range per month.</summary>
public sealed record CostRange
{
    public CostRange(decimal minPerMonth, decimal maxPerMonth, string currency = "EUR")
    {
        if (minPerMonth < 0 || maxPerMonth < minPerMonth)
        {
            throw new DomainException("Cost range must be non-negative and min must not exceed max.");
        }

        MinPerMonth = minPerMonth;
        MaxPerMonth = maxPerMonth;
        Currency = Guard.NotBlank(currency, nameof(currency));
    }

    public decimal MinPerMonth { get; }

    public decimal MaxPerMonth { get; }

    public string Currency { get; }
}

/// <summary>One way to satisfy a need, with effect, cost, risks and the properties the TPO must define.</summary>
public sealed record ProposalOption
{
    public ProposalOption(
        OptionId id,
        string title,
        string description,
        IEnumerable<ExpectedEffect> expectedEffects,
        CostRange cost,
        IEnumerable<string> risks,
        IEnumerable<PropertyType> requiredProperties,
        IEnumerable<AreaId> affectedAreas)
    {
        Id = id;
        Title = Guard.NotBlank(title, nameof(title));
        Description = description ?? "";
        ExpectedEffects = expectedEffects.ToList();
        Cost = cost;
        Risks = risks.ToList();
        RequiredProperties = requiredProperties.Distinct().ToList();
        AffectedAreas = Guard.NotEmpty(affectedAreas.Distinct(), nameof(affectedAreas));
    }

    public OptionId Id { get; }

    public string Title { get; }

    public string Description { get; }

    public IReadOnlyList<ExpectedEffect> ExpectedEffects { get; }

    public CostRange Cost { get; }

    public IReadOnlyList<string> Risks { get; }

    /// <summary>Property types the TPO has to define when accepting this option.</summary>
    public IReadOnlyList<PropertyType> RequiredProperties { get; }

    /// <summary>Decides which policy applies.</summary>
    public IReadOnlyList<AreaId> AffectedAreas { get; }
}

public sealed record ProposalCreated(
    ProposalId ProposalId,
    NeedId NeedId,
    string Analysis,
    IReadOnlyList<ProposalOption> Options,
    OptionId Recommended,
    Actor CreatedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ProposalAccepted(
    ProposalId ProposalId,
    OptionId OptionId,
    IReadOnlyList<PropertyType> DefinedProperties,
    Actor AcceptedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ProposalRejected(
    ProposalId ProposalId,
    string Reason,
    Actor RejectedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ProposalQuestioned(
    ProposalId ProposalId,
    string Question,
    Actor QuestionedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>AI analysis of a need with solution options and a recommendation.</summary>
public sealed class Proposal : AggregateRoot<ProposalId>
{
    private readonly List<ProposalOption> _options = [];

    private Proposal()
    {
    }

    public NeedId NeedId { get; private set; }

    public string Analysis { get; private set; } = "";

    public IReadOnlyList<ProposalOption> Options => _options;

    public OptionId Recommended { get; private set; }

    public ProposalStatus Status { get; private set; }

    public OptionId? AcceptedOption { get; private set; }

    public static Proposal Create(
        ProposalId id,
        NeedId needId,
        string analysis,
        IEnumerable<ProposalOption> options,
        OptionId recommended,
        Actor createdBy,
        DateTimeOffset at)
    {
        Guard.RequireAgent(createdBy, "create a proposal");
        var list = Guard.NotEmpty(options, nameof(options));
        if (list.Select(o => o.Id).Distinct().Count() != list.Count)
        {
            throw new DomainException("Option ids must be unique within a proposal.");
        }

        if (list.All(o => o.Id != recommended))
        {
            throw new DomainException($"Recommended option {recommended} is not part of the proposal.");
        }

        var proposal = new Proposal();
        proposal.Raise(new ProposalCreated(
            id, needId, Guard.NotBlank(analysis, nameof(analysis)), list, recommended, createdBy, at));
        return proposal;
    }

    public static Proposal FromHistory(IEnumerable<IDomainEvent> history)
    {
        var proposal = new Proposal();
        proposal.Rehydrate(history);
        return proposal;
    }

    public ProposalOption GetOption(OptionId optionId) =>
        _options.SingleOrDefault(o => o.Id == optionId)
        ?? throw new DomainException($"Option {optionId} is not part of proposal {Id}.");

    /// <summary>Required property types of the option that the TPO has not provided yet.</summary>
    public IReadOnlyList<PropertyType> MissingProperties(OptionId optionId, IEnumerable<PropertyType> provided)
    {
        var providedSet = provided.ToHashSet();
        return GetOption(optionId).RequiredProperties.Where(t => !providedSet.Contains(t)).ToList();
    }

    /// <summary>
    /// The TPO accepts an option. All property types the option requires must be defined in the same step.
    /// </summary>
    public void Accept(OptionId optionId, IEnumerable<PropertyType> definedProperties, Actor acceptedBy, DateTimeOffset at)
    {
        Guard.RequirePerson(acceptedBy, "accept a proposal");
        EnsureOpen();
        var defined = definedProperties.Distinct().ToList();
        var missing = MissingProperties(optionId, defined);
        if (missing.Count > 0)
        {
            throw new DomainException(
                $"Option {optionId} requires these properties to be defined: {string.Join(", ", missing)}.");
        }

        Raise(new ProposalAccepted(Id, optionId, defined, acceptedBy, at));
    }

    public void Reject(string reason, Actor rejectedBy, DateTimeOffset at)
    {
        Guard.RequirePerson(rejectedBy, "reject a proposal");
        EnsureOpen();
        Raise(new ProposalRejected(Id, Guard.NotBlank(reason, nameof(reason)), rejectedBy, at));
    }

    public void Question(string question, Actor questionedBy, DateTimeOffset at)
    {
        Guard.RequirePerson(questionedBy, "question a proposal");
        EnsureOpen();
        Raise(new ProposalQuestioned(Id, Guard.NotBlank(question, nameof(question)), questionedBy, at));
    }

    protected override void When(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case ProposalCreated e:
                Id = e.ProposalId;
                NeedId = e.NeedId;
                Analysis = e.Analysis;
                _options.AddRange(e.Options);
                Recommended = e.Recommended;
                Status = ProposalStatus.Open;
                break;
            case ProposalAccepted e:
                Status = ProposalStatus.Accepted;
                AcceptedOption = e.OptionId;
                break;
            case ProposalRejected:
                Status = ProposalStatus.Rejected;
                break;
            case ProposalQuestioned:
                Status = ProposalStatus.Questioned;
                break;
            default:
                throw new InvalidOperationException($"Unknown event {domainEvent.GetType().Name} for {nameof(Proposal)}.");
        }
    }

    private void EnsureOpen()
    {
        if (Status != ProposalStatus.Open)
        {
            throw new DomainException($"Proposal {Id} is {Status}, not open.");
        }
    }
}
