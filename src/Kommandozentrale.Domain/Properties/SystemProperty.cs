namespace Kommandozentrale.Domain;

public sealed record SystemPropertyDefined(
    SystemPropertyId PropertyId,
    PropertyDefinition Definition,
    DecisionId OriginatingDecision,
    Actor DefinedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record SystemPropertyChanged(
    SystemPropertyId PropertyId,
    PropertyDefinition Previous,
    PropertyDefinition Definition,
    DecisionId Decision,
    Actor ChangedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record SystemPropertyRetired(
    SystemPropertyId PropertyId,
    DecisionId Decision,
    Actor RetiredBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// System property: a measurable rule that applies permanently until the TPO changes it.
/// Ownership stays with the TPO; the only exception is autonomy level 4, where an agent may adjust
/// "should" properties (principle 2).
/// </summary>
public sealed class SystemProperty : AggregateRoot<SystemPropertyId>
{
    private SystemProperty()
    {
    }

    public PropertyDefinition Definition { get; private set; } = null!;

    public DecisionId OriginatingDecision { get; private set; }

    public bool IsRetired { get; private set; }

    public static SystemProperty Define(
        SystemPropertyId id,
        PropertyDefinition definition,
        DecisionId originatingDecision,
        Actor definedBy,
        DateTimeOffset at)
    {
        Guard.RequirePerson(definedBy, "define a system property");
        var property = new SystemProperty();
        property.Raise(new SystemPropertyDefined(id, definition, originatingDecision, definedBy, at));
        return property;
    }

    public static SystemProperty FromHistory(IEnumerable<IDomainEvent> history)
    {
        var property = new SystemProperty();
        property.Rehydrate(history);
        return property;
    }

    public bool IsActiveAt(DateTimeOffset at) => !IsRetired && Definition.Validity.IsActiveAt(at);

    /// <param name="autonomyInScope">Autonomy level of the policy that applies to the property's scope.</param>
    public void Change(
        PropertyDefinition definition,
        DecisionId decision,
        Actor changedBy,
        AutonomyLevel autonomyInScope,
        DateTimeOffset at)
    {
        EnsureNotRetired();
        if (!changedBy.IsPerson)
        {
            EnsureAgentMayChange(definition, autonomyInScope);
        }

        if (definition == Definition)
        {
            return;
        }

        Raise(new SystemPropertyChanged(Id, Definition, definition, decision, changedBy, at));
    }

    public void Retire(DecisionId decision, Actor retiredBy, DateTimeOffset at)
    {
        Guard.RequirePerson(retiredBy, "retire a system property");
        EnsureNotRetired();
        Raise(new SystemPropertyRetired(Id, decision, retiredBy, at));
    }

    protected override void When(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case SystemPropertyDefined e:
                Id = e.PropertyId;
                Definition = e.Definition;
                OriginatingDecision = e.OriginatingDecision;
                break;
            case SystemPropertyChanged e:
                Definition = e.Definition;
                break;
            case SystemPropertyRetired:
                IsRetired = true;
                break;
            default:
                throw new InvalidOperationException($"Unknown event {domainEvent.GetType().Name} for {nameof(SystemProperty)}.");
        }
    }

    private void EnsureAgentMayChange(PropertyDefinition definition, AutonomyLevel autonomyInScope)
    {
        if (autonomyInScope != AutonomyLevel.AdjustShouldProperties)
        {
            throw new DomainException(
                $"An agent may only adjust properties at autonomy level 4; the scope has level {(int)autonomyInScope}.");
        }

        if (Definition.Bindingness != Bindingness.Should || definition.Bindingness != Bindingness.Should)
        {
            throw new DomainException("An agent may only adjust \"should\" properties and must not make them \"must\".");
        }

        if (definition.Type != Definition.Type || definition.Scope != Definition.Scope)
        {
            throw new DomainException("An agent must not change the type or scope of a property.");
        }
    }

    private void EnsureNotRetired()
    {
        if (IsRetired)
        {
            throw new DomainException($"Property {Id} is retired.");
        }
    }
}
