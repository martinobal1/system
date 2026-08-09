using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;

namespace IsSuas.Domain.Identity;

public class Role : AuditableEntity
{
    public SystemRole Code { get; set; }
    public string NameSk { get; set; } = string.Empty;
    public string DescriptionSk { get; set; } = string.Empty;
    public bool GrantsBusinessDataAccess { get; set; } = true;
}
