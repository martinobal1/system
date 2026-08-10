using IsSuas.Domain.Common;
using IsSuas.Domain.Fleet;
using IsSuas.Domain.Identity;
using IsSuas.Domain.Pilots;

namespace IsSuas.Domain.Reporting;

/// <summary>Oprávnenie pilota lietat s konkrétnym UAS (M:N, platnosť od–do).</summary>
public class PilotUasAuthorization : AuditableEntity
{
    public Guid PilotId { get; set; }
    public Pilot Pilot { get; set; } = null!;
    public Guid UasId { get; set; }
    public Uas Uas { get; set; } = null!;
    public Guid OrgUnitId { get; set; }
    public OrgUnit OrgUnit { get; set; } = null!;
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? Note { get; set; }
}

public enum MonthlyFlightReportStatus
{
    Draft = 0,
    Confirmed = 1,
    NoActivity = 2
}

/// <summary>Mesačný výkaz letovej činnosti útvaru (súhrn z pilotných denníkov).</summary>
public class MonthlyFlightReport : AuditableEntity
{
    public Guid OrgUnitId { get; set; }
    public OrgUnit OrgUnit { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public MonthlyFlightReportStatus Status { get; set; } = MonthlyFlightReportStatus.Draft;
    public DateTimeOffset? ConfirmedAt { get; set; }
    public Guid? ConfirmedByAccountId { get; set; }
    public string? Note { get; set; }
    public ICollection<MonthlyFlightReportLine> Lines { get; set; } = new List<MonthlyFlightReportLine>();
    public ICollection<MonthlyFlightReportChangeLog> ChangeLogs { get; set; } = new List<MonthlyFlightReportChangeLog>();
}

public class MonthlyFlightReportLine : AuditableEntity
{
    public Guid ReportId { get; set; }
    public MonthlyFlightReport Report { get; set; } = null!;
    public Guid PilotId { get; set; }
    public Pilot Pilot { get; set; } = null!;
    public Guid UasId { get; set; }
    public Uas Uas { get; set; } = null!;
    public int Hours { get; set; }
    public int Minutes { get; set; }
    public int Takeoffs { get; set; }
    public int Landings { get; set; }
    public int OperationalFlights { get; set; }
    public int TrainingFlights { get; set; }
    public int OtherFlights { get; set; }
    public string? Note { get; set; }

    public int TotalMinutes => Hours * 60 + Minutes;
}

public class MonthlyFlightReportChangeLog : AuditableEntity
{
    public Guid ReportId { get; set; }
    public MonthlyFlightReport Report { get; set; } = null!;
    public Guid ChangedByAccountId { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string? PreviousJson { get; set; }
    public string? NewJson { get; set; }
}
