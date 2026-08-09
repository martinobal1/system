using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;

namespace IsSuas.Domain.Identity;

public class Account : AuditableEntity
{
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public AccountStatus Status { get; set; } = AccountStatus.Prepared;
    public string? ExternalSubjectId { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public ICollection<RoleAssignment> RoleAssignments { get; set; } = new List<RoleAssignment>();
}
