namespace Switchboard.Windows.Hotkeys;

/// <summary>Maps canonical <see cref="Switchboard.Core.Hotkeys.HotkeyChord.Key"/> names to VK codes.</summary>
internal static class VirtualKeys
{
    private static readonly Dictionary<string, uint> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["`"] = 0xC0,      // VK_OEM_3
        ["Space"] = 0x20,
        ["Escape"] = 0x1B,
        ["Enter"] = 0x0D,
        ["Tab"] = 0x09,
        ["+"] = 0xBB,      // VK_OEM_PLUS
        ["-"] = 0xBD,      // VK_OEM_MINUS
        [","] = 0xBC,
        ["."] = 0xBE,
        ["/"] = 0xBF,
        [";"] = 0xBA,
        ["'"] = 0xDE,
        ["["] = 0xDB,
        ["]"] = 0xDD,
        ["\\"] = 0xDC,
    };

    public static bool TryResolve(string key, out uint vk)
    {
        if (Named.TryGetValue(key, out vk))
        {
            return true;
        }

        if (key.Length == 1 && (char.IsAsciiLetterUpper(key[0]) || char.IsAsciiDigit(key[0])))
        {
            vk = key[0]; // Letters and digits map directly to their ASCII code.
            return true;
        }

        if (key.Length is 2 or 3 && (key[0] is 'F' or 'f') && int.TryParse(key[1..], out var f) && f is >= 1 and <= 24)
        {
            vk = (uint)(0x70 + f - 1);
            return true;
        }

        vk = 0;
        return false;
    }
}
