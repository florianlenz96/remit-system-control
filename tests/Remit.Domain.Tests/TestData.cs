namespace Remit.Domain.Tests;

/// <summary>Shared fixtures based on the cache scenario from docs/data-model.md.</summary>
internal static class TestData
{
    public static readonly DateTimeOffset T0 = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);

    public static readonly Actor Tpo = Actor.Person("tpo");
    public static readonly Actor BuildAgent = Actor.Agent("build-agent");
    public static readonly Actor OpsAgent = Actor.Agent("operations-agent");

    public static readonly SystemId Shop = new("online-shop");
    public static readonly AreaId Catalog = new("catalog-and-search");
    public static readonly AreaId Payment = new("payment");
    public static readonly DecisionId AcceptCache = new("D-1");

    public static Policy PolicyAt(int autonomy, int transparency = 1) =>
        new((AutonomyLevel)autonomy, (TransparencyLevel)transparency);

    public static MeasurementSpec AppInsights => new("application-insights", "p95", TimeSpan.FromMinutes(5));

    public static PropertyDefinition Threshold(
        string metric,
        ComparisonOperator op,
        decimal value,
        string unit,
        Bindingness bindingness = Bindingness.Must,
        PropertyType type = PropertyType.Performance,
        Validity? validity = null,
        AreaId? area = null) =>
        new(
            type,
            PropertyScope.ForArea(area ?? Catalog),
            new ThresholdRule(metric, op, value, unit),
            AppInsights,
            bindingness,
            validity);

    public static SystemProperty Property(string id, PropertyDefinition definition) =>
        SystemProperty.Define(new SystemPropertyId(id), definition, AcceptCache, Tpo, T0);

    public static SystemProperty ResponseTime(decimal ms = 250, Bindingness bindingness = Bindingness.Must) =>
        Property("E1", Threshold("p95_response_time", ComparisonOperator.LessOrEqual, ms, "ms", bindingness));

    public static SystemProperty Budget(decimal eur = 250, Bindingness bindingness = Bindingness.Must) =>
        Property("E5", Threshold("cost", ComparisonOperator.LessOrEqual, eur, "EUR/month", bindingness, PropertyType.Budget));

    public static ProposalOption CacheOption() => new(
        new OptionId("cache"),
        "Cache for catalog and search",
        "Answer repeated catalog queries from a cache.",
        [new ExpectedEffect("p95_response_time", 200, "ms")],
        new CostRange(150, 200),
        ["Prices may be stale"],
        [PropertyType.Freshness, PropertyType.Exclusion, PropertyType.FailureBehavior, PropertyType.Recovery, PropertyType.Budget],
        [Catalog]);

    public static ProposalOption CdnOption() => new(
        new OptionId("cdn"),
        "CDN",
        "Deliver static catalog pages via a CDN.",
        [],
        new CostRange(50, 80),
        [],
        [PropertyType.Freshness],
        [Catalog]);

    public static Proposal CacheProposal() => Proposal.Create(
        new ProposalId("P-1"),
        new NeedId("B-042"),
        "Repeated identical catalog queries to the database.",
        [CacheOption(), CdnOption()],
        new OptionId("cache"),
        BuildAgent,
        T0);
}
