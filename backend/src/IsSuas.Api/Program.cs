using IsSuas.Infrastructure;
using IsSuas.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "IS ŠUAS API",
        Version = "v1"
    });

    options.AddSecurityDefinition("bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Vlož sem iba JWT token (bez slova Bearer). Po logine skopíruj accessToken."
    });

    options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("bearer", document)] = []
    });
});
// Povolené cross-origin adresy sú v konfigurácii (Cors:AllowedOrigins), nie v kóde.
// SPA vo wwwroot beží na rovnakej adrese ako API, takže v produkcii zvyčajne netreba nič.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod();
        if (corsOrigins.Length > 0)
            policy.WithOrigins(corsOrigins);
    });
});

// Za reverznou proxy (nginx, IIS ARR, Traefik, load balancer...) — zapína sa v konfigurácii.
// Bez zoznamu KnownProxies sa dôveruje len proxy na tom istom stroji (loopback).
var useForwardedHeaders = builder.Configuration.GetValue("ReverseProxy:Enabled", false);
if (useForwardedHeaders)
{
    builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
            | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
        foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        {
            if (System.Net.IPAddress.TryParse(proxy, out var address))
                options.KnownProxies.Add(address);
        }
    });
}

builder.Services.AddInfrastructure(builder.Configuration);

// Núdzový vypínač: max. 5 pokusov za minútu z jednej IP adresy (ochrana proti hádaniu kľúča).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(IsSuas.Api.SystemMaintenance.KillSwitchEndpoints.RateLimitPolicy, context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});
builder.Services.AddHttpClient("lps-gis", client =>
{
    client.BaseAddress = new Uri("https://gis.lps.sk/server/rest/services/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("IsSuas/1.0 (VFR map proxy)");
});

var app = builder.Build();

if (useForwardedHeaders)
    app.UseForwardedHeaders();
app.UseMiddleware<IsSuas.Api.ExceptionHandlingMiddleware>();
app.UseMiddleware<IsSuas.Api.SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.DocumentTitle = "IS ŠUAS API (nie UI)";
        c.HeadContent = """
            <div style="padding:12px 20px;background:#1b4f72;color:#fff;font-family:Segoe UI,sans-serif">
              Toto je Swagger (test API), nie aplikácia.
              <a href="/" style="color:#fff;font-weight:700;margin-left:12px">→ Otvoriť prihlasovacie UI</a>
            </div>
            """;
    });
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<IsSuas.Api.SystemMaintenance.MaintenanceModeMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
IsSuas.Api.SystemMaintenance.KillSwitchEndpoints.MapKillSwitch(app);
app.MapFallbackToFile("index.html");

await DatabaseSeeder.SeedAsync(app.Services);

app.Run();

public partial class Program;
