using IsSuas.Application.Abstractions;
using IsSuas.Application.Notifications;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Reporting;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IsSuas.Infrastructure.Services;

public interface IMonthlyReportDeadlineService
{
    Task<int> RunRemindersAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Reminder/overdue notifications for previous-month flight reports (default due day = 5).
/// </summary>
public sealed class MonthlyReportDeadlineService(
    IsSuasDbContext db,
    INotificationPublisher notifications,
    IConfiguration configuration,
    IClock clock,
    ILogger<MonthlyReportDeadlineService> logger) : IMonthlyReportDeadlineService
{
    public async Task<int> RunRemindersAsync(CancellationToken cancellationToken = default)
    {
        var dueDay = configuration.GetValue("MonthlyReports:DueDayOfMonth", 5);
        dueDay = Math.Clamp(dueDay, 1, 28);
        var remindDaysBefore = configuration.GetValue("MonthlyReports:RemindDaysBefore", 2);
        remindDaysBefore = Math.Clamp(remindDaysBefore, 0, 14);

        var now = clock.UtcNow;
        var target = now.AddMonths(-1);
        var year = target.Year;
        var month = target.Month;

        var dueDate = new DateTimeOffset(now.Year, now.Month, Math.Min(dueDay, DateTime.DaysInMonth(now.Year, now.Month)), 0, 0, 0, TimeSpan.Zero);
        var remindFrom = dueDate.AddDays(-remindDaysBefore);
        if (now < remindFrom)
            return 0;

        var isOverdue = now.Date > dueDate.Date;
        var phase = isOverdue ? "OVERDUE" : "REMINDER";

        var leafUnits = await db.OrgUnits.AsNoTracking()
            .Where(o => o.IsActive && o.Kind == OrgUnitKind.Unit)
            .ToListAsync(cancellationToken);

        var submitted = await db.MonthlyFlightReports.AsNoTracking()
            .Where(r => r.IsActive && r.Year == year && r.Month == month
                && (r.Status == MonthlyFlightReportStatus.Confirmed || r.Status == MonthlyFlightReportStatus.NoActivity))
            .Select(r => r.OrgUnitId)
            .ToListAsync(cancellationToken);
        var submittedSet = submitted.ToHashSet();

        var unitAdmins = await db.RoleAssignments.AsNoTracking()
            .Include(a => a.Role)
            .Include(a => a.Account)
            .Where(a => a.IsActive
                && (a.ValidTo == null || a.ValidTo > now)
                && a.Role.Code == SystemRole.UnitAdministrator
                && a.Account.IsActive)
            .ToListAsync(cancellationToken);

        var created = 0;
        foreach (var unit in leafUnits)
        {
            if (submittedSet.Contains(unit.Id))
                continue;

            var recipients = unitAdmins.Where(a => a.OrgUnitId == unit.Id).Select(a => a.AccountId).Distinct().ToList();
            foreach (var accountId in recipients)
            {
                var roleCode = $"{phase}:{year}{month:D2}:{accountId:N}";
                var exists = await db.AppNotifications.AnyAsync(n =>
                    n.IsActive
                    && n.TypeCode == "MONTHLY_REPORT_DEADLINE"
                    && n.RelatedEntityType == "OrgUnit"
                    && n.RelatedEntityId == unit.Id
                    && n.RecipientRoleCode == roleCode, cancellationToken);
                if (exists)
                    continue;

                var title = isOverdue
                    ? $"Mesačný výkaz {month:D2}/{year} — po termíne ({unit.Name})"
                    : $"Mesačný výkaz {month:D2}/{year} — termín {dueDay}. dňa ({unit.Name})";
                var message = isOverdue
                    ? $"Útvar {unit.Name} nemá potvrdený mesačný výkaz za {month:D2}/{year}. Termín bol {dueDay}. deň mesiaca."
                    : $"Pripomienka: do {dueDay}. dňa odovzdajte mesačný výkaz za {month:D2}/{year} pre útvar {unit.Name}.";

                await notifications.PublishAsync(new PublishNotificationRequest(
                    null,
                    title,
                    message,
                    "MONTHLY_REPORT_DEADLINE",
                    isOverdue ? "High" : "Normal",
                    accountId,
                    roleCode,
                    null,
                    "OrgUnit",
                    unit.Id,
                    NotificationChannel.InApp), cancellationToken);
                created++;
            }
        }

        if (created > 0)
            logger.LogInformation("Monthly report deadline notifications created: {Count} ({Phase} {Year}-{Month})", created, phase, year, month);
        return created;
    }
}
