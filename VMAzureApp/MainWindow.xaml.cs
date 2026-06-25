using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using VMAzureApp.Models;
using VMAzureApp.Services;
using VMAzureApp.ViewModels;

namespace VMAzureApp;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly SystemTrayService _systemTrayService;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
        _systemTrayService = new SystemTrayService(this, viewModel);

        Loaded += async (_, _) => await viewModel.LoadSubscriptionsAsync();
        Closing += OnClosing;
        Closed += (_, _) => _systemTrayService.Dispose();
    }

    private void OnVmSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.DataGrid grid)
        {
            _viewModel.SetSelectedVirtualMachines(grid.SelectedItems.OfType<VirtualMachineInfo>());
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_systemTrayService.IsExitRequested)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        _systemTrayService.ShowMinimizedNotification();
    }
}
