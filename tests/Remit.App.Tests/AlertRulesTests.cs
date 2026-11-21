using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Remit.App.Application;
using Remit.App.Persistence;
using Remit.Domain;

namespace Remit.App.Tests;

public sealed class AlertRulesTests
{
    private static RemitService ServiceWith(params PropertyInput[] inputs)
    {
        var service = new RemitService(new InMemoryEventStore(), TimeProvider.System);
        service.EnsureArea();
        foreach (var input in inputs)
        {
            service.SetAreaProperty(input);
        }

        return service;
    }

    [Fact]
    public void Measurable_properties_become_alerts_and_budgets_the_rest_is_listed_as_not_covered()
    {
        var plan = ServiceWith(
            new(PropertyType.Performance, 250, null, Bindingness.Must),
            new(PropertyType.Availability, 99.9m, null, Bindingness.Should),
            new(PropertyType.Budget, 250, null, Bindingness.Must),
            new(PropertyType.Exclusion, null, "Stock levels are never served from a copy", Bindingness.Must)).GetAlertPlan();

        var p95 = plan.LogAlerts.Single(a => a.Signal == "responseTimeP95");
        Assert.Equal("GreaterThan", p95.Operator);
        Assert.Equal(250, p95.Threshold);
        Assert.Equal(1, p95.Severity);
        Assert.Contains("SP-001", p95.Description);
        Assert.Equal("remit-sp-001-performance", p95.Name);

        var availability = plan.LogAlerts.Single(a => a.Signal == "availability");
        Assert.Equal("LessThan", availability.Operator);
        Assert.Equal(3, availability.Severity);

        Assert.Equal(250, Assert.Single(plan.Budgets).Amount);
        Assert.Equal("Exclusion", Assert.Single(plan.NotCovered).Label);
    }

    [Fact]
    public async Task The_parameters_file_matches_the_bicep_module()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Remit:SeedExample", "false"));
        using var client = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        await client.PostAsJsonAsync("/api/area/properties", new { type = "Performance", value = 250, bindingness = "Must" }, ct);

        var file = await client.GetFromJsonAsync<JsonElement>("/api/setup/alert-rules.parameters.json", ct);

        var alert = file.GetProperty("parameters").GetProperty("logAlerts").GetProperty("value")[0];
        foreach (var field in new[] { "name", "propertyId", "description", "signal", "operator", "threshold", "windowMinutes", "severity" })
        {
            Assert.True(alert.TryGetProperty(field, out _), field);
        }

        Assert.Equal(0, file.GetProperty("parameters").GetProperty("budgets").GetProperty("value").GetArrayLength());
    }
}
