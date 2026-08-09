using IsSuas.Domain.Enums;

namespace IsSuas.Domain.Fleet;

public static class UasStateMachine
{
    private static readonly Dictionary<UasLifecycleStatus, HashSet<UasLifecycleStatus>> Transitions = new()
    {
        [UasLifecycleStatus.Draft] = [UasLifecycleStatus.Registered, UasLifecycleStatus.Decommissioned],
        [UasLifecycleStatus.Registered] = [UasLifecycleStatus.InService, UasLifecycleStatus.InStorage, UasLifecycleStatus.Suspended, UasLifecycleStatus.Decommissioned],
        [UasLifecycleStatus.InService] = [UasLifecycleStatus.InStorage, UasLifecycleStatus.InMaintenance, UasLifecycleStatus.Suspended, UasLifecycleStatus.Damaged, UasLifecycleStatus.Lost, UasLifecycleStatus.Decommissioned],
        [UasLifecycleStatus.InStorage] = [UasLifecycleStatus.InService, UasLifecycleStatus.InMaintenance, UasLifecycleStatus.Suspended, UasLifecycleStatus.Decommissioned],
        [UasLifecycleStatus.InMaintenance] = [UasLifecycleStatus.InService, UasLifecycleStatus.InStorage, UasLifecycleStatus.Damaged, UasLifecycleStatus.Suspended, UasLifecycleStatus.Decommissioned],
        [UasLifecycleStatus.Suspended] = [UasLifecycleStatus.InService, UasLifecycleStatus.InStorage, UasLifecycleStatus.InMaintenance, UasLifecycleStatus.Decommissioned],
        [UasLifecycleStatus.Damaged] = [UasLifecycleStatus.InMaintenance, UasLifecycleStatus.Suspended, UasLifecycleStatus.Decommissioned, UasLifecycleStatus.Lost],
        [UasLifecycleStatus.Lost] = [UasLifecycleStatus.Decommissioned],
        [UasLifecycleStatus.Decommissioned] = []
    };

    public static bool CanTransition(UasLifecycleStatus from, UasLifecycleStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static bool IsUsableInOperations(UasLifecycleStatus status) =>
        status is UasLifecycleStatus.InService or UasLifecycleStatus.Registered or UasLifecycleStatus.InStorage;
}
