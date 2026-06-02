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
        List<VirtualMachineInfo> virtualMachines = [];
        SubscriptionResource subscriptionResource = Client.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(subscription.Id));

        await foreach (ResourceGroupResource resourceGroup in subscriptionResource.GetResourceGroups().GetAllAsync(cancellationToken: cancellationToken))
        {
            await foreach (VirtualMachineResource virtualMachine in resourceGroup.GetVirtualMachines().GetAllAsync(cancellationToken: cancellationToken))
            {
                (string powerStateCode, string powerStateDisplay) = await GetPowerStateAsync(virtualMachine, cancellationToken);

                virtualMachines.Add(new VirtualMachineInfo(
                    virtualMachine.Id.ToString(),
                    subscription.Id,
                    subscription.Name,
                    virtualMachine.Id.ResourceGroupName ?? resourceGroup.Data.Name,
                    virtualMachine.Data.Name,
                    virtualMachine.Data.Location.Name,
                    virtualMachine.Data.HardwareProfile?.VmSize?.ToString() ?? "N/A",
                    powerStateCode,
                    powerStateDisplay));
            }
        }

        return virtualMachines
            .OrderBy(vm => vm.ResourceGroupName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(vm => vm.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task StartVirtualMachineAsync(VirtualMachineInfo virtualMachine, CancellationToken cancellationToken = default)
    {
        VirtualMachineResource resource = Client.GetVirtualMachineResource(new ResourceIdentifier(virtualMachine.ResourceId));
        await resource.PowerOnAsync(WaitUntil.Completed, cancellationToken);
    }

    public async Task DeallocateVirtualMachineAsync(VirtualMachineInfo virtualMachine, CancellationToken cancellationToken = default)
    {
        VirtualMachineResource resource = Client.GetVirtualMachineResource(new ResourceIdentifier(virtualMachine.ResourceId));
        await resource.DeallocateAsync(WaitUntil.Completed, cancellationToken: cancellationToken);
    }

    private static async Task<(string Code, string Display)> GetPowerStateAsync(
        VirtualMachineResource virtualMachine,
        CancellationToken cancellationToken)
    {
        Response<Azure.ResourceManager.Compute.Models.VirtualMachineInstanceView> instanceView =
            await virtualMachine.InstanceViewAsync(cancellationToken);

        Azure.ResourceManager.Compute.Models.InstanceViewStatus? powerState = instanceView.Value.Statuses
            .FirstOrDefault(status => status.Code is not null
                && status.Code.StartsWith("PowerState/", StringComparison.OrdinalIgnoreCase));

        return powerState is null
            ? ("PowerState/unknown", "Unknown state")
            : (powerState.Code ?? "PowerState/unknown", powerState.DisplayStatus ?? powerState.Code ?? "Unknown state");
    }
}
