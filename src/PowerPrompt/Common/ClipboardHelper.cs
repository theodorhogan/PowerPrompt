using System.Windows;

namespace PowerPrompt.Common;

/// <summary>
/// Robust clipboard access. The Windows clipboard can be transiently locked by
/// another app, so reads fail soft (return empty) and writes try a fallback rather
/// than throwing — callers must never lose the user's existing clipboard on error.
/// </summary>
public static class ClipboardHelper
{
    public static string GetText()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static void SetText(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            try { Clipboard.SetDataObject(text, copy: true); }
            catch { /* leave the existing clipboard intact */ }
        }
    }
}
