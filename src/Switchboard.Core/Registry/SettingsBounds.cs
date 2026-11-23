namespace Switchboard.Core.Registry;

/// <summary>
/// Clamps the numeric strip settings into ranges that leave the product usable. The
/// settings UI (ADR-0018) can reach these values with a slider, and the file stays
/// hand-editable, so the guard lives here rather than in the controls' own ranges.
/// </summary>
/// <remarks>
/// The interesting one is <see cref="StripSettings.HiddenThickness"/>: a sliver thicker
/// than the strip itself, or a strip of zero thickness, leaves nothing on screen to
/// hover — the user would have to edit JSON to get their strip back.
/// </remarks>
public static class SettingsBounds
{
    public const int MinThickness = 2;
    public const int MaxThickness = 40;
    public const int MinHiddenThickness = 1;
    public const int MaxCollapseDelayMs = 5000;
    public const int MaxHoverOpenDelayMs = 2000;
    public const int MinRailSizeDips = 160;
    public const int MaxRailSizeDips = 480;
    public const int MinRailTopHeightDips = 120;
    public const int MaxRailTopHeightDips = 400;

    // The settings window's own MinWidth/MinHeight and the editor's MinWidth (SettingsWindow.xaml).
    public const int MinSettingsWidth = 900;
    public const int MinSettingsHeight = 520;
    public const int MaxSettingsSize = 8000;
    public const int MinEditorWidth = 330;

    /// <summary>Returns the settings unchanged when they are already in range.</summary>
    public static StripSettings Clamp(StripSettings strip)
    {
        var thickness = Math.Clamp(strip.Thickness, MinThickness, MaxThickness);
        return strip with
        {
            Thickness = thickness,
            ActiveThickness = Math.Clamp(strip.ActiveThickness, thickness, MaxThickness),

            // Never thicker than the strip it collapses out of, and never invisible.
            HiddenThickness = Math.Clamp(strip.HiddenThickness, MinHiddenThickness, thickness),
            CollapseDelayMs = Math.Clamp(strip.CollapseDelayMs, 0, MaxCollapseDelayMs),
            HoverOpenDelayMs = Math.Clamp(strip.HoverOpenDelayMs, 0, MaxHoverOpenDelayMs),
            RailSizeDips = Math.Clamp(strip.RailSizeDips, MinRailSizeDips, MaxRailSizeDips),
            RailTopHeightDips = Math.Clamp(strip.RailTopHeightDips, MinRailTopHeightDips, MaxRailTopHeightDips),

            // An index past the last monitor falls back to primary downstream; negatives don't.
            Monitor = Math.Max(strip.Monitor, 0),
        };
    }

    /// <summary>
    /// Keeps a remembered settings layout sane: at least the window's minimum size and the
    /// editor's minimum width. How wide the editor may be depends on the live window, so the
    /// window caps it against its real grid as it lays out (SettingsWindow.CapEditorWidth);
    /// a constant copied from XAML here would drift.
    /// </summary>
    public static SettingsWindowLayout Clamp(SettingsWindowLayout layout) => layout with
    {
        Width = Math.Clamp(layout.Width, MinSettingsWidth, MaxSettingsSize),
        Height = Math.Clamp(layout.Height, MinSettingsHeight, MaxSettingsSize),
        EditorWidth = Math.Clamp(layout.EditorWidth, MinEditorWidth, MaxSettingsSize),
    };
}
