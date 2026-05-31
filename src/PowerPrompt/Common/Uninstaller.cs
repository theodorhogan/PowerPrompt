using System.Diagnostics;
using System.Text;
using PowerPrompt.Startup;

namespace PowerPrompt.Common;

/// <summary>
/// Cleanly removes PowerPrompt without an installer: deletes the system traces the
/// app creates (the startup registry entry and the stored GitHub token), then
/// schedules a detached command that — once this process has exited — deletes the
/// executable and, if requested, the %APPDATA%\PowerPrompt data folder.
/// The caller should shut the app down immediately after calling Run().
/// </summary>
public static class Uninstaller
{
    public static void Run(bool deleteData)
    {
        // Remove the hidden traces now (these are what a manual "delete the files"
        // would otherwise leave behind).
        try { StartupRegistrar.Disable(); } catch { }
        try { CredentialManager.DeleteToken(); } catch { }

        string exe = Environment.ProcessPath
                     ?? Process.GetCurrentProcess().MainModule?.FileName
                     ?? string.Empty;
        string dataRoot = AppPaths.Root;

        // A running exe can't delete itself, so hand the deletion to a detached cmd
        // that waits ~2s for this process to exit first.
        var args = new StringBuilder("/c timeout /t 2 /nobreak >nul");
        if (deleteData)
            args.Append($" & rmdir /s /q \"{dataRoot}\"");
        if (exe.Length > 0)
            args.Append($" & del /f /q \"{exe}\"");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = args.ToString(),
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch
        {
            // If we couldn't schedule the self-delete, the trace removal above still ran.
        }
    }
}
