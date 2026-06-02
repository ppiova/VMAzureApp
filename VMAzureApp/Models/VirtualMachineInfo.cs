using VMAzureApp.ViewModels;

namespace VMAzureApp.Models;

public sealed class VirtualMachineInfo : ObservableObject
{
    private bool _isOperationInProgress;
    private string _operationStatus = string.Empty;
    private string _powerStateCode;
    private string _powerStateDisplay;

    public VirtualMachineInfo(
        string resourceId,
        string subscriptionId,
        string subscriptionName,
        string resourceGroupName,
        string name,
        string location,
        string size,
        string powerStateCode,
        string powerStateDisplay)
    {
        ResourceId = resourceId;
        SubscriptionId = subscriptionId;
        SubscriptionName = subscriptionName;
        ResourceGroupName = resourceGroupName;
        Name = name;
        Location = location;
        Size = size;
        _powerStateCode = powerStateCode;
        _powerStateDisplay = powerStateDisplay;
    }

    public string ResourceId { get; }

    public string SubscriptionId { get; }

    public string SubscriptionName { get; }

    public string ResourceGroupName { get; }

    public string Name { get; }

    public string Location { get; }

    public string Size { get; }

    public string PowerStateCode
    {
        get => _powerStateCode;
        private set
        {
            if (SetProperty(ref _powerStateCode, value))
            {
                OnPropertyChanged(nameof(CanStart));
                OnPropertyChanged(nameof(CanStop));
            }
        }
    }

    public string PowerStateDisplay
    {
        get => _powerStateDisplay;
        private set => SetProperty(ref _powerStateDisplay, value);
    }

    public bool IsOperationInProgress
    {
        get => _isOperationInProgress;
        set
        {
            if (SetProperty(ref _isOperationInProgress, value))
            {
                OnPropertyChanged(nameof(CanStart));
                OnPropertyChanged(nameof(CanStop));
                OnPropertyChanged(nameof(DisplayStatus));
            }
        }
    }

    public string OperationStatus
    {
        get => _operationStatus;
        set
        {
            if (SetProperty(ref _operationStatus, value))
            {
                OnPropertyChanged(nameof(DisplayStatus));
            }
        }
    }

    public string DisplayStatus => IsOperationInProgress && !string.IsNullOrWhiteSpace(OperationStatus)
        ? OperationStatus
        : PowerStateDisplay;

    public bool CanStart => !IsOperationInProgress && !PowerStateCode.Equals("PowerState/running", StringComparison.OrdinalIgnoreCase);

    public bool CanStop => !IsOperationInProgress
        && !PowerStateCode.Equals("PowerState/deallocated", StringComparison.OrdinalIgnoreCase)
        && !PowerStateCode.Equals("PowerState/stopped", StringComparison.OrdinalIgnoreCase);

    public void UpdatePowerState(string code, string display)
    {
        PowerStateCode = code;
        PowerStateDisplay = display;
        OnPropertyChanged(nameof(DisplayStatus));
    }
}
