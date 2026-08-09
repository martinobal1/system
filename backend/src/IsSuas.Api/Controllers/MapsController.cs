using System.Globalization;
using System.Text.Json;
using IsSuas.Application.Maps;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

/// <summary>
/// Proxy / metadata pre LPS SR VFR Manual ArcGIS REST (CORS + C# identify fallback).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/maps")]
public sealed class MapsController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    private const string LpsBase = "https://gis.lps.sk/server/rest/services/";
    private const string PopupService = "VFRM/VFRM_popup/MapServer";

    private static readonly Dictionary<int, string> LayerLabelsSk = new()
    {
        [0] = "Letiská SRSS",
        [1] = "Letiská / heliporty",
        [2] = "Prekážky ENR",
        [3] = "Navigačné prostriedky",
        [4] = "Vzdušný priestor (body)",
        [5] = "Vzdušné priestory",
        [6] = "NON-AIXM línie",
        [7] = "NON-AIXM plochy",
        [8] = "Prekážky (detail)"
    };

    /// <summary>Zoznam sublayerov VFRM_popup (layer ID + názvy).</summary>
    [HttpGet("layers")]
    public async Task<ActionResult<MapServiceCatalogDto>> Layers(CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("lps-gis");
            using var res = await client.GetAsync($"{PopupService}?f=json", cancellationToken);
            res.EnsureSuccessStatusCode();
            await using var stream = await res.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var layers = new List<MapLayerInfoDto>();
            if (doc.RootElement.TryGetProperty("layers", out var arr))
            {
                foreach (var layer in arr.EnumerateArray())
                {
                    var id = layer.GetProperty("id").GetInt32();
                    var name = layer.GetProperty("name").GetString() ?? $"Layer {id}";
                    var visible = layer.TryGetProperty("defaultVisibility", out var dv) && dv.GetBoolean();
                    var geom = layer.TryGetProperty("geometryType", out var gt) ? gt.GetString() : null;
                    layers.Add(new MapLayerInfoDto(
                        id,
                        name,
                        LayerLabelsSk.GetValueOrDefault(id, name),
                        visible,
                        geom));
                }
            }

            return Ok(new MapServiceCatalogDto(
                LpsBase + PopupService,
                "VFR Manual — vzdušné priestory (LPS SR)",
                layers));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                title = "Mapová služba LPS nie je dostupná",
                detail = ex.Message
            });
        }
    }

    /// <summary>Identify na danej WGS84 pozícii (C# alternatíva k JS esri-leaflet).</summary>
    [HttpGet("identify")]
    public async Task<ActionResult<MapIdentifyResultDto>> Identify(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] int tolerance = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("lps-gis");
            // mapExtent okolo bodu (stupne) — ArcGIS identify vyžaduje extent + imageDisplay
            var pad = 0.35;
            var mapExtent = string.Create(CultureInfo.InvariantCulture,
                $"{lon - pad},{lat - pad},{lon + pad},{lat + pad}");
            var geometry = string.Create(CultureInfo.InvariantCulture, $"{lon},{lat}");
            var url =
                $"{PopupService}/identify?f=json" +
                $"&geometry={Uri.EscapeDataString(geometry)}" +
                "&geometryType=esriGeometryPoint&sr=4326" +
                $"&layers=visible:0,1,2,3,4,5,6,7,8" +
                $"&tolerance={tolerance}" +
                $"&mapExtent={Uri.EscapeDataString(mapExtent)}" +
                "&imageDisplay=1000,700,96&returnGeometry=false";

            using var res = await client.GetAsync(url, cancellationToken);
            res.EnsureSuccessStatusCode();
            await using var stream = await res.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var zones = new List<AirspaceZoneInfo>();
            if (doc.RootElement.TryGetProperty("results", out var results))
            {
                foreach (var item in results.EnumerateArray())
                    zones.Add(MapResult(item));
            }

            return Ok(new MapIdentifyResultDto(lat, lon, zones, null));
        }
        catch (Exception ex)
        {
            return Ok(new MapIdentifyResultDto(lat, lon, Array.Empty<AirspaceZoneInfo>(),
                "Identify zlyhal: " + ex.Message));
        }
    }

    /// <summary>
    /// Obmedzený proxy na VFRM/* ArcGIS REST (tiles/metadata/identify) — fallback pri CORS.
    /// </summary>
    [HttpGet("proxy/{*path}")]
    [AllowAnonymous] // tiles/img môžu ísť bez JWT; citlivé dáta tu nie sú
    public async Task Proxy(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !path.StartsWith("VFRM/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("..", StringComparison.Ordinal))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync("Povolené sú len cesty VFRM/*.", cancellationToken);
            return;
        }

        var qs = Request.QueryString.HasValue ? Request.QueryString.Value : "";
        var client = httpClientFactory.CreateClient("lps-gis");
        using var upstream = await client.GetAsync(path + qs, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        Response.StatusCode = (int)upstream.StatusCode;
        if (upstream.Content.Headers.ContentType is not null)
            Response.ContentType = upstream.Content.Headers.ContentType.ToString();
        await upstream.Content.CopyToAsync(Response.Body, cancellationToken);
    }

    private static AirspaceZoneInfo MapResult(JsonElement item)
    {
        var attrs = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (item.TryGetProperty("attributes", out var a) && a.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in a.EnumerateObject())
                attrs[p.Name] = p.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                    ? null
                    : p.Value.ToString();
        }

        string? Get(params string[] keys)
        {
            foreach (var k in keys)
                if (attrs.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) && v != "Null")
                    return v;
            return null;
        }

        int? oid = null;
        if (int.TryParse(Get("OBJECTID"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            oid = id;

        return new AirspaceZoneInfo(
            oid,
            item.TryGetProperty("layerName", out var ln) ? ln.GetString() : null,
            Get("NAME_TXT", "name", "label", "IDENT_TXT_NAME_TXT"),
            Get("IDENT_TXT", "ident"),
            Get("TYPE_CODE", "TYPE_CODE_1", "type"),
            Get("CLASS_CODE", "class"),
            Get("lower", "DistVertLowerVal_Txt", "DISTVERTLOWER_VAL"),
            Get("upper", "DistVertUpperVal_Txt", "DISTVERTUPPER_VAL"),
            Get("REMARKS_TXT", "remarks"),
            Get("activity", "ACTIVITY_CODE", "ACTIVITY_CODE_TS"),
            Get("MIL_CODE"),
            attrs);
    }
}
