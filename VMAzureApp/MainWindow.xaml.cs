using System.Windows;
using System.ComponentModel;
using VMAzureApp.Services;
using VMAzureApp.ViewModels;

namespace VMAzureApp;

public partial class MainWindow : Window
{
    private readonly SystemTrayService _systemTrayService;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        _systemTrayService = new SystemTrayService(this, viewModel);

        Loaded += async (_, _) => await viewModel.LoadSubscriptionsAsync();
        Closing += OnClosing;
        Closed += (_, _) => _systemTrayService.Dispose();
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
