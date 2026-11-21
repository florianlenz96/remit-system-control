namespace Kommandozentrale.Domain;

/// <summary>
/// Something that happened in the domain. Events are immutable and never changed once stored (ADR 0004).
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Event-sourced aggregate: state changes only by applying events. New events are collected as
/// uncommitted until the event store has persisted them.
/// </summary>
public abstract class AggregateRoot<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _uncommitted = [];

    public TId Id { get; protected set; } = default!;

    /// <summary>Number of events applied so far, including uncommitted ones.</summary>
    public int Version { get; private set; }

    public IReadOnlyList<IDomainEvent> UncommittedEvents => _uncommitted;

    public void MarkCommitted() => _uncommitted.Clear();

    protected void Raise(IDomainEvent domainEvent)
    {
        Apply(domainEvent);
        _uncommitted.Add(domainEvent);
    }

    protected void Rehydrate(IEnumerable<IDomainEvent> history)
    {
        foreach (var domainEvent in history)
        {
            Apply(domainEvent);
        }
    }

    protected abstract void When(IDomainEvent domainEvent);

    private void Apply(IDomainEvent domainEvent)
    {
        When(domainEvent);
        Version++;
    }
}
