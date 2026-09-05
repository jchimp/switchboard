using Switchboard.Core.Status;

namespace Switchboard.Core.Registry;

/// <summary>Root of <c>registry.json</c>. This file is the whole product state.</summary>
public sealed record RegistryFile
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public IReadOnlyList<TerminalDefinition> Terminals { get; init; } = [];

    public StripSettings Strip { get; init; } = new();

    public HotkeySettings Hotkeys { get; init; } = new();

    public QuickLaunchSettings QuickLaunch { get; init; } = new();

    /// <summary>Rules applied to every terminal after its own rules. Sensible defaults ship in <see cref="StatusRule.Defaults"/>.</summary>
    public IReadOnlyList<StatusRule> GlobalStatusRules { get; init; } = StatusRule.Defaults;

    /// <summary>Remove bound windows' taskbar buttons while bound; restored on unbind and on exit (ADR-0009).</summary>
    public bool HideFromTaskbar { get; init; }

    /// <summary>Detect output in unfocused terminals by sampling window pixels (ADR-0013).</summary>
    public bool ThumbnailMotionEnabled { get; init; } = true;

    public static RegistryFile Empty => new();
}

public enum ScreenEdge
{
    Left,
    Top,
    Right,
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

    /// <summary>Rail height in DIPs when docked to the top edge.</summary>
    public int RailTopHeightDips { get; init; } = 150;

    /// <summary>App-bar mode: reserve the strip's pixels so maximized windows stop at it.</summary>
    public bool ReserveScreenSpace { get; init; }

    public bool HideOverFullscreen { get; init; } = true;

    public bool ShowClosedTerminals { get; init; } = true;

    /// <summary>Monitor index (0 = primary). Follow-cursor is a later milestone.</summary>
    public int Monitor { get; init; }
}

/// <summary>Hotkey chords in a compact, human-editable form, e.g. <c>"Ctrl+Alt+N"</c>.</summary>
public sealed record HotkeySettings
{
    public string OpenSwitcher { get; init; } = "Ctrl+`";

    public string CycleRecent { get; init; } = "Ctrl+Shift+`";

    public string NewTerminal { get; init; } = "Ctrl+Alt+N";

    public string JumpToAttention { get; init; } = "Ctrl+Alt+Space";

    public string ToggleRail { get; init; } = "Ctrl+Alt+`";

    public string ReapplyColors { get; init; } = "Ctrl+Alt+R";

    /// <summary>Prefix for the always-on 1–9 jump keys; the digit is appended.</summary>
    public string JumpPrefix { get; init; } = "Ctrl+Alt";
}

public sealed record QuickLaunchSettings
{
    /// <summary>Profile used when the new-terminal hotkey is pressed with no further input.</summary>
    public string DefaultProfile { get; init; } = "PowerShell";

    public ShellKind DefaultShell { get; init; } = ShellKind.Pwsh;

    public string? DefaultDistro { get; init; }

    public string NamePrefix { get; init; } = "term-";

    public bool PersistByDefault { get; init; } = true;
}
