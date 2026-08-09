namespace IsSuas.Application.Weather;

public sealed record WeatherSnapshotDto(
    string ProviderCode,
    string SourceLabel,
    string StationName,
    string StationCode,
    double Latitude,
    double Longitude,
    DateTimeOffset ObservedAt,
    DateTimeOffset ValidUntil,
    double TemperatureC,
    double FeelsLikeC,
    double WindSpeedMs,
    double WindGustMs,
    int WindDirectionDeg,
    int RelativeHumidityPct,
    double PressureHpa,
    string ConditionCode,
    string ConditionLabel,
    string FlightAdvice,
    IReadOnlyList<WeatherHourPointDto> Hourly);

public sealed record WeatherHourPointDto(
    DateTimeOffset At,
    double TemperatureC,
    double WindSpeedMs,
    int PrecipitationChancePct,
    string ConditionCode);
