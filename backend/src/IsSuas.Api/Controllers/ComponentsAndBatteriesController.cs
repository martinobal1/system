using IsSuas.Application.Fleet;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/components")]
public sealed class ComponentsController(IFleetService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ComponentDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListComponentsAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ComponentDto>> Create([FromBody] CreateComponentRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateComponentAsync(request, cancellationToken));

    [HttpPost("{id:guid}/installations")]
    public async Task<ActionResult<InstallationDto>> Install(Guid id, [FromBody] InstallComponentRequest request, CancellationToken cancellationToken)
        => Ok(await service.InstallComponentAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/installations/{installationId:guid}/remove")]
    public async Task<ActionResult<InstallationDto>> Remove(Guid id, Guid installationId, [FromBody] RemoveInstallationRequest request, CancellationToken cancellationToken)
        => Ok(await service.RemoveInstallationAsync(id, installationId, request, cancellationToken));

    [HttpGet("{id:guid}/compatibility")]
    public async Task<ActionResult<IReadOnlyList<CompatibilityRuleDto>>> ListCompat(Guid id, CancellationToken cancellationToken)
        => Ok(await service.ListComponentCompatAsync(id, cancellationToken));

    [HttpPost("{id:guid}/compatibility")]
    public async Task<ActionResult<CompatibilityRuleDto>> AddCompat(Guid id, [FromBody] CreateCompatibilityRuleRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddComponentCompatAsync(id, request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/batteries")]
public sealed class BatteriesController(IFleetService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BatteryDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListBatteriesAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<BatteryDto>> Create([FromBody] CreateBatteryRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateBatteryAsync(request, cancellationToken));

    [HttpGet("{id:guid}/compatibility")]
    public async Task<ActionResult<IReadOnlyList<CompatibilityRuleDto>>> ListCompat(Guid id, CancellationToken cancellationToken)
        => Ok(await service.ListBatteryCompatAsync(id, cancellationToken));

    [HttpPost("{id:guid}/compatibility")]
    public async Task<ActionResult<CompatibilityRuleDto>> AddCompat(Guid id, [FromBody] CreateCompatibilityRuleRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddBatteryCompatAsync(id, request, cancellationToken));

    [HttpGet("{id:guid}/technical-records")]
    public async Task<ActionResult<IReadOnlyList<BatteryTechnicalRecordDto>>> ListTechnical(Guid id, CancellationToken cancellationToken)
        => Ok(await service.ListBatteryTechnicalAsync(id, cancellationToken));

    [HttpPost("{id:guid}/technical-records")]
    public async Task<ActionResult<BatteryTechnicalRecordDto>> AddTechnical(Guid id, [FromBody] CreateBatteryTechnicalRecordRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddBatteryTechnicalAsync(id, request, cancellationToken));

    [HttpGet("{id:guid}/operation-records")]
    public async Task<ActionResult<IReadOnlyList<BatteryOperationRecordDto>>> ListOperations(Guid id, CancellationToken cancellationToken)
        => Ok(await service.ListBatteryOperationsAsync(id, cancellationToken));

    [HttpPost("{id:guid}/operation-records")]
    public async Task<ActionResult<BatteryOperationRecordDto>> AddOperation(Guid id, [FromBody] CreateBatteryOperationRecordRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddBatteryOperationAsync(id, request, cancellationToken));

    [HttpGet("{id:guid}/status-history")]
    public async Task<ActionResult<IReadOnlyList<BatteryStatusHistoryDto>>> StatusHistory(Guid id, CancellationToken cancellationToken)
        => Ok(await service.ListBatteryStatusHistoryAsync(id, cancellationToken));

    [HttpPost("{id:guid}/transition")]
    public async Task<ActionResult<BatteryDto>> Transition(Guid id, [FromBody] TransitionBatteryStatusRequest request, CancellationToken cancellationToken)
        => Ok(await service.TransitionBatteryStatusAsync(id, request, cancellationToken));
}
