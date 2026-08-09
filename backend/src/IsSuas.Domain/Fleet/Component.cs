using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Identity;

namespace IsSuas.Domain.Fleet;

public class Component : AuditableEntity
{
    public string InternalNumber { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ComponentTypeCode { get; set; }
    public AssetUsabilityStatus Status { get; set; } = AssetUsabilityStatus.Available;
    public Guid? OwningOrgUnitId { get; set; }
    public OrgUnit? OwningOrgUnit { get; set; }
    public ICollection<Installation> Installations { get; set; } = new List<Installation>();
}
