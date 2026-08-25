using IsSuas.Application.Common;
using IsSuas.Application.Fleet;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/uas")]
public sealed class UasController(IFleetService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<UasDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, CancellationToken cancellationToken = default)
        => Ok(await service.ListUasAsync(page, pageSize, search, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UasDetailDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await service.GetUasAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<UasDto>> Create([FromBody] CreateUasRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateUasAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<UasDto>> Update(Guid id, [FromBody] UpdateUasRequest request, CancellationToken cancellationToken)
        => Ok(await service.UpdateUasAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<UasDto>> Submit(Guid id, CancellationToken cancellationToken)
        => Ok(await service.SubmitUasAsync(id, cancellationToken));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<UasDto>> Approve(Guid id, CancellationToken cancellationToken)
        => Ok(await service.ApproveUasAsync(id, cancellationToken));

    [HttpPost("{id:guid}/transition")]
    public async Task<ActionResult<UasDto>> Transition(Guid id, [FromBody] TransitionUasRequest request, CancellationToken cancellationToken)
        => Ok(await service.TransitionUasAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/assignments")]
    public async Task<ActionResult<UasAssignmentDto>> Assign(Guid id, [FromBody] AssignUasRequest request, CancellationToken cancellationToken)
        => Ok(await service.AssignUasAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/transfer")]
    public async Task<ActionResult<UasAssignmentDto>> Transfer(Guid id, [FromBody] TransferUasRequest request, CancellationToken cancellationToken)
        => Ok(await service.TransferUasAsync(id, request, cancellationToken));
}
