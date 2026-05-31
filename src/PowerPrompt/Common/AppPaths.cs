using System.IO;

namespace PowerPrompt.Common;

/// <summary>
/// Resolves the app's data folders under %APPDATA%\PowerPrompt so the app works
/// whether it was downloaded as a release exe or built from a clone (it does NOT
/// assume it lives inside the source repo).
///
///  - data\   : library prompts + rewrite-templates.json. This is the folder git
///              sync turns into its own working tree (shared between machines).
///  - local\  : device-settings.json + history.json. Per-device, never synced.
/// </summary>
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PowerPrompt");

    public static string DataDir => Path.Combine(Root, "data");
    public static string LibraryDir => Path.Combine(DataDir, "library");
    public static string RewriteTemplatesFile => Path.Combine(DataDir, "rewrite-templates.json");

    public static string LocalDir => Path.Combine(Root, "local");
    public static string HistoryFile => Path.Combine(LocalDir, "history.json");
    public static string SettingsFile => Path.Combine(LocalDir, "device-settings.json");
}
