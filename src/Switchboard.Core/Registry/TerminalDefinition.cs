using System.Text.RegularExpressions;
using Switchboard.Core.Status;

namespace Switchboard.Core.Registry;

/// <summary>
/// One row in the registry: everything needed to launch, recognise, color, and
/// interpret a single named Windows Terminal window.
/// </summary>
/// <remarks>
/// <see cref="Name"/> doubles as the <c>wt -w &lt;name&gt;</c> window name and the
/// initial tab title, which is how the tracker associates a freshly launched HWND
/// with this definition (see ADR-0002).
/// </remarks>
public sealed record TerminalDefinition
{
    /// <summary>Lower-case slug. Registry key, wt window name, initial title.</summary>
    public required string Name { get; init; }

    /// <summary>Two-character label for compact surfaces (thin rail, tooltips).</summary>
    public string Tag { get; init; } = string.Empty;

    /// <summary>Hex color, <c>#RRGGBB</c>. Owned by this terminal across every surface.</summary>
    public required string Color { get; init; }

    /// <summary>Windows Terminal profile name or GUID passed to <c>-p</c>.</summary>
    public required string Profile { get; init; }

    /// <summary>Which shell the profile hosts. Decides how <see cref="Command"/> is wrapped.</summary>
    public ShellKind Shell { get; init; } = ShellKind.Pwsh;

    /// <summary>WSL distro name. Only meaningful when <see cref="Shell"/> is <see cref="ShellKind.Wsl"/>.</summary>
    public string? Distro { get; init; }

    /// <summary>Starting directory. Native to the shell (a WSL path for WSL, a Windows path otherwise).</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Optional command to run inside the shell after it starts. The shell stays open afterwards.</summary>
    public string? Command { get; init; }

    /// <summary>1–9 jump key, or null for none.</summary>
    public int? JumpKey { get; init; }

    /// <summary>
    /// When true, the window title is pinned to <see cref="Name"/> for its lifetime
    /// (<c>--suppressApplicationTitle</c>). Rock-solid tracking, but title-based
    /// status rules will never fire. See ADR-0003.
    /// </summary>
    public bool LockTitle { get; init; }

    /// <summary>False for throwaways created from the quick-launch hotkey; removed from the registry when closed.</summary>
    public bool Persistent { get; init; } = true;

    /// <summary>Per-terminal status rules, evaluated before the global rules.</summary>
    public IReadOnlyList<StatusRule> StatusRules { get; init; } = [];

    /// <summary>
    /// HWND of the window last bound to this terminal, written on every bind. Restart
    /// recovery tries this before title matching: handles survive a manager restart while
    /// titles drift, and a stale handle (reboot) fails window-class validation safely
    /// (ADR-0002 amendment). Runtime state, but the registry is the only durable store
    /// (invariant 3). Not user-edited.
    /// </summary>
    public long? LastHwnd { get; init; }

    private static readonly Regex NamePattern = new("^[a-z0-9][a-z0-9-]{0,31}$", RegexOptions.Compiled);
    private static readonly Regex ColorPattern = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    /// <summary>Returns a validation error, or null when the definition is well-formed.</summary>
    public string? Validate()
    {
        if (!NamePattern.IsMatch(Name))
        {
            return $"Name '{Name}' must be 1–32 chars of [a-z0-9-] and start alphanumeric.";
        }

        if (!ColorPattern.IsMatch(Color))
        {
            return $"Color '{Color}' must be #RRGGBB.";
        }

        if (string.IsNullOrWhiteSpace(Profile))
        {
            return "Profile is required.";
        }

        if (Tag.Length > 2)
        {
            return "Tag must be at most 2 characters.";
        }

        if (JumpKey is < 1 or > 9)
        {
            return "JumpKey must be between 1 and 9.";
        }

        if (Shell == ShellKind.Wsl && string.IsNullOrWhiteSpace(Distro) && !string.IsNullOrWhiteSpace(Command))
        {
            return "Distro is required to run a command in a WSL shell.";
        }

        return null;
    }

    /// <summary>Tag to display: the configured tag, or the first two letters of the name.</summary>
    public string DisplayTag => Tag.Length > 0 ? Tag : Name[..Math.Min(2, Name.Length)].ToUpperInvariant();
}

public enum ShellKind
{
    Pwsh,
    WindowsPowerShell,
    Cmd,
    Wsl,
}
