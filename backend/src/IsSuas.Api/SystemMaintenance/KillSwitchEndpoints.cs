using IsSuas.Application.SystemMaintenance;
using IsSuas.Infrastructure.SystemMaintenance;
using Microsoft.Extensions.Options;

namespace IsSuas.Api.SystemMaintenance;

/// <summary>
/// Núdzový vypínač: stránka (GET) a prepnutie (POST) na tej istej tajnej adrese z konfigurácie
/// (MaintenanceMode:KillSwitchPath). Adresa sa nikde v kóde ani v statických súboroch nevyskytuje.
/// Bez nastavenej adresy a kľúča sa nič nezaregistruje — vypínač vtedy neexistuje.
/// </summary>
public static class KillSwitchEndpoints
{
    public const string RateLimitPolicy = "kill-switch";

    public static WebApplication MapKillSwitch(this WebApplication app)
    {
        var opts = app.Services.GetRequiredService<IOptions<MaintenanceModeOptions>>().Value;
        var path = opts.GetValidKillSwitchPath();
        if (path is null || !opts.HasValidKillSwitchKey())
        {
            if (!string.IsNullOrWhiteSpace(opts.KillSwitchPath) || !string.IsNullOrWhiteSpace(opts.KillSwitchKey))
                app.Logger.LogWarning(
                    "Núdzový vypínač je VYPNUTÝ: MaintenanceMode:KillSwitchPath musí mať 16–128 znakov (A-Z, a-z, 0-9, -, _) " +
                    "a MaintenanceMode:KillSwitchKey aspoň {Min} znakov.", MaintenanceModeOptions.MinKillSwitchKeyLength);
            return app;
        }

        app.MapGet(path, (HttpContext context) =>
            {
                SetPrivateHeaders(context);
                return Results.Content(KillSwitchPage.Html, "text/html; charset=utf-8");
            })
            .ExcludeFromDescription();

        app.MapPost(path, async (KillSwitchRequest request, IMaintenanceModeService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                SetPrivateHeaders(context);
                var client = context.Connection.RemoteIpAddress?.ToString();
                var result = await service.SetWithKillSwitchKeyAsync(request, client, cancellationToken);
                return result is null
                    ? Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Nesprávny kľúč.")
                    : Results.Ok(result);
            })
            .RequireRateLimiting(RateLimitPolicy)
            .ExcludeFromDescription();

        app.Logger.LogInformation("Núdzový vypínač je zapnutý na tajnej adrese (MaintenanceMode:KillSwitchPath).");
        return app;
    }

    private static void SetPrivateHeaders(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
