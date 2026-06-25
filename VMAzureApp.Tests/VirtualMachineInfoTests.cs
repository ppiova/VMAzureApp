using VMAzureApp.Models;
using Xunit;

namespace VMAzureApp.Tests;

public class VirtualMachineInfoTests
{
    private static VirtualMachineInfo Vm(string powerStateCode, bool supportsHibernation = false)
    {
        return new VirtualMachineInfo(
            resourceId: "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm",
            subscriptionId: "s",
            subscriptionName: "Sub",
            resourceGroupName: "rg",
            name: "vm",
            location: "eastus",
            size: "Standard_D2s_v5",
            osType: "Linux",
            computerName: "vm",
            priority: "Regular",
            diskSummary: "OS 30 GB",
            zones: "None",
            tags: "None",
            powerStateCode: powerStateCode,
            powerStateDisplay: powerStateCode,
            supportsHibernation: supportsHibernation);
    }

    [Fact]
    public void RunningVm_CanStopRestartButNotStart()
    {
        VirtualMachineInfo vm = Vm("PowerState/running");

        Assert.False(vm.CanStart);
        Assert.True(vm.CanStop);
        Assert.True(vm.CanRestart);
    }

    [Fact]
    public void DeallocatedVm_CanStartButNotStopRestartOrHibernate()
    {
        VirtualMachineInfo vm = Vm("PowerState/deallocated", supportsHibernation: true);

        Assert.True(vm.CanStart);
        Assert.False(vm.CanStop);
        Assert.False(vm.CanRestart);
        Assert.False(vm.CanHibernate);
    }

    [Fact]
    public void CanHibernate_OnlyWhenRunningAndSupported()
    {
        Assert.True(Vm("PowerState/running", supportsHibernation: true).CanHibernate);
        Assert.False(Vm("PowerState/running", supportsHibernation: false).CanHibernate);
    }

    [Fact]
    public void OperationInProgress_DisablesAllActions()
    {
        VirtualMachineInfo vm = Vm("PowerState/running", supportsHibernation: true);
        vm.IsOperationInProgress = true;

        Assert.False(vm.CanStart);
        Assert.False(vm.CanStop);
        Assert.False(vm.CanRestart);
        Assert.False(vm.CanHibernate);
    }

    [Fact]
    public void UpdatePowerState_RecomputesCapabilities()
    {
        VirtualMachineInfo vm = Vm("PowerState/deallocated", supportsHibernation: true);
        Assert.True(vm.CanStart);

        vm.UpdatePowerState("PowerState/running", "VM running");

        Assert.False(vm.CanStart);
        Assert.True(vm.CanRestart);
        Assert.True(vm.CanHibernate);
    }
}
