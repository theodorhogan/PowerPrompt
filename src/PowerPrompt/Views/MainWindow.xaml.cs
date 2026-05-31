using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using PowerPrompt.History;
using PowerPrompt.Hotkeys;
using PowerPrompt.Library;
using PowerPrompt.Settings;
using PowerPrompt.Sync;

namespace PowerPrompt.Views;

public partial class MainWindow : Window
{
    // Edge-resize hit-testing for the borderless (transparent, rounded) window.
    private const int WM_NCHITTEST = 0x0084;
    private const double ResizeBorder = 7;
    private static readonly IntPtr HTCLIENT = (IntPtr)1, HTLEFT = (IntPtr)10, HTRIGHT = (IntPtr)11,
        HTTOP = (IntPtr)12, HTTOPLEFT = (IntPtr)13, HTTOPRIGHT = (IntPtr)14,
        HTBOTTOM = (IntPtr)15, HTBOTTOMLEFT = (IntPtr)16, HTBOTTOMRIGHT = (IntPtr)17;

    private readonly LibraryView _libraryView;
    private readonly HistoryView _historyView;
    private readonly SettingsView _settingsView;

    public MainWindow(HistoryStore history, SettingsStore settings, HotkeyManager hotkeys, LibraryStore library, GitSync git)
    {
        InitializeComponent();
        _libraryView = new LibraryView(library);
        _historyView = new HistoryView(history);
        _settingsView = new SettingsView(settings, hotkeys, git);
        _settingsView.LibraryChangedBySync += ReloadLibrary;
        ShowLibrary();
    }

    /// <summary>Selects the Library view and reloads it from disk.</summary>
    public void ShowLibrary()
    {
        _libraryView.Reload();
        ContentHost.Content = _libraryView;
        SetActiveNav(NavLibrary);
    }

    /// <summary>Reloads the library from disk (e.g. after a git pull changed it).</summary>
    public void ReloadLibrary() => _libraryView.Reload();

    private void NavLibrary_Click(object sender, RoutedEventArgs e) => ShowLibrary();

    private void NavHistory_Click(object sender, RoutedEventArgs e)
    {
        _historyView.Reload();
        ContentHost.Content = _historyView;
        SetActiveNav(NavHistory);
    }

    private void NavSettings_Click(object sender, RoutedEventArgs e)
    {
        _settingsView.Reload();
        ContentHost.Content = _settingsView;
        SetActiveNav(NavSettings);
    }

    private void SetActiveNav(Button active)
    {
        NavLibrary.Tag = NavHistory.Tag = NavSettings.Tag = null;
        active.Tag = "active";
    }

    // ---- Custom title bar ----

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ((HwndSource)PresentationSource.FromVisual(this)!).AddHook(HitTestHook);
    }

    /// <summary>Returns resize hit-test codes near the edges so the borderless window resizes.</summary>
    private IntPtr HitTestHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_NCHITTEST)
            return IntPtr.Zero;

        handled = true;
        int screenX = (short)(lParam.ToInt32() & 0xFFFF);
        int screenY = (short)((lParam.ToInt32() >> 16) & 0xFFFF);
        Point p = PointFromScreen(new Point(screenX, screenY));

        bool left = p.X <= ResizeBorder, right = p.X >= ActualWidth - ResizeBorder;
        bool top = p.Y <= ResizeBorder, bottom = p.Y >= ActualHeight - ResizeBorder;

        if (top && left) return HTTOPLEFT;
        if (top && right) return HTTOPRIGHT;
        if (bottom && left) return HTBOTTOMLEFT;
        if (bottom && right) return HTBOTTOMRIGHT;
        if (left) return HTLEFT;
        if (right) return HTRIGHT;
        if (top) return HTTOP;
        if (bottom) return HTBOTTOM;
        return HTCLIENT;
    }

    /// <summary>
    /// Closing the window hides it instead of exiting. The app only exits via
    /// Quit in the tray menu (Application.Shutdown).
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
