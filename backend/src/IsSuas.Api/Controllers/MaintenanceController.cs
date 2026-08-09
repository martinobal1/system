using IsSuas.Application.Maintenance;
using IsSuas.Domain.Enums;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/maintenance-plans")]
public sealed class MaintenancePlansController(IMaintenanceService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MaintenancePlanDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListPlansAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<MaintenancePlanDto>> Create([FromBody] CreateMaintenancePlanRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreatePlanAsync(request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/work-orders")]
public sealed class WorkOrdersController(IMaintenanceService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkOrderDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListWorkOrdersAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<WorkOrderDto>> Create([FromBody] CreateWorkOrderRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateWorkOrderAsync(request, cancellationToken));

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<MaintenanceRecordDto>> Complete(Guid id, [FromBody] CompleteWorkOrderRequest request, CancellationToken cancellationToken)
        => Ok(await service.CompleteWorkOrderAsync(id, request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/defects")]
public sealed class DefectsController(IMaintenanceService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DefectDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListDefectsAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<DefectDto>> Report([FromBody] ReportDefectRequest request, CancellationToken cancellationToken)
        => Ok(await service.ReportDefectAsync(request, cancellationToken));

    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult<DefectDto>> Resolve(Guid id, [FromBody] ResolveDefectRequest request, CancellationToken cancellationToken)
        => Ok(await service.ResolveDefectAsync(id, request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/service-requests")]
public sealed class ServiceRequestsController(IMaintenanceService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ServiceRequestDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListServiceRequestsAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ServiceRequestDto>> Create([FromBody] CreateServiceRequestRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateServiceRequestAsync(request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/technical-restrictions")]
public sealed class TechnicalRestrictionsController(IMaintenanceService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TechnicalRestrictionDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListRestrictionsAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<TechnicalRestrictionDto>> Create([FromBody] CreateTechnicalRestrictionRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateRestrictionAsync(request, cancellationToken));

    [HttpPost("{id:guid}/end")]
    public async Task<ActionResult<TechnicalRestrictionDto>> End(Guid id, [FromBody] EndTechnicalRestrictionRequest request, CancellationToken cancellationToken)
        => Ok(await service.EndRestrictionAsync(id, request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/releases-to-service")]
public sealed class ReleasesToServiceController(IMaintenanceService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReleaseToServiceDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListReleasesAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ReleaseToServiceDto>> Create([FromBody] CreateReleaseToServiceRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateReleaseAsync(request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/technical-status-history")]
public sealed class TechnicalStatusHistoryController(IMaintenanceService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TechnicalStatusHistoryDto>>> List(
        [FromQuery] MaintenanceTargetType targetType,
        [FromQuery] Guid targetId,
        CancellationToken cancellationToken)
        => Ok(await service.ListTechnicalStatusHistoryAsync(targetType, targetId, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/uas/{uasId:guid}/block-status")]
public sealed class UasBlockStatusController(IMaintenanceService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AssetBlockCheckDto>> Get(Guid uasId, CancellationToken cancellationToken)
        => Ok(await service.GetUasBlockStatusAsync(uasId, cancellationToken));
}
