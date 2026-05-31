using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace PowerPrompt.Hotkeys;

/// <summary>
/// Registers global hotkeys via Win32 RegisterHotKey against a hidden
/// message-only window. RegisterHotKey fails silently at the OS level when a
/// combo is already taken, so <see cref="TryRegister"/> returns the real result
/// for the Settings editor to report (Stage 6).
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    public const int RewriteHotkeyId = 1;
    public const int LibraryHotkeyId = 2;

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private readonly HashSet<int> _registered = new();

    /// <summary>Raised on the UI thread with the hotkey id when a registered combo is pressed.</summary>
    public event Action<int>? HotkeyPressed;

    /// <summary>Win32 error code from the most recent failed <see cref="TryRegister"/>.</summary>
    public int LastError { get; private set; }

    public HotkeyManager()
    {
        var parameters = new HwndSourceParameters("PowerPrompt.HotkeyWindow")
        {
            ParentWindow = HWND_MESSAGE,
            Width = 0,
            Height = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    /// <summary>
    /// Attempts to register a hotkey. Returns false if the combo is unavailable
    /// (e.g. already owned by another app) — caller decides how to report it.
    /// </summary>
    public bool TryRegister(int id, ModifierKeys modifiers, Key key)
    {
        Unregister(id);

        uint fsModifiers = ToWin32Modifiers(modifiers) | MOD_NOREPEAT;
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        if (RegisterHotKey(_source.Handle, id, fsModifiers, vk))
        {
            _registered.Add(id);
            return true;
        }
        LastError = Marshal.GetLastWin32Error();
        return false;
    }

    public void Unregister(int id)
    {
        if (_registered.Remove(id))
            UnregisterHotKey(_source.Handle, id);
    }

    public bool IsRegistered(int id) => _registered.Contains(id);

    private static uint ToWin32Modifiers(ModifierKeys modifiers)
    {
        uint f = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) f |= MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Control)) f |= MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Shift)) f |= MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Windows)) f |= MOD_WIN;
        return f;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            HotkeyPressed?.Invoke(wParam.ToInt32());
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _registered)
            UnregisterHotKey(_source.Handle, id);
        _registered.Clear();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
