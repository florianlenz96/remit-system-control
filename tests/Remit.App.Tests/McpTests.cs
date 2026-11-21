using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Remit.App.Tests;

/// <summary>
/// The cache scenario from the operations agent's side: an existing Redis cache is taken over, the TPO sets
/// targets, the agent reports a violation, may not resolve the target/budget conflict itself, asks the TPO
/// and acts on the answer.
/// </summary>
public sealed class McpTests : IAsyncLifetime
{
    private const string Key = "test-key-not-a-secret";
    private const string Redis = "/subscriptions/0000/resourceGroups/shop/providers/Microsoft.Cache/Redis/catalog-cache";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _http = null!;
    private McpClient _mcp = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("Remit:SeedExample", "false")
            .UseSetting("Remit:Mcp:ApiKey", Key)
            .UseSetting("Remit:Mcp:AgentName", "SRE Agent")
            .UseSetting("Remit:ExistingComponents:0:Name", "Catalog cache")
            .UseSetting("Remit:ExistingComponents:0:Kind", "Cache")
            .UseSetting("Remit:ExistingComponents:0:ResourceId", Redis));
        _http = _factory.CreateClient();

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(_http.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {Key}" },
            },
            _factory.CreateClient(),
            ownsHttpClient: true);
        _mcp = await McpClient.CreateAsync(transport, cancellationToken: Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _mcp.DisposeAsync();
        _http.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task<string> SetProperty(string type, decimal value)
    {
        var response = await _http.PostAsJsonAsync("/api/area/properties", new { type, value, bindingness = "Must" }, Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json, Ct)).GetProperty("id").GetString()!;
    }

    private async Task<(bool IsError, JsonElement Body, string Text)> Call(string tool, object args)
    {
        var arguments = JsonSerializer.SerializeToElement(args, Json).EnumerateObject()
            .ToDictionary(p => p.Name, p => (object?)p.Value);
        var result = await _mcp.CallToolAsync(tool, arguments, cancellationToken: Ct);
        var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(t => t.Text));
        var body = result.IsError == true ? default : JsonDocument.Parse(text).RootElement.Clone();
        return (result.IsError == true, body, text);
    }

    [Fact]
    public async Task The_tools_are_offered()
    {
        var tools = await _mcp.ListToolsAsync(cancellationToken: Ct);

        Assert.Equal(
            ["ask_tpo", "check_change", "get_question", "get_rules", "report_decision", "report_deviation", "resolve_deviation"],
            tools.Select(t => t.Name).Order().ToArray());
    }

    [Fact]
    public async Task The_operations_agent_follows_the_rules_of_remit_for_an_existing_cache()
    {
        var responseTime = await SetProperty("Performance", 250);
        await SetProperty("Budget", 250);

        // 1. The agent learns what applies to the cache (a sub-resource is mapped to its component).
        var (_, rules, _) = await Call("get_rules", new { resourceId = Redis + "/firewallRules/x" });
        Assert.Equal(3, rules.GetProperty("policy").GetProperty("autonomy").GetInt32());
        Assert.Equal(2, rules.GetProperty("properties").GetArrayLength());
        var cache = rules.GetProperty("components").EnumerateArray().Single();
        Assert.Equal("Catalog cache", cache.GetProperty("name").GetString());
        Assert.True(cache.GetProperty("takenOver").GetBoolean());

        // 2. It reports the violation.
        var (_, deviation, _) = await Call("report_deviation", new { propertyId = responseTime, kind = "Violated", description = "p95 at 900 ms since 18:00" });
        var deviationId = deviation.GetProperty("id").GetString();

        // 3. A restart within all properties may be done on its own ...
        var (_, restart, _) = await Call("check_change", new
        {
            resourceId = Redis, kind = "technical_change",
            expectedEffects = new[] { new { metric = "p95_response_time", value = 200, unit = "ms" } },
        });
        Assert.Equal("act", restart.GetProperty("outcome").GetString());

        // ... but scaling up breaks the budget: the TPO must decide.
        var scaleUp = new
        {
            resourceId = Redis, kind = "technical_change", extraMonthlyCost = 150, currentMonthlyCost = 180,
            expectedEffects = new[] { new { metric = "p95_response_time", value = 180, unit = "ms" } },
        };
        var (_, check, _) = await Call("check_change", scaleUp);
        Assert.Equal("ask_tpo", check.GetProperty("outcome").GetString());
        Assert.Equal(330, check.GetProperty("projectedMonthlyCost").GetDecimal());
        Assert.Equal("Budget", check.GetProperty("blocking").EnumerateArray().Single().GetProperty("label").GetString());

        // 4. Acting without the TPO is refused.
        var (refused, _, reason) = await Call("report_decision", new { chosen = "Scaled the cache up", rationale = "Load", kind = "technical_change", resourceId = Redis, extraMonthlyCost = 150, currentMonthlyCost = 180 });
        Assert.True(refused);
        Assert.Contains("needs the TPO", reason);

        // 5. It asks the TPO.
        var (_, asked, _) = await Call("ask_tpo", new
        {
            question = "Response time and budget cannot both be met tonight",
            options = new[]
            {
                new { key = "raise-budget", title = "Allow 330 EUR this month", description = "Response time is met." },
                new { key = "keep-budget", title = "Keep the budget", description = "Pages stay slow in the evening." },
            },
            defaultOption = "keep-budget", deadlineHours = 4, resourceId = Redis, deviationId,
        });
        var questionId = asked.GetProperty("id").GetString()!;

        var overview = await _http.GetFromJsonAsync<JsonElement>("/api/overview", Json, Ct);
        var open = overview.GetProperty("openQuestions").EnumerateArray().Single();
        Assert.Equal("Response time", open.GetProperty("about").GetString());
        Assert.DoesNotContain("/subscriptions/", overview.GetRawText());

        var (_, waiting, _) = await Call("get_question", new { questionId });
        Assert.Equal("open", waiting.GetProperty("status").GetString());

        // 6. The TPO answers in Remit; the agent acts on the answer and reports it.
        var answer = await _http.PostAsJsonAsync($"/api/questions/{questionId}/answer", new { optionKey = "raise-budget", rationale = "Black Friday" }, Json, Ct);
        Assert.Equal(HttpStatusCode.NoContent, answer.StatusCode);
        var (_, answered, _) = await Call("get_question", new { questionId });
        Assert.Equal("raise-budget", answered.GetProperty("chosenOption").GetString());

        var (failed, decision, text) = await Call("report_decision", new
        {
            chosen = "Scaled the catalog cache to the next size", rationale = "Protects the response time target.",
            kind = "technical_change", resourceId = Redis, extraMonthlyCost = 150, currentMonthlyCost = 180,
            deviationId, questionId, howToUndo = "Scale back after the weekend", references = new[] { "incident-4711" },
        });
        Assert.False(failed, text);

        await Call("resolve_deviation", new { deviationId, note = "p95 back at 170 ms" });

        overview = await _http.GetFromJsonAsync<JsonElement>("/api/overview", Json, Ct);
        var ai = overview.GetProperty("decisions").EnumerateArray().First(d => d.GetProperty("id").GetString() == decision.GetProperty("id").GetString());
        Assert.Equal("SRE Agent", ai.GetProperty("by").GetString());
        Assert.Equal("AiDecision", ai.GetProperty("kind").GetString());
        var dev = overview.GetProperty("deviations").EnumerateArray().Single();
        Assert.True(dev.GetProperty("resolved").GetBoolean());
        Assert.Equal(questionId, dev.GetProperty("measureId").GetString());
        Assert.DoesNotContain("/subscriptions/", overview.GetRawText());
        Assert.DoesNotContain("incident-4711", overview.GetRawText());
    }

    [Fact]
    public async Task Unknown_resources_and_unknown_properties_are_readable_errors()
    {
        var (unknownResource, _, text) = await Call("get_rules", new { resourceId = "/subscriptions/0000/other" });
        Assert.True(unknownResource);
        Assert.Contains("does not know the resource", text);

        var (unknownProperty, _, text2) = await Call("report_deviation", new { propertyId = "SP-999", kind = "Violated", description = "x" });
        Assert.True(unknownProperty);
        Assert.Contains("no active property", text2);
    }

    [Fact]
    public async Task Without_the_key_the_endpoint_refuses()
    {
        var response = await _http.PostAsync("/mcp", JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "tools/list" }), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var wrong = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "tools/list" }) };
        wrong.Headers.Authorization = new("Bearer", "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _http.SendAsync(wrong, Ct)).StatusCode);
    }

    [Fact]
    public async Task Without_a_configured_key_the_endpoint_is_closed_outside_development()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Production"));
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/mcp", JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "tools/list" }), Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
