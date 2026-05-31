namespace PowerPrompt.Models;

/// <summary>
/// Per-device settings persisted to local/device-settings.json (gitignored — these
/// differ per machine). Defaults match the build spec; ClaudeModel reflects the
/// project decision to pin the model (see memory: claude-cli-invocation).
/// </summary>
public sealed class AppSettings
{
    public string RewriteHotkey { get; set; } = "Ctrl+Alt+R";
    public string LibraryHotkey { get; set; } = "Ctrl+Alt+L";
    public string ClaudePath { get; set; } = "claude";
    public string ClaudeModel { get; set; } = "sonnet";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxInputChars { get; set; } = 6000;
    public bool RunOnStartup { get; set; }
    public string GitRemoteUrl { get; set; } = string.Empty;
}
