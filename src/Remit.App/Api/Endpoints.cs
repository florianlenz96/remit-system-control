using Remit.App.Application;
using Remit.Domain;

namespace Remit.App.Api;

public sealed record RegisterNeedRequest(string Title, string Description, Priority Priority);

public sealed record AcceptOptionRequest(string OptionId, IReadOnlyList<PropertyInput> Properties);

public sealed record RejectRequest(string Reason);

public sealed record AnswerRequest(string OptionKey, string? Rationale);

public sealed record ChangePolicyRequest(AutonomyLevel Autonomy, TransparencyLevel Transparency, string Reason);

public sealed record CreatedResponse(string Id);

public static class Endpoints
{
    public static void MapRemitApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/overview", (RemitService s) => s.GetOverview());

        api.MapGet("/property-templates", () => PropertyTemplates.All);

        api.MapGet("/needs/{id}", (string id, RemitService s) => s.GetNeed(id));

        api.MapPost("/needs", (RegisterNeedRequest r, RemitService s) =>
        {
            var id = s.RegisterNeed(r.Title, r.Description, r.Priority);
            return Results.Created($"/api/needs/{id}", new CreatedResponse(id));
        });

        api.MapPost("/needs/{id}/accept", (string id, AcceptOptionRequest r, RemitService s) =>
        {
            s.AcceptOption(id, r.OptionId, r.Properties ?? []);
            return Results.NoContent();
        });

        api.MapPost("/needs/{id}/reject", (string id, RejectRequest r, RemitService s) =>
        {
            s.RejectProposal(id, r.Reason);
            return Results.NoContent();
        });

        api.MapPost("/questions/{id}/answer", (string id, AnswerRequest r, RemitService s) =>
        {
            s.AnswerQuestion(id, r.OptionKey, r.Rationale);
            return Results.NoContent();
        });

        api.MapPut("/area/policy", (ChangePolicyRequest r, RemitService s) =>
        {
            s.ChangePolicy(r.Autonomy, r.Transparency, r.Reason);
            return Results.NoContent();
        });
    }

    /// <summary>Domain rule violations become 400 with a message the TPO can read; unknown ids become 404.</summary>
    public static IApplicationBuilder UseDomainErrors(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (DomainException e)
            {
                await Problem(context, StatusCodes.Status400BadRequest, e.Message);
            }
            catch (NotFoundException e)
            {
                await Problem(context, StatusCodes.Status404NotFound, e.Message);
            }
        });

    private static Task Problem(HttpContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { title = message, status });
    }
}
