using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using PowerPrompt.Common;
using PowerPrompt.Models;

namespace PowerPrompt.History;

/// <summary>
/// Local (not git-synced) log of Feature A rewrites in local/history.json, newest
/// first, capped to the most recent entries. A single shared instance is used by
/// the rewrite pipeline (append) and the History view (display/clear).
/// </summary>
public sealed class HistoryStore
{
    private const int Cap = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _file;
    private readonly List<HistoryEntry> _entries;

    public HistoryStore() : this(AppPaths.HistoryFile) { }

    public HistoryStore(string file)
    {
        _file = file;
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        _entries = LoadFromDisk();
    }

    public IReadOnlyList<HistoryEntry> Entries => _entries;

    public void Add(HistoryEntry entry)
    {
        _entries.Insert(0, entry);
        if (_entries.Count > Cap)
            _entries.RemoveRange(Cap, _entries.Count - Cap);
        Save();
    }

    public void Clear()
    {
        _entries.Clear();
        Save();
    }

    private List<HistoryEntry> LoadFromDisk()
    {
        try
        {
            if (File.Exists(_file))
                return JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(_file), JsonOptions)
                       ?? new List<HistoryEntry>();
        }
        catch
        {
            // Corrupt/partial file — start fresh rather than crash.
        }
        return new List<HistoryEntry>();
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_file, JsonSerializer.Serialize(_entries, JsonOptions));
        }
        catch
        {
            // History is best-effort; never let a logging failure break a rewrite.
        }
    }
}
