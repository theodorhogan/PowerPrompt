using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PowerPrompt.Common;
using PowerPrompt.Hotkeys;
using PowerPrompt.Settings;
using PowerPrompt.Startup;
using PowerPrompt.Sync;

namespace PowerPrompt.Views;

/// <summary>
/// Settings: rewrite templates, hotkey editors (with real collision detection),
/// Claude path/model/timeout/max-input, run-on-startup, and git remote URL.
/// Changes are applied to the shared SettingsStore and persisted on Save; hotkeys
/// are applied (and validated) immediately when captured.
/// </summary>
public partial class SettingsView : UserControl
{
    private enum Capture { None, Rewrite, Library }

    private readonly SettingsStore _settings;
    private readonly HotkeyManager _hotkeys;
    private readonly GitSync _git;
    private readonly DispatcherTimer _savedTimer;
    private readonly DispatcherTimer _captureTimer;
    private Capture _capturing = Capture.None;
    private bool _sawModifierDown;
    private ModifierKeys _attemptedMods;

    /// <summary>Raised after a successful sync so the host can reload the library.</summary>
    public event Action? LibraryChangedBySync;

    public SettingsView(SettingsStore settings, HotkeyManager hotkeys, GitSync git)
    {
        InitializeComponent();
        _settings = settings;
        _hotkeys = hotkeys;
        _git = git;
        _git.StatusChanged += status => Dispatcher.Invoke(() => ShowSyncStatus(status));

        _savedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _savedTimer.Tick += (_, _) => { _savedTimer.Stop(); SavedNote.Visibility = Visibility.Collapsed; };

        // Backstop: if capture is armed but no usable key ever arrives (e.g. the combo
        // is a global hotkey owned by another app and the OS swallows the keystroke),
        // end the wait and tell the user instead of sitting silently.
        _captureTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _captureTimer.Tick += (_, _) => OnCaptureTimedOut();

        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += OnPreviewKeyUp;
        Loaded += (_, _) => Reload();
    }

    public void Reload()
    {
        var s = _settings.Settings;
        ClaudePathBox.Text = s.ClaudePath;

        var models = new List<string> { "sonnet", "haiku", "opus" };
        if (!models.Contains(s.ClaudeModel))
            models.Insert(0, s.ClaudeModel);
        ModelBox.ItemsSource = models;
        ModelBox.SelectedItem = s.ClaudeModel;

        TimeoutBox.Text = s.TimeoutSeconds.ToString();
        MaxInputBox.Text = s.MaxInputChars.ToString();
        GitRemoteBox.Text = s.GitRemoteUrl;

        CorrectSpellingBox.Text = _settings.Templates.CorrectSpelling;
        PolishBox.Text = _settings.Templates.Polish;
        ProfessionalizeBox.Text = _settings.Templates.Professionalize;

        RunOnStartupCheck.IsChecked = StartupRegistrar.IsEnabled();
        TokenStatus.Text = CredentialManager.HasToken() ? "token saved ✓" : "no token";
        ShowSyncStatus(_git.Status);

        string? claudeLoc = Rewrite.ClaudeRunner.Locate(s.ClaudePath);
        if (claudeLoc is not null)
        {
            ClaudeStatus.Text = $"✓ found: {claudeLoc}";
            ClaudeStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x53, 0xC0, 0x7A));
        }
        else
        {
            ClaudeStatus.Text = "✕ claude CLI not found — install Claude Code (claude.ai/code) for the rewrite feature. The library works without it.";
            ClaudeStatus.Foreground = Brushes.IndianRed;
        }

        _capturing = Capture.None;
        RefreshHotkeyDisplays();
    }

    // ---- Hotkey editor ----

    private void CaptureRewrite_Click(object sender, RoutedEventArgs e) => BeginCapture(Capture.Rewrite, RewriteHotkeyButton);
    private void CaptureLibrary_Click(object sender, RoutedEventArgs e) => BeginCapture(Capture.Library, LibraryHotkeyButton);

    private void BeginCapture(Capture which, Button button)
    {
        _capturing = which;
        _sawModifierDown = false;
        _attemptedMods = ModifierKeys.None;
        button.Content = "Press keys… (Esc cancels)";
        button.Focus();
        SetStatus(which, "listening…", ok: true);
        _captureTimer.Stop();
        _captureTimer.Start();
    }

    private void EndCapture()
    {
        _captureTimer.Stop();
        _capturing = Capture.None;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_capturing == Capture.None)
            return;

        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            EndCapture();
            RefreshHotkeyDisplays();
            return;
        }

        // Track modifiers so we can describe an intercepted combo, and wait until a
        // real (non-modifier) key is pressed.
        if (HotkeyCombo.IsModifierKey(key))
        {
            _sawModifierDown = true;
            _attemptedMods |= Keyboard.Modifiers;
            return;
        }

        var which = _capturing;
        var combo = new HotkeyCombo(Keyboard.Modifiers, key);
        EndCapture();

        if (!combo.IsValid)
        {
            RefreshHotkeyDisplays();
            SetStatus(which, "use at least one modifier (Ctrl/Alt/Shift)", ok: false);
            return;
        }

        ApplyHotkey(which, combo);
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (_capturing == Capture.None)
            return;

        // All modifiers released but no usable key was ever captured. Either the user
        // pressed only modifiers, or the final key was a global hotkey owned by another
        // app and the OS intercepted it before it reached us. Tell them either way.
        if (_sawModifierDown && Keyboard.Modifiers == ModifierKeys.None)
        {
            var which = _capturing;
            EndCapture();
            RefreshHotkeyDisplays();
            string mods = DescribeMods(_attemptedMods);
            SetStatus(which, $"\"{mods}+…\" can't be assigned — that shortcut is already in use by another app. Try a different combination.", ok: false);
        }
    }

    private void OnCaptureTimedOut()
    {
        if (_capturing == Capture.None)
            return;

        var which = _capturing;
        EndCapture();
        RefreshHotkeyDisplays();
        SetStatus(which, "No shortcut captured — it may be in use by another app. Try a different combination.", ok: false);
    }

    private static string DescribeMods(ModifierKeys mods)
    {
        var parts = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        return parts.Count > 0 ? string.Join("+", parts) : "That key";
    }

    private void ApplyHotkey(Capture which, HotkeyCombo combo)
    {
        int id = which == Capture.Rewrite ? HotkeyManager.RewriteHotkeyId : HotkeyManager.LibraryHotkeyId;
        string previous = which == Capture.Rewrite ? _settings.Settings.RewriteHotkey : _settings.Settings.LibraryHotkey;

        if (_hotkeys.TryRegister(id, combo.Modifiers, combo.Key))
        {
            if (which == Capture.Rewrite)
                _settings.Settings.RewriteHotkey = combo.ToString();
            else
                _settings.Settings.LibraryHotkey = combo.ToString();
            _settings.SaveSettings();
            RefreshHotkeyDisplays();
        }
        else
        {
            // TryRegister unregistered the old combo before failing — restore it.
            if (HotkeyCombo.TryParse(previous, out var old))
                _hotkeys.TryRegister(id, old.Modifiers, old.Key);
            RefreshHotkeyDisplays();
            SetStatus(which, $"\"{combo}\" is unavailable — choose another", ok: false);
        }
    }

    private void RefreshHotkeyDisplays()
    {
        RewriteHotkeyButton.Content = _settings.Settings.RewriteHotkey;
        LibraryHotkeyButton.Content = _settings.Settings.LibraryHotkey;
        SetStatus(Capture.Rewrite, _hotkeys.IsRegistered(HotkeyManager.RewriteHotkeyId) ? "active" : "unavailable",
            _hotkeys.IsRegistered(HotkeyManager.RewriteHotkeyId));
        SetStatus(Capture.Library, _hotkeys.IsRegistered(HotkeyManager.LibraryHotkeyId) ? "active" : "unavailable",
            _hotkeys.IsRegistered(HotkeyManager.LibraryHotkeyId));
    }

    private void SetStatus(Capture which, string text, bool ok)
    {
        var target = which == Capture.Rewrite ? RewriteHotkeyStatus : LibraryHotkeyStatus;
        target.Text = text;
        target.Foreground = ok ? new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)) : Brushes.IndianRed;
    }

    // ---- Startup ----

    private void RunOnStartup_Click(object sender, RoutedEventArgs e)
    {
        if (RunOnStartupCheck.IsChecked == true)
            StartupRegistrar.Enable();
        else
            StartupRegistrar.Disable();

        bool enabled = StartupRegistrar.IsEnabled();
        RunOnStartupCheck.IsChecked = enabled;
        _settings.Settings.RunOnStartup = enabled;
        _settings.SaveSettings();
    }

    // ---- Sync ----

    private void SaveToken_Click(object sender, RoutedEventArgs e)
    {
        if (TokenBox.Password.Length == 0)
            return;
        CredentialManager.SaveToken(TokenBox.Password);
        TokenBox.Clear();
        TokenStatus.Text = "token saved ✓";
    }

    private async void SyncNow_Click(object sender, RoutedEventArgs e)
    {
        string url = GitRemoteBox.Text.Trim();
        if (url.Length == 0)
        {
            SyncStatus.Text = "Enter a remote URL first.";
            return;
        }
        if (!CredentialManager.HasToken())
        {
            SyncStatus.Text = "Save a GitHub token first.";
            return;
        }

        _settings.Settings.GitRemoteUrl = url;
        _settings.SaveSettings();
        _git.RemoteUrl = url;

        SyncNowButton.IsEnabled = false;
        try
        {
            if (!_git.IsLinked)
            {
                var confirm = MessageBox.Show(
                    "First sync will pull the remote's prompts and back up your current local prompts. Continue?",
                    "PowerPrompt — link sync", MessageBoxButton.OKCancel, MessageBoxImage.Information);
                if (confirm != MessageBoxResult.OK)
                    return;
                await _git.LinkToRemoteAsync(url);
            }
            else
            {
                await _git.SyncNowAsync();
            }
            LibraryChangedBySync?.Invoke();
        }
        finally
        {
            SyncNowButton.IsEnabled = true;
        }
    }

    private void ShowSyncStatus(SyncStatus status)
    {
        SyncStatus.Text = status.Message;
        SyncStatus.Foreground = status.State switch
        {
            SyncState.Ok => new SolidColorBrush(Color.FromRgb(0x53, 0xC0, 0x7A)),
            SyncState.Conflict => Brushes.Orange,
            SyncState.Error => Brushes.IndianRed,
            _ => (Brush)FindResource("TextDim")
        };
        string pull = status.LastPull?.ToString("HH:mm:ss") ?? "—";
        string push = status.LastPush?.ToString("HH:mm:ss") ?? "—";
        SyncTimes.Text = $"Last pull: {pull}   ·   Last push: {push}";
    }

    // ---- Uninstall ----

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Uninstall PowerPrompt?\n\nThis removes the Windows startup entry and the saved GitHub token, then closes and deletes the program.",
            "PowerPrompt — uninstall", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK)
            return;

        var data = MessageBox.Show(
            "Also delete your saved prompts, templates, history and settings?\n\n" +
            "Yes — remove everything (the %APPDATA%\\PowerPrompt folder)\n" +
            "No — keep your data in case you reinstall",
            "PowerPrompt — uninstall", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (data == MessageBoxResult.Cancel)
            return;

        Uninstaller.Run(deleteData: data == MessageBoxResult.Yes);
        Application.Current.Shutdown();
    }

    // ---- Save ----

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TimeoutBox.Text, out int timeout) || timeout < 1)
        {
            MessageBox.Show("Timeout must be a positive whole number of seconds.", "PowerPrompt");
            return;
        }
        if (!int.TryParse(MaxInputBox.Text, out int maxInput) || maxInput < 1)
        {
            MessageBox.Show("Max input chars must be a positive whole number.", "PowerPrompt");
            return;
        }

        var s = _settings.Settings;
        s.ClaudePath = string.IsNullOrWhiteSpace(ClaudePathBox.Text) ? "claude" : ClaudePathBox.Text.Trim();
        s.ClaudeModel = ModelBox.SelectedItem as string ?? "sonnet";
        s.TimeoutSeconds = timeout;
        s.MaxInputChars = maxInput;
        s.GitRemoteUrl = GitRemoteBox.Text.Trim();

        _settings.Templates.CorrectSpelling = CorrectSpellingBox.Text;
        _settings.Templates.Polish = PolishBox.Text;
        _settings.Templates.Professionalize = ProfessionalizeBox.Text;

        _settings.SaveSettings();
        _settings.SaveTemplates();
        _git.ScheduleSync(); // templates live in the synced data folder

        SavedNote.Visibility = Visibility.Visible;
        _savedTimer.Stop();
        _savedTimer.Start();
    }
}
