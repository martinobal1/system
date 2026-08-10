using IsSuas.Domain.Common;
using IsSuas.Domain.Documents;
using IsSuas.Domain.Identity;

namespace IsSuas.Domain.GeoZones;

public enum GeoZoneImportStatus
{
    Preview = 0,
    Active = 1,
    Superseded = 2,
    Failed = 3
}

/// <summary>Verzia importu GEO zón Dopravného úradu SR (ZIP/KML).</summary>
public class GeoZoneImport : AuditableEntity
{
    public string FileName { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string Format { get; set; } = "kml";
    public DateOnly DataVersionDate { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public Guid ImportedByAccountId { get; set; }
    public Account ImportedByAccount { get; set; } = null!;
    public int ZoneCount { get; set; }
    public GeoZoneImportStatus Status { get; set; } = GeoZoneImportStatus.Preview;
    public string? ResultMessage { get; set; }
    public Guid? StoredFileId { get; set; }
    public StoredFile? StoredFile { get; set; }
    /// <summary>Celá FeatureCollection aktívnej verzie (pre rýchle mapové načítanie).</summary>
    public string? FeatureCollectionGeoJson { get; set; }
    public ICollection<GeoZone> Zones { get; set; } = new List<GeoZone>();
}

/// <summary>Jedna GEO zóna z importovaného KML.</summary>
public class GeoZone : AuditableEntity
{
    public Guid ImportId { get; set; }
    public GeoZoneImport Import { get; set; } = null!;
    public string ExternalKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ZoneType { get; set; }
    public string? RestrictionDescription { get; set; }
    public string? FlightConditions { get; set; }
    public string? HorizontalLimits { get; set; }
    public string? VerticalLimits { get; set; }
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? AuthorityContact { get; set; }
    public string? SourceLabel { get; set; }
    public string? DescriptionHtml { get; set; }
    /// <summary>GeoJSON Geometry (Polygon / MultiPolygon / …).</summary>
    public string GeometryGeoJson { get; set; } = string.Empty;
    /// <summary>Všetky ExtendedData / SimpleData ako JSON objekt.</summary>
    public string PropertiesJson { get; set; } = "{}";
    public int Sequence { get; set; }
}
