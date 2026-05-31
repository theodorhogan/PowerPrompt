namespace PowerPrompt.Models;

/// <summary>
/// The 3 rewrite prompt templates. Defaults match the build spec (§5). The
/// "{text}" placeholder is replaced with the clipboard content. Properties are
/// mutable so Stage 6 can edit and persist them to data/rewrite-templates.json.
/// </summary>
public sealed class RewriteTemplates
{
    public string CorrectSpelling { get; set; } =
        "Correct only the spelling and grammar in the following text. Return only the corrected text, nothing else:\n\n{text}";

    public string Polish { get; set; } =
        "Improve the clarity and flow of the following text while keeping its meaning and tone. Return only the revised text:\n\n{text}";

    public string Professionalize { get; set; } =
        "Rewrite the following text to sound more professional. Return only the rewritten text:\n\n{text}";

    public string TemplateFor(RewriteOption option) => option switch
    {
        RewriteOption.CorrectSpelling => CorrectSpelling,
        RewriteOption.Polish => Polish,
        RewriteOption.Professionalize => Professionalize,
        _ => throw new ArgumentOutOfRangeException(nameof(option))
    };

    /// <summary>Builds the final prompt by injecting the clipboard text at "{text}".</summary>
    public string Build(RewriteOption option, string text) =>
        TemplateFor(option).Replace("{text}", text);
}
