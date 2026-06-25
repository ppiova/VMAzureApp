using System.Drawing;
using System.IO;
using System.Windows;
using VMAzureApp.Models;
using VMAzureApp.ViewModels;
using Forms = System.Windows.Forms;

namespace VMAzureApp.Services;

public sealed class SystemTrayService : IDisposable
{
    private readonly Window _window;
    private readonly MainWindowViewModel _viewModel;
    private readonly Forms.ContextMenuStrip _contextMenu = new();
    private readonly Forms.NotifyIcon _notifyIcon;
    private bool _isDisposed;

    public SystemTrayService(Window window, MainWindowViewModel viewModel)
    {
        _window = window;
        _viewModel = viewModel;

        _contextMenu.Opening += (_, _) => BuildContextMenu();

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _contextMenu,
            Icon = LoadTrayIcon(),
            Text = "Cloud VM Manager",
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    public bool IsExitRequested { get; private set; }

    public void ShowMinimizedNotification()
    {
        _notifyIcon.ShowBalloonTip(
            2500,
            "Cloud VM Manager is still running",
            "Use the tray icon to open the app or manage loaded VMs.",
            Forms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        _isDisposed = true;
    }

    private void BuildContextMenu()
    {
        _contextMenu.Items.Clear();

        _contextMenu.Items.Add(CreateMenuItem("Open Cloud VM Manager", (_, _) => Dispatch(ShowMainWindow)));
        _contextMenu.Items.Add(CreateMenuItem(
            "Refresh VMs",
            (_, _) => Dispatch(() => _viewModel.RefreshCommand.Execute(null)),
            _viewModel.RefreshCommand.CanExecute(null)));
        _contextMenu.Items.Add(new Forms.ToolStripSeparator());

        Forms.ToolStripMenuItem vmMenu = new("Virtual machines");
        if (_viewModel.VirtualMachines.Count == 0)
        {
            vmMenu.DropDownItems.Add(new Forms.ToolStripMenuItem("No VMs loaded") { Enabled = false });
        }
        else
        {
            foreach (VirtualMachineInfo virtualMachine in _viewModel.VirtualMachines)
            {
                Forms.ToolStripMenuItem vmItem = new($"{virtualMachine.Name} ({virtualMachine.DisplayStatus})");
                vmItem.DropDownItems.Add(CreateMenuItem(
                    "Start",
                    (_, _) => Dispatch(() => _viewModel.StartVmCommand.Execute(virtualMachine)),
                    _viewModel.StartVmCommand.CanExecute(virtualMachine)));
                vmItem.DropDownItems.Add(CreateMenuItem(
                    "Stop / Deallocate",
                    (_, _) => Dispatch(() => _viewModel.StopVmCommand.Execute(virtualMachine)),
                    _viewModel.StopVmCommand.CanExecute(virtualMachine)));
                vmItem.DropDownItems.Add(CreateMenuItem(
                    "Restart",
                    (_, _) => Dispatch(() => _viewModel.RestartVmCommand.Execute(virtualMachine)),
                    _viewModel.RestartVmCommand.CanExecute(virtualMachine)));

                if (virtualMachine.SupportsHibernation)
                {
                    vmItem.DropDownItems.Add(CreateMenuItem(
                        "Hibernate",
                        (_, _) => Dispatch(() => _viewModel.HibernateVmCommand.Execute(virtualMachine)),
                        _viewModel.HibernateVmCommand.CanExecute(virtualMachine)));
                }

                vmItem.DropDownItems.Add(new Forms.ToolStripSeparator());
                vmItem.DropDownItems.Add(new Forms.ToolStripMenuItem($"Resource group: {virtualMachine.ResourceGroupName}") { Enabled = false });
                vmItem.DropDownItems.Add(new Forms.ToolStripMenuItem($"Region: {virtualMachine.Location}") { Enabled = false });
                vmMenu.DropDownItems.Add(vmItem);
            }
        }

        _contextMenu.Items.Add(vmMenu);
        _contextMenu.Items.Add(new Forms.ToolStripSeparator());

        Forms.ToolStripMenuItem schedulesMenu = new("Schedules");
        if (_viewModel.Schedules.Count == 0)
        {
            schedulesMenu.DropDownItems.Add(new Forms.ToolStripMenuItem("No schedules configured") { Enabled = false });
        }
        else
        {
            foreach (VmSchedule schedule in _viewModel.Schedules)
            {
                Forms.ToolStripMenuItem scheduleItem = new(schedule.Summary)
                {
                    Checked = schedule.Enabled
                };
                scheduleItem.DropDownItems.Add(new Forms.ToolStripMenuItem($"Last result: {schedule.LastResult}") { Enabled = false });
                scheduleItem.DropDownItems.Add(CreateMenuItem(
                    "Run now",
                    (_, _) => Dispatch(() => _viewModel.RunScheduleNowCommand.Execute(schedule)),
                    _viewModel.RunScheduleNowCommand.CanExecute(schedule)));
                schedulesMenu.DropDownItems.Add(scheduleItem);
            }
        }

        _contextMenu.Items.Add(schedulesMenu);
        _contextMenu.Items.Add(new Forms.ToolStripSeparator());
        _contextMenu.Items.Add(CreateMenuItem("Hide window", (_, _) => Dispatch(() => _window.Hide())));
        _contextMenu.Items.Add(CreateMenuItem("Exit", (_, _) => Dispatch(ExitApplication)));
    }

    private static Forms.ToolStripMenuItem CreateMenuItem(string text, EventHandler onClick, bool enabled = true)
    {
        Forms.ToolStripMenuItem item = new(text)
        {
            Enabled = enabled
        };

        item.Click += onClick;
        return item;
    }

    private void Dispatch(Action action)
    {
        if (_window.Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _window.Dispatcher.Invoke(action);
    }

    private void ShowMainWindow()
    {
        _window.Show();

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
    }

    private void ExitApplication()
    {
        IsExitRequested = true;
        _notifyIcon.Visible = false;
        _window.Close();
        System.Windows.Application.Current.Shutdown();
    }

    private static Icon LoadTrayIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        return File.Exists(iconPath)
            ? new Icon(iconPath)
            : SystemIcons.Application;
    }
}
