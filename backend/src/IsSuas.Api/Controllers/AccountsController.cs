using IsSuas.Application.Accounts;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/accounts")]
public sealed class AccountsController(IAccountService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<AccountCreatedDto>> Create([FromBody] CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = created.Account.Id }, created);
    }

    [HttpPost("{id:guid}/confirm-license")]
    public async Task<ActionResult<AccountDto>> ConfirmLicense(Guid id, [FromBody] ConfirmLicenseRequest request, CancellationToken cancellationToken)
        => Ok(await service.ConfirmLicenseAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/password")]
    public async Task<ActionResult<AccountDto>> SetPassword(Guid id, [FromBody] SetAccountPasswordRequest request, CancellationToken cancellationToken)
        => Ok(await service.SetPasswordAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<AccountDto>> ChangeStatus(Guid id, [FromBody] ChangeAccountStatusRequest request, CancellationToken cancellationToken)
        => Ok(await service.ChangeStatusAsync(id, request, cancellationToken));

    [HttpGet("{id:guid}/role-assignments")]
    public async Task<ActionResult<IReadOnlyList<RoleAssignmentDto>>> ListRoles(Guid id, CancellationToken cancellationToken)
        => Ok(await service.ListRoleAssignmentsAsync(id, cancellationToken));

    [HttpPost("{id:guid}/role-assignments")]
    public async Task<ActionResult<RoleAssignmentDto>> AssignRole(Guid id, [FromBody] CreateRoleAssignmentRequest request, CancellationToken cancellationToken)
        => Ok(await service.AssignRoleAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/role-assignments/{assignmentId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateRole(Guid id, Guid assignmentId, [FromBody] DeactivateRoleAssignmentRequest request, CancellationToken cancellationToken)
    {
        await service.DeactivateRoleAssignmentAsync(id, assignmentId, request, cancellationToken);
        return NoContent();
    }
}
