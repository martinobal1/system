using IsSuas.Application.OrgUnits;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/org-units")]
public sealed class OrgUnitsController(IOrgUnitService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrgUnitDto>>> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
        => Ok(await service.ListAsync(includeInactive, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<OrgUnitDto>> Create([FromBody] CreateOrgUnitRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<OrgUnitDto>> Update(Guid id, [FromBody] UpdateOrgUnitRequest request, CancellationToken cancellationToken)
        => Ok(await service.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/move")]
    public async Task<ActionResult<OrgUnitDto>> Move(Guid id, [FromBody] MoveOrgUnitRequest request, CancellationToken cancellationToken)
        => Ok(await service.MoveAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await service.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        await service.ReactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
