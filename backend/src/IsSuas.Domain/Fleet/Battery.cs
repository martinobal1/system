using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Identity;

namespace IsSuas.Domain.Fleet;

public class Battery : AuditableEntity
{
    public string InternalNumber { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? CompatibleUasTypeCodes { get; set; }
    public AssetUsabilityStatus Status { get; set; } = AssetUsabilityStatus.Available;
    public Guid? OwningOrgUnitId { get; set; }
    public OrgUnit? OwningOrgUnit { get; set; }
    public int CycleCount { get; set; }
    public decimal? CapacityPercent { get; set; }
    public decimal? HealthPercent { get; set; }
    public decimal? MaxTemperatureC { get; set; }
    public decimal? MinTemperatureC { get; set; }
    public ICollection<BatteryAssignment> Assignments { get; set; } = new List<BatteryAssignment>();
}
