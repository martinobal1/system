using IsSuas.Domain.Enums;

namespace IsSuas.Application.Policies;

/// <summary>Static role→permission map helpers used by policy and delegation validation.</summary>
public static class PermissionCatalog
{
    public static readonly IReadOnlyDictionary<string, HashSet<SystemRole>> RoleMap =
        new Dictionary<string, HashSet<SystemRole>>
        {
            [Permissions.OrgUnitsRead] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.RemotePilot, SystemRole.ControllerAuditor, SystemRole.ReadOnlyUser, SystemRole.SystemAdministrator],
            [Permissions.OrgUnitsWrite] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator],
            [Permissions.AccountsRead] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.ControllerAuditor, SystemRole.SystemAdministrator],
            [Permissions.AccountsWrite] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator],
            [Permissions.RolesAssign] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator],
            [Permissions.LookupsRead] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.Approver, SystemRole.RemotePilot, SystemRole.TechnicalWorker, SystemRole.SafetyOfficer, SystemRole.ControllerAuditor, SystemRole.ReadOnlyUser, SystemRole.SystemAdministrator],
            [Permissions.LookupsWrite] = [SystemRole.CentralGestor, SystemRole.SystemAdministrator],
            [Permissions.AuditRead] = [SystemRole.CentralGestor, SystemRole.ControllerAuditor, SystemRole.SystemAdministrator],
            [Permissions.UasRead] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.Approver, SystemRole.RemotePilot, SystemRole.TechnicalWorker, SystemRole.SafetyOfficer, SystemRole.ControllerAuditor, SystemRole.ReadOnlyUser],
            [Permissions.UasWrite] = [SystemRole.CentralGestor, SystemRole.TechnicalWorker],
            [Permissions.UasApprove] = [SystemRole.CentralGestor, SystemRole.Approver],
            [Permissions.ComponentsRead] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.TechnicalWorker, SystemRole.ControllerAuditor, SystemRole.ReadOnlyUser],
            [Permissions.ComponentsWrite] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.TechnicalWorker],
            [Permissions.BatteriesRead] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.TechnicalWorker, SystemRole.RemotePilot, SystemRole.ControllerAuditor, SystemRole.ReadOnlyUser],
            [Permissions.BatteriesWrite] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.TechnicalWorker],
            [Permissions.PilotsRead] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.Approver, SystemRole.RemotePilot, SystemRole.TechnicalWorker, SystemRole.SafetyOfficer, SystemRole.ControllerAuditor, SystemRole.ReadOnlyUser],
            [Permissions.PilotsWrite] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator],
            [Permissions.OperationsRead] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.Approver, SystemRole.RemotePilot, SystemRole.TechnicalWorker, SystemRole.SafetyOfficer, SystemRole.ControllerAuditor, SystemRole.ReadOnlyUser],
            [Permissions.OperationsWrite] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.RemotePilot],
            [Permissions.OperationsApprove] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.Approver],
            [Permissions.FlightsRead] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.Approver, SystemRole.RemotePilot, SystemRole.TechnicalWorker, SystemRole.SafetyOfficer, SystemRole.ControllerAuditor, SystemRole.ReadOnlyUser],
            [Permissions.FlightsWrite] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.RemotePilot, SystemRole.TechnicalWorker],
            [Permissions.MaintenanceRead] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.TechnicalWorker, SystemRole.RemotePilot, SystemRole.ControllerAuditor, SystemRole.ReadOnlyUser],
            [Permissions.MaintenanceWrite] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.TechnicalWorker, SystemRole.RemotePilot],
            [Permissions.SafetyRead] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.Approver, SystemRole.RemotePilot, SystemRole.TechnicalWorker, SystemRole.SafetyOfficer, SystemRole.ControllerAuditor, SystemRole.ReadOnlyUser],
            [Permissions.SafetyWrite] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.SafetyOfficer, SystemRole.RemotePilot, SystemRole.TechnicalWorker],
            [Permissions.SafetySensitiveRead] = [SystemRole.CentralGestor, SystemRole.SafetyOfficer, SystemRole.ControllerAuditor],
            [Permissions.SafetyClose] = [SystemRole.CentralGestor, SystemRole.SafetyOfficer],
            [Permissions.DocumentsRead] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.Approver, SystemRole.RemotePilot, SystemRole.TechnicalWorker, SystemRole.SafetyOfficer, SystemRole.ControllerAuditor, SystemRole.ReadOnlyUser],
            [Permissions.DocumentsWrite] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.RemotePilot, SystemRole.TechnicalWorker, SystemRole.SafetyOfficer],
            [Permissions.DocumentsSensitiveRead] = [SystemRole.CentralGestor, SystemRole.SafetyOfficer, SystemRole.ControllerAuditor],
            [Permissions.ReportsRead] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.Approver, SystemRole.RemotePilot, SystemRole.TechnicalWorker, SystemRole.SafetyOfficer, SystemRole.ControllerAuditor, SystemRole.ReadOnlyUser],
            [Permissions.ReportsWrite] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator],
            [Permissions.ReportsOfficial] = [SystemRole.CentralGestor, SystemRole.Approver],
            [Permissions.AuditExport] = [SystemRole.CentralGestor, SystemRole.ControllerAuditor, SystemRole.SystemAdministrator],
            [Permissions.IntegrationsRead] = [SystemRole.CentralGestor, SystemRole.SystemAdministrator, SystemRole.ControllerAuditor],
            [Permissions.IntegrationsWrite] = [SystemRole.CentralGestor, SystemRole.SystemAdministrator],
            [Permissions.SystemInfoRead] = [SystemRole.CentralGestor, SystemRole.SystemAdministrator, SystemRole.ControllerAuditor, SystemRole.UnitAdministrator],
            [Permissions.ControlsRead] = [SystemRole.CentralGestor, SystemRole.ControllerAuditor, SystemRole.UnitAdministrator, SystemRole.ReadOnlyUser],
            [Permissions.ControlsWrite] = [SystemRole.CentralGestor, SystemRole.ControllerAuditor],
            [Permissions.ControlsClose] = [SystemRole.CentralGestor, SystemRole.ControllerAuditor],
            [Permissions.NotificationsRead] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.Approver, SystemRole.RemotePilot, SystemRole.TechnicalWorker, SystemRole.SafetyOfficer, SystemRole.ControllerAuditor, SystemRole.ReadOnlyUser],
            [Permissions.NotificationsWrite] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.SafetyOfficer],
            [Permissions.GeoZonesRead] = [SystemRole.CentralGestor, SystemRole.UnitAdministrator, SystemRole.Approver, SystemRole.RemotePilot, SystemRole.TechnicalWorker, SystemRole.SafetyOfficer, SystemRole.ControllerAuditor, SystemRole.ReadOnlyUser],
            [Permissions.GeoZonesWrite] = [SystemRole.CentralGestor],
            [Permissions.TrashManage] = [SystemRole.CentralGestor]
        };

    public static bool IsKnown(string permission) => RoleMap.ContainsKey(permission);

    public static bool IsGrantedByRoles(string permission, IEnumerable<string> roleCodes)
    {
        if (!RoleMap.TryGetValue(permission, out var allowed))
            return false;
        foreach (var role in roleCodes)
        {
            if (Enum.TryParse<SystemRole>(role, out var code) && allowed.Contains(code))
                return true;
        }
        return false;
    }
}
