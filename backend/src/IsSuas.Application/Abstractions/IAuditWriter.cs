namespace IsSuas.Application.Abstractions;

public interface IAuditWriter
{
    Task WriteAsync(
        string action,
        string entityType,
        Guid? entityId,
        object? before,
        object? after,
        string result = "Success",
        CancellationToken cancellationToken = default);
}
