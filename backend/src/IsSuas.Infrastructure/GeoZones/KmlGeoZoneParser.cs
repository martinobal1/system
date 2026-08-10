using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using IsSuas.Application.Policies;

namespace IsSuas.Infrastructure.GeoZones;

internal sealed record ParsedGeoZone(
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
    Dictionary<string, string> Properties);

/// <summary>Parsuje KML (vrátane KML zo ZIP) na zoznam GEO zón + FeatureCollection GeoJSON.</summary>
internal static class KmlGeoZoneParser
{
    public static (IReadOnlyList<ParsedGeoZone> Zones, string FeatureCollectionGeoJson) Parse(byte[] bytes, string fileName)
    {
        var kmlBytes = ExtractKmlBytes(bytes, fileName);
        string xml;
        try
        {
            xml = DecodeXml(kmlBytes);
            xml = SanitizeKmlXml(xml);
        }
        catch (Exception ex)
        {
            throw new ValidationException("Súbor nie je platný textový KML: " + ex.Message);
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex)
        {
            throw new ValidationException("KML nie je platné XML: " + ex.Message);
        }

        var placemarks = doc.Descendants().Where(e => Local(e) == "Placemark").ToList();
        if (placemarks.Count == 0)
            throw new ValidationException("V KML neboli nájdené žiadne Placemark (GEO zóny).");

        var zones = new List<ParsedGeoZone>();
        var features = new List<object>();
        var index = 0;
        foreach (var pm in placemarks)
        {
            index++;
            var props = ReadExtendedData(pm);
            MergeDescriptionTableProps(ElementText(pm, "description"), props);

            var name = FirstNonEmpty(
                ElementText(pm, "name"),
                Prop(props, "name", "nazov", "název", "identification", "id", "UASZoneIdentifier", "Označenie", "Oznacenie"))
                ?? ("Zona-" + index);

            var geometry = ReadGeometry(pm);
            if (geometry is null)
                continue;

            var description = ElementText(pm, "description");
            var headline = Prop(props, "_headline", "Nadpis", "nadpis");
            var zoneType = FirstNonEmpty(
                Prop(props, "type", "zoneType", "uasZoneType", "typ", "restrictionType", "category"),
                headline,
                InferTypeFromName(name),
                InferTypeFromName(StripHtml(description) ?? ""));
            var restriction = FirstNonEmpty(
                Prop(props, "restriction", "restrictionReason", "reason", "opis", "description", "message",
                    "Obmedzenie", "obmedzenie"),
                headline,
                StripHtml(description));
            var conditions = FirstNonEmpty(
                Prop(props, "condition", "conditions", "flightConditions", "podmienky", "requirement", "requirements",
                    "Doba platnosti", "Platnosť", "Platnost"));
            var horizontal = FirstNonEmpty(
                Prop(props, "horizontal", "horizontalLimit", "horizontalLimits", "hranice", "boundary"));
            var vertical = FirstNonEmpty(
                Prop(props, "vertical", "verticalLimit", "verticalLimits", "lowerLimit", "upperLimit", "vyska", "height",
                    "Výška", "Vyska", "lowerLimitReference", "upperLimitReference"),
                CombineLimits(Prop(props, "lowerLimit", "lower"), Prop(props, "upperLimit", "upper")));
            var contact = FirstNonEmpty(
                Prop(props, "authority", "contact", "responsible", "kontakt", "approvingAuthority", "UASZoneAuthority",
                    "Kontakt", "Subjekt"));
            var source = FirstNonEmpty(
                Prop(props, "source", "dataSource", "zdroj"),
                "Dopravný úrad SR");

            var validFrom = ParseDate(
                Prop(props, "validFrom", "begin", "start", "effectiveFrom", "platnostOd"),
                ElementText(pm.Descendants().FirstOrDefault(e => Local(e) is "TimeSpan" or "TimeStamp"), "begin"));
            var validTo = ParseDate(
                Prop(props, "validTo", "end", "effectiveTo", "platnostDo"),
                ElementText(pm.Descendants().FirstOrDefault(e => Local(e) is "TimeSpan" or "TimeStamp"), "end"));

            var externalKey = FirstNonEmpty(
                Prop(props, "id", "identifier", "UASZoneIdentifier", "zoneId", "Označenie", "Oznacenie"),
                name + "#" + index)!;

            var zone = new ParsedGeoZone(
                externalKey,
                name,
                zoneType,
                restriction,
                conditions,
                horizontal,
                vertical,
                validFrom,
                validTo,
                contact,
                source,
                description,
                JsonSerializer.Serialize(geometry),
                props);

            zones.Add(zone);

            var featureProps = new Dictionary<string, object?>
            {
                ["name"] = zone.Name,
                ["zoneType"] = zone.ZoneType,
                ["restrictionDescription"] = zone.RestrictionDescription,
                ["flightConditions"] = zone.FlightConditions,
                ["horizontalLimits"] = zone.HorizontalLimits,
                ["verticalLimits"] = zone.VerticalLimits,
                ["validFrom"] = zone.ValidFrom,
                ["validTo"] = zone.ValidTo,
                ["authorityContact"] = zone.AuthorityContact,
                ["sourceLabel"] = zone.SourceLabel,
                ["externalKey"] = zone.ExternalKey
            };
            foreach (var kv in props)
                featureProps.TryAdd(kv.Key, kv.Value);

            features.Add(new Dictionary<string, object?>
            {
                ["type"] = "Feature",
                ["properties"] = featureProps,
                ["geometry"] = geometry
            });
        }

        if (zones.Count == 0)
            throw new ValidationException("KML neobsahuje žiadnu použiteľnú geometriu (Polygon/LineString/Point).");

        var collection = new Dictionary<string, object?>
        {
            ["type"] = "FeatureCollection",
            ["features"] = features
        };
        return (zones, JsonSerializer.Serialize(collection));
    }

    private static byte[] ExtractKmlBytes(byte[] bytes, string fileName)
    {
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (ext == ".zip" || LooksLikeZip(bytes))
        {
            using var ms = new MemoryStream(bytes);
            using var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: false);
            var entry = zip.Entries
                .Where(e => e.FullName.EndsWith(".kml", StringComparison.OrdinalIgnoreCase) && e.Length > 0)
                .OrderBy(e => e.FullName.Count(c => c == '/' || c == '\\'))
                .ThenBy(e => e.FullName.Length)
                .FirstOrDefault()
                ?? throw new ValidationException("ZIP neobsahuje žiadny .kml súbor.");
            using var entryStream = entry.Open();
            using var outMs = new MemoryStream();
            entryStream.CopyTo(outMs);
            return outMs.ToArray();
        }

        if (string.IsNullOrEmpty(ext) || ext is ".kml" or ".xml" || LooksLikeXml(bytes))
            return bytes;

        throw new ValidationException("Podporované sú iba súbory .kml alebo .zip s KML.");
    }

    private static bool LooksLikeZip(byte[] bytes) =>
        bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B;

    private static bool LooksLikeXml(byte[] bytes)
    {
        var preview = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 200)).TrimStart();
        return preview.StartsWith('<');
    }

    private static string DecodeXml(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Oficiálne KML z NSAT obsahuje xsi:schemaLocation na Placemark bez xmlns:xsi.
    /// Doplníme chýbajúce namespace deklarácie / odstránime neplatné xsi atribúty.
    /// </summary>
    private static string SanitizeKmlXml(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return xml;

        // Strip BOM / weird control chars that break XmlReader
        xml = xml.TrimStart('\uFEFF', '\u200B');

        if (xml.Contains("xsi:", StringComparison.Ordinal)
            && !xml.Contains("xmlns:xsi=", StringComparison.OrdinalIgnoreCase))
        {
            xml = Regex.Replace(
                xml,
                @"<kml(\s[^>]*)?>",
                m =>
                {
                    var tag = m.Value;
                    if (tag.Contains("xmlns:xsi=", StringComparison.OrdinalIgnoreCase))
                        return tag;
                    return tag.Insert(tag.Length - 1,
                        " xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"");
                },
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        // Fallback: remove orphaned xsi:* attributes if still undeclared somehow
        if (xml.Contains("xsi:", StringComparison.Ordinal)
            && !xml.Contains("xmlns:xsi=", StringComparison.OrdinalIgnoreCase))
        {
            xml = Regex.Replace(xml, @"\s+xsi:[A-Za-z0-9_-]+=(""[^""]*""|'[^']*')", "", RegexOptions.CultureInvariant);
        }

        return xml;
    }

    /// <summary>Extrahuje polia z HTML tabuľky v &lt;description&gt; (formát Dopravného úradu).</summary>
    private static void MergeDescriptionTableProps(string? descriptionHtml, Dictionary<string, string> props)
    {
        if (string.IsNullOrWhiteSpace(descriptionHtml)) return;

        // Headline in yellow rowspan cell
        var headline = Regex.Match(
            descriptionHtml,
            @"colspan\s*=\s*[""']?2[""']?[^>]*>\s*([^<]+?)\s*<",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (headline.Success)
        {
            var h = System.Net.WebUtility.HtmlDecode(headline.Groups[1].Value).Trim();
            if (!string.IsNullOrWhiteSpace(h))
                props["_headline"] = Regex.Replace(h, @"\s+", " ");
        }

        var rowMatches = Regex.Matches(
            descriptionHtml,
            @"<tr[^>]*>\s*<td[^>]*>\s*(?:<[^>]+>)*\s*([^<]+?)\s*(?:</[^>]+>)*\s*</td>\s*<td[^>]*>\s*(?:<[^>]+>)*\s*([^<]+?)\s*",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        foreach (Match m in rowMatches)
        {
            var key = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
            var val = System.Net.WebUtility.HtmlDecode(m.Groups[2].Value).Trim();
            key = Regex.Replace(key, @"\s+", " ");
            val = Regex.Replace(val, @"\s+", " ");
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(val)) continue;
            if (key.Equals(val, StringComparison.OrdinalIgnoreCase)) continue;
            props.TryAdd(key, val);
        }
    }

    private static string Local(XElement e) => e.Name.LocalName;

    private static string? ElementText(XElement? parent, string localName)
    {
        if (parent is null) return null;
        var el = parent.Elements().FirstOrDefault(e => Local(e) == localName)
                 ?? parent.Descendants().FirstOrDefault(e => Local(e) == localName && e.Parent == parent);
        var text = el?.Value?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static Dictionary<string, string> ReadExtendedData(XElement placemark)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var data in placemark.Descendants().Where(e => Local(e) == "Data"))
        {
            var name = (string?)data.Attribute("name");
            var value = data.Elements().FirstOrDefault(e => Local(e) == "value")?.Value
                        ?? data.Value;
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
                dict[name.Trim()] = value.Trim();
        }
        foreach (var data in placemark.Descendants().Where(e => Local(e) == "SimpleData"))
        {
            var name = (string?)data.Attribute("name");
            var value = data.Value;
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
                dict[name.Trim()] = value.Trim();
        }
        return dict;
    }

    private static string? Prop(Dictionary<string, string> props, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (props.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        return null;
    }

    private static string? CombineLimits(string? lower, string? upper)
    {
        if (string.IsNullOrWhiteSpace(lower) && string.IsNullOrWhiteSpace(upper)) return null;
        if (string.IsNullOrWhiteSpace(lower)) return "do " + upper;
        if (string.IsNullOrWhiteSpace(upper)) return "od " + lower;
        return lower + " – " + upper;
    }

    private static string? InferTypeFromName(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("zakaz") || n.Contains("prohibit") || n.Contains("no-fly") || n.Contains("nofly"))
            return "Zákaz";
        if (n.Contains("obmedz") || n.Contains("restrict"))
            return "Obmedzenie";
        if (n.Contains("podmien") || n.Contains("condit"))
            return "Podmienka";
        return null;
    }

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static DateTimeOffset? ParseDate(params string?[] values)
    {
        foreach (var raw in values)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
                return dto;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
                return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
        }
        return null;
    }

    private static object? ReadGeometry(XElement placemark)
    {
        var multi = placemark.Descendants().FirstOrDefault(e => Local(e) is "MultiGeometry" or "MultiTrack");
        if (multi is not null)
        {
            var parts = multi.Elements()
                .Select(ReadSingleGeometry)
                .Where(g => g is not null)
                .Cast<object>()
                .ToList();
            if (parts.Count == 0) return null;
            if (parts.Count == 1) return parts[0];

            // Prefer MultiPolygon when all are polygons
            if (parts.All(p => p is Dictionary<string, object?> d && (string?)d["type"] == "Polygon"))
            {
                return new Dictionary<string, object?>
                {
                    ["type"] = "MultiPolygon",
                    ["coordinates"] = parts.Select(p => ((Dictionary<string, object?>)p)["coordinates"]).ToList()
                };
            }

            return new Dictionary<string, object?>
            {
                ["type"] = "GeometryCollection",
                ["geometries"] = parts
            };
        }

        return placemark.Descendants()
            .Select(ReadSingleGeometry)
            .FirstOrDefault(g => g is not null);
    }

    private static object? ReadSingleGeometry(XElement el)
    {
        var local = Local(el);
        if (local == "Polygon")
        {
            var outer = el.Descendants().FirstOrDefault(e => Local(e) == "outerBoundaryIs")
                ?.Descendants().FirstOrDefault(e => Local(e) == "coordinates");
            var ring = ParseCoordinates(outer?.Value);
            if (ring.Count < 3) return null;
            EnsureClosed(ring);
            var rings = new List<List<double[]>> { ring };
            foreach (var inner in el.Descendants().Where(e => Local(e) == "innerBoundaryIs"))
            {
                var coords = ParseCoordinates(inner.Descendants().FirstOrDefault(e => Local(e) == "coordinates")?.Value);
                if (coords.Count >= 3)
                {
                    EnsureClosed(coords);
                    rings.Add(coords);
                }
            }
            return new Dictionary<string, object?> { ["type"] = "Polygon", ["coordinates"] = rings };
        }

        if (local == "LineString" || local == "LinearRing")
        {
            var coords = ParseCoordinates(ElementText(el, "coordinates") ?? el.Descendants().FirstOrDefault(e => Local(e) == "coordinates")?.Value);
            if (coords.Count < 2) return null;
            return new Dictionary<string, object?> { ["type"] = "LineString", ["coordinates"] = coords };
        }

        if (local == "Point")
        {
            var coords = ParseCoordinates(ElementText(el, "coordinates") ?? el.Descendants().FirstOrDefault(e => Local(e) == "coordinates")?.Value);
            if (coords.Count < 1) return null;
            return new Dictionary<string, object?> { ["type"] = "Point", ["coordinates"] = coords[0] };
        }

        return null;
    }

    private static List<double[]> ParseCoordinates(string? raw)
    {
        var list = new List<double[]>();
        if (string.IsNullOrWhiteSpace(raw)) return list;
        var tokens = raw.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var parts = token.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2) continue;
            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) continue;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) continue;
            if (parts.Length >= 3
                && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var alt))
                list.Add([lon, lat, alt]);
            else
                list.Add([lon, lat]);
        }
        return list;
    }

    private static void EnsureClosed(List<double[]> ring)
    {
        if (ring.Count < 3) return;
        var first = ring[0];
        var last = ring[^1];
        if (Math.Abs(first[0] - last[0]) > 1e-9 || Math.Abs(first[1] - last[1]) > 1e-9)
            ring.Add([first[0], first[1]]);
    }
}
