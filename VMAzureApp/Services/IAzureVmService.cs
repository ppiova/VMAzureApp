using VMAzureApp.Models;

namespace VMAzureApp.Services;

public interface IAzureVmService
{
    Task<IReadOnlyList<AzureSubscriptionInfo>> GetSubscriptionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VirtualMachineInfo>> GetVirtualMachinesAsync(
        AzureSubscriptionInfo subscription,
        CancellationToken cancellationToken = default);

    Task StartVirtualMachineAsync(VirtualMachineInfo virtualMachine, CancellationToken cancellationToken = default);

    Task DeallocateVirtualMachineAsync(VirtualMachineInfo virtualMachine, CancellationToken cancellationToken = default);
}
