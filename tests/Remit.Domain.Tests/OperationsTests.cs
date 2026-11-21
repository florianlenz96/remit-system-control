using static Remit.Domain.Tests.TestData;

namespace Remit.Domain.Tests;

public sealed class AdoptedComponentTests
{
    [Fact]
    public void An_existing_building_block_is_taken_over_active_and_traceable()
    {
        var component = Component.Adopt(
            new ComponentId("C-1"), Catalog, "Catalog cache", ComponentKind.Cache,
            ["/subscriptions/x/resourceGroups/shop/providers/Microsoft.Cache/redis/catalog"], AcceptCache, Tpo, T0);

        Assert.True(component.IsAdopted);
        Assert.Equal(ComponentStatus.Active, component.Status);
        Assert.Equal(AcceptCache, component.CreatedFrom);

        var rebuilt = Component.FromHistory(component.UncommittedEvents);
        Assert.True(rebuilt.IsAdopted);
        Assert.Single(rebuilt.ResourceRefs);
    }

    [Fact]
    public void Only_a_person_takes_over_and_a_resource_reference_is_required()
    {
        Assert.Throws<DomainException>(() => Component.Adopt(
            new ComponentId("C-1"), Catalog, "Cache", ComponentKind.Cache, ["ref"], AcceptCache, OpsAgent, T0));
        Assert.Throws<DomainException>(() => Component.Adopt(
            new ComponentId("C-1"), Catalog, "Cache", ComponentKind.Cache, [" "], AcceptCache, Tpo, T0));
    }
}

public sealed class DeviationTests
{
    private static Deviation Detected() =>
        Deviation.Detect(new DeviationId("A-1"), new SystemPropertyId("E1"), DeviationKind.Violated, "p95 at 900 ms", OpsAgent, T0);

    [Fact]
    public void A_deviation_is_resolved_once()
    {
        var deviation = Detected();

        deviation.Resolve("Back at 180 ms", OpsAgent, T0.AddMinutes(20));

        Assert.True(deviation.IsResolved);
        Assert.Equal(T0.AddMinutes(20), Deviation.FromHistory(deviation.UncommittedEvents).ResolvedAt);
        Assert.Throws<DomainException>(() => deviation.Resolve("again", OpsAgent, T0.AddMinutes(30)));
    }

    [Fact]
    public void A_resolved_deviation_gets_no_further_measure()
    {
        var deviation = Detected();
        deviation.Resolve("ok", OpsAgent, T0.AddMinutes(1));

        Assert.Throws<DomainException>(() => deviation.AssignMeasure(new DecisionId("D-9"), T0.AddMinutes(2)));
    }

    [Fact]
    public void A_deviation_cannot_be_resolved_before_it_was_detected()
    {
        Assert.Throws<DomainException>(() => Detected().Resolve("ok", OpsAgent, T0.AddMinutes(-1)));
    }
}
