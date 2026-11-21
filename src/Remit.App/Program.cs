using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol;
using Remit.App.Api;
using Remit.App.Application;
using Remit.App.Mcp;
using Remit.App.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<InMemoryEventStore>();
builder.Services.AddSingleton<RemitService>();

// MCP server for operations agents (ADR 0006, ADR 0009).
var mcpJson = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
mcpJson.Converters.Add(new JsonStringEnumConverter());
builder.Services.AddSingleton(new OperationsAgent(builder.Configuration["Remit:Mcp:AgentName"] ?? "Operations agent"));
builder.Services.AddMcpServer(o => o.ServerInfo = new() { Name = "remit", Version = RemitService.Version })
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<RemitMcpTools>(mcpJson);

var app = builder.Build();

var remit = app.Services.GetRequiredService<RemitService>();
remit.EnsureArea();
if (app.Configuration.GetValue("Remit:SeedExample", true))
{
    remit.SeedExample();
}

remit.AdoptExisting(app.Configuration.GetSection("Remit:ExistingComponents").Get<ExistingComponent[]>() ?? []);

app.UseDomainErrors();
app.UseMcpAccess(app.Configuration, app.Environment);
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/health", () => Results.Ok(new { status = "ok", version = RemitService.Version }));
app.MapRemitApi();
app.MapMcp(McpAccess.Path);
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
