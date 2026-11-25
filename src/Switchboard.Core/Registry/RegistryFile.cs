using Switchboard.Core.Status;

namespace Switchboard.Core.Registry;

/// <summary>Root of <c>switchboard.json</c>. This file is the whole product state.</summary>
public sealed record RegistryFile
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public IReadOnlyList<TerminalDefinition> Terminals { get; init; } = [];

    public StripSettings Strip { get; init; } = new();

    public HotkeySettings Hotkeys { get; init; } = new();

    public QuickLaunchSettings QuickLaunch { get; init; } = new();

    public TerminalHostSettings TerminalHost { get; init; } = new();

    public SettingsWindowLayout SettingsWindow { get; init; } = new();

    /// <summary>Rules applied to every terminal after its own rules. Sensible defaults ship in <see cref="StatusRule.Defaults"/>.</summary>
    public IReadOnlyList<StatusRule> GlobalStatusRules { get; init; } = StatusRule.Defaults;

    /// <summary>Remove bound windows' taskbar buttons while bound; restored on unbind and on exit (ADR-0009).</summary>
    public bool HideFromTaskbar { get; init; }

    /// <summary>Detect output in unfocused terminals by sampling window pixels (ADR-0013).</summary>
    public bool ThumbnailMotionEnabled { get; init; } = true;

    public static RegistryFile Empty => new();

    /// <summary>How many starter terminals a first run gets (ADR-0025).</summary>
    public const int FirstRunTerminalCount = 4;

    /// <summary>
    /// The registry a first run writes: every default, plus starter terminals named from the
    /// quick-launch prefix (<c>term-1</c>…) so the next quick launch continues the sequence.
    /// Each takes the quick-launch profile, its own palette hue and the matching jump key.
    /// </summary>
    public static RegistryFile FirstRun
    {
        get
        {
            var file = new RegistryFile();
            var q = file.QuickLaunch;
            var terminals = new List<TerminalDefinition>();
            for (var i = 1; i <= FirstRunTerminalCount; i++)
            {
                terminals.Add(new TerminalDefinition
                {
                    Name = $"{q.NamePrefix}{i}",
                    Color = ColorPalette.NextUnused(terminals),
                    Profile = q.DefaultProfile,
                    Shell = q.DefaultShell,
                    Distro = q.DefaultDistro,
                    JumpKey = i,
                });
            }

            return file with { Terminals = terminals };
        }
    }
}

public enum ScreenEdge
{
    Left,
    Top,
    Right,
}

/// <summary>How the top-edge strip and rail span the screen (ADR-0012 as amended).</summary>
public enum TopWidthMode
{
    /// <summary>Span the full work-area width.</summary>
    Full,

    /// <summary>Only as wide as the terminal content, centered; strip and rail align.</summary>
    Centered,
}

public sealed record StripSettings
{
    public ScreenEdge Edge { get; init; } = ScreenEdge.Top;

    /// <summary>Strip thickness in device-independent pixels.</summary>
    public int Thickness { get; init; } = 8;

    /// <summary>Thickness the active segment grows to. Reserved for M2 segment growth; unused in M1.</summary>
    public int ActiveThickness { get; init; } = 14;

    /// <summary>Collapse to a sliver when the cursor leaves; ignored while <see cref="ReserveScreenSpace"/> is on (ADR-0010).</summary>
    public bool AutoHide { get; init; }

    /// <summary>Sliver thickness in device-independent pixels while collapsed.</summary>
    public int HiddenThickness { get; init; } = 2;

    /// <summary>Delay before collapsing after the cursor leaves the strip.</summary>
    public int CollapseDelayMs { get; init; } = 800;

    public bool OpenRailOnHover { get; init; } = true;

    public int HoverOpenDelayMs { get; init; } = 250;

    /// <summary>Rail width in DIPs when docked to a vertical edge (ADR-0012).</summary>
    public int RailSizeDips { get; init; } = 236;

    /// <summary>Rail height in DIPs when docked to the top edge. Sized to fit one stacked
    /// card with ~3 DIP for pixel rounding: 115-DIP thumb + 18-pt name row + card and list
    /// padding + border (~173 DIP; RailWindow.xaml). Lower and cards lose their thumbnails.</summary>
    public int RailTopHeightDips { get; init; } = 176;

    /// <summary>
    /// Top-edge width: full screen, or only as wide as the terminals and centered. The
    /// strip and the rail both follow it so the bar and the slide-down tray always align.
    /// App-bar mode (<see cref="ReserveScreenSpace"/>) is always full width — a reserved
    /// band spans the whole edge by nature.
    /// </summary>
    public TopWidthMode TopWidth { get; init; } = TopWidthMode.Centered;

    /// <summary>App-bar mode: reserve the strip's pixels so maximized windows stop at it.</summary>
    public bool ReserveScreenSpace { get; init; }

    public bool HideOverFullscreen { get; init; } = true;

    public bool ShowClosedTerminals { get; init; } = true;

    /// <summary>Clicking the segment of the terminal that is already foreground minimizes it (ADR-0019).</summary>
    public bool ClickMinimizesFocused { get; init; } = true;

    /// <summary>Monitor index (0 = primary). Follow-cursor is a later milestone.</summary>
    public int Monitor { get; init; }
}

/// <summary>Hotkey chords in a compact, human-editable form, e.g. <c>"Ctrl+Alt+N"</c>.</summary>
public sealed record HotkeySettings
{
    public string OpenSwitcher { get; init; } = "Ctrl+`";

    /// <summary>Launches straight from the quick-launch defaults, bypassing the picker (ADR-0017).</summary>
    public string NewTerminalQuiet { get; init; } = "Ctrl+Alt+Shift+N";

    public string CycleRecent { get; init; } = "Ctrl+Shift+`";

    public string NewTerminal { get; init; } = "Ctrl+Alt+N";

    public string JumpToAttention { get; init; } = "Ctrl+Alt+Space";

    public string ToggleRail { get; init; } = "Ctrl+Alt+`";

    public string ReapplyColors { get; init; } = "Ctrl+Alt+R";

    /// <summary>Prefix for the always-on 1–9 jump keys; the digit is appended.</summary>
    public string JumpPrefix { get; init; } = "Ctrl+Alt";
}

/// <summary>
/// Where the settings window was left: its size and the Terminals page's split between the
/// list and the editor. Saved when the window hides (ADR-0018 amendment); DIPs throughout.
/// </summary>
public sealed record SettingsWindowLayout
{
    public int Width { get; init; } = 1240;

    public int Height { get; init; } = 760;

    /// <summary>Width of the terminal editor beside the list; wider than the old fixed 330.</summary>
    public int EditorWidth { get; init; } = 440;
}

/// <summary>Switchboard's private Windows Terminal (ADR-0021).</summary>
public sealed record TerminalHostSettings
{
    /// <summary>
    /// Folder of a portable WT (<c>wt.exe</c>, <c>WindowsTerminal.exe</c>, <c>.portable</c>).
    /// Environment variables are expanded. Null means <c>wt.exe</c> on PATH, and then every
    /// WT window on the desktop can be bound — the pre-0021 behaviour.
    /// </summary>
    public string? Folder { get; init; }
}

public sealed record QuickLaunchSettings
{
    /// <summary>Profile used when the new-terminal hotkey is pressed with no further input.</summary>
    public string DefaultProfile { get; init; } = "PowerShell";

    public ShellKind DefaultShell { get; init; } = ShellKind.Pwsh;

    public string? DefaultDistro { get; init; }

    public string NamePrefix { get; init; } = "term-";

    public bool PersistByDefault { get; init; } = true;

    /// <summary>
    /// The directory of the last picker launch; new quick launches start here (R21).
    /// Written via the quiet-save path — hand-edits win on the next hot-reload.
    /// </summary>
    public string? LastDirectory { get; init; }
}
