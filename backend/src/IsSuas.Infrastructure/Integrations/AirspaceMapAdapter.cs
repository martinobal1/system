using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace IsSuas.Infrastructure.Integrations;

public enum AirspaceMapCheckResultCode
{
    Clear = 0,
    Conflict = 1,
    InsufficientData = 2,
    Error = 3
}

public sealed record AirspaceMapCheckRequest(
    Guid OperationId,
    string? LocationGeoJson,
    double? LocationLat,
    double? LocationLon,
    string? LocationAddress);

public sealed record AirspaceMapCheckResult(
    string ProviderCode,
    AirspaceMapCheckResultCode ResultCode,
    bool IsBlocking,
    string Summary,
    string? DetailsJson,
    long ElapsedMs);

public interface IAirspaceMapAdapter
{
    string ProviderCode { get; }
    Task<AirspaceMapCheckResult> CheckAsync(AirspaceMapCheckRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Offline / fail-closed LPS stub — no outbound calls, deterministic rules for DEV/Acc.
/// </summary>
public sealed class OfflineAirspaceMapAdapter(IConfiguration configuration) : IAirspaceMapAdapter
{
    public string ProviderCode => configuration["Airspace:Provider"] ?? "Offline";

    public Task<AirspaceMapCheckResult> CheckAsync(AirspaceMapCheckRequest request, CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var failClosed = configuration.GetValue("Airspace:FailClosed", true);
        var timeoutMs = configuration.GetValue("Airspace:TimeoutMs", 2000);

        // Simulate bounded processing time budget (no external I/O).
        _ = timeoutMs;

        if (string.IsNullOrWhiteSpace(request.LocationGeoJson)
            && request.LocationLat is null
            && request.LocationLon is null)
        {
            var result = new AirspaceMapCheckResult(
                ProviderCode,
                AirspaceMapCheckResultCode.InsufficientData,
                IsBlocking: failClosed,
                Summary: "Chýbajú priestorové údaje operácie (GeoJSON / lat-lon).",
                DetailsJson: JsonSerializer.Serialize(new { request.OperationId, failClosed }),
                ElapsedMs: (long)(DateTime.UtcNow - started).TotalMilliseconds);
            return Task.FromResult(result);
        }

        var geo = request.LocationGeoJson ?? string.Empty;
        var conflictMarker = configuration["Airspace:ConflictMarker"] ?? "RESTRICTED";
        var isConflict = geo.Contains(conflictMarker, StringComparison.OrdinalIgnoreCase)
            || IsInMockRestrictedBox(request.LocationLat, request.LocationLon);

        if (isConflict)
        {
            return Task.FromResult(new AirspaceMapCheckResult(
                ProviderCode,
                AirspaceMapCheckResultCode.Conflict,
                IsBlocking: true,
                Summary: "Offline LPS: konflikt s obmedzeným priestorom.",
                DetailsJson: JsonSerializer.Serialize(new
                {
                    request.OperationId,
                    conflictMarker,
                    request.LocationLat,
                    request.LocationLon
                }),
                ElapsedMs: (long)(DateTime.UtcNow - started).TotalMilliseconds));
        }

        return Task.FromResult(new AirspaceMapCheckResult(
            ProviderCode,
            AirspaceMapCheckResultCode.Clear,
            IsBlocking: false,
            Summary: "Offline LPS: bez konfliktu.",
            DetailsJson: JsonSerializer.Serialize(new { request.OperationId, provider = ProviderCode }),
            ElapsedMs: (long)(DateTime.UtcNow - started).TotalMilliseconds));
    }

    private static bool IsInMockRestrictedBox(double? lat, double? lon)
    {
        // Synthetic restricted box around Bratislava center for deterministic Acc tests.
        if (lat is null || lon is null)
            return false;
        return lat is >= 48.14 and <= 48.16 && lon is >= 17.10 and <= 17.12;
    }
}
