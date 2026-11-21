namespace Kommandozentrale.Domain;

public enum Priority
{
    High,
    Medium,
    Low,
}

public enum NeedStatus
{
    New,
    InAnalysis,
    ProposalAvailable,
    Rejected,
    PropertiesDefined,
    InImplementation,
    InOperation,
    Closed,
}

/// <summary>Where a need came from: entered manually or taken over from a ticket system.</summary>
public sealed record NeedSource(string Kind, string? ExternalId)
{
    public static NeedSource Manual { get; } = new("manual", null);

    public string Kind { get; } = Guard.NotBlank(Kind, nameof(Kind));

    public static NeedSource TicketSystem(string externalId) =>
        new("ticket-system", Guard.NotBlank(externalId, nameof(externalId)));
}

public sealed record NeedRegistered(
    NeedId NeedId,
    SystemId SystemId,
    string Title,
    string Description,
    IReadOnlyList<AreaId> AffectedAreas,
    string RequestedBy,
    Priority Priority,
    NeedSource Source,
    Actor RegisteredBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record NeedAreasAdded(
    NeedId NeedId,
    IReadOnlyList<AreaId> Areas,
    Actor AddedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record NeedStatusChanged(
    NeedId NeedId,
    NeedStatus From,
    NeedStatus To,
    string? Reason,
    Actor ChangedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// What someone needs from the system, in domain language, without a solution.
/// Lifecycle: New → InAnalysis → ProposalAvailable (→ Rejected) → PropertiesDefined →
/// InImplementation → InOperation → Closed. The properties keep applying after closing.
/// </summary>
public sealed class Need : AggregateRoot<NeedId>
{
    private static readonly Dictionary<NeedStatus, NeedStatus[]> AllowedTransitions = new()
    {
        [NeedStatus.New] = [NeedStatus.InAnalysis, NeedStatus.Rejected],
        [NeedStatus.InAnalysis] = [NeedStatus.ProposalAvailable],
        [NeedStatus.ProposalAvailable] = [NeedStatus.PropertiesDefined, NeedStatus.Rejected, NeedStatus.InAnalysis],
        [NeedStatus.PropertiesDefined] = [NeedStatus.InImplementation],
        [NeedStatus.InImplementation] = [NeedStatus.InOperation],
        [NeedStatus.InOperation] = [NeedStatus.Closed],
        [NeedStatus.Rejected] = [],
        [NeedStatus.Closed] = [],
    };

    private readonly List<AreaId> _affectedAreas = [];

    private Need()
    {
    }

    public SystemId SystemId { get; private set; }

    public string Title { get; private set; } = "";

    public string Description { get; private set; } = "";

    public IReadOnlyList<AreaId> AffectedAreas => _affectedAreas;

    public string RequestedBy { get; private set; } = "";

    public Priority Priority { get; private set; }

    public NeedSource Source { get; private set; } = NeedSource.Manual;

    public NeedStatus Status { get; private set; }

    public static Need Register(
        NeedId id,
        SystemId systemId,
        string title,
        string description,
        IEnumerable<AreaId> affectedAreas,
        string requestedBy,
        Priority priority,
        NeedSource source,
        Actor registeredBy,
        DateTimeOffset at)
    {
        var need = new Need();
        need.Raise(new NeedRegistered(
            id,
            systemId,
            Guard.NotBlank(title, nameof(title)),
            Guard.NotBlank(description, nameof(description)),
            affectedAreas.Distinct().ToList(),
            Guard.NotBlank(requestedBy, nameof(requestedBy)),
            Guard.Defined(priority, nameof(priority)),
            source,
            registeredBy,
            at));
        return need;
    }

    public static Need FromHistory(IEnumerable<IDomainEvent> history)
    {
        var need = new Need();
        need.Rehydrate(history);
        return need;
    }

    /// <summary>The AI may add areas it finds affected during analysis.</summary>
    public void AddAffectedAreas(IEnumerable<AreaId> areas, Actor addedBy, DateTimeOffset at)
    {
        var added = areas.Distinct().Where(a => !_affectedAreas.Contains(a)).ToList();
        if (added.Count > 0)
        {
            Raise(new NeedAreasAdded(Id, added, addedBy, at));
        }
    }

    public void StartAnalysis(Actor by, DateTimeOffset at) => TransitionTo(NeedStatus.InAnalysis, null, by, at);

    public void MarkProposalAvailable(Actor by, DateTimeOffset at) =>
        TransitionTo(NeedStatus.ProposalAvailable, null, by, at);

    /// <summary>The TPO sends the proposal back for another analysis round.</summary>
    public void RequestNewAnalysis(string reason, Actor by, DateTimeOffset at)
    {
        Guard.RequirePerson(by, "request a new analysis");
        TransitionTo(NeedStatus.InAnalysis, Guard.NotBlank(reason, nameof(reason)), by, at);
    }

    public void Reject(string reason, Actor by, DateTimeOffset at)
    {
        Guard.RequirePerson(by, "reject a need");
        TransitionTo(NeedStatus.Rejected, Guard.NotBlank(reason, nameof(reason)), by, at);
    }

    public void MarkPropertiesDefined(Actor by, DateTimeOffset at)
    {
        Guard.RequirePerson(by, "define the properties of a need");
        TransitionTo(NeedStatus.PropertiesDefined, null, by, at);
    }

    public void StartImplementation(Actor by, DateTimeOffset at) =>
        TransitionTo(NeedStatus.InImplementation, null, by, at);

    public void StartOperation(Actor by, DateTimeOffset at) => TransitionTo(NeedStatus.InOperation, null, by, at);

    public void Close(Actor by, DateTimeOffset at) => TransitionTo(NeedStatus.Closed, null, by, at);

    protected override void When(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case NeedRegistered e:
                Id = e.NeedId;
                SystemId = e.SystemId;
                Title = e.Title;
                Description = e.Description;
                _affectedAreas.AddRange(e.AffectedAreas);
                RequestedBy = e.RequestedBy;
                Priority = e.Priority;
                Source = e.Source;
                Status = NeedStatus.New;
                break;
            case NeedAreasAdded e:
                _affectedAreas.AddRange(e.Areas);
                break;
            case NeedStatusChanged e:
                Status = e.To;
                break;
            default:
                throw new InvalidOperationException($"Unknown event {domainEvent.GetType().Name} for {nameof(Need)}.");
        }
    }

    private void TransitionTo(NeedStatus to, string? reason, Actor by, DateTimeOffset at)
    {
        if (!AllowedTransitions[Status].Contains(to))
        {
            throw new DomainException($"Need {Id} cannot move from {Status} to {to}.");
        }

        Raise(new NeedStatusChanged(Id, Status, to, reason, by, at));
    }
}
