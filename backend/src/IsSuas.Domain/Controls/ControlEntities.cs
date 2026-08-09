using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Identity;

namespace IsSuas.Domain.Controls;

/// <summary>ENT-104 — kontrola alebo audit (MOD-15).</summary>
public class ControlAudit : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public ControlAuditType Type { get; set; } = ControlAuditType.InternalControl;
    public ControlAuditStatus Status { get; set; } = ControlAuditStatus.Planned;
    public Guid OrgUnitId { get; set; }
    public OrgUnit OrgUnit { get; set; } = null!;
    public DateTimeOffset? PlannedFrom { get; set; }
    public DateTimeOffset? PlannedTo { get; set; }
    public string? ScopeDescription { get; set; }
    public string? Summary { get; set; }
    public ICollection<ControlTeamMember> TeamMembers { get; set; } = new List<ControlTeamMember>();
    public ICollection<ControlChecklistItem> ChecklistItems { get; set; } = new List<ControlChecklistItem>();
    public ICollection<ControlFinding> Findings { get; set; } = new List<ControlFinding>();
    public ICollection<ControlReport> Reports { get; set; } = new List<ControlReport>();
}

public class ControlTeamMember : AuditableEntity
{
    public Guid ControlAuditId { get; set; }
    public ControlAudit ControlAudit { get; set; } = null!;
    public Guid AccountId { get; set; }
    public string RoleInTeam { get; set; } = "Member";
}

/// <summary>ENT-106/107 — kontrolný bod a výsledok (zjednodušene v jednej entite).</summary>
public class ControlChecklistItem : AuditableEntity
{
    public Guid ControlAuditId { get; set; }
    public ControlAudit ControlAudit { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string QuestionSk { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ControlCheckpointResultStatus ResultStatus { get; set; } = ControlCheckpointResultStatus.Pending;
    public string? ResultNote { get; set; }
}

/// <summary>ENT-108 — kontrolné zistenie.</summary>
public class ControlFinding : AuditableEntity
{
    public Guid ControlAuditId { get; set; }
    public ControlAudit ControlAudit { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ControlFindingSeverity Severity { get; set; } = ControlFindingSeverity.Medium;
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public ICollection<ControlCorrectiveAction> Actions { get; set; } = new List<ControlCorrectiveAction>();
}

/// <summary>ENT-109/110 — nápravné opatrenie a plnenie.</summary>
public class ControlCorrectiveAction : AuditableEntity
{
    public Guid ControlFindingId { get; set; }
    public ControlFinding ControlFinding { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerAccountId { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public ControlActionStatus Status { get; set; } = ControlActionStatus.Open;
    public string? ProgressNote { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? VerifiedByAccountId { get; set; }
}

/// <summary>ENT-111 — kontrolná/auditná správa (zmrazený výstup).</summary>
public class ControlReport : AuditableEntity
{
    public Guid ControlAuditId { get; set; }
    public ControlAudit ControlAudit { get; set; } = null!;
    public int Version { get; set; } = 1;
    public string ContentJson { get; set; } = "{}";
    public bool IsFrozen { get; set; }
    public Guid IssuedByAccountId { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
}
