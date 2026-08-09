using IsSuas.Application.Weather;
using IsSuas.Infrastructure.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/weather")]
public sealed class WeatherController(IWeatherProvider weatherProvider) : ControllerBase
{
    /// <summary>
    /// Aktuálne počasie + 24h rad (SHMÚ offline snapshot v DEV).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<WeatherSnapshotDto>> Get(
        [FromQuery] string? station = null,
        CancellationToken cancellationToken = default)
        => Ok(await weatherProvider.GetCurrentAsync(station, cancellationToken));
}
