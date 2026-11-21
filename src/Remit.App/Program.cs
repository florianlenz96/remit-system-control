using System.Text.Json.Serialization;
using Remit.App.Api;
using Remit.App.Application;
using Remit.App.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<InMemoryEventStore>();
builder.Services.AddSingleton<RemitService>();

var app = builder.Build();

if (app.Configuration.GetValue("Remit:SeedExample", true))
{
    app.Services.GetRequiredService<RemitService>().SeedExample();
}

app.UseDomainErrors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/health", () => Results.Ok(new { status = "ok", version = RemitService.Version }));
app.MapRemitApi();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
