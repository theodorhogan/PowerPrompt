using System.Text.Json.Serialization;

namespace PowerPrompt.Models;

/// <summary>
/// One Feature A rewrite attempt, logged locally (not git-synced). The display
/// properties are derived and not serialized.
/// </summary>
public sealed class HistoryEntry
{
    public DateTime Timestamp { get; set; }
    public string Option { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public bool Ok { get; set; }

    [JsonIgnore] public string TimeDisplay => Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    [JsonIgnore] public string StatusDisplay => Ok ? "OK" : "failed";
    [JsonIgnore] public string InputPreview => Preview(Input);
    [JsonIgnore] public string OutputPreview => Preview(Output);

    private static string Preview(string text)
    {
        string oneLine = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return oneLine.Length <= 60 ? oneLine : oneLine[..60] + "…";
    }
}
