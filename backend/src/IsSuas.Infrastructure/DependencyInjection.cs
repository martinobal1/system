using System.Text;
using IsSuas.Application;
using IsSuas.Application.Abstractions;
using IsSuas.Infrastructure.Audit;
using IsSuas.Infrastructure.Auth;
using IsSuas.Infrastructure.Background;
using IsSuas.Infrastructure.Integrations;
using IsSuas.Infrastructure.Messaging;
using IsSuas.Infrastructure.Persistence;
using IsSuas.Infrastructure.Services;
using IsSuas.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace IsSuas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApplication();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOrgUnitService, OrgUnitService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IDelegationService, DelegationService>();
        services.AddScoped<IDelegatedPermissionChecker, DbDelegatedPermissionChecker>();
        services.AddScoped<IFleetService, FleetService>();
        services.AddScoped<IPilotService, PilotService>();
        services.AddScoped<IOperationService, OperationService>();
        services.AddScoped<IFlightService, FlightService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<ISafetyService, SafetyService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
        services.AddScoped<INotificationPublisher>(sp => sp.GetRequiredService<NotificationService>());
        services.AddObjectStorage(configuration);
        services.AddEmailSender(configuration);
        services.AddSingleton<IAirspaceMapAdapter, OfflineAirspaceMapAdapter>();
        services.AddSingleton<IWeatherProvider, OfflineShmuWeatherProvider>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IPilotUasAuthorizationService, PilotUasAuthorizationService>();
        services.AddScoped<IMonthlyFlightReportService, MonthlyFlightReportService>();
        services.AddScoped<IMonthlyReportDeadlineService, MonthlyReportDeadlineService>();
        services.AddScoped<IGeoZoneService, GeoZoneService>();
        services.AddScoped<IControlAuditService, ControlAuditService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IConfigRuleService, ConfigRuleService>();
        services.AddScoped<IIntegrationConnectorService, IntegrationConnectorService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<ISystemInfoService, SystemInfoService>();
        services.AddScoped<IGlobalSearchService, GlobalSearchService>();
        services.AddHostedService<IsSuasMaintenanceWorker>();

        var useInMemory = configuration.GetValue("Database:UseInMemory", false);
        if (useInMemory)
        {
            services.AddDbContext<IsSuasDbContext>(opt =>
                opt.UseInMemoryDatabase(configuration["Database:InMemoryName"] ?? "IsSuas"));
        }
        else
        {
            var cs = configuration.GetConnectionString("Default")
                ?? "Host=localhost;Port=5432;Database=issuas;Username=issuas;Password=issuas_dev";
            services.AddDbContext<IsSuasDbContext>(opt => opt.UseNpgsql(cs));
        }

        var signingKey = configuration["Jwt:SigningKey"] ?? "DEV_ONLY_CHANGE_ME_TO_A_LONG_RANDOM_SECRET_KEY_32+";
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "IsSuas",
                    ValidAudience = configuration["Jwt:Audience"] ?? "IsSuas",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
                };
            });
        services.AddAuthorization();

        return services;
    }
}
