using System.Windows.Input;

namespace PowerPrompt.Common;

/// <summary>
/// A hotkey combination (modifiers + key) with string round-tripping like
/// "Ctrl+Alt+R". Used to persist hotkeys in settings and to drive the editor.
/// </summary>
public readonly record struct HotkeyCombo(ModifierKeys Modifiers, Key Key)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }

    /// <summary>Requires at least one modifier and a non-modifier key.</summary>
    public bool IsValid => Modifiers != ModifierKeys.None && !IsModifierKey(Key);

    public static bool TryParse(string? text, out HotkeyCombo combo)
    {
        combo = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var mods = ModifierKeys.None;
        Key? key = null;

        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl":
                case "control": mods |= ModifierKeys.Control; break;
                case "alt": mods |= ModifierKeys.Alt; break;
                case "shift": mods |= ModifierKeys.Shift; break;
                case "win":
                case "windows": mods |= ModifierKeys.Windows; break;
                default:
                    if (Enum.TryParse<Key>(raw, ignoreCase: true, out var k))
                        key = k;
                    else
                        return false;
                    break;
            }
        }

        if (key is null)
            return false;

        combo = new HotkeyCombo(mods, key.Value);
        return true;
    }

    public static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin or
        Key.System;
}
