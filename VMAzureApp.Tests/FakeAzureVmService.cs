using VMAzureApp.Models;
using VMAzureApp.Services;

namespace VMAzureApp.Tests;

/// <summary>
/// In-memory <see cref="IAzureVmService"/> for view-model tests. Every method
/// returns an already-completed task so awaiting continuations run inline on
/// the calling thread, keeping the WPF CollectionView single-threaded.
/// </summary>
internal sealed class FakeAzureVmService : IAzureVmService
{
    private readonly IReadOnlyList<AzureSubscriptionInfo> _subscriptions;
    private readonly IReadOnlyList<VirtualMachineInfo> _virtualMachines;

    public FakeAzureVmService(
        IReadOnlyList<AzureSubscriptionInfo> subscriptions,
        IReadOnlyList<VirtualMachineInfo> virtualMachines)
    {
        _subscriptions = subscriptions;
        _virtualMachines = virtualMachines;
    }

    public Task<IReadOnlyList<AzureSubscriptionInfo>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_subscriptions);

    public Task<IReadOnlyList<VirtualMachineInfo>> GetVirtualMachinesAsync(AzureSubscriptionInfo subscription, CancellationToken cancellationToken = default)
        => Task.FromResult(_virtualMachines);

    public Task StartVirtualMachineAsync(VirtualMachineInfo virtualMachine, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartVirtualMachineAsync(string resourceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeallocateVirtualMachineAsync(VirtualMachineInfo virtualMachine, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeallocateVirtualMachineAsync(string resourceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RestartVirtualMachineAsync(VirtualMachineInfo virtualMachine, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RestartVirtualMachineAsync(string resourceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task HibernateVirtualMachineAsync(VirtualMachineInfo virtualMachine, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task HibernateVirtualMachineAsync(string resourceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<VmPowerState> GetPowerStateAsync(string resourceId, CancellationToken cancellationToken = default)
        => Task.FromResult(new VmPowerState("PowerState/running", "VM running"));
}
