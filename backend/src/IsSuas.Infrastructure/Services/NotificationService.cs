using IsSuas.Application.Abstractions;
using IsSuas.Application.Notifications;
using IsSuas.Application.Policies;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Notifications;
using IsSuas.Domain.Operations;
using IsSuas.Domain.Safety;
using IsSuas.Infrastructure.Messaging;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IsSuas.Infrastructure.Services;

public interface INotificationPublisher
{
    Task<Guid> PublishAsync(PublishNotificationRequest request, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDetailDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<NotificationDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NotificationDetailDto> PublishAsync(PublishNotificationRequest request, CancellationToken cancellationToken = default);
    Task<NotificationDetailDto> MarkReadAsync(Guid id, MarkNotificationReadRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessageTemplateDto>> ListTemplatesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EscalationRuleDto>> ListEscalationRulesAsync(CancellationToken cancellationToken = default);
    Task<int> RunEscalationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkTaskHistoryDto>> ListTaskHistoryAsync(Guid workTaskId, CancellationToken cancellationToken = default);
    Task UpdateWorkTaskStatusAsync(Guid workTaskId, UpdateWorkTaskStatusRequest request, CancellationToken cancellationToken = default);
}

public sealed class NotificationService(
    IsSuasDbContext db,
    ICurrentUser currentUser,
    IAuthorizationPolicyService policy,
    IAuditWriter audit,
    IClock clock,
    IEmailSender emailSender,
    IConfiguration configuration) : INotificationService, INotificationPublisher
{
    public async Task<IReadOnlyList<NotificationDetailDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotifyRead();
        var items = await db.AppNotifications.AsNoTracking()
            .Where(n => n.IsActive)
            .OrderByDescending(n => n.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        var ids = items.Select(i => i.Id).ToList();
        var recipients = await db.NotificationRecipients.AsNoTracking()
            .Where(r => ids.Contains(r.AppNotificationId) && r.IsActive).ToListAsync(cancellationToken);
        var deliveries = await db.DeliveryAttempts.AsNoTracking()
            .Where(d => ids.Contains(d.AppNotificationId) && d.IsActive).ToListAsync(cancellationToken);
        return items.Select(n => MapDetail(n,
            recipients.Where(r => r.AppNotificationId == n.Id).ToList(),
            deliveries.Where(d => d.AppNotificationId == n.Id).ToList())).ToList();
    }

    public async Task<NotificationDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureNotifyRead();
        var n = await db.AppNotifications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Notifikácia nebola nájdená.");
        var recipients = await db.NotificationRecipients.AsNoTracking()
            .Where(r => r.AppNotificationId == id && r.IsActive).ToListAsync(cancellationToken);
        var deliveries = await db.DeliveryAttempts.AsNoTracking()
            .Where(d => d.AppNotificationId == id && d.IsActive).ToListAsync(cancellationToken);
        return MapDetail(n, recipients, deliveries);
    }

    public async Task<NotificationDetailDto> PublishAsync(PublishNotificationRequest request, CancellationToken cancellationToken = default)
        => await PublishInternalAsync(request, cancellationToken);

    async Task<Guid> INotificationPublisher.PublishAsync(PublishNotificationRequest request, CancellationToken cancellationToken)
    {
        var detail = await PublishInternalAsync(request, cancellationToken);
        return detail.Id;
    }

    private async Task<NotificationDetailDto> PublishInternalAsync(PublishNotificationRequest request, CancellationToken cancellationToken)
    {
        // Allow system/internal callers (safety/docs) without extra write permission when already authenticated with business access.
        if (!policy.HasPermission(Permissions.NotificationsWrite) && !policy.HasPermission(Permissions.SafetyWrite)
            && !policy.HasPermission(Permissions.DocumentsWrite) && !policy.HasPermission(Permissions.OperationsWrite))
            policy.EnsurePermission(Permissions.NotificationsWrite);

        var title = request.Title;
        var message = request.Message;
        if (!string.IsNullOrWhiteSpace(request.TemplateCode))
        {
            var template = await db.MessageTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Code == request.TemplateCode && t.IsActive, cancellationToken)
                ?? throw new NotFoundException("Šablóna správy nebola nájdená.");
            title = ApplyVars(template.SubjectTemplate, request);
            message = ApplyVars(template.BodyTemplate, request);
        }

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            throw new ValidationException("Názov a text notifikácie sú povinné.");

        var notification = new AppNotification
        {
            Title = title.Trim(),
            Message = message.Trim(),
            TypeCode = string.IsNullOrWhiteSpace(request.TypeCode) ? "GENERAL" : request.TypeCode.Trim(),
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Normal" : request.Priority.Trim(),
            RecipientAccountId = request.RecipientAccountId,
            RecipientRoleCode = request.RecipientRoleCode,
            RelatedSafetyEventId = request.RelatedSafetyEventId,
            RelatedEntityType = request.RelatedEntityType,
            RelatedEntityId = request.RelatedEntityId,
            Status = NotificationStatus.Unread,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.AppNotifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);

        var recipient = new NotificationRecipient
        {
            AppNotificationId = notification.Id,
            AccountId = request.RecipientAccountId,
            RoleCode = request.RecipientRoleCode,
            Status = NotificationStatus.Unread,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.NotificationRecipients.Add(recipient);
        await db.SaveChangesAsync(cancellationToken);

        var channel = request.Channel;
        var attempt = new DeliveryAttempt
        {
            AppNotificationId = notification.Id,
            NotificationRecipientId = recipient.Id,
            Channel = channel,
            AttemptedAt = clock.UtcNow,
            AttemptNumber = 1,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };

        if (channel == NotificationChannel.InApp)
        {
            attempt.Status = DeliveryAttemptStatus.Sent;
        }
        else if (channel == NotificationChannel.Email)
        {
            try
            {
                var to = await ResolveRecipientEmailAsync(request.RecipientAccountId, request.RecipientRoleCode, cancellationToken);
                if (string.IsNullOrWhiteSpace(to))
                {
                    attempt.Status = DeliveryAttemptStatus.Skipped;
                    attempt.ErrorMessage = "E-mail príjemcu nebol nájdený.";
                }
                else
                {
                    await emailSender.SendAsync(new EmailMessage(to, title.Trim(), message.Trim()), cancellationToken);
                    attempt.Status = DeliveryAttemptStatus.Sent;
                    attempt.ErrorMessage = $"Provider={emailSender.ProviderName}";
                }
            }
            catch (Exception ex)
            {
                attempt.Status = DeliveryAttemptStatus.Failed;
                attempt.ErrorMessage = ex.Message;
            }
        }
        else
        {
            attempt.Status = DeliveryAttemptStatus.Skipped;
            attempt.ErrorMessage = "SMS kanál ešte nie je napojený — doručenie označené ako Skipped.";
        }

        db.DeliveryAttempts.Add(attempt);
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync("Notification.Publish", nameof(AppNotification), notification.Id, null,
            new { notification.Title, notification.TypeCode, channel }, cancellationToken: cancellationToken);

        return MapDetail(notification, [recipient], [attempt]);
    }

    public async Task<NotificationDetailDto> MarkReadAsync(Guid id, MarkNotificationReadRequest request, CancellationToken cancellationToken = default)
    {
        EnsureNotifyRead();
        var n = await db.AppNotifications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Notifikácia nebola nájdená.");
        if (n.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
        n.Status = NotificationStatus.Read;
        n.UpdatedBy = currentUser.AccountId;

        var recipients = await db.NotificationRecipients
            .Where(r => r.AppNotificationId == id && r.IsActive).ToListAsync(cancellationToken);
        foreach (var r in recipients)
        {
            if (r.AccountId is null || r.AccountId == currentUser.AccountId || currentUser.IsSystemAdministrator)
            {
                r.Status = NotificationStatus.Read;
                r.ReadAt = clock.UtcNow;
                r.UpdatedBy = currentUser.AccountId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        var deliveries = await db.DeliveryAttempts.AsNoTracking()
            .Where(d => d.AppNotificationId == id && d.IsActive).ToListAsync(cancellationToken);
        return MapDetail(n, recipients, deliveries);
    }

    public async Task<IReadOnlyList<MessageTemplateDto>> ListTemplatesAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotifyRead();
        return await db.MessageTemplates.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Code)
            .Select(t => new MessageTemplateDto(t.Id, t.Code, t.NameSk, t.SubjectTemplate, t.BodyTemplate, t.Channel, t.Language))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EscalationRuleDto>> ListEscalationRulesAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotifyRead();
        return await db.EscalationRules.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Code)
            .Select(r => new EscalationRuleDto(r.Id, r.Code, r.NameSk, r.Trigger, r.ThresholdHours, r.TargetRoleCode, r.SourceTypeCode))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> RunEscalationsAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.NotificationsWrite);
        var rules = await db.EscalationRules.AsNoTracking().Where(r => r.IsActive).ToListAsync(cancellationToken);
        var created = 0;
        var now = clock.UtcNow;

        foreach (var rule in rules.Where(r => r.Trigger == EscalationTrigger.UnreadAfterHours))
        {
            var threshold = now.AddHours(-rule.ThresholdHours);
            var unread = await db.AppNotifications.AsNoTracking()
                .Where(n => n.IsActive && n.Status == NotificationStatus.Unread && n.CreatedAt <= threshold
                    && (rule.SourceTypeCode == null || n.TypeCode == rule.SourceTypeCode))
                .Take(50)
                .ToListAsync(cancellationToken);

            foreach (var n in unread)
            {
                var escType = "ESCALATION_" + n.TypeCode;
                var exists = await db.AppNotifications.AnyAsync(x =>
                    x.TypeCode == escType && x.RelatedEntityId == n.Id && x.RecipientRoleCode == rule.TargetRoleCode, cancellationToken);
                if (exists) continue;

                await PublishInternalAsync(new PublishNotificationRequest(
                    null,
                    $"Eskalácia: {n.Title}",
                    string.IsNullOrWhiteSpace(rule.EscalationMessageTemplate)
                        ? n.Message
                        : rule.EscalationMessageTemplate.Replace("{title}", n.Title, StringComparison.OrdinalIgnoreCase),
                    escType,
                    "High",
                    null,
                    rule.TargetRoleCode,
                    n.RelatedSafetyEventId,
                    nameof(AppNotification),
                    n.Id), cancellationToken);
                created++;
            }
        }

        foreach (var rule in rules.Where(r => r.Trigger == EscalationTrigger.TaskOverdueHours))
        {
            var threshold = now.AddHours(-rule.ThresholdHours);
            var overdue = await db.WorkTasks.AsNoTracking()
                .Where(t => t.IsActive && t.Status == WorkTaskStatus.Open && t.DueAt != null && t.DueAt < threshold)
                .Take(50)
                .ToListAsync(cancellationToken);
            foreach (var task in overdue)
            {
                var escType = "ESCALATION_TASK";
                var exists = await db.AppNotifications.AnyAsync(x =>
                    x.TypeCode == escType && x.RelatedEntityId == task.Id && x.RecipientRoleCode == rule.TargetRoleCode, cancellationToken);
                if (exists) continue;
                await PublishInternalAsync(new PublishNotificationRequest(
                    null,
                    $"Eskalácia úlohy: {task.Title}",
                    rule.EscalationMessageTemplate.Replace("{title}", task.Title, StringComparison.OrdinalIgnoreCase),
                    escType,
                    "High",
                    null,
                    rule.TargetRoleCode,
                    null,
                    nameof(WorkTask),
                    task.Id), cancellationToken);
                created++;
            }
        }

        return created;
    }

    public async Task<IReadOnlyList<WorkTaskHistoryDto>> ListTaskHistoryAsync(Guid workTaskId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OperationsRead);
        return await db.WorkTaskHistories.AsNoTracking()
            .Where(h => h.WorkTaskId == workTaskId && h.IsActive)
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new WorkTaskHistoryDto(h.Id, h.WorkTaskId, h.FromStatus, h.ToStatus, h.ChangedByAccountId, h.Note, h.ChangedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateWorkTaskStatusAsync(Guid workTaskId, UpdateWorkTaskStatusRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OperationsWrite);
        var task = await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == workTaskId, cancellationToken)
            ?? throw new NotFoundException("Pracovná úloha nebola nájdená.");
        if (task.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");

        var from = task.Status;
        if (from == request.Status)
            return;

        task.Status = request.Status;
        task.UpdatedBy = currentUser.AccountId;
        db.WorkTaskHistories.Add(new WorkTaskHistory
        {
            WorkTaskId = task.Id,
            FromStatus = from,
            ToStatus = request.Status,
            ChangedByAccountId = currentUser.AccountId,
            Note = request.Note?.Trim(),
            ChangedAt = clock.UtcNow,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        });
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("WorkTask.StatusChange", nameof(WorkTask), task.Id,
            new { from }, new { to = request.Status, request.Note }, cancellationToken: cancellationToken);
    }

    private async Task<string?> ResolveRecipientEmailAsync(Guid? accountId, string? roleCode, CancellationToken cancellationToken)
    {
        if (accountId is Guid id)
        {
            var email = await db.Accounts.AsNoTracking()
                .Where(a => a.Id == id && a.IsActive)
                .Select(a => a.Person.Email)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(email))
                return email;
        }

        if (!string.IsNullOrWhiteSpace(roleCode)
            && Enum.TryParse<SystemRole>(roleCode, ignoreCase: true, out var role))
        {
            var email = await db.RoleAssignments.AsNoTracking()
                .Where(r => r.IsActive && r.Role.Code == role)
                .Select(r => r.Account.Person.Email)
                .FirstOrDefaultAsync(e => e != null && e != "", cancellationToken);
            if (!string.IsNullOrWhiteSpace(email))
                return email;
        }

        return configuration["Email:DefaultRecipient"];
    }

    private void EnsureNotifyRead()
    {
        if (!policy.HasPermission(Permissions.NotificationsRead) && !policy.HasPermission(Permissions.SafetyRead)
            && !policy.HasPermission(Permissions.OperationsRead))
            policy.EnsurePermission(Permissions.NotificationsRead);
    }

    private static string ApplyVars(string template, PublishNotificationRequest request) =>
        template
            .Replace("{title}", request.Title ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{message}", request.Message ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{type}", request.TypeCode ?? "", StringComparison.OrdinalIgnoreCase);

    private static NotificationDetailDto MapDetail(
        AppNotification n,
        IReadOnlyList<NotificationRecipient> recipients,
        IReadOnlyList<DeliveryAttempt> deliveries) =>
        new(n.Id, n.Title, n.Message, n.TypeCode, n.Priority, n.RelatedSafetyEventId, n.RelatedEntityType, n.RelatedEntityId,
            n.Status, n.CreatedAt, n.RowVersion,
            recipients.Select(r => new NotificationRecipientDto(r.Id, r.AccountId, r.RoleCode, r.Status, r.ReadAt)).ToList(),
            deliveries.OrderBy(d => d.AttemptNumber)
                .Select(d => new DeliveryAttemptDto(d.Id, d.Channel, d.Status, d.AttemptedAt, d.ErrorMessage, d.AttemptNumber)).ToList());
}
