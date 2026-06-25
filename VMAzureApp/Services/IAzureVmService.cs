using VMAzureApp.Models;

namespace VMAzureApp.Services;

public interface IAzureVmService
{
    Task<IReadOnlyList<AzureSubscriptionInfo>> GetSubscriptionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VirtualMachineInfo>> GetVirtualMachinesAsync(
        AzureSubscriptionInfo subscription,
        CancellationToken cancellationToken = default);

    Task StartVirtualMachineAsync(VirtualMachineInfo virtualMachine, CancellationToken cancellationToken = default);

    Task StartVirtualMachineAsync(string resourceId, CancellationToken cancellationToken = default);

    Task DeallocateVirtualMachineAsync(VirtualMachineInfo virtualMachine, CancellationToken cancellationToken = default);

    Task DeallocateVirtualMachineAsync(string resourceId, CancellationToken cancellationToken = default);

    Task RestartVirtualMachineAsync(VirtualMachineInfo virtualMachine, CancellationToken cancellationToken = default);

    Task RestartVirtualMachineAsync(string resourceId, CancellationToken cancellationToken = default);

    Task HibernateVirtualMachineAsync(VirtualMachineInfo virtualMachine, CancellationToken cancellationToken = default);

    Task HibernateVirtualMachineAsync(string resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-queries the current power state of a single VM from Azure.
    /// </summary>
    Task<VmPowerState> GetPowerStateAsync(string resourceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The power state of a VM as reported by Azure: the raw code (for example
/// "PowerState/running") and a human-friendly display string.
/// </summary>
public readonly record struct VmPowerState(string Code, string Display);
