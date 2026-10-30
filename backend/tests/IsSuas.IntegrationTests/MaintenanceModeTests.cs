using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IsSuas.Application.Auth;
using IsSuas.Application.Lookups;
using IsSuas.Application.SystemMaintenance;
using Microsoft.Extensions.Configuration;

namespace IsSuas.IntegrationTests;

/// <summary>
/// Režim údržby a núdzový vypínač na tajnej adrese. Vlastná fixture (vlastná DB),
/// aby zapnutá údržba neovplyvnila ostatné testy.
/// </summary>
public class MaintenanceModeTests : IClassFixture<IsSuasWebApplicationFactory>
{
    private const string SecretPath = "/TestTajnaAdresa1234567890";
    private const string SecretKey = "TEST_KILL_SWITCH_KEY_0123456789_ABCDEFGHIJ";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IsSuasWebApplicationFactory _factory;

    public MaintenanceModeTests(IsSuasWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithKillSwitch() =>
        _factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MaintenanceMode:KillSwitchPath"] = SecretPath.TrimStart('/'),
                ["MaintenanceMode:KillSwitchKey"] = SecretKey
            }))).CreateClient();

    [Fact]
    public async Task BezNastavenia_TajnaAdresaNeexistuje()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync(SecretPath, new KillSwitchRequest(SecretKey, true, null, null));
        // Bez nastavenej adresy a kľúča sa nič nevypne (požiadavka skončí na SPA fallbacku).
        Assert.False((await GetStatus(client)).IsActive);
    }

    [Fact]
    public async Task KillSwitch_VypneVsetkyUctyVrataneGestora_AZnovaZapne()
    {
        var client = ClientWithKillSwitch();

        var page = await client.GetAsync(SecretPath);
        page.EnsureSuccessStatusCode();
        Assert.Contains("Núdzový vypínač", await page.Content.ReadAsStringAsync());

        var wrong = await client.PostAsJsonAsync(SecretPath, new KillSwitchRequest("zly-kluc", true, null, null));
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        var gestorToken = await Login(client, "gestor", "huginko123");

        var off = await client.PostAsJsonAsync(SecretPath, new KillSwitchRequest(SecretKey, true, "Núdzové vypnutie", null));
        off.EnsureSuccessStatusCode();
        Assert.True((await GetStatus(client)).IsActive);

        // Prihlásenie nefunguje nikomu, ani gestorovi.
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("gestor", "huginko123"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, login.StatusCode);

        // Ani už prihlásený gestor sa k dátam nedostane.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", gestorToken);
        var blocked = await client.GetAsync("/api/v1/uas");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, blocked.StatusCode);
        Assert.True(blocked.Headers.Contains("X-Maintenance-Mode"));
        Assert.Contains("Núdzové vypnutie", await blocked.Content.ReadAsStringAsync());

        // Vypínač funguje aj počas údržby.
        client.DefaultRequestHeaders.Authorization = null;
        var on = await client.PostAsJsonAsync(SecretPath, new KillSwitchRequest(SecretKey, false, null, null));
        on.EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", gestorToken);
        (await client.GetAsync("/api/v1/uas")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ConfigRules_NedovoliaObistKlucRezimuUdrzby()
    {
        var client = _factory.CreateClient();
        var token = await Login(client, "gestor", "huginko123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync("/api/v1/config-rules", new CreateConfigRuleRequest(
            MaintenanceModeConstants.ConfigKey, "{\"enabled\":true}", DateTimeOffset.UtcNow, null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<MaintenanceModeStatusDto> GetStatus(HttpClient client)
    {
        var status = await client.GetFromJsonAsync<MaintenanceModeStatusDto>("/api/v1/system/maintenance", JsonOptions);
        Assert.NotNull(status);
        return status;
    }

    private static async Task<string> Login(HttpClient client, string userName, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(userName, password));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        Assert.NotNull(login);
        return login.AccessToken;
    }
}
