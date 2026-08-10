using IsSuas.Domain.Reporting;

namespace IsSuas.Application.Reporting;

public sealed record MonthlyFlightReportDto(
    Guid Id,
    Guid OrgUnitId,
    string OrgUnitName,
    int Year,
    int Month,
    MonthlyFlightReportStatus Status,
    DateTimeOffset? ConfirmedAt,
    Guid? ConfirmedByAccountId,
    string? Note,
    IReadOnlyList<MonthlyFlightReportLineDto> Lines,
    uint RowVersion);

public sealed record MonthlyFlightReportLineDto(
    Guid Id,
    Guid PilotId,
    string PilotName,
    Guid UasId,
    string UasLabel,
    int Hours,
    int Minutes,
    int Takeoffs,
    int Landings,
    int OperationalFlights,
    int TrainingFlights,
    int OtherFlights,
    string? Note);

public sealed record UpsertMonthlyFlightReportLineRequest(
    Guid? LineId,
    Guid PilotId,
    Guid UasId,
    int Hours,
    int Minutes,
    int Takeoffs,
    int Landings,
    int OperationalFlights,
    int TrainingFlights,
    int OtherFlights,
    string? Note);

public sealed record SaveMonthlyFlightReportRequest(
    string? Note,
    IReadOnlyList<UpsertMonthlyFlightReportLineRequest> Lines);

public sealed record ConfirmMonthlyFlightReportRequest(uint RowVersion);
public sealed record MarkNoActivityMonthlyFlightReportRequest(uint RowVersion, string? Note);
public sealed record AmendMonthlyFlightReportRequest(string Reason, SaveMonthlyFlightReportRequest Payload, uint RowVersion);

public sealed record MonthlyReportDeadlineStatsDto(
    int Year,
    int Month,
    int DueDay,
    int TotalUnits,
    int SubmittedUnits,
    int MissingUnits);
