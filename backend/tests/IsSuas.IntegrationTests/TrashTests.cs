using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IsSuas.Application.Auth;
using IsSuas.Application.Trash;

namespace IsSuas.IntegrationTests;

/// <summary>Kôš: presun, skrytie, blokovanie väzbami, obnovenie a trvalé vymazanie.</summary>
public class TrashTests : IClassFixture<IsSuasWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _client;

    public TrashTests(IsSuasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Let_PresunDoKosa_Obnova_TrvaleVymazanie()
    {
        await LoginAs("gestor", "huginko123");
        var flights = await GetIds("/api/v1/flights");
        Assert.NotEmpty(flights);

        // Prvý let, na ktorý sa neviaže porucha ani incident.
        Guid? flightId = null;
        foreach (var id in flights)
        {
            var move = await _client.PostAsJsonAsync("/api/v1/trash", new MoveToTrashRequest(TrashCategories.Flights, id, "test"));
            if (move.StatusCode == HttpStatusCode.OK) { flightId = id; break; }
            Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
        }
        Assert.NotNull(flightId);

        // Zmizol zo zoznamu aj z detailu.
        Assert.DoesNotContain(flightId.Value, await GetIds("/api/v1/flights"));
        Assert.NotEqual(HttpStatusCode.OK, (await _client.GetAsync($"/api/v1/flights/{flightId}")).StatusCode);

        var entries = await _client.GetFromJsonAsync<List<TrashEntryDto>>("/api/v1/trash?category=flights", JsonOptions);
        var entry = Assert.Single(entries!, e => e.EntityId == flightId);

        // Obnovenie.
        (await _client.PostAsync($"/api/v1/trash/{entry.Id}/restore", null)).EnsureSuccessStatusCode();
        Assert.Contains(flightId.Value, await GetIds("/api/v1/flights"));

        // Znova do koša a natrvalo vymazať — bez potvrdenia to nejde.
        (await _client.PostAsJsonAsync("/api/v1/trash", new MoveToTrashRequest(TrashCategories.Flights, flightId.Value, null))).EnsureSuccessStatusCode();
        entries = await _client.GetFromJsonAsync<List<TrashEntryDto>>("/api/v1/trash?category=flights", JsonOptions);
        entry = Assert.Single(entries!, e => e.EntityId == flightId);

        var noConfirm = await _client.PostAsJsonAsync($"/api/v1/trash/{entry.Id}/purge", new PurgeTrashRequest("nie"));
        Assert.Equal(HttpStatusCode.BadRequest, noConfirm.StatusCode);

        (await _client.PostAsJsonAsync($"/api/v1/trash/{entry.Id}/purge", new PurgeTrashRequest("VYMAZAŤ"))).EnsureSuccessStatusCode();
        entries = await _client.GetFromJsonAsync<List<TrashEntryDto>>("/api/v1/trash?category=flights", JsonOptions);
        Assert.DoesNotContain(entries!, e => e.EntityId == flightId);
        Assert.DoesNotContain(flightId.Value, await GetIds("/api/v1/flights"));
    }

    [Fact]
    public async Task Pilot_SLetmi_SaNedaVymazat()
    {
        await LoginAs("gestor", "huginko123");
        var flights = await _client.GetFromJsonAsync<JsonElement>("/api/v1/flights");
        var pilotId = flights.EnumerateArray()
            .Select(f => f.TryGetProperty("pilotId", out var p) && p.ValueKind == JsonValueKind.String ? p.GetGuid() : (Guid?)null)
            .FirstOrDefault(p => p is not null);
        Assert.NotNull(pilotId);

        var move = await _client.PostAsJsonAsync("/api/v1/trash", new MoveToTrashRequest(TrashCategories.Pilots, pilotId.Value, null));
        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
        Assert.Contains("Denník letov", await move.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Pilot_HromadneSoVsetkymiVazbami_DoKosa_AObnova()
    {
        await LoginAs("gestor", "huginko123");
        var flights = await _client.GetFromJsonAsync<JsonElement>("/api/v1/flights");
        var pilotId = flights.EnumerateArray()
            .Select(f => f.TryGetProperty("pilotId", out var p) && p.ValueKind == JsonValueKind.String ? p.GetGuid() : (Guid?)null)
            .Last(p => p is not null)!.Value;

        var deps = await _client.GetFromJsonAsync<TrashDependentsDto>(
            $"/api/v1/trash/dependents?category={TrashCategories.Pilots}&id={pilotId}", JsonOptions);
        Assert.NotNull(deps);
        Assert.Contains(deps.Items, i => i.Category == TrashCategories.Flights);

        // Bez väzieb to nejde, so všetkými áno.
        var alone = await _client.PostAsJsonAsync("/api/v1/trash/batch",
            new MoveToTrashBatchRequest([new TrashItemRef(TrashCategories.Pilots, pilotId)], null));
        Assert.Equal(HttpStatusCode.BadRequest, alone.StatusCode);

        var batch = deps.Items.Select(i => new TrashItemRef(i.Category, i.Id))
            .Append(new TrashItemRef(TrashCategories.Pilots, pilotId)).ToList();
        (await _client.PostAsJsonAsync("/api/v1/trash/batch", new MoveToTrashBatchRequest(batch, "test"))).EnsureSuccessStatusCode();
        Assert.DoesNotContain(pilotId, await GetIds("/api/v1/pilots"));

        // Hromadné obnovenie všetkého.
        var entries = await _client.GetFromJsonAsync<List<TrashEntryDto>>("/api/v1/trash", JsonOptions);
        var ids = entries!.Where(e => batch.Any(b => b.Id == e.EntityId)).Select(e => e.Id).ToList();
        Assert.Equal(batch.Count, ids.Count);
        (await _client.PostAsJsonAsync("/api/v1/trash/restore-batch", new RestoreTrashBatchRequest(ids))).EnsureSuccessStatusCode();
        Assert.Contains(pilotId, await GetIds("/api/v1/pilots"));
    }

    [Fact]
    public async Task Kos_VidiIbaGestor()
    {
        await LoginAs("schvalovatel", "Heslo123!");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/v1/trash/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/v1/trash")).StatusCode);
    }

    private async Task<List<Guid>> GetIds(string url)
    {
        var json = await _client.GetFromJsonAsync<JsonElement>(url);
        var array = json.ValueKind == JsonValueKind.Array ? json : json.GetProperty("items");
        return array.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
    }

    private async Task LoginAs(string userName, string password)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(userName, password));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        Assert.NotNull(login);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
    }
}
