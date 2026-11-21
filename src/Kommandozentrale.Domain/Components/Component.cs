namespace Kommandozentrale.Domain;

public enum ComponentKind
{
    Service,
    DataStore,
    Cache,
    Interface,
    External,
}

public enum ComponentStatus
{
    Planned,
    Active,
    Retired,
}

public sealed record ComponentPlanned(
    ComponentId ComponentId,
    AreaId AreaId,
    string Name,
    ComponentKind Kind,
    IReadOnlyList<ComponentId> DependsOn,
    DecisionId CreatedFrom,
    Actor PlannedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ComponentActivated(
    ComponentId ComponentId,
    IReadOnlyList<string> ResourceRefs,
    DecisionId Decision,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ComponentRetired(
    ComponentId ComponentId,
    DecisionId Decision,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// Technical building block created by the AI. The TPO sees it on the system map but does not maintain it.
/// Every component originates from a decision (principle 5: fully traceable).
/// </summary>
public sealed class Component : AggregateRoot<ComponentId>
{
    private readonly List<ComponentId> _dependsOn = [];
    private readonly List<string> _resourceRefs = [];

    private Component()
    {
    }

    public AreaId AreaId { get; private set; }

    public string Name { get; private set; } = "";

    public ComponentKind Kind { get; private set; }

    public ComponentStatus Status { get; private set; }

    public DecisionId CreatedFrom { get; private set; }

    public IReadOnlyList<ComponentId> DependsOn => _dependsOn;

    /// <summary>Azure resource ids, as references only. Never shown to the TPO.</summary>
    public IReadOnlyList<string> ResourceRefs => _resourceRefs;

    public static Component Plan(
        ComponentId id,
        AreaId areaId,
        string name,
        ComponentKind kind,
        IEnumerable<ComponentId> dependsOn,
        DecisionId createdFrom,
        Actor plannedBy,
        DateTimeOffset at)
    {
        var dependencies = dependsOn.Distinct().ToList();
        if (dependencies.Contains(id))
        {
            throw new DomainException("A component cannot depend on itself.");
        }

        var component = new Component();
        component.Raise(new ComponentPlanned(
            id,
            areaId,
            Guard.NotBlank(name, nameof(name)),
            Guard.Defined(kind, nameof(kind)),
            dependencies,
            createdFrom,
            plannedBy,
            at));
        return component;
    }

    public static Component FromHistory(IEnumerable<IDomainEvent> history)
    {
        var component = new Component();
        component.Rehydrate(history);
        return component;
    }

    public void Activate(IEnumerable<string> resourceRefs, DecisionId decision, DateTimeOffset at)
    {
        if (Status != ComponentStatus.Planned)
        {
            throw new DomainException($"Component {Id} is {Status}; only planned components can be activated.");
        }

        Raise(new ComponentActivated(Id, resourceRefs.ToList(), decision, at));
    }

    public void Retire(DecisionId decision, DateTimeOffset at)
    {
        if (Status == ComponentStatus.Retired)
        {
            throw new DomainException($"Component {Id} is already retired.");
        }

        Raise(new ComponentRetired(Id, decision, at));
    }

    protected override void When(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case ComponentPlanned e:
                Id = e.ComponentId;
                AreaId = e.AreaId;
                Name = e.Name;
                Kind = e.Kind;
                _dependsOn.AddRange(e.DependsOn);
                CreatedFrom = e.CreatedFrom;
                Status = ComponentStatus.Planned;
                break;
            case ComponentActivated e:
                _resourceRefs.AddRange(e.ResourceRefs);
                Status = ComponentStatus.Active;
                break;
            case ComponentRetired:
                Status = ComponentStatus.Retired;
                break;
            default:
                throw new InvalidOperationException($"Unknown event {domainEvent.GetType().Name} for {nameof(Component)}.");
        }
    }
}
