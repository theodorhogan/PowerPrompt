using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PowerPrompt.Common;
using PowerPrompt.History;
using PowerPrompt.Hotkeys;
using PowerPrompt.Library;
using PowerPrompt.Rewrite;
using PowerPrompt.Settings;
using PowerPrompt.Sync;
using PowerPrompt.Tray;
using PowerPrompt.Views;

namespace PowerPrompt;

public partial class App : Application
{
    private const string MutexName = "PowerPrompt.SingleInstance.9C1F7A2E";
    private Mutex? _singleInstanceMutex;

    private MainWindow? _mainWindow;
    private TrayController? _tray;
    private HotkeyManager? _hotkeys;
    private RewriteService? _rewrite;
    private HistoryStore? _history;
    private SettingsStore? _settings;
    private LibraryStore? _library;
    private GitSync? _git;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Single-instance guard: if another instance owns the mutex, exit immediately.
        _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        _settings = new SettingsStore();
        _history = new HistoryStore();

        _library = new LibraryStore();
        LibrarySeeder.EnsureSeeded(_library); // first run: example prompts (no-op if non-empty)

        _git = new GitSync(AppPaths.DataDir, CredentialManager.GetToken)
        {
            RemoteUrl = _settings.Settings.GitRemoteUrl
        };
        // Any library or template change schedules a debounced commit+push.
        _library.Changed += () => _git.ScheduleSync();

        _hotkeys = new HotkeyManager();
        _hotkeys.HotkeyPressed += OnHotkeyPressed;

        _mainWindow = new MainWindow(_history, _settings, _hotkeys, _library, _git);
        _tray = new TrayController(_mainWindow);

        _rewrite = new RewriteService(_tray.State, _history, _settings);
        _tray.QuickAction = _rewrite.RunQuickAction;
        _tray.SyncNow = () => _ = SyncFromTrayAsync();

        RegisterHotkeysFromSettings();

        // Pull on launch (fast-forward only) if sync is configured.
        if (!string.IsNullOrWhiteSpace(_settings.Settings.GitRemoteUrl) && _git.IsLinked)
            _ = PullOnLaunchAsync();
    }

    private async Task PullOnLaunchAsync()
    {
        await _git!.PullOnLaunchAsync();
        Dispatcher.Invoke(() => _mainWindow!.ReloadLibrary());
    }

    private async Task SyncFromTrayAsync()
    {
        await _git!.SyncNowAsync();
        Dispatcher.Invoke(() => _mainWindow!.ReloadLibrary());
    }

    /// <summary>
    /// Registers the configured hotkeys. If a combo is unavailable (taken by another
    /// app), it is left unregistered — the Settings view surfaces this as "unavailable"
    /// and lets the user pick another (no silent fallback).
    /// </summary>
    private void RegisterHotkeysFromSettings()
    {
        if (HotkeyCombo.TryParse(_settings!.Settings.RewriteHotkey, out var rewrite))
            _hotkeys!.TryRegister(HotkeyManager.RewriteHotkeyId, rewrite.Modifiers, rewrite.Key);
        if (HotkeyCombo.TryParse(_settings.Settings.LibraryHotkey, out var library))
            _hotkeys!.TryRegister(HotkeyManager.LibraryHotkeyId, library.Modifiers, library.Key);
    }

    private void OnHotkeyPressed(int id)
    {
        if (id == HotkeyManager.RewriteHotkeyId)
            _rewrite?.ShowOptionPopup();
        else if (id == HotkeyManager.LibraryHotkeyId)
            ToggleLibraryWindow();
    }

    /// <summary>Library hotkey: open to the Library view, or close it if already visible.</summary>
    private void ToggleLibraryWindow()
    {
        if (_mainWindow is null)
            return;

        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
        }
        else
        {
            _mainWindow.ShowLibrary();
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            WindowActivation.BringToForeground(_mainWindow);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _git?.Dispose();
        _hotkeys?.Dispose();
        _tray?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
