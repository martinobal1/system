using IsSuas.Application.SystemMaintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

/// <summary>Verejný stav režimu údržby — UI ho potrebuje aj bez prihlásenia.</summary>
[ApiController]
[Route("api/v1/system/maintenance")]
public sealed class SystemMaintenanceController(IMaintenanceModeService service) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<MaintenanceModeStatusDto>> Get(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await service.GetStatusAsync(cancellationToken));
    }
}
