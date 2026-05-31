using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PowerPrompt.Common;

/// <summary>
/// Applies the Windows "immersive dark mode" title bar to standard-chrome windows
/// (the small dialogs) so they match the dark theme instead of showing a light
/// OS title bar. Supported on Windows 10 2004+ and Windows 11.
/// </summary>
public static class WindowTheming
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void ApplyDarkTitleBar(Window window)
    {
        try
        {
            IntPtr hwnd = new WindowInteropHelper(window).EnsureHandle();
            int on = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
        }
        catch
        {
            // Older Windows builds may not support the attribute — ignore.
        }
    }
}
