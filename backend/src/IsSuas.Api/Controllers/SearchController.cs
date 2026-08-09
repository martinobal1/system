using IsSuas.Application.Search;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/search")]
public sealed class SearchController(IGlobalSearchService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GlobalSearchResponse>> Search(
        [FromQuery] string? q,
        [FromQuery] int limit = 8,
        CancellationToken cancellationToken = default)
        => Ok(await service.SearchAsync(q, limit, cancellationToken));
}
