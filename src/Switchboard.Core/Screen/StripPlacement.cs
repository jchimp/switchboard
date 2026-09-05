using Switchboard.Core.Registry;

namespace Switchboard.Core.Screen;

/// <summary>A rectangle in physical screen pixels.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height);

/// <summary>Platform-neutral snapshot of one monitor. The Windows layer produces these (ADR-0007).</summary>
public sealed record MonitorBounds
{
    /// <summary>Work area (desktop minus taskbar) in physical pixels.</summary>
    public required PixelRect WorkArea { get; init; }

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

    /// <summary>Computes the strip's rect on <paramref name="monitor"/>; thickness is given in DIPs.</summary>
    public static PixelRect Compute(MonitorBounds monitor, ScreenEdge edge, int thicknessDips)
    {
        var wa = monitor.WorkArea;
        // Never round a sliver down to nothing — a 0-px strip can't be hovered back out.
        var px = Math.Max(1, (int)Math.Round(thicknessDips * monitor.DpiScale));
        return edge switch
        {
            ScreenEdge.Left => new PixelRect(wa.X, wa.Y, px, wa.Height),
            ScreenEdge.Right => new PixelRect(wa.X + wa.Width - px, wa.Y, px, wa.Height),
            _ => new PixelRect(wa.X, wa.Y, wa.Width, px),
        };
    }
}
