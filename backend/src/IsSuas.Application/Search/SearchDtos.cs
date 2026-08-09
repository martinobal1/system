namespace IsSuas.Application.Search;

public sealed record GlobalSearchResponse(
    string Query,
    IReadOnlyList<SearchHitDto> Hits,
    IReadOnlyDictionary<string, int> CountsByType);

public sealed record SearchHitDto(
    string EntityType,
    Guid Id,
    string Title,
    string? Subtitle,
    string? OrgUnitName,
    string? Status);
