using VMAzureApp.Models;
using Xunit;

namespace VMAzureApp.Tests;

public class VmSearchFilterTests
{
    private static VirtualMachineInfo Vm(
        string name = "web-01",
        string resourceGroup = "production",
        string location = "eastus",
        string size = "Standard_D2s_v5",
        string osType = "Linux",
        string powerStateDisplay = "VM running")
    {
        return new VirtualMachineInfo(
            resourceId: "/subscriptions/s/resourceGroups/" + resourceGroup + "/providers/Microsoft.Compute/virtualMachines/" + name,
            subscriptionId: "s",
            subscriptionName: "Sub",
            resourceGroupName: resourceGroup,
            name: name,
            location: location,
            size: size,
            osType: osType,
            computerName: name,
            priority: "Regular",
            diskSummary: "OS 30 GB",
            zones: "None",
            tags: "None",
            powerStateCode: "PowerState/running",
            powerStateDisplay: powerStateDisplay);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Matches_ReturnsTrue_WhenSearchIsEmpty(string? search)
    {
        Assert.True(VmSearchFilter.Matches(Vm(), search));
    }

    [Fact]
    public void Matches_ReturnsFalse_WhenVirtualMachineIsNull()
    {
        Assert.False(VmSearchFilter.Matches(null, "web"));
    }

    [Fact]
    public void Matches_ByName_CaseInsensitive()
    {
        Assert.True(VmSearchFilter.Matches(Vm(name: "Web-Server"), "web"));
    }

    [Fact]
    public void Matches_ByResourceGroup()
    {
        Assert.True(VmSearchFilter.Matches(Vm(resourceGroup: "staging-rg"), "staging"));
    }

    [Fact]
    public void Matches_ByStatus()
    {
        Assert.True(VmSearchFilter.Matches(Vm(powerStateDisplay: "VM deallocated"), "deallocated"));
    }

    [Fact]
    public void Matches_BySize()
    {
        Assert.True(VmSearchFilter.Matches(Vm(size: "Standard_B2ms"), "b2ms"));
    }

    [Fact]
    public void Matches_ReturnsFalse_WhenNothingContainsTheTerm()
    {
        Assert.False(VmSearchFilter.Matches(Vm(), "nonexistent-zzz"));
    }
}
