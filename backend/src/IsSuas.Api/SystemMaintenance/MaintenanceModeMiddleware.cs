using System.Globalization;
using IsSuas.Application.SystemMaintenance;
using IsSuas.Infrastructure.SystemMaintenance;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IsSuas.Api.SystemMaintenance;

/// <summary>
/// Počas režimu údržby vráti na VŠETKY API požiadavky 503 — aj na prihlásenie, pre každý účet vrátane gestora.
/// Výnimkou je len verejný stav (GET /api/v1/system/maintenance). Núdzový vypínač beží mimo /api na tajnej adrese.
/// Statické súbory a SPA sa servírujú ďalej, aby UI vedelo zobraziť obrazovku údržby.
/// Cesty sú relatívne voči koreňu aplikácie (Request.Path bez PathBase), takže nezávisia od domény.
/// </summary>
public sealed class MaintenanceModeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IMaintenanceModeService maintenance,
        IOptionsMonitor<MaintenanceModeOptions> options)
    {
        if (!IsBlockableRequest(context.Request))
        {
            await next(context);
            return;
        }

        var state = await maintenance.GetStateAsync(context.RequestAborted);
        if (!state.IsActive)
        {
            await next(context);
            return;
        }

        var retryAfter = options.CurrentValue.RetryAfterSeconds;
        if (state.PlannedEndAt is { } end)
        {
            var seconds = (int)Math.Ceiling((end - DateTimeOffset.UtcNow).TotalSeconds);
            if (seconds > 0)
                retryAfter = seconds;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = Math.Clamp(retryAfter, 30, 86400).ToString(CultureInfo.InvariantCulture);
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Maintenance-Mode"] = "on";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Prebieha údržba systému",
            Detail = state.Message ?? MaintenanceModeConstants.DefaultMessage
        };
        problem.Extensions["maintenance"] = true;
        problem.Extensions["plannedEndAt"] = state.PlannedEndAt;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }

    private static bool IsBlockableRequest(HttpRequest request)
    {
        if (!request.Path.StartsWithSegments("/api"))
            return false; // index.html, js, css, obrázky, swagger, tajná adresa vypínača
        if (HttpMethods.IsOptions(request.Method))
            return false; // CORS preflight
        if (HttpMethods.IsGet(request.Method) && request.Path.Equals("/api/v1/system/maintenance", StringComparison.OrdinalIgnoreCase))
            return false; // verejný stav pre UI
        return true;
    }
}
