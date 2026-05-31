using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using PowerPrompt.Common;
using PowerPrompt.Models;

namespace PowerPrompt.Settings;

/// <summary>
/// Loads and persists the two config files:
///  - local/device-settings.json   (per-device, gitignored) → <see cref="AppSettings"/>
///  - data/rewrite-templates.json   (git-shared)            → <see cref="RewriteTemplates"/>
/// Both instances are shared live: editing them in Settings affects the rewrite
/// pipeline immediately, and Save persists to disk.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Keep git-shared files human-readable (don't escape +, <, >, & as \uXXXX).
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public AppSettings Settings { get; }
    public RewriteTemplates Templates { get; }

    public SettingsStore()
    {
        Settings = LoadSettings();
        Templates = LoadTemplates();
    }

    private static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), JsonOptions)
                       ?? new AppSettings();
        }
        catch
        {
            // Corrupt file — fall back to defaults rather than crash.
        }
        return new AppSettings();
    }

    private static RewriteTemplates LoadTemplates()
    {
        try
        {
            if (File.Exists(AppPaths.RewriteTemplatesFile))
                return JsonSerializer.Deserialize<RewriteTemplates>(File.ReadAllText(AppPaths.RewriteTemplatesFile), JsonOptions)
                       ?? new RewriteTemplates();
        }
        catch
        {
            // Corrupt file — fall back to defaults.
        }

        // First run: write defaults so the git-shared file exists.
        var defaults = new RewriteTemplates();
        WriteTemplates(defaults);
        return defaults;
    }

    public void SaveSettings()
    {
        Directory.CreateDirectory(AppPaths.LocalDir);
        File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(Settings, JsonOptions));
    }

    public void SaveTemplates() => WriteTemplates(Templates);

    private static void WriteTemplates(RewriteTemplates templates)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            File.WriteAllText(AppPaths.RewriteTemplatesFile, JsonSerializer.Serialize(templates, JsonOptions));
        }
        catch
        {
            // Best-effort; never let a settings save break the app.
        }
    }
}
