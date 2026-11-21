namespace Remit.Domain;

public enum PropertyState
{
    Fulfilled,

    /// <summary>Currently fulfilled, but the forecast predicts a violation.</summary>
    AtRisk,

    Violated,
}

public enum DeviationKind
{
    Violated,
    Forecast,

    /// <summary>The system no longer matches the decisions, e.g. a manual change in the portal.</summary>
    Drift,
}

/// <summary>
/// Computes the state of a numeric property. Raw measurements stay in Azure Monitor (ADR 0004);
/// only the aggregated value and an optional forecast are passed in.
/// </summary>
public static class PropertyStateEvaluator
{
    public static PropertyState Evaluate(ThresholdRule rule, decimal current, decimal? forecast = null)
    {
        if (!rule.IsSatisfiedBy(current))
        {
            return PropertyState.Violated;
        }

        return forecast is { } f && !rule.IsSatisfiedBy(f) ? PropertyState.AtRisk : PropertyState.Fulfilled;
    }

    public static DeviationKind? ToDeviationKind(PropertyState state) => state switch
    {
        PropertyState.Violated => DeviationKind.Violated,
        PropertyState.AtRisk => DeviationKind.Forecast,
        _ => null,
    };
}

public sealed record DeviationDetected(
    DeviationId DeviationId,
    SystemPropertyId PropertyId,
    DeviationKind Kind,
    string Description,
    Actor DetectedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DeviationMeasureAssigned(
    DeviationId DeviationId,
    DecisionId Measure,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>A violation, forecast violation or drift of a property; leads to a follow-up decision.</summary>
public sealed class Deviation : AggregateRoot<DeviationId>
{
    private Deviation()
    {
    }

    public SystemPropertyId PropertyId { get; private set; }

    public DeviationKind Kind { get; private set; }

    public string Description { get; private set; } = "";

    public Actor DetectedBy { get; private set; } = null!;

    public DateTimeOffset DetectedAt { get; private set; }

    /// <summary>The follow-up decision (by the AI or as a TPO question).</summary>
    public DecisionId? Measure { get; private set; }

    public static Deviation Detect(
        DeviationId id,
        SystemPropertyId propertyId,
        DeviationKind kind,
        string description,
        Actor detectedBy,
        DateTimeOffset at)
    {
        var deviation = new Deviation();
        deviation.Raise(new DeviationDetected(
            id, propertyId, Guard.Defined(kind, nameof(kind)), Guard.NotBlank(description, nameof(description)), detectedBy, at));
        return deviation;
    }

    public static Deviation FromHistory(IEnumerable<IDomainEvent> history)
    {
        var deviation = new Deviation();
        deviation.Rehydrate(history);
        return deviation;
    }

    public void AssignMeasure(DecisionId decision, DateTimeOffset at)
    {
        if (Measure is not null)
        {
            throw new DomainException($"Deviation {Id} already has a measure ({Measure}).");
        }

        Raise(new DeviationMeasureAssigned(Id, decision, at));
    }

    protected override void When(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case DeviationDetected e:
                Id = e.DeviationId;
                PropertyId = e.PropertyId;
                Kind = e.Kind;
                Description = e.Description;
                DetectedBy = e.DetectedBy;
                DetectedAt = e.OccurredAt;
                break;
            case DeviationMeasureAssigned e:
                Measure = e.Measure;
                break;
            default:
                throw new InvalidOperationException($"Unknown event {domainEvent.GetType().Name} for {nameof(Deviation)}.");
        }
    }
}
