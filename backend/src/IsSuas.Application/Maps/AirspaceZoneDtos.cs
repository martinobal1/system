namespace IsSuas.Application.Maps;

/// <summary>Katalóg sublayerov ArcGIS MapServer (VFRM_popup).</summary>
public sealed record MapLayerInfoDto(int Id, string Name, string LabelSk, bool DefaultVisible, string? GeometryType);

public sealed record MapServiceCatalogDto(
    string ServiceUrl,
    string Title,
    IReadOnlyList<MapLayerInfoDto> Layers);

/// <summary>Atribúty zóny vzdušného priestoru z LPS VFRM identify/query.</summary>
public sealed record AirspaceZoneInfo(
    int? ObjectId,
    string? LayerName,
    string? Name,
    string? Ident,
    string? TypeCode,
    string? ClassCode,
    string? LowerLimit,
    string? UpperLimit,
    string? Remarks,
    string? Activity,
    string? MilitaryCode,
    IReadOnlyDictionary<string, string?> RawAttributes);

public sealed record MapIdentifyResultDto(
    double Latitude,
    double Longitude,
    IReadOnlyList<AirspaceZoneInfo> Zones,
    string? ErrorMessage);
