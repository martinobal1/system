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
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().WithOrigins(
            "http://localhost:5173",
            "http://127.0.0.1:5173"));
});

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

await DatabaseSeeder.SeedAsync(app.Services);

app.Run();

public partial class Program;
