namespace Kommandozentrale.Domain;

/// <summary>Violation of a domain rule.</summary>
public sealed class DomainException(string message) : Exception(message);

public enum ActorKind
{
    Person,
    Agent,
}

/// <summary>Who did something: a person (in the PoC always the TPO) or an AI agent.</summary>
public sealed record Actor(ActorKind Kind, string Name)
{
    public string Name { get; } = Guard.NotBlank(Name, nameof(Name));

    public bool IsPerson => Kind == ActorKind.Person;

    public static Actor Person(string name) => new(ActorKind.Person, name);

    public static Actor Agent(string name) => new(ActorKind.Agent, name);

    public override string ToString() => $"{Kind}:{Name}";
}

internal static class Guard
{
    public static string NotBlank(string? value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new DomainException($"{name} must not be empty.") : value;

    public static T Defined<T>(T value, string name) where T : struct, Enum =>
        Enum.IsDefined(value) ? value : throw new DomainException($"{name} has an undefined value '{value}'.");

    public static IReadOnlyList<T> NotEmpty<T>(IEnumerable<T>? items, string name)
    {
        var list = items?.ToList() ?? [];
        return list.Count == 0 ? throw new DomainException($"{name} must contain at least one entry.") : list;
    }

    public static void RequirePerson(Actor actor, string action)
    {
        if (!actor.IsPerson)
        {
            throw new DomainException($"Only the TPO may {action}; '{actor}' is an agent.");
        }
    }

    public static void RequireAgent(Actor actor, string action)
    {
        if (actor.IsPerson)
        {
            throw new DomainException($"Only an agent may {action}; '{actor}' is a person.");
        }
    }
}
