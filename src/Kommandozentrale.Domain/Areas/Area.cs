namespace Kommandozentrale.Domain;

/// <summary>Autonomy level: what the AI may do on its own in an area.</summary>
public enum AutonomyLevel
{
    /// <summary>1 – proposes, implements nothing (read-only rights).</summary>
    ProposeOnly = 1,

    /// <summary>2 – implements after approval of every step.</summary>
    ApproveEachStep = 2,

    /// <summary>3 – decides on its own within the system properties.</summary>
    DecideWithinProperties = 3,

    /// <summary>4 – like 3, and may also adjust "should" properties (and reports it).</summary>
    AdjustShouldProperties = 4,
}

/// <summary>Transparency level: how much the AI has to explain.</summary>
public enum TransparencyLevel
{
    /// <summary>1 – result: target and actual values, costs.</summary>
    Result = 1,

    /// <summary>2 – additionally every decision with its rationale.</summary>
    Decisions = 2,

    /// <summary>3 – additionally the data flow.</summary>
    DataFlow = 3,
}

public enum ProtectionNeed
{
    Low,
    Medium,
    High,
}

/// <summary>Policy: autonomy and transparency level of an area.</summary>
public sealed record Policy
{
    public Policy(AutonomyLevel autonomy, TransparencyLevel transparency)
    {
        Autonomy = Guard.Defined(autonomy, nameof(autonomy));
        Transparency = Guard.Defined(transparency, nameof(transparency));
    }

    public AutonomyLevel Autonomy { get; }

    public TransparencyLevel Transparency { get; }

    /// <summary>
    /// The policy that applies when a change touches several areas: the lowest autonomy and the
    /// highest transparency of all of them.
    /// </summary>
    public static Policy Strictest(IEnumerable<Policy> policies)
    {
        var list = Guard.NotEmpty(policies, nameof(policies));
        return new Policy(list.Min(p => p.Autonomy), list.Max(p => p.Transparency));
    }
}

public sealed record AreaDefined(
    AreaId AreaId,
    SystemId SystemId,
    string Name,
    ProtectionNeed ProtectionNeed,
    Policy Policy,
    string Responsible,
    Actor DefinedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record AreaPolicyChanged(
    AreaId AreaId,
    Policy Previous,
    Policy Policy,
    string Reason,
    Actor ChangedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Area: the part of a system a policy applies to.</summary>
public sealed class Area : AggregateRoot<AreaId>
{
    private Area()
    {
    }

    public SystemId SystemId { get; private set; }

    public string Name { get; private set; } = "";

    public ProtectionNeed ProtectionNeed { get; private set; }

    public Policy Policy { get; private set; } = null!;

    public string Responsible { get; private set; } = "";

    public static Area Define(
        AreaId id,
        SystemId systemId,
        string name,
        ProtectionNeed protectionNeed,
        Policy policy,
        Actor definedBy,
        DateTimeOffset at,
        string? responsible = null)
    {
        Guard.RequirePerson(definedBy, "define an area");
        var area = new Area();
        area.Raise(new AreaDefined(
            id,
            systemId,
            Guard.NotBlank(name, nameof(name)),
            Guard.Defined(protectionNeed, nameof(protectionNeed)),
            policy,
            responsible ?? definedBy.Name,
            definedBy,
            at));
        return area;
    }

    public static Area FromHistory(IEnumerable<IDomainEvent> history)
    {
        var area = new Area();
        area.Rehydrate(history);
        return area;
    }

    /// <summary>Only the TPO may change a policy – never an agent, at no level.</summary>
    public void ChangePolicy(Policy policy, string reason, Actor changedBy, DateTimeOffset at)
    {
        Guard.RequirePerson(changedBy, "change the policy of an area");
        if (policy == Policy)
        {
            return;
        }

        Raise(new AreaPolicyChanged(Id, Policy, policy, Guard.NotBlank(reason, nameof(reason)), changedBy, at));
    }

    protected override void When(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case AreaDefined e:
                Id = e.AreaId;
                SystemId = e.SystemId;
                Name = e.Name;
                ProtectionNeed = e.ProtectionNeed;
                Policy = e.Policy;
                Responsible = e.Responsible;
                break;
            case AreaPolicyChanged e:
                Policy = e.Policy;
                break;
            default:
                throw new InvalidOperationException($"Unknown event {domainEvent.GetType().Name} for {nameof(Area)}.");
        }
    }
}
