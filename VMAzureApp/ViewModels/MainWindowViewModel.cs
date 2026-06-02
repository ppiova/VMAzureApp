using System.Collections.ObjectModel;
using System.Windows;
using VMAzureApp.Models;
using VMAzureApp.Services;

namespace VMAzureApp.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IAzureVmService _azureVmService;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private AzureSubscriptionInfo? _selectedSubscription;
    private string _statusMessage = "Ready.";

    public MainWindowViewModel(IAzureVmService azureVmService)
    {
        _azureVmService = azureVmService;
        LoadSubscriptionsCommand = new AsyncRelayCommand(LoadSubscriptionsAsync, () => !IsBusy);
        RefreshCommand = new AsyncRelayCommand(RefreshVirtualMachinesAsync, () => !IsBusy && SelectedSubscription is not null);
        StartVmCommand = new AsyncRelayCommand(StartVirtualMachineAsync, parameter => CanStartVirtualMachine(parameter));
        StopVmCommand = new AsyncRelayCommand(StopVirtualMachineAsync, parameter => CanStopVirtualMachine(parameter));
    }

    public ObservableCollection<AzureSubscriptionInfo> Subscriptions { get; } = [];

    public ObservableCollection<VirtualMachineInfo> VirtualMachines { get; } = [];

    public AsyncRelayCommand LoadSubscriptionsCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand StartVmCommand { get; }

    public AsyncRelayCommand StopVmCommand { get; }

    public AzureSubscriptionInfo? SelectedSubscription
    {
        get => _selectedSubscription;
        set
        {
            if (SetProperty(ref _selectedSubscription, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStatesChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public int TotalVmCount => VirtualMachines.Count;

    public int RunningVmCount => VirtualMachines.Count(vm =>
        vm.PowerStateCode.Equals("PowerState/running", StringComparison.OrdinalIgnoreCase));

    public int StoppedVmCount => VirtualMachines.Count(vm =>
        vm.PowerStateCode.Equals("PowerState/deallocated", StringComparison.OrdinalIgnoreCase)
        || vm.PowerStateCode.Equals("PowerState/stopped", StringComparison.OrdinalIgnoreCase));

    public async Task LoadSubscriptionsAsync()
    {
        await RunBusyAsync(async () =>
        {
            ErrorMessage = string.Empty;
            StatusMessage = "Signing in to Azure...";
            Subscriptions.Clear();
            VirtualMachines.Clear();
            RaiseDashboardCountsChanged();

            IReadOnlyList<AzureSubscriptionInfo> subscriptions = await _azureVmService.GetSubscriptionsAsync();

            foreach (AzureSubscriptionInfo subscription in subscriptions)
            {
                Subscriptions.Add(subscription);
            }

            SelectedSubscription = Subscriptions.FirstOrDefault();

            if (SelectedSubscription is null)
            {
                StatusMessage = "No accessible subscriptions were found for this user.";
                return;
            }

            await LoadVirtualMachinesCoreAsync();
        });
    }

    private async Task RefreshVirtualMachinesAsync()
    {
        await RunBusyAsync(async () =>
        {
            ErrorMessage = string.Empty;
            await LoadVirtualMachinesCoreAsync();
        });
    }

    private async Task LoadVirtualMachinesCoreAsync()
    {
        if (SelectedSubscription is null)
        {
            return;
        }

        StatusMessage = $"Loading VMs from {SelectedSubscription.Name}...";
        VirtualMachines.Clear();
        RaiseDashboardCountsChanged();

        IReadOnlyList<VirtualMachineInfo> virtualMachines = await _azureVmService.GetVirtualMachinesAsync(SelectedSubscription);

        foreach (VirtualMachineInfo virtualMachine in virtualMachines)
        {
            VirtualMachines.Add(virtualMachine);
        }

        RaiseDashboardCountsChanged();

        StatusMessage = virtualMachines.Count == 0
            ? $"No VMs found in {SelectedSubscription.Name}."
            : $"Loaded {virtualMachines.Count} VMs from {SelectedSubscription.Name}.";
    }

    private async Task StartVirtualMachineAsync(object? parameter)
    {
        if (parameter is not VirtualMachineInfo virtualMachine)
        {
            return;
        }

        await RunVmOperationAsync(virtualMachine, "Starting...", async () =>
        {
            await _azureVmService.StartVirtualMachineAsync(virtualMachine);
            virtualMachine.UpdatePowerState("PowerState/running", "VM running");
            RaiseDashboardCountsChanged();
            StatusMessage = $"{virtualMachine.Name} is running.";
        });
    }

    private async Task StopVirtualMachineAsync(object? parameter)
    {
        if (parameter is not VirtualMachineInfo virtualMachine)
        {
            return;
        }

        MessageBoxResult confirmation = System.Windows.MessageBox.Show(
            $"Stop and deallocate VM '{virtualMachine.Name}'?",
            "Confirm stop",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await RunVmOperationAsync(virtualMachine, "Stopping...", async () =>
        {
            await _azureVmService.DeallocateVirtualMachineAsync(virtualMachine);
            virtualMachine.UpdatePowerState("PowerState/deallocated", "VM deallocated");
            RaiseDashboardCountsChanged();
            StatusMessage = $"{virtualMachine.Name} is stopped and deallocated.";
        });
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        try
        {
            IsBusy = true;
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "The operation failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunVmOperationAsync(VirtualMachineInfo virtualMachine, string operationStatus, Func<Task> operation)
    {
        try
        {
            ErrorMessage = string.Empty;
            virtualMachine.OperationStatus = operationStatus;
            virtualMachine.IsOperationInProgress = true;
            StatusMessage = $"{operationStatus.TrimEnd('.')} {virtualMachine.Name}...";
            RaiseCommandStatesChanged();

            await operation();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = $"Could not complete the operation for {virtualMachine.Name}.";
        }
        finally
        {
            virtualMachine.IsOperationInProgress = false;
            virtualMachine.OperationStatus = string.Empty;
            RaiseCommandStatesChanged();
        }
    }

    private bool CanStartVirtualMachine(object? parameter)
    {
        return !IsBusy && parameter is VirtualMachineInfo virtualMachine && virtualMachine.CanStart;
    }

    private bool CanStopVirtualMachine(object? parameter)
    {
        return !IsBusy && parameter is VirtualMachineInfo virtualMachine && virtualMachine.CanStop;
    }

    private void RaiseCommandStatesChanged()
    {
        LoadSubscriptionsCommand.RaiseCanExecuteChanged();
        RefreshCommand.RaiseCanExecuteChanged();
        StartVmCommand.RaiseCanExecuteChanged();
        StopVmCommand.RaiseCanExecuteChanged();
    }

    private void RaiseDashboardCountsChanged()
    {
        OnPropertyChanged(nameof(TotalVmCount));
        OnPropertyChanged(nameof(RunningVmCount));
        OnPropertyChanged(nameof(StoppedVmCount));
    }
}
