using IsSuas.Domain.Enums;
using IsSuas.Domain.Fleet;

namespace IsSuas.UnitTests;

public class UasStateMachineTests
{
    [Theory]
    [InlineData(UasLifecycleStatus.Draft, UasLifecycleStatus.Registered, true)]
    [InlineData(UasLifecycleStatus.Draft, UasLifecycleStatus.InService, false)]
    [InlineData(UasLifecycleStatus.Registered, UasLifecycleStatus.InService, true)]
    [InlineData(UasLifecycleStatus.Decommissioned, UasLifecycleStatus.InService, false)]
    public void CanTransition_RespectsRules(UasLifecycleStatus from, UasLifecycleStatus to, bool expected)
        => Assert.Equal(expected, UasStateMachine.CanTransition(from, to));
}
