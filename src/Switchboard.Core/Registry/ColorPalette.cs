namespace Switchboard.Core.Registry;

/// <summary>
/// The fixed hue set every terminal draws from. Order matters: it is the order
/// auto-assignment walks, chosen so neighbours are visually far apart.
/// </summary>
public static class ColorPalette
{
    public static readonly IReadOnlyList<string> Hues =
    [
        "#B07CF5", // violet     — AI harnesses
        "#4F8EF7", // blue       — PowerShell
        "#C9D43A", // chartreuse — Python
        "#62C554", // green      — Node
        "#22B8CF", // cyan       — Docker
        "#F28C28", // orange     — WSL scratch
        "#E05C9B", // magenta
        "#EF6B6B", // red
        "#F2C94C", // yellow
    ];

    /// <summary>First hue not already used; wraps around when every hue is taken.</summary>
    public static string NextUnused(IEnumerable<TerminalDefinition> existing)
    {
        var used = new HashSet<string>(existing.Select(t => t.Color), StringComparer.OrdinalIgnoreCase);
        return Hues.FirstOrDefault(h => !used.Contains(h)) ?? Hues[used.Count % Hues.Count];
    }

    /// <summary>Neutral grey a Closed terminal's color is pulled toward.</summary>
    public const string DormantGrey = "#6B7383";

    // Fading alone left saturated hues reading as "on"; most of the way to grey reads as
    // off, and the remaining fifth keeps which-terminal-is-which.
    private const double DormantMix = 0.8;

    /// <summary>
    /// The color a Closed terminal's strip segment shows: <paramref name="hex"/> mixed
    /// mostly toward <see cref="DormantGrey"/>. Anything not <c>#RRGGBB</c> yields the grey.
    /// </summary>
    public static string Dormant(string hex)
    {
        if (!TryParse(hex, out var c) || !TryParse(DormantGrey, out var g))
        {
            return DormantGrey;
        }

        static int Mix(int from, int to) => (int)Math.Round(from + (to - from) * DormantMix);
        return $"#{Mix(c.R, g.R):X2}{Mix(c.G, g.G):X2}{Mix(c.B, g.B):X2}";
    }

    private static bool TryParse(string hex, out (int R, int G, int B) rgb)
    {
        rgb = default;
        if (hex is not { Length: 7 } || hex[0] != '#' ||
            !int.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out var v))
        {
            return false;
        }

        rgb = ((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
        return true;
    }
}
