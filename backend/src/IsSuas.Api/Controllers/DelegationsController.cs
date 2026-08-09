using IsSuas.Application.Accounts;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/delegations")]
public sealed class DelegationsController(IDelegationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DelegatedPermissionDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<DelegatedPermissionDto>> Create(
        [FromBody] CreateDelegatedPermissionRequest request,
        CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<ActionResult<DelegatedPermissionDto>> Revoke(
        Guid id,
        [FromBody] RevokeDelegatedPermissionRequest request,
        CancellationToken cancellationToken)
        => Ok(await service.RevokeAsync(id, request, cancellationToken));
}
