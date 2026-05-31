using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using PowerPrompt.Models;
using PowerPrompt.Startup;
using PowerPrompt.Views;

namespace PowerPrompt.Tray;

/// <summary>
/// Owns the tray icon, its context menu, and the click handlers. Left-click opens
/// the main window; right-click shows the context menu. Holds the TrayState machine.
/// </summary>
public sealed class TrayController : IDisposable
{
    private readonly TaskbarIcon _taskbarIcon;
    private readonly MainWindow _mainWindow;
    private readonly MenuItem _runOnStartupItem;

    public TrayState State { get; }

    /// <summary>Invoked when a Quick Actions submenu item is chosen (wired by App).</summary>
    public Action<RewriteOption>? QuickAction { get; set; }

    /// <summary>Invoked when "Sync Now" is chosen (wired by App).</summary>
    public Action? SyncNow { get; set; }

    public TrayController(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;

        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "PowerPrompt"
        };
        _taskbarIcon.TrayLeftMouseUp += (_, _) => ShowMainWindow();

        State = new TrayState(icon => _taskbarIcon.Icon = icon);

        _runOnStartupItem = new MenuItem
        {
            Header = "Run on startup",
            IsCheckable = true,
            IsChecked = StartupRegistrar.IsEnabled()
        };
        _runOnStartupItem.Click += OnToggleRunOnStartup;

        _taskbarIcon.ContextMenu = BuildContextMenu();
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var open = new MenuItem { Header = "Open Window" };
        open.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(open);

        // Quick Actions submenu — runs a rewrite on the current clipboard.
        var quickActions = new MenuItem { Header = "Quick Actions" };
        foreach (var option in RewriteOptionInfo.All)
        {
            var captured = option;
            var item = new MenuItem { Header = RewriteOptionInfo.DisplayName(option) };
            item.Click += (_, _) => QuickAction?.Invoke(captured);
            quickActions.Items.Add(item);
        }
        menu.Items.Add(quickActions);

        var syncNow = new MenuItem { Header = "Sync Now" };
        syncNow.Click += (_, _) => SyncNow?.Invoke();
        menu.Items.Add(syncNow);

        menu.Items.Add(_runOnStartupItem);

        menu.Items.Add(new Separator());

        var quit = new MenuItem { Header = "Quit" };
        quit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(quit);

        return menu;
    }

    private void ShowMainWindow()
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OnToggleRunOnStartup(object sender, RoutedEventArgs e)
    {
        if (_runOnStartupItem.IsChecked)
            StartupRegistrar.Enable();
        else
            StartupRegistrar.Disable();

        // Reflect the real registry state in case the write failed.
        _runOnStartupItem.IsChecked = StartupRegistrar.IsEnabled();
    }

    public void Dispose()
    {
        State.Dispose();
        _taskbarIcon.Dispose();
    }
}
