using System.Text.Json;
using IsSuas.Application.Weather;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IsSuas.Infrastructure.Integrations;

/// <summary>
/// Real weather from Open-Meteo (free, no API key).
/// Falls back to <see cref="OfflineShmuWeatherProvider"/> when the API is unreachable.
/// </summary>
public sealed class OpenMeteoWeatherProvider : IWeatherProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenMeteoWeatherProvider> _logger;
    private readonly HttpClient _http;
    private readonly OfflineShmuWeatherProvider _fallback;

    public OpenMeteoWeatherProvider(IConfiguration configuration, ILogger<OpenMeteoWeatherProvider> logger, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _http = httpClientFactory.CreateClient("OpenMeteo");
        _http.Timeout = TimeSpan.FromSeconds(8);
        _fallback = new OfflineShmuWeatherProvider(configuration);
    }

    public string ProviderCode => "OpenMeteo";

    public async Task<WeatherSnapshotDto> GetCurrentAsync(string? stationCode = null, CancellationToken cancellationToken = default)
    {
        var (name, lat, lon) = ResolveStation(stationCode, _configuration);
        try
        {
            var url = $"https://api.open-meteo.com/v1/forecast?" +
                      $"latitude={lat}&longitude={lon}" +
                      "&current=temperature_2m,relative_humidity_2m,wind_speed_10m,wind_gusts_10m,wind_direction_10m,surface_pressure,weather_code,apparent_temperature" +
                      "&hourly=temperature_2m,wind_speed_10m,precipitation_probability,weather_code" +
                      "&forecast_days=2&timezone=Europe%2FBratislava";

            var json = await _http.GetStringAsync(url, cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var cur = root.GetProperty("current");
            var tempC = cur.GetProperty("temperature_2m").GetDouble();
            var feelsC = cur.GetProperty("apparent_temperature").GetDouble();
            var windMs = Math.Round(cur.GetProperty("wind_speed_10m").GetDouble() / 3.6, 1);
            var gustMs = Math.Round(cur.GetProperty("wind_gusts_10m").GetDouble() / 3.6, 1);
            var windDir = (int)cur.GetProperty("wind_direction_10m").GetDouble();
            var humidity = (int)cur.GetProperty("relative_humidity_2m").GetDouble();
            var pressure = Math.Round(cur.GetProperty("surface_pressure").GetDouble(), 1);
            var wmoCode = cur.GetProperty("weather_code").GetInt32();
            var (condCode, condLabel) = WmoToCondition(wmoCode);
            var advice = FlightAdvice(condCode, windMs);

            var timeStr = cur.GetProperty("time").GetString()!;
            var observedAt = DateTimeOffset.Parse(timeStr);
            var validUntil = observedAt.AddHours(1);

            var hourlyTimes = root.GetProperty("hourly").GetProperty("time");
            var hourlyTemp = root.GetProperty("hourly").GetProperty("temperature_2m");
            var hourlyWind = root.GetProperty("hourly").GetProperty("wind_speed_10m");
            var hourlyPrecip = root.GetProperty("hourly").GetProperty("precipitation_probability");
            var hourlyWmo = root.GetProperty("hourly").GetProperty("weather_code");

            var hourly = new List<WeatherHourPointDto>();
            var startIdx = -1;
            for (var i = 0; i < hourlyTimes.GetArrayLength(); i++)
            {
                if (hourlyTimes[i].GetString() == timeStr) { startIdx = i; break; }
            }
            if (startIdx < 0) startIdx = 0;

            for (var i = startIdx; i < Math.Min(startIdx + 24, hourlyTimes.GetArrayLength()); i++)
            {
                var at = DateTimeOffset.Parse(hourlyTimes[i].GetString()!);
                var t = hourlyTemp[i].GetDouble();
                var w = Math.Round(hourlyWind[i].GetDouble() / 3.6, 1);
                var p = hourlyPrecip[i].GetInt32();
                var c = WmoToCondition(hourlyWmo[i].GetInt32()).code;
                hourly.Add(new WeatherHourPointDto(at, t, w, p, c));
            }

            return new WeatherSnapshotDto(
                ProviderCode, "Open-Meteo", name, stationCode ?? "BRATISLAVA",
                lat, lon, observedAt, validUntil,
                tempC, feelsC, windMs, gustMs, windDir, humidity, pressure,
                condCode, condLabel, advice, hourly);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open-Meteo API nedostupne, pouzivam offline fallback");
            return await _fallback.GetCurrentAsync(stationCode, cancellationToken);
        }
    }

    private static (string name, double lat, double lon) ResolveStation(string? code, IConfiguration cfg)
    {
        var c = string.IsNullOrWhiteSpace(code)
            ? (cfg["Weather:DefaultStation"] ?? "BRATISLAVA")
            : code.Trim().ToUpperInvariant();
        return c switch
        {
            "KOSICE" or "KE" => ("Košice", 48.7164, 21.2611),
            "ZILINA" or "ZA" => ("Žilina", 49.2231, 18.7394),
            "POPRAD" or "PP" => ("Poprad-Tatry", 49.0736, 20.2411),
            "BANSKABYSTRICA" or "BB" => ("Banská Bystrica", 48.7363, 19.1462),
            _ => ("Bratislava-Koliba", 48.1678, 17.1106)
        };
    }

    private static (string code, string label) WmoToCondition(int wmo) => wmo switch
    {
        0 => ("CLEAR", "Jasno"),
        1 or 2 => ("PARTLY", "Polojasno"),
        3 => ("CLOUDY", "Oblačno"),
        45 or 48 => ("FOGGY", "Hmla"),
        51 or 53 or 55 => ("DRIZZLE", "Mrholenie"),
        61 or 63 or 65 => ("RAIN", "Dážď"),
        71 or 73 or 75 or 77 => ("SNOW", "Sneženie"),
        80 or 81 or 82 => ("SHOWERS", "Prehánky"),
        85 or 86 => ("SNOW_SHOWERS", "Snehové prehánky"),
        95 or 96 or 99 => ("STORM", "Búrka"),
        _ => ("PARTLY", "Polojasno")
    };

    private static string FlightAdvice(string condCode, double windMs)
    {
        if (windMs >= 10)
            return "Silný vietor — skontrolujte limity UAS pred letom.";
        if (condCode is "STORM")
            return "Búrková činnosť — let sa neodporúča.";
        if (condCode is "CLOUDY" or "RAIN" or "SHOWERS" or "FOGGY")
            return "Obmedzené vizuálne podmienky — zvýšená opatrnosť.";
        return "Podmienky vhodné na vizuálne lety (VLOS) podľa lokálnych limít.";
    }
}
