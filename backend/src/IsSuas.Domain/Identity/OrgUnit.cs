using IsSuas.Domain.Common;

namespace IsSuas.Domain.Identity;

public class OrgUnit : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public OrgUnit? Parent { get; set; }
    public ICollection<OrgUnit> Children { get; set; } = new List<OrgUnit>();
    /// <summary>Materialized path for hierarchy queries, e.g. /rootId/childId/</summary>
    public string Path { get; set; } = "/";
    public int Level { get; set; }
    public ICollection<OrgUnitMoveHistory> MoveHistory { get; set; } = new List<OrgUnitMoveHistory>();
}
