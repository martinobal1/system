using IsSuas.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IsSuas.Infrastructure.Background;

public sealed class IsSuasMaintenanceWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<IsSuasMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Background:Enabled", true))
        {
            logger.LogInformation("IsSuasMaintenanceWorker is disabled (Background:Enabled=false).");
            return;
        }

        var intervalSeconds = configuration.GetValue("Background:IntervalSeconds", 300);
        intervalSeconds = Math.Clamp(intervalSeconds, 30, 3600);
        logger.LogInformation("IsSuasMaintenanceWorker started (interval {Interval}s).", intervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var documents = scope.ServiceProvider.GetRequiredService<IDocumentService>();
                var escalated = await notifications.RunEscalationsAsync(stoppingToken);
                var expired = await documents.RefreshExpirationsAsync(stoppingToken);
                if (escalated > 0 || expired > 0)
                    logger.LogInformation("Background maintenance: escalations={Escalations}, expiredDocs={Expired}", escalated, expired);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background maintenance tick failed.");
            }
        }
    }
}
