using System.Text.Json.Serialization;

namespace PowerPrompt.Models;

/// <summary>
/// A saved library prompt. Persisted one-per-file at
/// data/library/&lt;category&gt;/&lt;slug&gt;.json. Category and Slug are runtime-only
/// (derived from the folder and file name) and are not serialized.
/// </summary>
public sealed class Prompt
{
    public string Name { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }

    [JsonIgnore] public string Category { get; set; } = string.Empty;
    [JsonIgnore] public string Slug { get; set; } = string.Empty;
}
