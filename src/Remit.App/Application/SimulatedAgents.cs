using Remit.Domain;

namespace Remit.App.Application;

/// <summary>What the build agent does once an option may be implemented.</summary>
public sealed record BuildPlan(string ComponentName, ComponentKind Kind, string Choice, string Rationale);

public sealed record AnalysisResult(string Analysis, IReadOnlyList<ProposalOption> Options, OptionId Recommended);

/// <summary>
/// Stand-in for the analysis and build agents in version 0.0.1. It recognises a few kinds of needs
/// by keywords and answers with prepared options. The real agents connect later via MCP (ADR 0006);
/// all rules about what they may do stay in the domain core.
/// </summary>
public static class SimulatedAgents
{
    public static readonly Actor Analyst = Actor.Agent("Analysis agent");
    public static readonly Actor Builder = Actor.Agent("Build agent");

    private static readonly string[] PerformanceWords =
        ["fast", "slow", "speed", "response", "latency", "performance", "load time", "schnell", "langsam", "ladezeit", "antwortzeit", "dauert"];

    private static readonly string[] AvailabilityWords =
        ["outage", "down", "available", "availability", "reliable", "ausfall", "verfügbar", "erreichbar", "stabil"];

    private sealed record Template(OptionId Id, Func<AreaId, ProposalOption> Option, BuildPlan Plan);

    private static readonly Template[] Performance =
    [
        new(new("cache"), area => new ProposalOption(
                new("cache"),
                "Cache for frequently requested data",
                "Repeated, identical requests are answered from a fast intermediate store instead of the database.",
                [new ExpectedEffect("p95_response_time", 200, "ms")],
                new CostRange(150, 200),
                ["Shown data can be slightly out of date", "Additional building block that has to be operated"],
                [PropertyType.Performance, PropertyType.Freshness, PropertyType.Exclusion, PropertyType.FailureBehavior, PropertyType.Budget],
                [area]),
            new("Catalog cache", ComponentKind.Cache, "Managed cache in an EU region, sized for today's load",
                "Cheapest variant that meets the response time target and stays within the budget.")),
        new(new("cdn"), area => new ProposalOption(
                new("cdn"),
                "Content delivery network for static content",
                "Images and static pages are delivered from locations close to the users.",
                [new ExpectedEffect("p95_response_time", 450, "ms")],
                new CostRange(40, 80),
                ["Only helps for static content; search stays slow"],
                [PropertyType.Performance, PropertyType.Budget],
                [area]),
            new("Content delivery", ComponentKind.Interface, "Content delivery network with EU locations",
                "Lowest cost; covers the static share of the requests.")),
        new(new("scale-up"), area => new ProposalOption(
                new("scale-up"),
                "Larger database",
                "The database gets more computing power; nothing else changes.",
                [new ExpectedEffect("p95_response_time", 500, "ms")],
                new CostRange(300, 400),
                ["High running costs", "Effect is limited by the number of repeated requests"],
                [PropertyType.Performance, PropertyType.Budget],
                [area]),
            new("Database (larger size)", ComponentKind.DataStore, "Next larger database size in the same region",
                "No new building block; the simplest change.")),
    ];

    private static readonly Template[] Availability =
    [
        new(new("standby"), area => new ProposalOption(
                new("standby"),
                "Standby copy in a second location",
                "A second copy of the system takes over automatically if the first location fails.",
                [new ExpectedEffect("availability", 99.95m, "%")],
                new CostRange(200, 300),
                ["Higher running costs", "Data of the last few seconds can be lost on switch-over"],
                [PropertyType.Availability, PropertyType.Recovery, PropertyType.Location, PropertyType.Budget],
                [area]),
            new("Standby site", ComponentKind.Service, "Second instance in another EU region with automatic switch-over",
                "Meets the availability target; both regions stay within the allowed data location.")),
        new(new("self-healing"), area => new ProposalOption(
                new("self-healing"),
                "Automatic restart and health checks",
                "Broken parts are detected and restarted automatically.",
                [new ExpectedEffect("availability", 99.9m, "%")],
                new CostRange(10, 30),
                ["Does not help if the whole location fails"],
                [PropertyType.Availability, PropertyType.Budget],
                [area]),
            new("Health monitoring", ComponentKind.Service, "Health checks with automatic restart",
                "Lowest cost; covers the most frequent cause of outages.")),
    ];

    private static readonly Template[] General =
    [
        new(new("extend"), area => new ProposalOption(
                new("extend"),
                "Extend an existing building block",
                "The need is met by extending a part of the system that already exists.",
                [],
                new CostRange(0, 50),
                ["Existing part becomes larger and harder to change"],
                [PropertyType.Budget],
                [area]),
            new("Extension of an existing part", ComponentKind.Service, "Extension of the existing service",
                "Lowest cost and no new building block to operate.")),
        new(new("new-part"), area => new ProposalOption(
                new("new-part"),
                "New, separate building block",
                "The need is met by a new part of the system that can be changed independently.",
                [],
                new CostRange(50, 150),
                ["Additional building block that has to be operated"],
                [PropertyType.Budget, PropertyType.FailureBehavior],
                [area]),
            new("New service", ComponentKind.Service, "New service next to the existing ones",
                "Keeps the existing parts unchanged and can fail on its own.")),
    ];

    private static IEnumerable<Template> AllTemplates => Performance.Concat(Availability).Concat(General);

    public static AnalysisResult Analyse(string title, string description, AreaId area)
    {
        var text = $"{title} {description}".ToLowerInvariant();
        if (PerformanceWords.Any(text.Contains))
        {
            return new(
                "Many identical requests reach the database again and again, which makes responses slow at peak times.",
                Performance.Select(t => t.Option(area)).ToList(),
                new("cache"));
        }

        if (AvailabilityWords.Any(text.Contains))
        {
            return new(
                "The system runs in a single location and parts are not restarted automatically after a failure.",
                Availability.Select(t => t.Option(area)).ToList(),
                new("self-healing"));
        }

        return new(
            "The need can be met with existing means; there are two ways that differ in cost and independence.",
            General.Select(t => t.Option(area)).ToList(),
            new("extend"));
    }

    public static BuildPlan PlanFor(OptionId option) =>
        AllTemplates.FirstOrDefault(t => t.Id == option)?.Plan
        ?? throw new DomainException($"No build plan for option {option}.");
}
