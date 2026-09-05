using Switchboard.Core.Registry;

namespace Switchboard.Core.Screen;

/// <summary>
/// Where the rail goes: a band adjacent to the strip on the same edge — a vertical panel
/// for left/right, a full-width drop-down for top (ADR-0012). Pure math like
/// <see cref="StripPlacement"/>; the App layer applies the result via SetWindowPos.
/// </summary>
public static class RailPlacement
{
    public static bool IsVertical(ScreenEdge edge) => edge is ScreenEdge.Left or ScreenEdge.Right;

    /// <summary>
    /// The rail's rect on <paramref name="monitor"/>, sitting flush against the strip so
    /// the cursor can travel strip → rail without a gap. Sizes are in DIPs.
    /// </summary>
    public static PixelRect Compute(
        MonitorBounds monitor, ScreenEdge edge, int stripThicknessDips, int railSizeDips)
    {
        var wa = monitor.WorkArea;
        var strip = Math.Max(1, (int)Math.Round(stripThicknessDips * monitor.DpiScale));
        var rail = Math.Max(1, (int)Math.Round(railSizeDips * monitor.DpiScale));
        return edge switch
        {
            ScreenEdge.Left => new PixelRect(wa.X + strip, wa.Y, rail, wa.Height),
            ScreenEdge.Right => new PixelRect(wa.X + wa.Width - strip - rail, wa.Y, rail, wa.Height),
            _ => new PixelRect(wa.X, wa.Y + strip, wa.Width, rail),
        };
    }

    /// <summary>
    /// Where the rail starts a slide-out (and ends a slide-in): the same band tucked
    /// entirely behind the screen edge, under the strip.
    /// </summary>
    public static PixelRect ComputeHidden(
        MonitorBounds monitor, ScreenEdge edge, int stripThicknessDips, int railSizeDips)
    {
        var open = Compute(monitor, edge, stripThicknessDips, railSizeDips);
        return edge switch
        {
            ScreenEdge.Left => open with { X = open.X - open.Width },
            ScreenEdge.Right => open with { X = open.X + open.Width },
            _ => open with { Y = open.Y - open.Height },
        };
    }
}
