using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Resources;
using VMAzureApp.Models;

namespace VMAzureApp.Services;

public sealed class AzureVmService : IAzureVmService
{
    // How many per-VM instance-view (power state) requests to run at once.
    // Bounded so large subscriptions don't flood ARM, but high enough to be
    // far faster than the previous one-at-a-time approach.
    private const int MaxConcurrentInstanceViewQueries = 8;

    private readonly TokenCredential _credential;
    private ArmClient? _client;

    public AzureVmService()
        : this(new InteractiveBrowserCredential())
    {
    }

    public AzureVmService(TokenCredential credential)
    {
        _credential = credential;
    }

    private ArmClient Client => _client ??= new ArmClient(_credential);

    public async Task<IReadOnlyList<AzureSubscriptionInfo>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        List<AzureSubscriptionInfo> subscriptions = [];

        await foreach (SubscriptionResource subscription in Client.GetSubscriptions().GetAllAsync(cancellationToken))
        {
            string id = subscription.Data.SubscriptionId ?? subscription.Id.SubscriptionId ?? subscription.Id.ToString();
            string name = string.IsNullOrWhiteSpace(subscription.Data.DisplayName)
                ? id
                : subscription.Data.DisplayName;

            subscriptions.Add(new AzureSubscriptionInfo(id, name));
        }

        return subscriptions
            .OrderBy(subscription => subscription.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<VirtualMachineInfo>> GetVirtualMachinesAsync(
        AzureSubscriptionInfo subscription,
        CancellationToken cancellationToken = default)
    {
        SubscriptionResource subscriptionResource = Client.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(subscription.Id));

        // Enumerate every VM in the subscription in a single paged call instead
        // of walking each resource group separately.
        List<VirtualMachineResource> resources = [];
        await foreach (VirtualMachineResource virtualMachine in
            subscriptionResource.GetVirtualMachinesAsync(cancellationToken: cancellationToken))
        {
            resources.Add(virtualMachine);
        }

        // Fetch power state for each VM concurrently rather than one-at-a-time.
        IReadOnlyList<VirtualMachineInfo> virtualMachines = await ConcurrentTasks.MapAsync(
            resources,
            MaxConcurrentInstanceViewQueries,
            async (virtualMachine, token) =>
            {
                VmPowerState powerState = await GetPowerStateAsync(virtualMachine, token);
                return MapToInfo(virtualMachine, subscription, powerState);
            },
            cancellationToken);

        return virtualMachines
            .OrderBy(vm => vm.ResourceGroupName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(vm => vm.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static VirtualMachineInfo MapToInfo(
        VirtualMachineResource virtualMachine,
        AzureSubscriptionInfo subscription,
        VmPowerState powerState)
    {
        return new VirtualMachineInfo(
            virtualMachine.Id.ToString(),
            subscription.Id,
            subscription.Name,
            virtualMachine.Id.ResourceGroupName ?? "N/A",
            virtualMachine.Data.Name,
            virtualMachine.Data.Location.Name,
            virtualMachine.Data.HardwareProfile?.VmSize?.ToString() ?? "N/A",
            virtualMachine.Data.StorageProfile?.OSDisk?.OSType?.ToString() ?? "N/A",
            virtualMachine.Data.OSProfile?.ComputerName ?? "N/A",
            virtualMachine.Data.Priority?.ToString() ?? "Regular",
            GetDiskSummary(virtualMachine),
            GetZonesSummary(virtualMachine),
            GetTagsSummary(virtualMachine),
            powerState.Code,
            powerState.Display,
            virtualMachine.Data.AdditionalCapabilities?.HibernationEnabled ?? false);
    }

    public async Task StartVirtualMachineAsync(VirtualMachineInfo virtualMachine, CancellationToken cancellationToken = default)
    {
        await StartVirtualMachineAsync(virtualMachine.ResourceId, cancellationToken);
    }

    public async Task StartVirtualMachineAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        VirtualMachineResource resource = Client.GetVirtualMachineResource(new ResourceIdentifier(resourceId));
        await resource.PowerOnAsync(WaitUntil.Completed, cancellationToken);
    }

    public async Task DeallocateVirtualMachineAsync(VirtualMachineInfo virtualMachine, CancellationToken cancellationToken = default)
    {
        await DeallocateVirtualMachineAsync(virtualMachine.ResourceId, cancellationToken);
    }

    public async Task DeallocateVirtualMachineAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        VirtualMachineResource resource = Client.GetVirtualMachineResource(new ResourceIdentifier(resourceId));
        await resource.DeallocateAsync(WaitUntil.Completed, cancellationToken: cancellationToken);
    }

    public async Task RestartVirtualMachineAsync(VirtualMachineInfo virtualMachine, CancellationToken cancellationToken = default)
    {
        await RestartVirtualMachineAsync(virtualMachine.ResourceId, cancellationToken);
    }

    public async Task RestartVirtualMachineAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        VirtualMachineResource resource = Client.GetVirtualMachineResource(new ResourceIdentifier(resourceId));
        await resource.RestartAsync(WaitUntil.Completed, cancellationToken);
    }

    public async Task HibernateVirtualMachineAsync(VirtualMachineInfo virtualMachine, CancellationToken cancellationToken = default)
    {
        await HibernateVirtualMachineAsync(virtualMachine.ResourceId, cancellationToken);
    }

    public async Task HibernateVirtualMachineAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        VirtualMachineResource resource = Client.GetVirtualMachineResource(new ResourceIdentifier(resourceId));
        await resource.DeallocateAsync(WaitUntil.Completed, hibernate: true, cancellationToken: cancellationToken);
    }

    public async Task<VmPowerState> GetPowerStateAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        VirtualMachineResource resource = Client.GetVirtualMachineResource(new ResourceIdentifier(resourceId));
        return await GetPowerStateAsync(resource, cancellationToken);
    }

    private static string GetDiskSummary(VirtualMachineResource virtualMachine)
    {
        string osDisk = virtualMachine.Data.StorageProfile?.OSDisk?.DiskSizeGB is int osDiskSize
            ? $"OS {osDiskSize} GB"
            : "OS disk";
        int dataDiskCount = virtualMachine.Data.StorageProfile?.DataDisks.Count ?? 0;

        return dataDiskCount == 0
            ? osDisk
            : $"{osDisk}, {dataDiskCount} data disk(s)";
    }

    private static string GetTagsSummary(VirtualMachineResource virtualMachine)
    {
        if (virtualMachine.Data.Tags.Count == 0)
        {
            return "None";
        }

        return string.Join(", ", virtualMachine.Data.Tags.Select(tag => $"{tag.Key}={tag.Value}"));
    }

    private static string GetZonesSummary(VirtualMachineResource virtualMachine)
    {
        return virtualMachine.Data.Zones.Count == 0
            ? "None"
            : string.Join(", ", virtualMachine.Data.Zones);
    }

    private static async Task<VmPowerState> GetPowerStateAsync(
        VirtualMachineResource virtualMachine,
        CancellationToken cancellationToken)
    {
        try
        {
            Response<Azure.ResourceManager.Compute.Models.VirtualMachineInstanceView> instanceView =
                await virtualMachine.InstanceViewAsync(cancellationToken);

            Azure.ResourceManager.Compute.Models.InstanceViewStatus? powerState = instanceView.Value.Statuses
                .FirstOrDefault(status => status.Code is not null
                    && status.Code.StartsWith("PowerState/", StringComparison.OrdinalIgnoreCase));

            return powerState is null
                ? new VmPowerState("PowerState/unknown", "Unknown state")
                : new VmPowerState(powerState.Code ?? "PowerState/unknown", powerState.DisplayStatus ?? powerState.Code ?? "Unknown state");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RequestFailedException)
        {
            // A single VM failing to report its status should not break the
            // whole listing; show it as unknown instead.
            return new VmPowerState("PowerState/unknown", "Unknown state");
        }
    }
}
