using IsSuas.Application.Weather;
using Microsoft.Extensions.Configuration;

namespace IsSuas.Infrastructure.Integrations;

public interface IWeatherProvider
{
    string ProviderCode { get; }
    Task<WeatherSnapshotDto> GetCurrentAsync(string? stationCode = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Offline SHMÚ-style forecast — no outbound calls; fixed deterministic values (stable across restarts).
/// </summary>
public sealed class OfflineShmuWeatherProvider(IConfiguration configuration) : IWeatherProvider
{
    public string ProviderCode => configuration["Weather:Provider"] ?? "OfflineShmu";

    public Task<WeatherSnapshotDto> GetCurrentAsync(string? stationCode = null, CancellationToken cancellationToken = default)
    {
        var code = string.IsNullOrWhiteSpace(stationCode)
            ? (configuration["Weather:DefaultStation"] ?? "BRATISLAVA")
            : stationCode.Trim().ToUpperInvariant();

        var (name, lat, lon, baseTempC, baseWindMs, humidity, pressure, windDir) = ResolveStation(code);
        var condition = PickCondition(baseWindMs, humidity);
        var advice = FlightAdviceFor(condition, baseWindMs);

        // Fixed 24h profile relative to „now“ for timestamps only — values do not depend on RNG / hash / day.
        var now = DateTimeOffset.UtcNow;
        var hourly = new List<WeatherHourPointDto>(24);
        for (var h = 0; h < 24; h++)
        {
            var at = now.AddHours(h); // UTC — Npgsql-safe if ever persisted; UI formátuje lokálne
            // Deterministic diurnal curve: coolest near hour 5, warmest near hour 15.
            var wave = Math.Sin((h - 5) / 24.0 * Math.PI * 2);
            var temp = Math.Round(baseTempC + wave * 3.0, 1);
            var wind = Math.Round(Math.Max(0.5, baseWindMs + wave * 1.2), 1);
            var precip = (int)Math.Clamp(humidity / 5.0 + (wave > 0.4 ? 8 : 2), 0, 90);
            hourly.Add(new WeatherHourPointDto(at, temp, wind, precip, condition.code));
        }

        var current = hourly[0];
        var snapshot = new WeatherSnapshotDto(
            ProviderCode,
            "SHMÚ (lokálny snímok)",
            name,
            code,
            lat,
            lon,
            now,
            now.AddHours(1),
            current.TemperatureC,
            Math.Round(current.TemperatureC - Math.Min(2.0, current.WindSpeedMs * 0.3), 1),
            current.WindSpeedMs,
            Math.Round(current.WindSpeedMs + 1.5, 1),
            windDir,
            humidity,
            pressure,
            condition.code,
            condition.label,
            advice,
            hourly);

        return Task.FromResult(snapshot);
    }

    private static (string name, double lat, double lon, double baseTempC, double baseWindMs, int humidity, double pressure, int windDir)
        ResolveStation(string code) => code switch
    {
        "KOSICE" or "KE" => ("Košice", 48.7164, 21.2611, 17.0, 3.2, 58, 1014, 120),
        "ZILINA" or "ZA" => ("Žilina", 49.2231, 18.7394, 15.5, 2.8, 62, 1012, 90),
        "POPRAD" or "PP" => ("Poprad-Tatry", 49.0736, 20.2411, 12.0, 4.0, 55, 1010, 200),
        "BANSKABYSTRICA" or "BB" => ("Banská Bystrica", 48.7363, 19.1462, 16.0, 2.5, 60, 1013, 150),
        _ => ("Bratislava-Koliba", 48.1678, 17.1106, 18.0, 3.5, 55, 1015, 180)
    };

    private static (string code, string label) PickCondition(double wind, int humidity)
    {
        if (wind >= 8) return ("WINDY", "Veterno");
        if (humidity >= 78) return ("CLOUDY", "Zamračené");
        if (humidity >= 62) return ("PARTLY", "Polojasno");
        return ("CLEAR", "Jasno");
    }

    private static string FlightAdviceFor((string code, string label) condition, double windMs)
    {
        if (windMs >= 10)
            return "Silný vietor — skontrolujte limity UAS pred letom.";
        if (condition.code is "CLOUDY" or "WINDY")
            return "Obmedzené vizuálne podmienky / vietor — zvýšená opatrnosť.";
        return "Podmienky vhodné na vizuálne lety (VLOS) podľa lokálnych limít.";
    }
}
