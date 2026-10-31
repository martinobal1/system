using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using IsSuas.Application.Abstractions;
using IsSuas.Application.Fleet;
using IsSuas.Application.Policies;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Fleet;
using IsSuas.Infrastructure.Persistence;
using IsSuas.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IsSuas.Infrastructure.Services;

public interface IUasTypeDocService
{
    Task<IReadOnlyList<UasTypeDocDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<UasTypeDocDto> SaveAsync(string slug, SaveUasTypeDocRequest request, CancellationToken cancellationToken = default);
    Task HideAsync(string slug, HideUasTypeDocRequest request, CancellationToken cancellationToken = default);
    Task<UasTypeDocDto> UploadManualAsync(string slug, UploadUasTypeManualRequest request, CancellationToken cancellationToken = default);
    Task<(Stream Content, string FileName)> DownloadManualAsync(string slug, CancellationToken cancellationToken = default);
    Task<UasTypeExtractionDto> ExtractSpecsAsync(ExtractUasTypeSpecsRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Katalóg „Technická dokumentácia“ v databáze: vlastné typy UAS, skryté vstavané typy, PDF manuály
/// a AI extrakcia tabuliek špecifikácií z textu manuálu (Anthropic Claude API).
/// </summary>
public sealed class UasTypeDocService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock,
    IObjectStorage storage,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<UasTypeDocService> logger) : IUasTypeDocService
{
    private const long MaxManualBytes = 50 * 1024 * 1024;
    private const int MaxDataJsonChars = 300_000;
    private const int MaxExtractChars = 120_000;
    /// <summary>Koľko textu za nadpisom „Specifications“ sa pošle AI (tabuľka; prílohy za ňou nie).</summary>
    private const int MaxSpecChars = 30_000;
    private static readonly Regex SlugRx = new("^[a-z0-9][a-z0-9-]{0,127}$", RegexOptions.Compiled);

    public async Task<IReadOnlyList<UasTypeDocDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.UasRead);
        var rows = await db.UasTypeDocs.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Manufacturer).ThenBy(x => x.Model)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<UasTypeDocDto> SaveAsync(string slug, SaveUasTypeDocRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureGestorAsync(cancellationToken);
        slug = NormalizeSlug(slug);
        if (string.IsNullOrWhiteSpace(request.Manufacturer) || string.IsNullOrWhiteSpace(request.Model))
            throw new ValidationException("Výrobca a model sú povinné.");
        if (request.Data.ValueKind != JsonValueKind.Object)
            throw new ValidationException("Údaje typu musia byť objekt.");
        if (!request.Data.TryGetProperty("sections", out var sections) || sections.ValueKind != JsonValueKind.Array)
            throw new ValidationException("Údaje typu musia obsahovať zoznam sekcií (sections).");
        var raw = request.Data.GetRawText();
        if (raw.Length > MaxDataJsonChars)
            throw new ValidationException("Údaje typu sú príliš veľké.");

        var entity = await db.UasTypeDocs.FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);
        var before = entity is null ? null : Map(entity);
        if (entity is null)
        {
            entity = new UasTypeDoc { Slug = slug, CreatedBy = currentUser.AccountId };
            db.UasTypeDocs.Add(entity);
        }
        entity.Manufacturer = Trim(request.Manufacturer, 128);
        entity.Model = Trim(request.Model, 128);
        entity.DataJson = raw;
        entity.IsHidden = false;
        entity.IsActive = true;
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        var after = Map(entity);
        await audit.WriteAsync("UasTypeDoc.Save", nameof(UasTypeDoc), entity.Id, before, after, cancellationToken: cancellationToken);
        return after;
    }

    public async Task HideAsync(string slug, HideUasTypeDocRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureGestorAsync(cancellationToken);
        slug = NormalizeSlug(slug);
        var entity = await db.UasTypeDocs.FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);
        if (entity is null)
        {
            entity = new UasTypeDoc
            {
                Slug = slug,
                Manufacturer = Trim(request.Manufacturer ?? slug, 128),
                Model = Trim(request.Model ?? slug, 128),
                CreatedBy = currentUser.AccountId
            };
            db.UasTypeDocs.Add(entity);
        }
        entity.IsHidden = true;
        entity.IsActive = true;
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("UasTypeDoc.Hide", nameof(UasTypeDoc), entity.Id, null, Map(entity), cancellationToken: cancellationToken);
    }

    public async Task<UasTypeDocDto> UploadManualAsync(string slug, UploadUasTypeManualRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureGestorAsync(cancellationToken);
        slug = NormalizeSlug(slug);
        byte[] payload;
        try { payload = Convert.FromBase64String(request.ContentBase64 ?? ""); }
        catch (FormatException) { throw new ValidationException("Súbor nie je platný (Base64)."); }
        if (payload.Length == 0 || payload.LongLength > MaxManualBytes)
            throw new ValidationException("PDF musí mať 1 B až 50 MB.");
        if (payload.Length < 5 || Encoding.ASCII.GetString(payload, 0, 5) != "%PDF-")
            throw new ValidationException("Súbor nie je PDF.");

        var fileName = Path.GetFileName(string.IsNullOrWhiteSpace(request.FileName) ? "manual.pdf" : request.FileName.Trim());
        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) fileName += ".pdf";
        if (fileName.Length > 200) fileName = fileName[^200..];

        using var ms = new MemoryStream(payload);
        var stored = await storage.PutAsync(fileName, "application/pdf", ms, cancellationToken);

        var entity = await db.UasTypeDocs.FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);
        if (entity is null)
        {
            // Manuál k vstavanému typu (napr. Mavic 3T) — údaje ostávajú v katalógu na webe.
            entity = new UasTypeDoc
            {
                Slug = slug,
                Manufacturer = Trim(request.Manufacturer ?? slug, 128),
                Model = Trim(request.Model ?? slug, 128),
                CreatedBy = currentUser.AccountId
            };
            db.UasTypeDocs.Add(entity);
        }
        entity.ManualStorageKey = stored.StorageKey;
        entity.ManualFileName = fileName;
        entity.ManualSizeBytes = payload.LongLength;
        entity.ManualUploadedAt = clock.UtcNow;
        entity.IsActive = true;
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        var dto = Map(entity);
        await audit.WriteAsync("UasTypeDoc.ManualUpload", nameof(UasTypeDoc), entity.Id, null, dto, cancellationToken: cancellationToken);
        return dto;
    }

    public async Task<(Stream Content, string FileName)> DownloadManualAsync(string slug, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.UasRead);
        slug = NormalizeSlug(slug);
        var entity = await db.UasTypeDocs.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Typ UAS nebol nájdený.");
        if (string.IsNullOrWhiteSpace(entity.ManualStorageKey))
            throw new NotFoundException("K tomuto typu nie je nahraný manuál.");
        var stream = await storage.GetAsync(entity.ManualStorageKey, cancellationToken);
        return (stream, entity.ManualFileName ?? "manual.pdf");
    }

    public async Task<UasTypeExtractionDto> ExtractSpecsAsync(ExtractUasTypeSpecsRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureGestorAsync(cancellationToken);
        var provider = ResolveAiProvider();
        var model = provider switch
        {
            "openai" => configuration["OpenAI:Model"] is { Length: > 0 } m1 ? m1 : "gpt-5-mini",
            "gemini" => configuration["Gemini:Model"] is { Length: > 0 } m2 ? m2 : "gemini-flash-latest",
            _ => configuration["Anthropic:Model"] is { Length: > 0 } m3 ? m3 : "claude-sonnet-5-5"
        };

        var text = (request.Text ?? "").Trim();
        if (text.Length < 200)
            throw new ValidationException("Z PDF sa nepodarilo vytiahnuť text (možno je to sken). Vyplňte parametre ručne.");
        // Novšie manuály (napr. DJI Air 3S) tabuľku nemajú — len odkaz na web výrobcu.
        var specLink = Regex.Match(text,
            @"(?:Visit|See|Refer to)[^\n]{0,60}(?:website|site)[^\n]{0,40}specifications?[^\n]{0,10}\s*(https?://\S+)",
            RegexOptions.IgnoreCase);
        var specIdx = text.LastIndexOf("Specifications", StringComparison.Ordinal);
        if (specLink.Success && specIdx >= 0 && Math.Abs(specLink.Index - specIdx) < 300
            && !Regex.IsMatch(text.Substring(specIdx, Math.Min(2500, text.Length - specIdx)), @"(?i)takeoff weight|max(?:imum)? flight time"))
            throw new ValidationException("Tento manuál neobsahuje tabuľku špecifikácií — výrobca ich uvádza na webe: " +
                specLink.Groups[1].Value.TrimEnd('.', ',', ')') +
                ". Otvorte túto stránku, uložte ju ako PDF (Ctrl+P → Uložiť ako PDF) a nahrajte toto PDF.");
        text = PickSpecificationPart(text);

        var hints = string.Join(" ", new[] { request.ManufacturerHint, request.ModelHint, request.FileName }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        var userPrompt = "Hint (file name / entered values): " + (hints.Length > 0 ? hints : "-") +
                         "\n<manual>\n" + text + "\n</manual>";

        var answer = provider switch
        {
            "openai" => await CallOpenAiAsync(model, userPrompt, cancellationToken),
            "gemini" => await CallGeminiAsync(model, userPrompt, cancellationToken),
            _ => await CallAnthropicAsync(model, userPrompt, cancellationToken)
        };

        var start = answer.IndexOf('{');
        var end = answer.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new ValidationException("AI nevrátila tabuľky špecifikácií. Vyplňte parametre ručne.");
        JsonElement data;
        try
        {
            using var parsed = JsonDocument.Parse(answer[start..(end + 1)]);
            data = parsed.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new ValidationException("AI vrátila neplatné údaje. Skúste znova.");
        }
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("sections", out var secs) || secs.ValueKind != JsonValueKind.Array)
            throw new ValidationException("AI nevrátila tabuľky špecifikácií. Vyplňte parametre ručne.");

        await audit.WriteAsync("UasTypeDoc.AiExtract", nameof(UasTypeDoc), null, null,
            new { request.FileName, Provider = provider, Model = model, Sections = secs.GetArrayLength() }, cancellationToken: cancellationToken);
        return new UasTypeExtractionDto(data, model);
    }

    // Inštrukcie sú po anglicky (menej tokenov), výstup je po slovensky.
    private const string SystemPrompt =
        "Extract the technical specifications from the drone user-manual text. Reply with JSON only, no markdown:\n" +
        "{\"manufacturer\":\"DJI\",\"model\":\"Mavic 3T\",\"classEu\":\"C2\",\"tagline\":\"Termovízny dron\"," +
        "\"sections\":[{\"title\":\"Lietadlo\",\"rows\":[[\"Vzletová hmotnosť\",\"920 g\"]]}]}\n" +
        "Rules:\n" +
        "- Use only data present in the text; never invent; omit what is missing.\n" +
        "- Keep the manual's sections (Aircraft, Gimbal, Camera, Thermal Camera, Sensing, Video Transmission, Remote Controller, " +
        "Battery, Charger, Storage, App…) and translate titles and parameter names to Slovak " +
        "(Takeoff Weight → Vzletová hmotnosť, Max Flight Time → Maximálny čas letu, Max Wind Speed Resistance → Odolnosť proti vetru, " +
        "Operating Temperature → Prevádzková teplota).\n" +
        "- Copy values faithfully with units; Slovak number format: decimal comma, space as thousands separator (6 000 m), " +
        "dimensions with ×, ranges as „−10 až 40 °C“; keep conditions in parentheses, translated.\n" +
        "- If several variants are listed, use the column for the model in the hint; if unclear, put both values in one cell labelled by variant.\n" +
        "- classEu = EU class C0–C6 if stated; tagline = short Slovak description (e.g. „Termovízny dron“).\n" +
        "- Every row is exactly [parameter, value] as strings. Skip empty sections.";

    // ---------- poskytovatelia AI (výber cez Ai__Provider: openai | gemini | anthropic) ----------
    private string ResolveAiProvider()
    {
        var p = (configuration["Ai:Provider"] ?? "").Trim().ToLowerInvariant();
        if (p is "chatgpt") p = "openai";
        if (p is "claude") p = "anthropic";
        if (string.IsNullOrEmpty(p))
        {
            // bez výberu: prvý poskytovateľ, ktorý má vyplnený kľúč
            if (!string.IsNullOrWhiteSpace(configuration["OpenAI:ApiKey"])) p = "openai";
            else if (!string.IsNullOrWhiteSpace(configuration["Gemini:ApiKey"])) p = "gemini";
            else if (!string.IsNullOrWhiteSpace(configuration["Anthropic:ApiKey"])) p = "anthropic";
        }
        var keyName = p switch { "openai" => "OpenAI", "gemini" => "Gemini", "anthropic" => "Anthropic", _ => null };
        if (keyName is null || string.IsNullOrWhiteSpace(configuration[keyName + ":ApiKey"]))
            throw new ValidationException("AI spracovanie nie je nastavené: doplňte do infra/.env kľúč (OpenAI__ApiKey, Gemini__ApiKey alebo Anthropic__ApiKey) a reštartujte API.");
        return p;
    }

    private async Task<string> SendAiAsync(string providerName, HttpRequestMessage msg, Func<JsonElement, string> readText, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("Ai");
        HttpResponseMessage resp;
        try
        {
            resp = await client.SendAsync(msg, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "AI extrakcia: {Provider} nie je dostupná.", providerName);
            throw new ValidationException("AI služba nie je dostupná (sieť alebo časový limit). Skúste znova alebo vyplňte parametre ručne.");
        }
        using (resp)
        {
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("AI extrakcia ({Provider}) zlyhala: {Status} {Body}", providerName, (int)resp.StatusCode,
                    body.Length > 500 ? body[..500] : body);
                throw new ValidationException((int)resp.StatusCode switch
                {
                    400 when body.Contains("API_KEY", StringComparison.OrdinalIgnoreCase) => $"{providerName} odmietol API kľúč. Skontrolujte ho v infra/.env.",
                    401 or 403 => $"{providerName} odmietol API kľúč. Skontrolujte ho v infra/.env.",
                    404 => $"{providerName}: model nebol nájdený. Skontrolujte názov modelu v infra/.env.",
                    429 when body.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase)
                        || body.Contains("billing", StringComparison.OrdinalIgnoreCase)
                        || body.Contains("credit", StringComparison.OrdinalIgnoreCase)
                        => $"{providerName}: na API účte nie je kredit. Doplňte ho v nastaveniach účtu (Billing) a skúste znova.",
                    429 => $"{providerName}: príliš veľa požiadaviek naraz (limit). Skúste o minútu.",
                    _ => $"AI spracovanie ({providerName}) zlyhalo (HTTP {(int)resp.StatusCode})."
                });
            }
            try
            {
                using var doc = JsonDocument.Parse(body);
                return readText(doc.RootElement) ?? "";
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
            {
                logger.LogWarning(ex, "AI extrakcia ({Provider}): neočakávaná odpoveď.", providerName);
                throw new ValidationException("AI vrátila neočakávanú odpoveď.");
            }
        }
    }

    private static StringContent JsonBody(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    /// <summary>OpenAI (ChatGPT) — Chat Completions API.</summary>
    private Task<string> CallOpenAiAsync(string model, string userPrompt, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object>
        {
            ["model"] = model,
            ["response_format"] = new { type = "json_object" },
            ["messages"] = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = userPrompt }
            }
        };
        // Prepis tabuľky nepotrebuje „premýšľanie“ — šetrí skryté (platené) tokeny reasoning modelov.
        // Pre modely bez reasoningu (napr. gpt-4o) nastavte OpenAI__ReasoningEffort=off.
        var effort = (configuration["OpenAI:ReasoningEffort"] ?? "").Trim().ToLowerInvariant();
        if (effort.Length == 0) effort = "low";
        if (effort != "off") body["reasoning_effort"] = effort;

        var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = JsonBody(body)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration["OpenAI:ApiKey"]);
        return SendAiAsync("OpenAI (ChatGPT)", msg, root =>
            root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "", cancellationToken);
    }

    /// <summary>Google Gemini — generateContent.</summary>
    private Task<string> CallGeminiAsync(string model, string userPrompt, CancellationToken cancellationToken)
    {
        var url = "https://generativelanguage.googleapis.com/v1beta/models/" + Uri.EscapeDataString(model) + ":generateContent";
        var msg = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonBody(new
            {
                systemInstruction = new { parts = new[] { new { text = SystemPrompt } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
                generationConfig = new { temperature = 0, responseMimeType = "application/json" }
            })
        };
        msg.Headers.Add("x-goog-api-key", configuration["Gemini:ApiKey"]);
        return SendAiAsync("Google Gemini", msg, root =>
            string.Concat(root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts").EnumerateArray()
                .Select(p => p.TryGetProperty("text", out var t) ? t.GetString() : "")), cancellationToken);
    }

    /// <summary>Anthropic Claude — Messages API.</summary>
    private Task<string> CallAnthropicAsync(string model, string userPrompt, CancellationToken cancellationToken)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = JsonBody(new
            {
                model,
                max_tokens = 8000,
                system = SystemPrompt,
                messages = new object[] { new { role = "user", content = userPrompt } }
            })
        };
        msg.Headers.Add("x-api-key", configuration["Anthropic:ApiKey"]);
        msg.Headers.Add("anthropic-version", "2023-06-01");
        return SendAiAsync("Anthropic Claude", msg, root =>
            string.Concat(root.GetProperty("content").EnumerateArray()
                .Where(c => c.TryGetProperty("type", out var t) && t.GetString() == "text")
                .Select(c => c.GetProperty("text").GetString())), cancellationToken);
    }

    /// <summary>
    /// Z manuálu pošle AI hlavne časť so špecifikáciami (príloha „Specifications“), aby to bolo rýchlejšie a lacnejšie.
    /// </summary>
    private static string PickSpecificationPart(string text)
    {
        var markers = new[] { "Specifications", "SPECIFICATIONS", "Technické údaje", "Špecifikácie", "Technische Daten" };
        var best = -1;
        foreach (var m in markers)
        {
            // posledný výskyt nadpisu (obsah na začiatku manuálu ho tiež spomína)
            var idx = text.LastIndexOf(m, StringComparison.Ordinal);
            if (idx > best) best = idx;
        }
        if (best >= 0 && text.Length - best > 1500)
        {
            var from = Math.Max(0, best - 200);
            var part = text[from..];
            return part.Length > MaxSpecChars ? part[..MaxSpecChars] : part;
        }
        return text.Length > MaxExtractChars ? text[^MaxExtractChars..] : text;
    }

    private async Task EnsureGestorAsync(CancellationToken cancellationToken)
    {
        if (currentUser.AccountId is not Guid accountId)
            throw new ForbiddenException("Chýba identita používateľa.");
        var level = await db.Accounts.AsNoTracking().Where(a => a.Id == accountId)
            .Select(a => (AccessLevel?)a.AccessLevel).FirstOrDefaultAsync(cancellationToken);
        if (level != AccessLevel.Level1)
            throw new ForbiddenException("Technickú dokumentáciu spravuje gestor (úroveň 1).");
    }

    private static string NormalizeSlug(string slug)
    {
        var s = (slug ?? "").Trim().ToLowerInvariant();
        if (!SlugRx.IsMatch(s))
            throw new ValidationException("Neplatný identifikátor typu.");
        return s;
    }

    private static string Trim(string value, int max)
    {
        var v = (value ?? "").Trim();
        return v.Length > max ? v[..max] : v;
    }

    private static UasTypeDocDto Map(UasTypeDoc x)
    {
        JsonElement data;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(x.DataJson) ? "{}" : x.DataJson);
            data = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var empty = JsonDocument.Parse("{}");
            data = empty.RootElement.Clone();
        }
        return new UasTypeDocDto(x.Slug, x.Manufacturer, x.Model, data, x.IsHidden,
            !string.IsNullOrWhiteSpace(x.ManualStorageKey), x.ManualFileName, x.ManualSizeBytes, x.ManualUploadedAt, x.UpdatedAt);
    }
}
