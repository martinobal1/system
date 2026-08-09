using IsSuas.Domain.Common;

namespace IsSuas.Domain.Identity;

public class Person : AuditableEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PersonalIdHash { get; set; }
    public Guid? HomeOrgUnitId { get; set; }
    public OrgUnit? HomeOrgUnit { get; set; }
}
