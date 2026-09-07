namespace Switchboard.App.Rail;

/// <summary>
/// The top-edge content width, in DIPs. Owned here rather than in <see cref="RailWindow"/>
/// because the strip follows the same width in Centered mode (ADR-0012 as amended) — one
/// formula, so the bar and the tray can't drift apart.
/// </summary>
public static class RailMetrics
{
    /// <summary>Card width; the XAML card template binds to this so the two can't drift.</summary>
    public const double CardWidthDips = 216;

    /// <summary>The foot column RailWindow.ApplyOrientation docks right on the top edge.</summary>
    public const double FootColumnWidthDips = 250;

    /// <summary>The ListScroller's 8+8 padding plus the window border (1 each side).</summary>
    public const double TopEdgeChromeDips = 16 + 2;

    /// <summary>Width a top-edge rail needs for <paramref name="cardCount"/> terminal cards.</summary>
    public static int ContentLengthDips(int cardCount) =>
        (int)Math.Ceiling((cardCount * CardWidthDips) + FootColumnWidthDips + TopEdgeChromeDips);
}
