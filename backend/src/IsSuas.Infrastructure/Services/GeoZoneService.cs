using System.Security.Cryptography;
using System.Text.Json;
using IsSuas.Application.Abstractions;
using IsSuas.Application.GeoZones;
using IsSuas.Application.Policies;
using IsSuas.Domain.Documents;
using IsSuas.Domain.GeoZones;
using IsSuas.Infrastructure.GeoZones;
using IsSuas.Infrastructure.Persistence;
using IsSuas.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

public interface IGeoZoneService
{
    Task<GeoZoneImportDto> PreviewImportAsync(PreviewGeoZoneImportRequest request, CancellationToken cancellationToken = default);
    Task<GeoZoneImportDto> ActivateAsync(Guid importId, CancellationToken cancellationToken = default);
    Task<GeoZoneImportDto> DiscardPreviewAsync(Guid importId, CancellationToken cancellationToken = default);
    Task<GeoZoneImportDto> RemoveActiveAsync(Guid importId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GeoZoneImportDto>> ListImportsAsync(CancellationToken cancellationToken = default);
    Task<GeoZoneImportDto?> GetImportAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GeoZoneMapPayloadDto> GetActiveMapAsync(CancellationToken cancellationToken = default);
    Task<GeoZoneDto?> GetZoneAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GeoZoneDto>> ListActiveZonesAsync(CancellationToken cancellationToken = default);
}

public sealed class GeoZoneService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock,
    IObjectStorage objectStorage) : IGeoZoneService
{
    public const string DisclaimerSk =
        "Zobrazené GEO zóny pochádzajú z údajov Dopravného úradu SR a zodpovedajú verzii uvedenej v aplikácii. " +
        "Mapa nenahrádza úplnú predletovú prípravu ani kontrolu aktuálneho využitia vzdušného priestoru.";

    private const long MaxSizeBytes = 40 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<GeoZoneImportDto> PreviewImportAsync(PreviewGeoZoneImportRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.GeoZonesWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");
        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new ValidationException("Názov súboru je povinný.");
        if (string.IsNullOrWhiteSpace(request.ContentBase64))
            throw new ValidationException("Obsah súboru (ContentBase64) je povinný.");

        byte[] payload;
        try { payload = Convert.FromBase64String(request.ContentBase64); }
        catch (FormatException) { throw new ValidationException("ContentBase64 nie je platný Base64."); }

        if (payload.Length == 0 || payload.LongLength > MaxSizeBytes)
            throw new ValidationException($"Veľkosť súboru musí byť 1 B – {MaxSizeBytes} B.");

        var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var format = Path.GetExtension(request.FileName).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(format)) format = "kml";
        var dataVersion = request.DataVersionDate ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        // Drop unfinished previews from same user (keep history clean).
        var stalePreviews = await db.GeoZoneImports
            .Where(i => i.Status == GeoZoneImportStatus.Preview && i.ImportedByAccountId == currentUser.AccountId)
            .ToListAsync(cancellationToken);
        foreach (var stale in stalePreviews)
        {
            stale.Status = GeoZoneImportStatus.Failed;
            stale.ResultMessage = "Nahradené novým náhľadom importu.";
            stale.IsActive = false;
            stale.UpdatedBy = currentUser.AccountId;
        }

        GeoZoneImport import;
        try
        {
            var (zones, featureCollection) = KmlGeoZoneParser.Parse(payload, request.FileName);
            await using var stream = new MemoryStream(payload);
            var contentType = format == "zip" ? "application/zip" : "application/vnd.google-earth.kml+xml";
            var stored = await objectStorage.PutAsync(request.FileName.Trim(), contentType, stream, cancellationToken);
            var file = new StoredFile
            {
                StorageKey = stored.StorageKey,
                Provider = stored.Provider,
                FileName = request.FileName.Trim(),
                ContentType = contentType,
                SizeBytes = stored.SizeBytes,
                ContentHash = stored.ContentHash,
                ScanStatus = "SkippedDev",
                CreatedBy = currentUser.AccountId,
                UpdatedBy = currentUser.AccountId
            };
            db.StoredFiles.Add(file);

            import = new GeoZoneImport
            {
                FileName = request.FileName.Trim(),
                ContentHash = hash,
                Format = format,
                DataVersionDate = dataVersion,
                ImportedAt = clock.UtcNow,
                ImportedByAccountId = currentUser.AccountId.Value,
                ZoneCount = zones.Count,
                Status = GeoZoneImportStatus.Preview,
                ResultMessage = $"Náhľad: načítaných {zones.Count} zón.",
                StoredFileId = file.Id,
                FeatureCollectionGeoJson = featureCollection,
                CreatedBy = currentUser.AccountId,
                UpdatedBy = currentUser.AccountId
            };

            // Link file id after add — EF will set FKs on SaveChanges if we set navigation
            import.StoredFile = file;

            var seq = 0;
            foreach (var z in zones)
            {
                seq++;
                import.Zones.Add(new GeoZone
                {
                    ExternalKey = Truncate(z.ExternalKey, 128) ?? ("zone-" + seq),
                    Name = Truncate(z.Name, 256) ?? ("Zona-" + seq),
                    ZoneType = Truncate(z.ZoneType, 128),
                    RestrictionDescription = Truncate(z.RestrictionDescription, 4000),
                    FlightConditions = Truncate(z.FlightConditions, 4000),
                    HorizontalLimits = Truncate(z.HorizontalLimits, 1000),
                    VerticalLimits = Truncate(z.VerticalLimits, 1000),
                    ValidFrom = z.ValidFrom,
                    ValidTo = z.ValidTo,
                    AuthorityContact = Truncate(z.AuthorityContact, 512),
                    SourceLabel = Truncate(z.SourceLabel, 256),
                    DescriptionHtml = z.DescriptionHtml,
                    GeometryGeoJson = z.GeometryGeoJson,
                    PropertiesJson = JsonSerializer.Serialize(z.Properties),
                    Sequence = seq,
                    CreatedBy = currentUser.AccountId,
                    UpdatedBy = currentUser.AccountId
                });
            }

            // Inject importId into feature properties after save — rebuild with zone ids
            db.GeoZoneImports.Add(import);
            await db.SaveChangesAsync(cancellationToken);

            import.FeatureCollectionGeoJson = BuildFeatureCollection(import.Zones.OrderBy(z => z.Sequence).ToList(), import);
            await db.SaveChangesAsync(cancellationToken);
            await audit.WriteAsync("GeoZoneImport.Preview", nameof(GeoZoneImport), import.Id, null,
                new { import.FileName, import.ZoneCount, import.DataVersionDate }, cancellationToken: cancellationToken);
        }
        catch (ValidationException ex)
        {
            import = new GeoZoneImport
            {
                FileName = request.FileName.Trim(),
                ContentHash = hash,
                Format = format,
                DataVersionDate = dataVersion,
                ImportedAt = clock.UtcNow,
                ImportedByAccountId = currentUser.AccountId.Value,
                ZoneCount = 0,
                Status = GeoZoneImportStatus.Failed,
                ResultMessage = ex.Message,
                IsActive = true,
                CreatedBy = currentUser.AccountId,
                UpdatedBy = currentUser.AccountId
            };
            try
            {
                await using var stream = new MemoryStream(payload);
                var stored = await objectStorage.PutAsync(request.FileName.Trim(), "application/octet-stream", stream, cancellationToken);
                var file = new StoredFile
                {
                    StorageKey = stored.StorageKey,
                    Provider = stored.Provider,
                    FileName = request.FileName.Trim(),
                    ContentType = "application/octet-stream",
                    SizeBytes = stored.SizeBytes,
                    ContentHash = stored.ContentHash,
                    ScanStatus = "SkippedDev",
                    CreatedBy = currentUser.AccountId,
                    UpdatedBy = currentUser.AccountId
                };
                db.StoredFiles.Add(file);
                import.StoredFile = file;
            }
            catch
            {
                // ignore storage failure on already-invalid import
            }

            db.GeoZoneImports.Add(import);
            await db.SaveChangesAsync(cancellationToken);
            await audit.WriteAsync("GeoZoneImport.Failed", nameof(GeoZoneImport), import.Id, null,
                new { import.FileName, import.ResultMessage }, cancellationToken: cancellationToken);
            throw;
        }

        return await MapImportAsync(import.Id, cancellationToken)
               ?? throw new NotFoundException("Import nebol nájdený.");
    }

    public async Task<GeoZoneImportDto> ActivateAsync(Guid importId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.GeoZonesWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");

        var import = await db.GeoZoneImports.Include(i => i.Zones)
            .FirstOrDefaultAsync(i => i.Id == importId, cancellationToken)
            ?? throw new NotFoundException("Import GEO zón nebol nájdený.");

        if (import.Status == GeoZoneImportStatus.Failed)
            throw new ValidationException("Chybný import nie je možné aktivovať.");
        if (import.Status == GeoZoneImportStatus.Active)
            return (await MapImportAsync(import.Id, cancellationToken))!;
        if (import.Status != GeoZoneImportStatus.Preview)
            throw new ValidationException("Aktivovať je možné iba náhľad importu.");
        if (import.ZoneCount <= 0 || import.Zones.Count == 0)
            throw new ValidationException("Import neobsahuje žiadne zóny.");

        var previous = await db.GeoZoneImports
            .Where(i => i.Status == GeoZoneImportStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (var p in previous)
        {
            p.Status = GeoZoneImportStatus.Superseded;
            p.UpdatedBy = currentUser.AccountId;
            p.ResultMessage = (p.ResultMessage ?? "") + " | Nahradené novou verziou.";
        }

        import.Status = GeoZoneImportStatus.Active;
        import.ResultMessage = $"Aktívna verzia: {import.ZoneCount} zón (údaje ku dňu {import.DataVersionDate:dd.MM.yyyy}).";
        import.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("GeoZoneImport.Activate", nameof(GeoZoneImport), import.Id,
            new { previousIds = previous.Select(x => x.Id).ToArray() },
            new { import.ZoneCount, import.DataVersionDate }, cancellationToken: cancellationToken);

        return (await MapImportAsync(import.Id, cancellationToken))!;
    }

    public async Task<GeoZoneImportDto> DiscardPreviewAsync(Guid importId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.GeoZonesWrite);
        var import = await db.GeoZoneImports.FirstOrDefaultAsync(i => i.Id == importId, cancellationToken)
            ?? throw new NotFoundException("Import GEO zón nebol nájdený.");
        if (import.Status != GeoZoneImportStatus.Preview)
            throw new ValidationException("Zrušiť je možné iba náhľad.");
        import.Status = GeoZoneImportStatus.Failed;
        import.ResultMessage = "Náhľad zrušený správcom.";
        import.IsActive = false;
        import.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        return (await MapImportAsync(import.Id, cancellationToken))!;
    }

    public async Task<GeoZoneImportDto> RemoveActiveAsync(Guid importId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.GeoZonesWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");

        var import = await db.GeoZoneImports.Include(i => i.Zones)
            .FirstOrDefaultAsync(i => i.Id == importId, cancellationToken)
            ?? throw new NotFoundException("Import GEO zón nebol nájdený.");

        if (import.Status != GeoZoneImportStatus.Active)
            throw new ValidationException("Odstrániť z mapy je možné iba aktívny import.");

        import.Status = GeoZoneImportStatus.Removed;
        import.FeatureCollectionGeoJson = """{"type":"FeatureCollection","features":[]}""";
        import.ResultMessage = "Aktívna verzia odstránená správcom — mapa je bez GEO zón.";
        import.UpdatedBy = currentUser.AccountId;
        foreach (var z in import.Zones)
        {
            z.IsActive = false;
            z.UpdatedBy = currentUser.AccountId;
        }

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("GeoZoneImport.Remove", nameof(GeoZoneImport), import.Id,
            null, new { import.FileName, import.ZoneCount }, cancellationToken: cancellationToken);
        return (await MapImportAsync(import.Id, cancellationToken))!;
    }

    public async Task<IReadOnlyList<GeoZoneImportDto>> ListImportsAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.GeoZonesWrite);
        var items = await db.GeoZoneImports.AsNoTracking()
            .Include(i => i.ImportedByAccount)
            .OrderByDescending(i => i.ImportedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        return items.Select(MapImport).ToList();
    }

    public async Task<GeoZoneImportDto?> GetImportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.GeoZonesWrite);
        return await MapImportAsync(id, cancellationToken);
    }

    public async Task<GeoZoneMapPayloadDto> GetActiveMapAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.GeoZonesRead);
        var active = await db.GeoZoneImports.AsNoTracking()
            .Where(i => i.Status == GeoZoneImportStatus.Active)
            .OrderByDescending(i => i.ImportedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (active is null)
        {
            return new GeoZoneMapPayloadDto(
                null, null, null, 0,
                """{"type":"FeatureCollection","features":[]}""",
                DisclaimerSk);
        }

        var geoJson = active.FeatureCollectionGeoJson;
        if (string.IsNullOrWhiteSpace(geoJson))
        {
            var zones = await db.GeoZones.AsNoTracking()
                .Where(z => z.ImportId == active.Id && z.IsActive)
                .OrderBy(z => z.Sequence)
                .ToListAsync(cancellationToken);
            geoJson = BuildFeatureCollection(zones, active);
        }

        return new GeoZoneMapPayloadDto(
            active.Id,
            active.DataVersionDate,
            active.ImportedAt,
            active.ZoneCount,
            geoJson!,
            DisclaimerSk);
    }

    public async Task<GeoZoneDto?> GetZoneAsync(Guid id, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.GeoZonesRead);
        var zone = await db.GeoZones.AsNoTracking()
            .Include(z => z.Import)
            .FirstOrDefaultAsync(z => z.Id == id, cancellationToken);
        return zone is null ? null : MapZone(zone);
    }

    public async Task<IReadOnlyList<GeoZoneDto>> ListActiveZonesAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.GeoZonesRead);
        var activeId = await db.GeoZoneImports.AsNoTracking()
            .Where(i => i.Status == GeoZoneImportStatus.Active)
            .OrderByDescending(i => i.ImportedAt)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (activeId is null) return [];

        var zones = await db.GeoZones.AsNoTracking()
            .Include(z => z.Import)
            .Where(z => z.ImportId == activeId && z.IsActive)
            .OrderBy(z => z.Name)
            .ToListAsync(cancellationToken);
        return zones.Select(MapZone).ToList();
    }

    private async Task<GeoZoneImportDto?> MapImportAsync(Guid id, CancellationToken cancellationToken)
    {
        var import = await db.GeoZoneImports.AsNoTracking()
            .Include(i => i.ImportedByAccount)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        return import is null ? null : MapImport(import);
    }

    private static GeoZoneImportDto MapImport(GeoZoneImport i) =>
        new(i.Id, i.FileName, i.ContentHash, i.Format, i.DataVersionDate, i.ImportedAt,
            i.ImportedByAccountId,
            i.ImportedByAccount?.UserName ?? i.ImportedByAccountId.ToString("N")[..8],
            i.ZoneCount, i.Status, i.ResultMessage,
            i.Status == GeoZoneImportStatus.Active,
            i.StoredFileId);

    private static GeoZoneDto MapZone(GeoZone z)
    {
        Dictionary<string, string> props;
        try
        {
            props = JsonSerializer.Deserialize<Dictionary<string, string>>(z.PropertiesJson, JsonOpts)
                    ?? new Dictionary<string, string>();
        }
        catch
        {
            props = new Dictionary<string, string>();
        }

        return new GeoZoneDto(
            z.Id, z.ImportId, z.ExternalKey, z.Name, z.ZoneType, z.RestrictionDescription,
            z.FlightConditions, z.HorizontalLimits, z.VerticalLimits, z.ValidFrom, z.ValidTo,
            z.AuthorityContact, z.SourceLabel, z.DescriptionHtml, z.GeometryGeoJson, props,
            z.Import.ImportedAt, z.Import.DataVersionDate);
    }

    private static string BuildFeatureCollection(IReadOnlyList<GeoZone> zones, GeoZoneImport import)
    {
        var features = new List<object>();
        foreach (var z in zones)
        {
            object? geometry;
            try { geometry = JsonSerializer.Deserialize<object>(z.GeometryGeoJson); }
            catch { continue; }
            if (geometry is null) continue;

            features.Add(new Dictionary<string, object?>
            {
                ["type"] = "Feature",
                ["id"] = z.Id.ToString(),
                ["properties"] = new Dictionary<string, object?>
                {
                    ["id"] = z.Id,
                    ["importId"] = import.Id,
                    ["name"] = z.Name,
                    ["zoneType"] = z.ZoneType,
                    ["restrictionDescription"] = z.RestrictionDescription,
                    ["flightConditions"] = z.FlightConditions,
                    ["horizontalLimits"] = z.HorizontalLimits,
                    ["verticalLimits"] = z.VerticalLimits,
                    ["validFrom"] = z.ValidFrom,
                    ["validTo"] = z.ValidTo,
                    ["authorityContact"] = z.AuthorityContact,
                    ["sourceLabel"] = z.SourceLabel,
                    ["externalKey"] = z.ExternalKey,
                    ["dataVersionDate"] = import.DataVersionDate.ToString("yyyy-MM-dd"),
                    ["importDate"] = import.ImportedAt
                },
                ["geometry"] = geometry
            });
        }

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["type"] = "FeatureCollection",
            ["features"] = features
        });
    }

    private static string? Truncate(string? value, int max)
    {
        if (value is null) return null;
        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }
}
