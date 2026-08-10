using IsSuas.Domain.GeoZones;

namespace IsSuas.Application.GeoZones;

public sealed record GeoZoneImportDto(
    Guid Id,
    string FileName,
    string ContentHash,
    string Format,
    DateOnly DataVersionDate,
    DateTimeOffset ImportedAt,
    Guid ImportedByAccountId,
    string ImportedByUserName,
    int ZoneCount,
    GeoZoneImportStatus Status,
    string? ResultMessage,
    bool IsActiveVersion,
    Guid? StoredFileId);

public sealed record GeoZoneDto(
    Guid Id,
    Guid ImportId,
    string ExternalKey,
    string Name,
    string? ZoneType,
    string? RestrictionDescription,
    string? FlightConditions,
    string? HorizontalLimits,
    string? VerticalLimits,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? AuthorityContact,
    string? SourceLabel,
    string? DescriptionHtml,
    string GeometryGeoJson,
    IReadOnlyDictionary<string, string> Properties,
    DateTimeOffset ImportDate,
    DateOnly DataVersionDate);

public sealed record GeoZoneMapPayloadDto(
    Guid? ActiveImportId,
    DateOnly? DataVersionDate,
    DateTimeOffset? ImportedAt,
    int ZoneCount,
    string FeatureCollectionGeoJson,
    string DisclaimerSk);

public sealed record PreviewGeoZoneImportRequest(
    string FileName,
    string ContentBase64,
    DateOnly? DataVersionDate = null);

public sealed record ActivateGeoZoneImportRequest(uint RowVersion);
