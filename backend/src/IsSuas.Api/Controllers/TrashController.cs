using IsSuas.Application.Trash;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

/// <summary>
/// Kôš. POST /api/v1/trash = vymazať (presunúť do koša) — oprávnenia overuje služba podľa kategórie.
/// Zoznam, obnovenie a trvalé vymazanie smie iba gestor (oprávnenie trash.manage).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/trash")]
public sealed class TrashController(ITrashService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TrashActionResultDto>> Move([FromBody] MoveToTrashRequest request, CancellationToken cancellationToken)
        => Ok(await service.MoveAsync(request, cancellationToken));

    /// <summary>Hromadne do koša (napr. pilot aj s vybranými letmi).</summary>
    [HttpPost("batch")]
    public async Task<ActionResult<TrashActionResultDto>> MoveBatch([FromBody] MoveToTrashBatchRequest request, CancellationToken cancellationToken)
        => Ok(await service.MoveBatchAsync(request, cancellationToken));

    /// <summary>Čo sa viaže na záznam (pred vymazaním) — pre zaškrtávací zoznam.</summary>
    [HttpGet("dependents")]
    public async Task<ActionResult<TrashDependentsDto>> Dependents([FromQuery] string category, [FromQuery] Guid id, CancellationToken cancellationToken)
        => Ok(await service.GetDependentsForMoveAsync(category, id, cancellationToken));

    [HttpGet("summary")]
    public async Task<ActionResult<IReadOnlyList<TrashCategorySummaryDto>>> Summary(CancellationToken cancellationToken)
        => Ok(await service.SummaryAsync(cancellationToken));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TrashEntryDto>>> List([FromQuery] string? category, CancellationToken cancellationToken)
        => Ok(await service.ListAsync(category, cancellationToken));

    [HttpPost("{entryId:guid}/restore")]
    public async Task<ActionResult<TrashActionResultDto>> Restore(Guid entryId, CancellationToken cancellationToken)
        => Ok(await service.RestoreAsync(entryId, cancellationToken));

    [HttpPost("restore-batch")]
    public async Task<ActionResult<TrashActionResultDto>> RestoreBatch([FromBody] RestoreTrashBatchRequest request, CancellationToken cancellationToken)
        => Ok(await service.RestoreBatchAsync(request, cancellationToken));

    /// <summary>Čo sa viaže na záznam v koši (pred trvalým vymazaním).</summary>
    [HttpGet("{entryId:guid}/dependents")]
    public async Task<ActionResult<TrashDependentsDto>> PurgeDependents(Guid entryId, CancellationToken cancellationToken)
        => Ok(await service.GetDependentsForPurgeAsync(entryId, cancellationToken));

    [HttpPost("purge-batch")]
    public async Task<ActionResult<TrashActionResultDto>> PurgeBatch([FromBody] PurgeTrashBatchRequest request, CancellationToken cancellationToken)
        => Ok(await service.PurgeBatchAsync(request, cancellationToken));

    [HttpPost("{entryId:guid}/purge")]
    public async Task<ActionResult<TrashActionResultDto>> Purge(Guid entryId, [FromBody] PurgeTrashRequest request, CancellationToken cancellationToken)
        => Ok(await service.PurgeAsync(entryId, request, cancellationToken));
}
