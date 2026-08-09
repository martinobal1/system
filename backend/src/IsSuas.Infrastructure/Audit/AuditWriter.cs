using System.Text.Json;
using IsSuas.Application.Abstractions;
using IsSuas.Domain.Common;
using IsSuas.Infrastructure.Persistence;

namespace IsSuas.Infrastructure.Audit;

public sealed class AuditWriter(
    IsSuasDbContext db,
    ICurrentUser currentUser,
    IClock clock) : IAuditWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task WriteAsync(
        string action,
        string entityType,
        Guid? entityId,
        object? before,
        object? after,
        string result = "Success",
        CancellationToken cancellationToken = default)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            OccurredAt = clock.UtcNow,
            ActorAccountId = currentUser.AccountId,
            ActorUserName = currentUser.UserName ?? "system",
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Result = result,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after, JsonOptions)
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
