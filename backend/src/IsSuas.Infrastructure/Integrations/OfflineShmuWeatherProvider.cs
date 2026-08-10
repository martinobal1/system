using IsSuas.Application.Weather;
using Microsoft.Extensions.Configuration;

namespace IsSuas.Infrastructure.Integrations;

public interface IWeatherProvider
{
    string ProviderCode { get; }
    Task<WeatherSnapshotDto> GetCurrentAsync(string? stationCode = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Offline SHMÚ-style forecast — no outbound calls; deterministic values for SK local time.
/// Timestamps are always UTC (Offset=0) for Npgsql safety if ever persisted.
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
        var tz = SlovakiaTimeZone();
        var nowUtc = DateTimeOffset.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, tz);

        // Align to current local clock hour (wall time in SK).
        var localHourStart = new DateTimeOffset(
            nowLocal.Year, nowLocal.Month, nowLocal.Day, nowLocal.Hour, 0, 0, nowLocal.Offset);
        var seasonalBase = ApplySeason(baseTempC, localHourStart.Month);

        var hourly = new List<WeatherHourPointDto>(24);
        for (var h = 0; h < 24; h++)
        {
            var atLocal = localHourStart.AddHours(h);
            var clockHour = atLocal.Hour;
            // Diurnal: coolest ~05:00, warmest ~15:00 (local SK).
            var wave = Math.Sin((clockHour - 5) / 24.0 * Math.PI * 2);
            var temp = Math.Round(seasonalBase + wave * 4.5, 1);
            var wind = Math.Round(Math.Max(0.5, baseWindMs + wave * 1.4 + (clockHour is >= 11 and <= 17 ? 0.8 : 0)), 1);
            var precip = (int)Math.Clamp(humidity / 5.0 + (wave > 0.35 ? 10 : 2) + (humidity >= 70 ? 5 : 0), 0, 90);
            var condition = PickCondition(wind, humidity, precip);
            // Persist/serialize as UTC only (Offset=0).
            var atUtc = atLocal.ToUniversalTime();
            hourly.Add(new WeatherHourPointDto(atUtc, temp, wind, precip, condition.code));
        }

        var current = hourly[0];
        var nowCondition = PickCondition(current.WindSpeedMs, humidity, current.PrecipitationChancePct);
        var advice = FlightAdviceFor(nowCondition, current.WindSpeedMs);
        var observedUtc = localHourStart.ToUniversalTime();

        var snapshot = new WeatherSnapshotDto(
            ProviderCode,
            "SHMÚ (lokálny snímok)",
            name,
            code,
            lat,
            lon,
            observedUtc,
            observedUtc.AddHours(1),
            current.TemperatureC,
            Math.Round(current.TemperatureC - Math.Min(2.5, current.WindSpeedMs * 0.35), 1),
            current.WindSpeedMs,
            Math.Round(current.WindSpeedMs + 1.5, 1),
            windDir,
            humidity,
            pressure,
            nowCondition.code,
            nowCondition.label,
            advice,
            hourly);

        return Task.FromResult(snapshot);
    }

    private static TimeZoneInfo SlovakiaTimeZone()
    {
        foreach (var id in new[] { "Europe/Bratislava", "Central Europe Standard Time", "CET" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "Europe/Bratislava-approx",
            DateTime.UtcNow.Month is >= 4 and <= 10 ? TimeSpan.FromHours(2) : TimeSpan.FromHours(1),
            "SK",
            "SK");
    }

    /// <summary>Base station temps are spring-like; shift by month for SK climate.</summary>
    private static double ApplySeason(double springBaseTempC, int month) => month switch
    {
        12 or 1 or 2 => springBaseTempC - 10,
        3 or 11 => springBaseTempC - 4,
        4 or 10 => springBaseTempC - 1,
        5 or 9 => springBaseTempC + 2,
        6 => springBaseTempC + 5,
        7 or 8 => springBaseTempC + 7,
        _ => springBaseTempC
    };

    private static (string name, double lat, double lon, double baseTempC, double baseWindMs, int humidity, double pressure, int windDir)
        ResolveStation(string code) => code switch
        {
            "KOSICE" or "KE" => ("Košice", 48.7164, 21.2611, 17.0, 3.2, 58, 1014, 120),
            "ZILINA" or "ZA" => ("Žilina", 49.2231, 18.7394, 15.5, 2.8, 62, 1012, 90),
            "POPRAD" or "PP" => ("Poprad-Tatry", 49.0736, 20.2411, 12.0, 4.0, 55, 1010, 200),
            "BANSKABYSTRICA" or "BB" => ("Banská Bystrica", 48.7363, 19.1462, 16.0, 2.5, 60, 1013, 150),
            _ => ("Bratislava-Koliba", 48.1678, 17.1106, 18.0, 3.5, 55, 1015, 180)
        };

    private static (string code, string label) PickCondition(double wind, int humidity, int precipChance = 0)
    {
        if (wind >= 8) return ("WINDY", "Veterno");
        if (precipChance >= 55 || humidity >= 82) return ("CLOUDY", "Zamračené");
        if (humidity >= 62 || precipChance >= 30) return ("PARTLY", "Polojasno");
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
