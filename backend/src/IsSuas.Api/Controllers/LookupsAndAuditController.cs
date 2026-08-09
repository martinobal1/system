using IsSuas.Application.Controls;
using IsSuas.Application.Lookups;
using IsSuas.Infrastructure.Persistence;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/lookups")]
public sealed class LookupsController(ILookupService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LookupCodeSummaryDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListCodesAsync(cancellationToken));

    [HttpGet("{code}")]
    public async Task<ActionResult<LookupCodeDto>> Get(string code, CancellationToken cancellationToken)
        => Ok(await service.GetAsync(code, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<LookupCodeDto>> CreateCode([FromBody] CreateLookupCodeRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateCodeAsync(request, cancellationToken));

    [HttpPost("{code}/items")]
    public async Task<ActionResult<LookupItemDto>> AddItem(string code, [FromBody] CreateLookupItemRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddItemAsync(code, request, cancellationToken));

    [HttpPost("items/{itemId:guid}/deactivate")]
    public async Task<ActionResult<LookupItemDto>> DeactivateItem(Guid itemId, [FromBody] DeactivateLookupItemRequest request, CancellationToken cancellationToken)
        => Ok(await service.DeactivateItemAsync(itemId, request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/config-rules")]
public sealed class ConfigRulesController(IConfigRuleService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConfigRuleDto>>> List([FromQuery] string? key, CancellationToken cancellationToken)
        => Ok(await service.ListAsync(key, cancellationToken));

    [HttpGet("effective/{key}")]
    public async Task<ActionResult<ConfigRuleDto?>> Effective(string key, [FromQuery] DateTimeOffset? at, CancellationToken cancellationToken)
        => Ok(await service.GetEffectiveAsync(key, at, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ConfigRuleDto>> Create([FromBody] CreateConfigRuleRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateVersionAsync(request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/integrations")]
public sealed class IntegrationsController(IIntegrationConnectorService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IntegrationConnectorDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<IntegrationConnectorDto>> Upsert([FromBody] UpsertIntegrationConnectorRequest request, CancellationToken cancellationToken)
        => Ok(await service.UpsertAsync(request, cancellationToken));

    [HttpPost("{id:guid}/runs")]
    public async Task<ActionResult<IntegrationConnectorDto>> RecordRun(Guid id, [FromBody] RecordIntegrationRunRequest request, CancellationToken cancellationToken)
        => Ok(await service.RecordRunAsync(id, request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/audit-events")]
public sealed class AuditEventsController(IAuditQueryService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditEventDto>>> Search(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] string? action,
        [FromQuery] string? actorUserName,
        [FromQuery] string? result,
        CancellationToken cancellationToken)
        => Ok(await service.SearchAsync(from, to, entityType, entityId, action, actorUserName, result, cancellationToken));

    [HttpGet("export.csv")]
    public async Task<IActionResult> Export(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] string? action,
        [FromQuery] string? actorUserName,
        [FromQuery] string? result,
        CancellationToken cancellationToken)
    {
        var csv = await service.ExportCsvAsync(from, to, entityType, entityId, action, actorUserName, result, cancellationToken);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8",
            $"audit-{DateTime.UtcNow:yyyyMMddHHmm}.csv");
    }
}

[ApiController]
[Authorize]
[Route("api/v1/system/info")]
public sealed class SystemInfoController(ISystemInfoService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SystemInfoDto>> Get(CancellationToken cancellationToken)
        => Ok(await service.GetAsync(cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/permissions")]
public sealed class PermissionsController(IsSuasDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PermissionDto>>> List(CancellationToken cancellationToken)
    {
        var items = await db.Permissions.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.ModuleCode).ThenBy(p => p.Code)
            .Select(p => new PermissionDto(p.Id, p.Code, p.NameSk, p.ModuleCode))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("role-map")]
    public async Task<ActionResult<IReadOnlyList<RolePermissionDto>>> RoleMap(CancellationToken cancellationToken)
    {
        var items = await db.RolePermissions.AsNoTracking()
            .Include(rp => rp.Role)
            .Include(rp => rp.Permission)
            .Where(rp => rp.IsActive)
            .OrderBy(rp => rp.Role.Code)
            .Select(rp => new RolePermissionDto(rp.RoleId, rp.Role.Code.ToString(), rp.Permission.Code))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }
}

[ApiController]
[Authorize]
[Route("api/v1/user-sessions")]
public sealed class UserSessionsController(IsSuasDbContext db) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<UserSessionDto>>> MySessions(CancellationToken cancellationToken)
    {
        var accountIdClaim = User.FindFirst("account_id")?.Value;
        if (!Guid.TryParse(accountIdClaim, out var accountId))
            return Unauthorized();
        var items = await db.UserSessions.AsNoTracking()
            .Where(s => s.AccountId == accountId)
            .OrderByDescending(s => s.IssuedAt)
            .Take(50)
            .Select(s => new UserSessionDto(s.Id, s.AccountId, s.IssuedAt, s.ExpiresAt, s.RevokedAt, s.IpAddress, s.Result))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }
}
