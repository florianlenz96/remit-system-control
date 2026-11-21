using System.Security.Cryptography;
using System.Text;

namespace Remit.App.Mcp;

/// <summary>
/// Interim protection of <c>/mcp</c> (ADR 0009): a bearer key that only the operations agent knows. The key
/// comes from configuration (a Container Apps secret), never from the repository. Without a key the
/// endpoint is closed – except in local development.
/// </summary>
public static class McpAccess
{
    public const string Path = "/mcp";

    public static IApplicationBuilder UseMcpAccess(this IApplicationBuilder app, IConfiguration config, IHostEnvironment env)
    {
        var key = config["Remit:Mcp:ApiKey"];
        var expected = string.IsNullOrWhiteSpace(key) ? null : SHA256.HashData(Encoding.UTF8.GetBytes(key));

        return app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments(Path))
            {
                await next(context);
                return;
            }

            if (expected is null)
            {
                if (env.IsDevelopment())
                {
                    await next(context);
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync("The MCP endpoint is not configured (Remit:Mcp:ApiKey).");
                return;
            }

            var header = context.Request.Headers.Authorization.ToString();
            var presented = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : "";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
            if (!CryptographicOperations.FixedTimeEquals(hash, expected))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                return;
            }

            await next(context);
        });
    }
}
