using Switchboard.Core.Registry;

namespace Switchboard.Core.Screen;

/// <summary>A rectangle in physical screen pixels.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height);

/// <summary>Platform-neutral snapshot of one monitor. The Windows layer produces these (ADR-0007).</summary>
public sealed record MonitorBounds
{
    /// <summary>Work area (desktop minus taskbar and app bars) in physical pixels.</summary>
    public required PixelRect WorkArea { get; init; }

    /// <summary>
    /// Full monitor rect in physical pixels. App-bar placement must start from here, not the
    /// work area: a registered app bar shrinks the work area by its own band, so re-docking
    /// from it would walk the strip inward (ADR-0011).
    /// </summary>
    public required PixelRect Bounds { get; init; }

    /// <summary>Effective DPI scale, 1.0 = 96 dpi.</summary>
    public required double DpiScale { get; init; }

    public required bool IsPrimary { get; init; }
}

/// <summary>
/// Where the strip goes, in physical pixels. Pure math so the docking rules are testable;
/// the App layer applies the result via SetWindowPos (ADR-0007).
/// </summary>
public static class StripPlacement
{
    /// <summary>
    /// Index 0 is the primary monitor; 1..N are the rest ordered by virtual-screen position
    /// (left, then top), so an index survives whatever order Windows enumerates in.
    /// Out-of-range indexes fall back to primary.
    /// </summary>
    public static MonitorBounds SelectMonitor(IReadOnlyList<MonitorBounds> monitors, int index)
    {
        if (monitors.Count == 0)
        {
            throw new ArgumentException("At least one monitor is required.", nameof(monitors));
        }

        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
        if (index <= 0)
        {
            return primary;
        }

        var secondaries = monitors
            .Where(m => !ReferenceEquals(m, primary))
            .OrderBy(m => m.WorkArea.X)
            .ThenBy(m => m.WorkArea.Y)
            .ToList();
        return index <= secondaries.Count ? secondaries[index - 1] : primary;
    }

    /// <summary>
    /// Computes the strip's rect on <paramref name="monitor"/>; thickness is given in DIPs.
    /// For the top edge, <paramref name="contentLengthDips"/> sizes the bar to its content
    /// and centers it in the work area, matching the rail; null spans the full width.
    /// Left/right strips are full-height regardless.
    /// </summary>
    public static PixelRect Compute(
        MonitorBounds monitor, ScreenEdge edge, int thicknessDips, int? contentLengthDips = null)
    {
        var wa = monitor.WorkArea;
        // Never round a sliver down to nothing — a 0-px strip can't be hovered back out.
        var px = Math.Max(1, (int)Math.Round(thicknessDips * monitor.DpiScale));
        var width = contentLengthDips is { } content
            // Ceiling, not Round: half a pixel short (216n + 46 at 175% ends in .5, which
            // Round takes down to even) clips the last card and its strip segment.
            ? Math.Clamp((int)Math.Ceiling(content * monitor.DpiScale), 1, wa.Width)
            : wa.Width;
        return edge switch
        {
            ScreenEdge.Left => new PixelRect(wa.X, wa.Y, px, wa.Height),
            ScreenEdge.Right => new PixelRect(wa.X + wa.Width - px, wa.Y, px, wa.Height),
            _ => new PixelRect(wa.X + (wa.Width - width) / 2, wa.Y, width, px),
        };
    }

    /// <summary>
    /// The band to propose to the shell when registering as an app bar: anchored to the full
    /// monitor bounds, because the shell's QUERYPOS/SETPOS negotiation clips against the
    /// taskbar and other app bars but never against our own registration (ADR-0011).
    /// </summary>
    public static PixelRect ComputeAppBar(MonitorBounds monitor, ScreenEdge edge, int thicknessDips)
    {
        var b = monitor.Bounds;
        var px = Math.Max(1, (int)Math.Round(thicknessDips * monitor.DpiScale));
        return edge switch
        {
            ScreenEdge.Left => new PixelRect(b.X, b.Y, px, b.Height),
            ScreenEdge.Right => new PixelRect(b.X + b.Width - px, b.Y, px, b.Height),
            _ => new PixelRect(b.X, b.Y, b.Width, px),
        };
    }
}
