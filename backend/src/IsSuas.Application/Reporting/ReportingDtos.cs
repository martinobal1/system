using IsSuas.Domain.Reporting;

namespace IsSuas.Application.Reporting;

public sealed record DashboardDto(
    string RoleHint,
    FleetSummaryDto Fleet,
    int OperationsToday,
    int OpenTasks,
    int OpenDefects,
    int OpenIncidents,
    int UnreadNotifications,
    int OverdueMaintenance,
    IReadOnlyList<CalendarItemDto> UpcomingOperations,
    IReadOnlyList<AlertItemDto> CriticalAlerts,
    string Scope = "central",
    Guid? ScopeOrgUnitId = null,
    string? ScopeOrgUnitName = null,
    decimal TotalFlightHours = 0,
    IReadOnlyList<FlightHoursSliceDto>? FlightHoursByOrgUnit = null,
    IReadOnlyList<FlightHoursSliceDto>? FlightHoursByUas = null,
    IReadOnlyList<FlightHoursSliceDto>? FlightHoursByPilot = null,
    int ConfirmedMonthlyReports = 0,
    int MissingMonthlyReports = 0,
    int TotalTakeoffs = 0,
    int TotalLandings = 0,
    string FlightHoursSource = "monthly",
    IReadOnlyList<FleetModelCountDto>? FleetByModel = null);

public sealed record FleetSummaryDto(
    int TotalUas,
    int InService,
    int Suspended,
    int InMaintenance,
    int Draft,
    int InStorage = 0,
    int Registered = 0,
    int Damaged = 0,
    int Lost = 0,
    int Decommissioned = 0,
    int InUse = 0,
    int Idle = 0);

public sealed record FleetModelCountDto(
    string Label,
    string Manufacturer,
    string Model,
    int Count,
    int InUse,
    int Idle);

public sealed record FlightHoursSliceDto(string Key, string Label, decimal Hours, int FlightCount);

public sealed record CalendarItemDto(
    Guid Id,
    string Title,
    string Status,
    DateTimeOffset Start,
    DateTimeOffset End,
    string OrgUnitName,
    bool IsUrgent);

public sealed record AlertItemDto(Guid Id, string Title, string Priority, string TypeCode, Guid? RelatedEntityId, string? RelatedEntityType);

public sealed record ReportRequest(
    string ReportTypeCode,
    DateTimeOffset? From,
    DateTimeOffset? To,
    Guid? OrgUnitId);

public sealed record ReportDto(
    string ReportTypeCode,
    string TitleSk,
    DateTimeOffset DataAsOf,
    string RulesVersion,
    IReadOnlyList<ReportRowDto> Rows,
    IReadOnlyDictionary<string, decimal> Totals);

public sealed record ReportRowDto(string Key, string Label, decimal Value, string? Unit, string? Group);

public sealed record CreateOfficialReportRequest(
    string ReportTypeCode,
    string TitleSk,
    DateTimeOffset? From,
    DateTimeOffset? To,
    Guid? OrgUnitId,
    Guid? SupersedesReportId);

public sealed record ApproveOfficialReportRequest(string ApprovalReason, uint RowVersion);

public sealed record OfficialReportDto(
    Guid Id,
    Guid ReportFamilyId,
    string ReportTypeCode,
    string TitleSk,
    int Version,
    DateTimeOffset DataAsOf,
    OfficialReportStatus Status,
    Guid PreparedByAccountId,
    Guid? ApprovedByAccountId,
    DateTimeOffset? ApprovedAt,
    string? ApprovalReason,
    string SnapshotJson,
    string RulesJson,
    uint RowVersion);

public static class ReportRules
{
    public const string Version = "REPORT_RULES_V1";

    /// <summary>
    /// Flights and operations are counted in separate metrics — never both into the same indicator.
    /// </summary>
    public static string DescriptionJson =>
        """{"version":"REPORT_RULES_V1","noDoubleCount":"operations_count and flights_count are separate indicators","flightDuration":"sum closed flights durationMinutes"}""";
}
