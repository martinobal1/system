using System.Text.Json;

namespace IsSuas.Application.Fleet;

public sealed record UasTypeDocDto(
    string Slug,
    string Manufacturer,
    string Model,
    JsonElement Data,
    bool IsHidden,
    bool HasManual,
    string? ManualFileName,
    long? ManualSizeBytes,
    DateTimeOffset? ManualUploadedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Uloženie typu: Data = { manufacturer, model, classEu, tagline, matchModels, sections }.</summary>
public sealed record SaveUasTypeDocRequest(string Manufacturer, string Model, JsonElement Data);

public sealed record HideUasTypeDocRequest(string? Manufacturer, string? Model);

public sealed record UploadUasTypeManualRequest(string FileName, string ContentBase64, string? Manufacturer = null, string? Model = null);

/// <summary>Text vytiahnutý z PDF manuálu (v prehliadači), z ktorého AI pripraví tabuľky špecifikácií.</summary>
public sealed record ExtractUasTypeSpecsRequest(string Text, string? FileName, string? ManufacturerHint, string? ModelHint);

public sealed record UasTypeExtractionDto(JsonElement Data, string ModelUsed);
