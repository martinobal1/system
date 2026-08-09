using IsSuas.Domain.Common;

namespace IsSuas.Domain.Reporting;

public class OfficialReport : AuditableEntity
{
    public string ReportTypeCode { get; set; } = string.Empty;
    public string TitleSk { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public Guid ReportFamilyId { get; set; } = Guid.NewGuid();
    public Guid? SupersedesReportId { get; set; }
    public DateTimeOffset DataAsOf { get; set; }
    public string FiltersJson { get; set; } = "{}";
    /// <summary>Frozen computation rules used to produce SnapshotJson.</summary>
    public string RulesJson { get; set; } = "{}";
    public string SnapshotJson { get; set; } = "{}";
    public OfficialReportStatus Status { get; set; } = OfficialReportStatus.Draft;
    public Guid PreparedByAccountId { get; set; }
    public Guid? ApprovedByAccountId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovalReason { get; set; }
}

public enum OfficialReportStatus
{
    Draft = 0,
    Approved = 1,
    Superseded = 2
}
