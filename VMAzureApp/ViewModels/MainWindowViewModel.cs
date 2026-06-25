using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using VMAzureApp.Models;
using VMAzureApp.Services;

namespace VMAzureApp.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IAzureVmService _azureVmService;
    private readonly SemaphoreSlim _scheduleSemaphore = new(1, 1);
    private readonly VmScheduleStore _scheduleStore = new();
    private readonly DispatcherTimer _scheduleTimer;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private ScheduleActionOption _newScheduleAction;
    private bool _newScheduleFriday = true;
    private bool _newScheduleMonday = true;
    private bool _newScheduleSaturday;
    private string _newScheduleTime = "18:00";
    private bool _newScheduleSunday;
    private bool _newScheduleThursday = true;
    private bool _newScheduleTuesday = true;
    private bool _newScheduleWednesday = true;
    private AzureSubscriptionInfo? _selectedSubscription;
    private VirtualMachineInfo? _selectedVirtualMachine;
    private string _statusMessage = "Ready.";

    public MainWindowViewModel(IAzureVmService azureVmService)
    {
        _azureVmService = azureVmService;
        _newScheduleAction = ScheduleActions[1];
        LoadSubscriptionsCommand = new AsyncRelayCommand(LoadSubscriptionsAsync, () => !IsBusy);
        RefreshCommand = new AsyncRelayCommand(RefreshVirtualMachinesAsync, () => !IsBusy && SelectedSubscription is not null);
        StartVmCommand = new AsyncRelayCommand(StartVirtualMachineAsync, parameter => CanStartVirtualMachine(parameter));
        StopVmCommand = new AsyncRelayCommand(StopVirtualMachineAsync, parameter => CanStopVirtualMachine(parameter));
        AddScheduleCommand = new AsyncRelayCommand(AddScheduleAsync, () => SelectedVirtualMachine is not null);
        DeleteScheduleCommand = new AsyncRelayCommand(DeleteScheduleAsync, parameter => parameter is VmSchedule);
        RunScheduleNowCommand = new AsyncRelayCommand(RunScheduleNowAsync, parameter => parameter is VmSchedule schedule && schedule.Enabled);

        Schedules.CollectionChanged += OnSchedulesChanged;
        foreach (VmSchedule schedule in _scheduleStore.Load())
        {
            TrackSchedule(schedule);
            Schedules.Add(schedule);
        }

        _scheduleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _scheduleTimer.Tick += async (_, _) => await RunDueSchedulesAsync();
        _scheduleTimer.Start();
    }

    public ObservableCollection<AzureSubscriptionInfo> Subscriptions { get; } = [];

    public ObservableCollection<VirtualMachineInfo> VirtualMachines { get; } = [];

    public ObservableCollection<VmSchedule> Schedules { get; } = [];

    public IReadOnlyList<ScheduleActionOption> ScheduleActions { get; } =
    [
        new(VmScheduleAction.Start, "Start"),
        new(VmScheduleAction.StopDeallocate, "Stop / Deallocate")
    ];

    public AsyncRelayCommand LoadSubscriptionsCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand StartVmCommand { get; }

    public AsyncRelayCommand StopVmCommand { get; }

    public AsyncRelayCommand AddScheduleCommand { get; }

    public AsyncRelayCommand DeleteScheduleCommand { get; }

    public AsyncRelayCommand RunScheduleNowCommand { get; }

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

    public VirtualMachineInfo? SelectedVirtualMachine
    {
        get => _selectedVirtualMachine;
        set
        {
            if (SetProperty(ref _selectedVirtualMachine, value))
            {
                AddScheduleCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(HasSelectedVirtualMachine));
            }
        }
    }

    public bool HasSelectedVirtualMachine => SelectedVirtualMachine is not null;

    public ScheduleActionOption NewScheduleAction
    {
        get => _newScheduleAction;
        set => SetProperty(ref _newScheduleAction, value);
    }

    public string NewScheduleTime
    {
        get => _newScheduleTime;
        set => SetProperty(ref _newScheduleTime, value);
    }

    public bool NewScheduleMonday
    {
        get => _newScheduleMonday;
        set => SetProperty(ref _newScheduleMonday, value);
    }

    public bool NewScheduleTuesday
    {
        get => _newScheduleTuesday;
        set => SetProperty(ref _newScheduleTuesday, value);
    }

    public bool NewScheduleWednesday
    {
        get => _newScheduleWednesday;
        set => SetProperty(ref _newScheduleWednesday, value);
    }

    public bool NewScheduleThursday
    {
        get => _newScheduleThursday;
        set => SetProperty(ref _newScheduleThursday, value);
    }

    public bool NewScheduleFriday
    {
        get => _newScheduleFriday;
        set => SetProperty(ref _newScheduleFriday, value);
    }

    public bool NewScheduleSaturday
    {
        get => _newScheduleSaturday;
        set => SetProperty(ref _newScheduleSaturday, value);
    }

    public bool NewScheduleSunday
    {
        get => _newScheduleSunday;
        set => SetProperty(ref _newScheduleSunday, value);
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
            SelectedVirtualMachine = null;
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
        SelectedVirtualMachine = null;
        RaiseDashboardCountsChanged();

        IReadOnlyList<VirtualMachineInfo> virtualMachines = await _azureVmService.GetVirtualMachinesAsync(SelectedSubscription);

        foreach (VirtualMachineInfo virtualMachine in virtualMachines)
        {
            VirtualMachines.Add(virtualMachine);
        }

        SelectedVirtualMachine = VirtualMachines.FirstOrDefault();
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

    private async Task AddScheduleAsync()
    {
        if (SelectedVirtualMachine is null)
        {
            return;
        }

        if (!TimeOnly.TryParse(NewScheduleTime, out TimeOnly parsedTime))
        {
            ErrorMessage = "Use a valid schedule time, for example 18:00.";
            StatusMessage = "Schedule was not added.";
            return;
        }

        if (!NewScheduleMonday && !NewScheduleTuesday && !NewScheduleWednesday && !NewScheduleThursday
            && !NewScheduleFriday && !NewScheduleSaturday && !NewScheduleSunday)
        {
            ErrorMessage = "Select at least one day for the schedule.";
            StatusMessage = "Schedule was not added.";
            return;
        }

        VmSchedule schedule = new()
        {
            ResourceId = SelectedVirtualMachine.ResourceId,
            VmName = SelectedVirtualMachine.Name,
            SubscriptionName = SelectedVirtualMachine.SubscriptionName,
            ResourceGroupName = SelectedVirtualMachine.ResourceGroupName,
            Action = NewScheduleAction.Action,
            ScheduledTime = parsedTime.ToString("HH:mm"),
            Monday = NewScheduleMonday,
            Tuesday = NewScheduleTuesday,
            Wednesday = NewScheduleWednesday,
            Thursday = NewScheduleThursday,
            Friday = NewScheduleFriday,
            Saturday = NewScheduleSaturday,
            Sunday = NewScheduleSunday,
            LastResult = "Waiting"
        };

        TrackSchedule(schedule);
        Schedules.Add(schedule);
        SaveSchedules();
        ErrorMessage = string.Empty;
        StatusMessage = $"Added schedule: {schedule.Summary}.";
        await Task.CompletedTask;
    }

    private async Task DeleteScheduleAsync(object? parameter)
    {
        if (parameter is not VmSchedule schedule)
        {
            return;
        }

        UntrackSchedule(schedule);
        Schedules.Remove(schedule);
        SaveSchedules();
        StatusMessage = $"Deleted schedule for {schedule.VmName}.";
        await Task.CompletedTask;
    }

    private async Task RunScheduleNowAsync(object? parameter)
    {
        if (parameter is not VmSchedule schedule)
        {
            return;
        }

        await ExecuteScheduleAsync(schedule, "Manual schedule run");
    }

    private async Task RunDueSchedulesAsync()
    {
        if (!await _scheduleSemaphore.WaitAsync(0))
        {
            return;
        }

        try
        {
            DateTime now = DateTime.Now;
            foreach (VmSchedule schedule in Schedules.Where(schedule => schedule.IsDue(now)).ToList())
            {
                await ExecuteScheduleAsync(schedule, "Scheduled run");
            }
        }
        finally
        {
            _scheduleSemaphore.Release();
        }
    }

    private async Task ExecuteScheduleAsync(VmSchedule schedule, string source)
    {
        schedule.LastRunLocal = DateTime.Now;
        schedule.LastResult = $"{source}: running";
        SaveSchedules();

        VirtualMachineInfo? loadedVm = VirtualMachines.FirstOrDefault(vm =>
            vm.ResourceId.Equals(schedule.ResourceId, StringComparison.OrdinalIgnoreCase));

        try
        {
            if (loadedVm is not null)
            {
                loadedVm.IsOperationInProgress = true;
                loadedVm.OperationStatus = schedule.Action == VmScheduleAction.Start ? "Starting..." : "Stopping...";
            }

            if (schedule.Action == VmScheduleAction.Start)
            {
                await _azureVmService.StartVirtualMachineAsync(schedule.ResourceId);
                loadedVm?.UpdatePowerState("PowerState/running", "VM running");
            }
            else
            {
                await _azureVmService.DeallocateVirtualMachineAsync(schedule.ResourceId);
                loadedVm?.UpdatePowerState("PowerState/deallocated", "VM deallocated");
            }

            schedule.LastResult = $"{source}: success";
            StatusMessage = $"{source} completed: {schedule.Summary}.";
            ErrorMessage = string.Empty;
            RaiseDashboardCountsChanged();
        }
        catch (Exception ex)
        {
            schedule.LastResult = $"{source}: failed - {ex.Message}";
            ErrorMessage = ex.Message;
            StatusMessage = $"{source} failed for {schedule.VmName}.";
        }
        finally
        {
            if (loadedVm is not null)
            {
                loadedVm.IsOperationInProgress = false;
                loadedVm.OperationStatus = string.Empty;
            }

            SaveSchedules();
            RaiseCommandStatesChanged();
        }
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
        AddScheduleCommand.RaiseCanExecuteChanged();
        DeleteScheduleCommand.RaiseCanExecuteChanged();
        RunScheduleNowCommand.RaiseCanExecuteChanged();
    }

    private void RaiseDashboardCountsChanged()
    {
        OnPropertyChanged(nameof(TotalVmCount));
        OnPropertyChanged(nameof(RunningVmCount));
        OnPropertyChanged(nameof(StoppedVmCount));
    }

    private void OnSchedulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (VmSchedule schedule in e.NewItems.OfType<VmSchedule>())
            {
                TrackSchedule(schedule);
            }
        }

        if (e.OldItems is not null)
        {
            foreach (VmSchedule schedule in e.OldItems.OfType<VmSchedule>())
            {
                UntrackSchedule(schedule);
            }
        }

        SaveSchedules();
    }

    private void TrackSchedule(VmSchedule schedule)
    {
        schedule.PropertyChanged -= OnSchedulePropertyChanged;
        schedule.PropertyChanged += OnSchedulePropertyChanged;
    }

    private void UntrackSchedule(VmSchedule schedule)
    {
        schedule.PropertyChanged -= OnSchedulePropertyChanged;
    }

    private void OnSchedulePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        SaveSchedules();
    }

    private void SaveSchedules()
    {
        _scheduleStore.Save(Schedules);
    }
}
