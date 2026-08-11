using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Identity;

namespace IsSuas.Domain.Fleet;

public class Uas : AuditableEntity
{
    public string? RegistrationNumber { get; set; }
    /// <summary>Č. osvedčenia o zápise do registra.</summary>
    public string? CertificateNumber { get; set; }
    /// <summary>Dátum výmazu z registra.</summary>
    public DateTimeOffset? DeregisteredAt { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? UasTypeCode { get; set; }
    /// <summary>Evidenčné číslo denníka.</summary>
    public string? LogbookNumber { get; set; }
    /// <summary>Číslo investičného majetku (Č. IM).</summary>
    public string? InventoryAssetNumber { get; set; }
    public UasLifecycleStatus Status { get; set; } = UasLifecycleStatus.Draft;
    public Guid? OwningOrgUnitId { get; set; }
    public OrgUnit? OwningOrgUnit { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? RegisteredAt { get; set; }
    public Guid? SubmittedByAccountId { get; set; }
    public Guid? ApprovedByAccountId { get; set; }
    public int TotalFlightCount { get; set; }
    public int TotalFlightMinutes { get; set; }
    public ICollection<UasAssignment> Assignments { get; set; } = new List<UasAssignment>();
    public ICollection<Installation> Installations { get; set; } = new List<Installation>();
    public ICollection<UasStatusHistory> StatusHistory { get; set; } = new List<UasStatusHistory>();
}
