using System.Linq;
using VMAzureApp.Models;
using VMAzureApp.ViewModels;
using Xunit;

namespace VMAzureApp.Tests;

public class MainWindowViewModelTests
{
    private static VirtualMachineInfo Vm(string name, string resourceGroup, string powerStateCode = "PowerState/running")
    {
        return new VirtualMachineInfo(
            resourceId: $"/subscriptions/s/resourceGroups/{resourceGroup}/providers/Microsoft.Compute/virtualMachines/{name}",
            subscriptionId: "s",
            subscriptionName: "Sub",
            resourceGroupName: resourceGroup,
            name: name,
            location: "eastus",
            size: "Standard_D2s_v5",
            osType: "Linux",
            computerName: name,
            priority: "Regular",
            diskSummary: "OS 30 GB",
            zones: "None",
            tags: "None",
            powerStateCode: powerStateCode,
            powerStateDisplay: powerStateCode);
    }

    private static MainWindowViewModel CreateLoadedViewModel(params VirtualMachineInfo[] vms)
    {
        FakeAzureVmService service = new(
            new[] { new AzureSubscriptionInfo("s", "Sub") },
            vms);

        MainWindowViewModel viewModel = new(service);
        viewModel.LoadSubscriptionsAsync().GetAwaiter().GetResult();
        return viewModel;
    }

    private static int ViewCount(MainWindowViewModel viewModel)
        => viewModel.VirtualMachinesView.Cast<object>().Count();

    [Fact]
    public void Load_PopulatesVirtualMachinesAndCounts()
    {
        MainWindowViewModel viewModel = CreateLoadedViewModel(
            Vm("web-01", "prod"),
            Vm("db-01", "prod", "PowerState/deallocated"),
            Vm("cache-01", "staging"));

        Assert.Equal(3, viewModel.TotalVmCount);
        Assert.Equal(2, viewModel.RunningVmCount);
        Assert.Equal(1, viewModel.StoppedVmCount);
        Assert.Equal(3, ViewCount(viewModel));
    }

    [Fact]
    public void SearchText_FiltersTheView()
    {
        MainWindowViewModel viewModel = CreateLoadedViewModel(
            Vm("web-01", "prod"),
            Vm("db-01", "prod"),
            Vm("cache-01", "staging"));

        viewModel.SearchText = "staging";
        Assert.Equal(1, ViewCount(viewModel));

        viewModel.SearchText = "prod";
        Assert.Equal(2, ViewCount(viewModel));

        viewModel.SearchText = string.Empty;
        Assert.Equal(3, ViewCount(viewModel));
    }

    [Fact]
    public void Selection_DrivesBulkCommandCounts()
    {
        MainWindowViewModel viewModel = CreateLoadedViewModel(
            Vm("web-01", "prod", "PowerState/running"),
            Vm("db-01", "prod", "PowerState/deallocated"));

        viewModel.SetSelectedVirtualMachines(viewModel.VirtualMachines);

        Assert.Equal(2, viewModel.SelectedCount);
        Assert.Equal(1, viewModel.SelectedStartableCount); // only the deallocated one
        Assert.Equal(1, viewModel.SelectedStoppableCount); // only the running one
    }
}
