namespace Kommandozentrale.Domain;

public enum PropertyType
{
    /// <summary>e.g. p95 response time ≤ 250 ms.</summary>
    Performance,

    /// <summary>e.g. ≥ 99.9 % per month.</summary>
    Availability,

    /// <summary>e.g. prices at most 60 s old.</summary>
    Freshness,

    /// <summary>e.g. stock levels never from the cache.</summary>
    Exclusion,

    /// <summary>e.g. slower but working when the cache fails.</summary>
    FailureBehavior,

    /// <summary>e.g. RTO 15 min.</summary>
    Recovery,

    /// <summary>e.g. ≤ 250 EUR per month.</summary>
    Budget,

    /// <summary>e.g. EU regions only.</summary>
    Location,
}

/// <summary>Bindingness. On conflict, "should" yields first.</summary>
public enum Bindingness
{
    Must,
    Should,
}

public enum ScopeKind
{
    System,
    Area,
    Component,
}

/// <summary>What a property applies to.</summary>
public sealed record PropertyScope(ScopeKind Kind, string TargetId)
{
    public ScopeKind Kind { get; } = Guard.Defined(Kind, nameof(Kind));

    public string TargetId { get; } = Guard.NotBlank(TargetId, nameof(TargetId));

    public static PropertyScope ForSystem(SystemId id) => new(ScopeKind.System, id.Value);

    public static PropertyScope ForArea(AreaId id) => new(ScopeKind.Area, id.Value);

    public static PropertyScope ForComponent(ComponentId id) => new(ScopeKind.Component, id.Value);
}

public enum ComparisonOperator
{
    LessThan,
    LessOrEqual,
    Equal,
    GreaterOrEqual,
    GreaterThan,
}

/// <summary>The rule a property states. Either a numeric threshold or a qualitative constraint.</summary>
public abstract record PropertyRule;

/// <summary>Numeric rule, e.g. <c>p95_response_time &lt;= 250 ms</c>.</summary>
public sealed record ThresholdRule(string Metric, ComparisonOperator Operator, decimal Value, string Unit) : PropertyRule
{
    public string Metric { get; } = Guard.NotBlank(Metric, nameof(Metric));

    public ComparisonOperator Operator { get; } = Guard.Defined(Operator, nameof(Operator));

    public string Unit { get; } = Guard.NotBlank(Unit, nameof(Unit));

    public bool IsSatisfiedBy(decimal measured) => Operator switch
    {
        ComparisonOperator.LessThan => measured < Value,
        ComparisonOperator.LessOrEqual => measured <= Value,
        ComparisonOperator.Equal => measured == Value,
        ComparisonOperator.GreaterOrEqual => measured >= Value,
        ComparisonOperator.GreaterThan => measured > Value,
        _ => throw new InvalidOperationException($"Unknown operator {Operator}."),
    };

    public override string ToString()
    {
        var symbol = Operator switch
        {
            ComparisonOperator.LessThan => "<",
            ComparisonOperator.LessOrEqual => "<=",
            ComparisonOperator.Equal => "=",
            ComparisonOperator.GreaterOrEqual => ">=",
            _ => ">",
        };
        return $"{Metric} {symbol} {Value} {Unit}";
    }
}

/// <summary>
/// Qualitative rule that is checked rather than measured numerically, e.g. "stock levels never from
/// the cache" (checked during build) or "EU regions only" (Azure Policy).
/// </summary>
public sealed record ConstraintRule(string Statement) : PropertyRule
{
    public string Statement { get; } = Guard.NotBlank(Statement, nameof(Statement));

    public override string ToString() => Statement;
}

/// <summary>
/// Measurement spec: how a property is measured or checked. A property without one is invalid
/// (principle 3: everything measurable).
/// </summary>
public sealed record MeasurementSpec(string Source, string? Aggregation = null, TimeSpan? Window = null)
{
    public string Source { get; } = Guard.NotBlank(Source, nameof(Source));

    public TimeSpan? Window { get; } = Window is { } w && w <= TimeSpan.Zero
        ? throw new DomainException("Measurement window must be positive.")
        : Window;
}

/// <summary>Validity: optional time range, e.g. a Black Friday exception.</summary>
public sealed record Validity
{
    public Validity(DateTimeOffset? from, DateTimeOffset? until)
    {
        if (from is { } f && until is { } u && u <= f)
        {
            throw new DomainException("Validity must end after it starts.");
        }

        From = from;
        Until = until;
    }

    public static Validity Always { get; } = new(null, null);

    public DateTimeOffset? From { get; }

    /// <summary>Exclusive end.</summary>
    public DateTimeOffset? Until { get; }

    public bool IsActiveAt(DateTimeOffset at) => (From is null || at >= From) && (Until is null || at < Until);

    public bool Overlaps(Validity other) =>
        (Until is null || other.From is null || other.From < Until) &&
        (other.Until is null || From is null || From < other.Until);
}

/// <summary>Everything that defines a system property, independent of its identity and history.</summary>
public sealed record PropertyDefinition
{
    public PropertyDefinition(
        PropertyType type,
        PropertyScope scope,
        PropertyRule rule,
        MeasurementSpec measurement,
        Bindingness bindingness,
        Validity? validity = null,
        string? responsible = null)
    {
        Type = Guard.Defined(type, nameof(type));
        Scope = scope ?? throw new DomainException("A property needs a scope.");
        Rule = rule ?? throw new DomainException("A property needs a rule.");
        Measurement = measurement ?? throw new DomainException("A property without a measurement spec is invalid.");
        Bindingness = Guard.Defined(bindingness, nameof(bindingness));
        Validity = validity ?? Validity.Always;
        Responsible = responsible;
    }

    public PropertyType Type { get; }

    public PropertyScope Scope { get; }

    public PropertyRule Rule { get; }

    public MeasurementSpec Measurement { get; }

    public Bindingness Bindingness { get; }

    public Validity Validity { get; }

    /// <summary>Responsible person; null means the TPO.</summary>
    public string? Responsible { get; }
}
