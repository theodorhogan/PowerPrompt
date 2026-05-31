using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PowerPrompt.Common;

/// <summary>
/// Forces a window to the foreground with keyboard focus. A window summoned by a
/// global hotkey does not reliably get foreground focus because of Windows'
/// foreground-lock rules; briefly attaching to the current foreground thread's
/// input queue makes SetForegroundWindow honored.
/// </summary>
public static class WindowActivation
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);

    public static void BringToForeground(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        IntPtr foreground = GetForegroundWindow();
        uint foregroundThread = GetWindowThreadProcessId(foreground, out _);
        uint thisThread = GetCurrentThreadId();

        bool attached = foregroundThread != thisThread
                        && AttachThreadInput(thisThread, foregroundThread, true);
        try
        {
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
            window.Activate();
        }
        finally
        {
            if (attached)
                AttachThreadInput(thisThread, foregroundThread, false);
        }
    }
}
