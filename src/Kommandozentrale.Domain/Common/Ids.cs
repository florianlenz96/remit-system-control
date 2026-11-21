namespace Kommandozentrale.Domain;

// Readable, stable identifiers as used in the data model (e.g. "B-042" for a need, "E1" for a
// property). Generating them is the job of the application layer.

public readonly record struct SystemId(string Value)
{
    public string Value { get; } = Guard.NotBlank(Value, nameof(SystemId));

    public override string ToString() => Value;
}

public readonly record struct AreaId(string Value)
{
    public string Value { get; } = Guard.NotBlank(Value, nameof(AreaId));

    public override string ToString() => Value;
}

public readonly record struct ComponentId(string Value)
{
    public string Value { get; } = Guard.NotBlank(Value, nameof(ComponentId));

    public override string ToString() => Value;
}

public readonly record struct NeedId(string Value)
{
    public string Value { get; } = Guard.NotBlank(Value, nameof(NeedId));

    public override string ToString() => Value;
}

public readonly record struct ProposalId(string Value)
{
    public string Value { get; } = Guard.NotBlank(Value, nameof(ProposalId));

    public override string ToString() => Value;
}

public readonly record struct OptionId(string Value)
{
    public string Value { get; } = Guard.NotBlank(Value, nameof(OptionId));

    public override string ToString() => Value;
}

public readonly record struct SystemPropertyId(string Value)
{
    public string Value { get; } = Guard.NotBlank(Value, nameof(SystemPropertyId));

    public override string ToString() => Value;
}

public readonly record struct DecisionId(string Value)
{
    public string Value { get; } = Guard.NotBlank(Value, nameof(DecisionId));

    public override string ToString() => Value;
}

public readonly record struct DeviationId(string Value)
{
    public string Value { get; } = Guard.NotBlank(Value, nameof(DeviationId));

    public override string ToString() => Value;
}
