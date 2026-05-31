namespace PowerPrompt.Library;

/// <summary>
/// Seeds a fresh install with a few example prompts so the library isn't empty on
/// first run. Only runs when no categories exist yet — never overwrites real data.
/// </summary>
public static class LibrarySeeder
{
    private static readonly (string Category, string Name, string Body)[] Defaults =
    {
        ("writing", "Friendly email intro",
            "Write a warm, concise opening line for an email to {recipient} about {topic}. Keep it to one sentence and avoid clichés."),
        ("writing", "Meeting follow-up",
            "Summarize the meeting notes below into: (1) key decisions, (2) action items with owners, (3) open questions. Be terse.\n\n{notes}"),
        ("coding", "Code review",
            "Review the following code for correctness bugs, edge cases, and clarity. List concrete issues with line references; do not rewrite the whole file.\n\n{code}"),
        ("coding", "Git commit message",
            "Write a conventional-commits message for the following diff. One subject line under 72 chars, then a short body explaining why.\n\n{diff}"),
        ("ai-prompts", "Summarize text",
            "Summarize the text below in 3 bullet points a busy reader can skim in 10 seconds. Keep each bullet under 15 words.\n\n{text}"),
        ("ai-prompts", "Explain like I'm five",
            "Explain the following concept in plain language a curious 5-year-old would understand, using one everyday analogy.\n\n{concept}")
    };

    public static void EnsureSeeded(LibraryStore store)
    {
        if (store.Categories.Count > 0)
            return; // already has content — leave it alone

        foreach (var (category, name, body) in Defaults)
        {
            store.CreateCategory(category);
            store.CreatePrompt(category, name, body);
        }
    }
}
