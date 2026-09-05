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
}
