using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using PowerPrompt.Common;
using PowerPrompt.Models;

namespace PowerPrompt.Library;

public enum CategoryDeleteMode
{
    MovePrompts,
    DeletePrompts
}

/// <summary>
/// Loads and persists library prompts as one JSON file per prompt under
/// data/library/&lt;category&gt;/&lt;slug&gt;.json. The category is the folder; the prompt
/// is the file (minimizes git merge conflicts). Holds an in-memory mirror that is
/// kept in sync with disk on every mutation.
/// </summary>
public sealed class LibraryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _root;
    private readonly Dictionary<string, List<Prompt>> _byCategory = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised after any change is persisted to disk (drives debounced git sync).</summary>
    public event Action? Changed;

    public LibraryStore() : this(AppPaths.LibraryDir) { }

    public LibraryStore(string libraryDir)
    {
        _root = libraryDir;
        Directory.CreateDirectory(_root);
        Load();
    }

    public IReadOnlyList<string> Categories =>
        _byCategory.Keys.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<Prompt> PromptsIn(string category) =>
        _byCategory.TryGetValue(category, out var list)
            ? list.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList()
            : new List<Prompt>();

    /// <summary>Matches a query against prompt name or body across all categories.</summary>
    public IReadOnlyList<Prompt> Search(string query)
    {
        query = query.Trim();
        if (query.Length == 0)
            return Array.Empty<Prompt>();

        return _byCategory.Values
            .SelectMany(list => list)
            .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || p.Body.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Load()
    {
        _byCategory.Clear();
        if (!Directory.Exists(_root))
            return;

        foreach (var dir in Directory.GetDirectories(_root))
        {
            string category = Path.GetFileName(dir);
            var list = new List<Prompt>();
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var prompt = JsonSerializer.Deserialize<Prompt>(File.ReadAllText(file), JsonOptions);
                    if (prompt is null)
                        continue;
                    prompt.Category = category;
                    prompt.Slug = Path.GetFileNameWithoutExtension(file);
                    list.Add(prompt);
                }
                catch
                {
                    // Skip malformed files rather than failing the whole load.
                }
            }
            _byCategory[category] = list;
        }
    }

    // ---- Category CRUD ----

    public bool CreateCategory(string name)
    {
        name = SanitizeFolderName(name);
        if (name.Length == 0 || _byCategory.ContainsKey(name))
            return false;

        Directory.CreateDirectory(Path.Combine(_root, name));
        _byCategory[name] = new List<Prompt>();
        Changed?.Invoke();
        return true;
    }

    public bool RenameCategory(string oldName, string newName)
    {
        newName = SanitizeFolderName(newName);
        if (newName.Length == 0
            || !_byCategory.TryGetValue(oldName, out var prompts)
            || _byCategory.ContainsKey(newName))
            return false;

        Directory.Move(Path.Combine(_root, oldName), Path.Combine(_root, newName));
        foreach (var p in prompts)
            p.Category = newName;

        _byCategory.Remove(oldName);
        _byCategory[newName] = prompts;
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Deletes a category. If <paramref name="mode"/> is MovePrompts, its prompts are
    /// moved to <paramref name="moveTarget"/> first; otherwise they are deleted with it.
    /// Never silently loses data — the caller must have confirmed the choice.
    /// </summary>
    public bool DeleteCategory(string name, CategoryDeleteMode mode, string? moveTarget = null)
    {
        if (!_byCategory.TryGetValue(name, out var prompts))
            return false;

        if (mode == CategoryDeleteMode.MovePrompts)
        {
            if (moveTarget is null || !_byCategory.ContainsKey(moveTarget) || moveTarget == name)
                return false;

            foreach (var prompt in prompts.ToList())
                MovePrompt(prompt, moveTarget);
        }

        Directory.Delete(Path.Combine(_root, name), recursive: true);
        _byCategory.Remove(name);
        Changed?.Invoke();
        return true;
    }

    // ---- Prompt CRUD ----

    public Prompt? CreatePrompt(string category, string name, string body)
    {
        if (!_byCategory.TryGetValue(category, out var list))
            return null;

        name = name.Trim();
        if (name.Length == 0)
            return null;

        var now = DateTime.UtcNow;
        var prompt = new Prompt
        {
            Name = name,
            Body = body,
            Created = now,
            Modified = now,
            Category = category,
            Slug = UniqueSlug(category, name)
        };
        Write(prompt);
        list.Add(prompt);
        Changed?.Invoke();
        return prompt;
    }

    /// <summary>Updates name/body in place. The slug (filename) stays stable to minimize git churn.</summary>
    public void UpdatePrompt(Prompt prompt, string newName, string newBody)
    {
        prompt.Name = newName.Trim();
        prompt.Body = newBody;
        prompt.Modified = DateTime.UtcNow;
        Write(prompt);
        Changed?.Invoke();
    }

    public void DeletePrompt(Prompt prompt)
    {
        string path = PathOf(prompt);
        if (File.Exists(path))
            File.Delete(path);

        if (_byCategory.TryGetValue(prompt.Category, out var list))
            list.Remove(prompt);
        Changed?.Invoke();
    }

    public bool MovePrompt(Prompt prompt, string targetCategory)
    {
        if (targetCategory == prompt.Category
            || !_byCategory.TryGetValue(targetCategory, out var target)
            || !_byCategory.TryGetValue(prompt.Category, out var source))
            return false;

        string oldPath = PathOf(prompt);
        string newSlug = UniqueSlug(targetCategory, prompt.Name);

        source.Remove(prompt);
        prompt.Category = targetCategory;
        prompt.Slug = newSlug;

        string newPath = PathOf(prompt);
        if (File.Exists(oldPath))
            File.Move(oldPath, newPath);
        else
            Write(prompt);

        target.Add(prompt);
        Changed?.Invoke();
        return true;
    }

    // ---- Helpers ----

    private string PathOf(Prompt prompt) =>
        Path.Combine(_root, prompt.Category, prompt.Slug + ".json");

    private void Write(Prompt prompt)
    {
        Directory.CreateDirectory(Path.Combine(_root, prompt.Category));
        File.WriteAllText(PathOf(prompt), JsonSerializer.Serialize(prompt, JsonOptions));
    }

    private string UniqueSlug(string category, string name)
    {
        string baseSlug = Slugify(name);
        if (baseSlug.Length == 0)
            baseSlug = "prompt";

        string categoryDir = Path.Combine(_root, category);
        string slug = baseSlug;
        int n = 2;
        while (File.Exists(Path.Combine(categoryDir, slug + ".json")))
            slug = $"{baseSlug}-{n++}";
        return slug;
    }

    private static string Slugify(string name)
    {
        var sb = new StringBuilder();
        bool lastDash = false;
        foreach (char c in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastDash = false;
            }
            else if (!lastDash)
            {
                sb.Append('-');
                lastDash = true;
            }
        }
        return sb.ToString().Trim('-');
    }

    private static string SanitizeFolderName(string name)
    {
        name = name.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c.ToString(), string.Empty);
        return name.Trim().TrimEnd('.');
    }
}
