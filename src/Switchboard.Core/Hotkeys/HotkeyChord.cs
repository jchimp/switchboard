namespace Switchboard.Core.Hotkeys;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>
/// A parsed chord such as <c>Ctrl+Alt+N</c>. Platform-neutral: <see cref="Key"/> is a
/// canonical name (<c>"N"</c>, <c>"1"</c>, <c>"Space"</c>, <c>"`"</c>) that the Windows
/// layer maps to a virtual-key code.
/// </summary>
public sealed record HotkeyChord(HotkeyModifiers Modifiers, string Key)
{
    private static readonly Dictionary<string, HotkeyModifiers> ModifierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = HotkeyModifiers.Ctrl,
        ["control"] = HotkeyModifiers.Ctrl,
        ["alt"] = HotkeyModifiers.Alt,
        ["shift"] = HotkeyModifiers.Shift,
        ["win"] = HotkeyModifiers.Win,
        ["super"] = HotkeyModifiers.Win,
    };

    private static readonly Dictionary<string, string> KeyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["`"] = "`",
        ["backtick"] = "`",
        ["grave"] = "`",
        ["tilde"] = "`",
        ["space"] = "Space",
        ["esc"] = "Escape",
        ["escape"] = "Escape",
        ["enter"] = "Enter",
        ["return"] = "Enter",
        ["tab"] = "Tab",
    };

    public static HotkeyChord Parse(string text)
    {
        if (!TryParse(text, out var chord, out var error))
        {
            throw new FormatException(error);
        }

        return chord;
    }

    public static bool TryParse(string text, out HotkeyChord chord, out string? error)
    {
        chord = new HotkeyChord(HotkeyModifiers.None, string.Empty);
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Hotkey is empty.";
            return false;
        }

        // Split on '+' but allow a literal '+' as the final key ("Ctrl++").
        var trimmed = text.Trim();
        string[] parts = trimmed.EndsWith("++", StringComparison.Ordinal)
            ? [.. trimmed[..^2].Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), "+"]
            : trimmed.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            error = $"Hotkey '{text}' has no key.";
            return false;
        }

        var mods = HotkeyModifiers.None;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (!ModifierNames.TryGetValue(parts[i], out var m))
            {
                error = $"Unknown modifier '{parts[i]}' in '{text}'.";
                return false;
            }

            mods |= m;
        }

        var keyRaw = parts[^1];
        if (ModifierNames.ContainsKey(keyRaw))
        {
            error = $"Hotkey '{text}' ends with a modifier; it needs a key.";
            return false;
        }

        var key = KeyAliases.TryGetValue(keyRaw, out var alias)
            ? alias
            : keyRaw.Length == 1 ? keyRaw.ToUpperInvariant() : keyRaw;

        if (mods == HotkeyModifiers.None)
        {
            error = $"Hotkey '{text}' needs at least one modifier; bare keys would hijack typing.";
            return false;
        }

        chord = new HotkeyChord(mods, key);
        return true;
    }

    /// <summary>Convenience for the 1–9 jump keys: <c>prefix + digit</c>.</summary>
    public static HotkeyChord Jump(string prefix, int digit) => Parse($"{prefix}+{digit}");

    public override string ToString()
    {
        var parts = new List<string>(4);
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            parts.Add("Win");
        }

        parts.Add(Key);
        return string.Join('+', parts);
    }
}
