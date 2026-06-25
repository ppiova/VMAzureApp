using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using VMAzureApp.Models;
using VMAzureApp.Services;

namespace VMAzureApp.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    // Upper bound on concurrent VM operations during bulk actions and status
    // refreshes, so large selections don't flood ARM.
    private const int MaxConcurrentBulkOperations = 6;

    private readonly IAzureVmService _azureVmService;
    private readonly SemaphoreSlim _scheduleSemaphore = new(1, 1);
    private readonly VmScheduleStore _scheduleStore = new();
    private readonly DispatcherTimer _scheduleTimer;
    private readonly DispatcherTimer _statusRefreshTimer;
    private IReadOnlyList<VirtualMachineInfo> _selectedVirtualMachines = [];
    private string _searchText = string.Empty;
    private bool _autoRefreshStatuses;
    private bool _suppressSubscriptionAutoLoad;
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
        RefreshStatusesCommand = new AsyncRelayCommand(RefreshAllStatusesAsync, () => !IsBusy && VirtualMachines.Count > 0);
        StartVmCommand = new AsyncRelayCommand(StartVirtualMachineAsync, parameter => CanStartVirtualMachine(parameter));
        StopVmCommand = new AsyncRelayCommand(StopVirtualMachineAsync, parameter => CanStopVirtualMachine(parameter));
        RestartVmCommand = new AsyncRelayCommand(RestartVirtualMachineAsync, parameter => CanRestartVirtualMachine(parameter));
        HibernateVmCommand = new AsyncRelayCommand(HibernateVirtualMachineAsync, parameter => CanHibernateVirtualMachine(parameter));
        StartSelectedCommand = new AsyncRelayCommand(StartSelectedAsync, () => !IsBusy && SelectedStartableCount > 0);
        StopSelectedCommand = new AsyncRelayCommand(StopSelectedAsync, () => !IsBusy && SelectedStoppableCount > 0);
        AddScheduleCommand = new AsyncRelayCommand(AddScheduleAsync, () => SelectedVirtualMachine is not null);
        DeleteScheduleCommand = new AsyncRelayCommand(DeleteScheduleAsync, parameter => parameter is VmSchedule);
        RunScheduleNowCommand = new AsyncRelayCommand(RunScheduleNowAsync, parameter => parameter is VmSchedule schedule && schedule.Enabled);

        VirtualMachinesView = CollectionViewSource.GetDefaultView(VirtualMachines);
        VirtualMachinesView.Filter = item => VmSearchFilter.Matches(item as VirtualMachineInfo, SearchText);

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

        _statusRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(45)
        };
        _statusRefreshTimer.Tick += async (_, _) => await AutoRefreshStatusesAsync();
    }

    public ObservableCollection<AzureSubscriptionInfo> Subscriptions { get; } = [];

    public ObservableCollection<VirtualMachineInfo> VirtualMachines { get; } = [];

    public ObservableCollection<VmSchedule> Schedules { get; } = [];

    public IReadOnlyList<ScheduleActionOption> ScheduleActions { get; } =
    [
        new(VmScheduleAction.Start, "Start"),
        new(VmScheduleAction.StopDeallocate, "Stop / Deallocate")
    ];

    public ICollectionView VirtualMachinesView { get; }

    public AsyncRelayCommand LoadSubscriptionsCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand RefreshStatusesCommand { get; }

    public AsyncRelayCommand StartVmCommand { get; }

    public AsyncRelayCommand StopVmCommand { get; }

    public AsyncRelayCommand RestartVmCommand { get; }

    public AsyncRelayCommand HibernateVmCommand { get; }

    public AsyncRelayCommand StartSelectedCommand { get; }

    public AsyncRelayCommand StopSelectedCommand { get; }

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

                // Automatically reload VMs when the user picks another
                // subscription. Suppressed during the initial sign-in load,
                // which selects the first subscription and loads it itself.
                if (!_suppressSubscriptionAutoLoad && value is not null)
                {
                    _ = RefreshVirtualMachinesAsync();
                }
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

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                VirtualMachinesView.Refresh();
            }
        }
    }

    public bool AutoRefreshStatuses
    {
        get => _autoRefreshStatuses;
        set
        {
            if (SetProperty(ref _autoRefreshStatuses, value))
            {
                if (value)
                {
                    _statusRefreshTimer.Start();
                }
                else
                {
                    _statusRefreshTimer.Stop();
                }
            }
        }
    }

    public int SelectedCount => _selectedVirtualMachines.Count;

    public int SelectedStartableCount => _selectedVirtualMachines.Count(vm => vm.CanStart);

    public int SelectedStoppableCount => _selectedVirtualMachines.Count(vm => vm.CanStop);

    public bool HasSelection => SelectedCount > 1;

    /// <summary>
    /// Called from the view when the grid selection changes so the view model
    /// can drive the bulk-action commands. The DataGrid's SelectedItems is not
    /// directly bindable, so the selection is pushed in here instead.
    /// </summary>
    public void SetSelectedVirtualMachines(IEnumerable<VirtualMachineInfo> virtualMachines)
    {
        _selectedVirtualMachines = virtualMachines.ToList();
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedStartableCount));
        OnPropertyChanged(nameof(SelectedStoppableCount));
        OnPropertyChanged(nameof(HasSelection));
        StartSelectedCommand.RaiseCanExecuteChanged();
        StopSelectedCommand.RaiseCanExecuteChanged();
    }

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
                OnPropertyChanged(nameof(IsIdle));
                RaiseCommandStatesChanged();
            }
        }
    }

    public bool IsIdle => !IsBusy;

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
            // Selecting the first subscription below must not kick off its own
            // auto-load; this method loads it explicitly once instead.
            _suppressSubscriptionAutoLoad = true;
            try
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
            }
            finally
            {
                _suppressSubscriptionAutoLoad = false;
            }
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
            await RefreshPowerStateAsync(virtualMachine);
            StatusMessage = $"{virtualMachine.Name} is {virtualMachine.PowerStateDisplay}.";
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
            await RefreshPowerStateAsync(virtualMachine);
            StatusMessage = $"{virtualMachine.Name} is {virtualMachine.PowerStateDisplay}.";
        });
    }

    private async Task RestartVirtualMachineAsync(object? parameter)
    {
        if (parameter is not VirtualMachineInfo virtualMachine)
        {
            return;
        }

        await RunVmOperationAsync(virtualMachine, "Restarting...", async () =>
        {
            await _azureVmService.RestartVirtualMachineAsync(virtualMachine);
            await RefreshPowerStateAsync(virtualMachine);
            StatusMessage = $"{virtualMachine.Name} restarted.";
        });
    }

    private async Task HibernateVirtualMachineAsync(object? parameter)
    {
        if (parameter is not VirtualMachineInfo virtualMachine)
        {
            return;
        }

        MessageBoxResult confirmation = System.Windows.MessageBox.Show(
            $"Hibernate VM '{virtualMachine.Name}'? Its memory is saved to disk and compute charges stop.",
            "Confirm hibernate",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await RunVmOperationAsync(virtualMachine, "Hibernating...", async () =>
        {
            await _azureVmService.HibernateVirtualMachineAsync(virtualMachine);
            await RefreshPowerStateAsync(virtualMachine);
            StatusMessage = $"{virtualMachine.Name} is hibernated.";
        });
    }

    private async Task StartSelectedAsync()
    {
        List<VirtualMachineInfo> targets = _selectedVirtualMachines.Where(vm => vm.CanStart).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        await RunBulkOperationAsync(
            targets,
            "Starting...",
            (vm, token) => _azureVmService.StartVirtualMachineAsync(vm, token),
            $"Started {targets.Count} VM(s).");
    }

    private async Task StopSelectedAsync()
    {
        List<VirtualMachineInfo> targets = _selectedVirtualMachines.Where(vm => vm.CanStop).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        MessageBoxResult confirmation = System.Windows.MessageBox.Show(
            $"Stop and deallocate {targets.Count} selected VM(s)?",
            "Confirm stop",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await RunBulkOperationAsync(
            targets,
            "Stopping...",
            (vm, token) => _azureVmService.DeallocateVirtualMachineAsync(vm, token),
            $"Stopped {targets.Count} VM(s).");
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
            }
            else
            {
                await _azureVmService.DeallocateVirtualMachineAsync(schedule.ResourceId);
            }

            if (loadedVm is not null)
            {
                VmPowerState state = await _azureVmService.GetPowerStateAsync(schedule.ResourceId);
                loadedVm.UpdatePowerState(state.Code, state.Display);
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

    private async Task RunBulkOperationAsync(
        IReadOnlyList<VirtualMachineInfo> targets,
        string operationStatus,
        Func<VirtualMachineInfo, CancellationToken, Task> operation,
        string successMessage)
    {
        ErrorMessage = string.Empty;
        StatusMessage = $"{operationStatus.TrimEnd('.')} {targets.Count} VM(s)...";

        foreach (VirtualMachineInfo virtualMachine in targets)
        {
            virtualMachine.OperationStatus = operationStatus;
            virtualMachine.IsOperationInProgress = true;
        }

        RaiseCommandStatesChanged();
        int failures = 0;

        try
        {
            await ConcurrentTasks.RunAsync(targets, MaxConcurrentBulkOperations, async (virtualMachine, token) =>
            {
                try
                {
                    await operation(virtualMachine, token);
                    VmPowerState state = await _azureVmService.GetPowerStateAsync(virtualMachine.ResourceId, token);
                    virtualMachine.UpdatePowerState(state.Code, state.Display);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Interlocked.Increment(ref failures);
                    ErrorMessage = ex.Message;
                }
                finally
                {
                    virtualMachine.IsOperationInProgress = false;
                    virtualMachine.OperationStatus = string.Empty;
                }
            });
        }
        finally
        {
            RaiseDashboardCountsChanged();
            RaiseCommandStatesChanged();
        }

        StatusMessage = failures == 0
            ? successMessage
            : $"{successMessage} {failures} failed.";
    }

    private async Task RefreshPowerStateAsync(VirtualMachineInfo virtualMachine)
    {
        VmPowerState state = await _azureVmService.GetPowerStateAsync(virtualMachine.ResourceId);
        virtualMachine.UpdatePowerState(state.Code, state.Display);
        RaiseDashboardCountsChanged();
    }

    private async Task RefreshAllStatusesAsync()
    {
        await RunBusyAsync(async () =>
        {
            ErrorMessage = string.Empty;
            StatusMessage = "Refreshing power states...";
            await RefreshStatusesCoreAsync();
            StatusMessage = $"Power states refreshed for {VirtualMachines.Count} VMs.";
        });
    }

    private async Task AutoRefreshStatusesAsync()
    {
        if (IsBusy || VirtualMachines.Count == 0)
        {
            return;
        }

        try
        {
            await RefreshStatusesCoreAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task RefreshStatusesCoreAsync()
    {
        List<VirtualMachineInfo> snapshot = VirtualMachines.ToList();
        await ConcurrentTasks.RunAsync(snapshot, MaxConcurrentBulkOperations, async (virtualMachine, token) =>
        {
            VmPowerState state = await _azureVmService.GetPowerStateAsync(virtualMachine.ResourceId, token);
            virtualMachine.UpdatePowerState(state.Code, state.Display);
        });

        RaiseDashboardCountsChanged();
    }

    private bool CanStartVirtualMachine(object? parameter)
    {
        return !IsBusy && parameter is VirtualMachineInfo virtualMachine && virtualMachine.CanStart;
    }

    private bool CanStopVirtualMachine(object? parameter)
    {
        return !IsBusy && parameter is VirtualMachineInfo virtualMachine && virtualMachine.CanStop;
    }

    private bool CanRestartVirtualMachine(object? parameter)
    {
        return !IsBusy && parameter is VirtualMachineInfo virtualMachine && virtualMachine.CanRestart;
    }

    private bool CanHibernateVirtualMachine(object? parameter)
    {
        return !IsBusy && parameter is VirtualMachineInfo virtualMachine && virtualMachine.CanHibernate;
    }

    private void RaiseCommandStatesChanged()
    {
        LoadSubscriptionsCommand.RaiseCanExecuteChanged();
        RefreshCommand.RaiseCanExecuteChanged();
        RefreshStatusesCommand.RaiseCanExecuteChanged();
        StartVmCommand.RaiseCanExecuteChanged();
        StopVmCommand.RaiseCanExecuteChanged();
        RestartVmCommand.RaiseCanExecuteChanged();
        HibernateVmCommand.RaiseCanExecuteChanged();
        StartSelectedCommand.RaiseCanExecuteChanged();
        StopSelectedCommand.RaiseCanExecuteChanged();
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
