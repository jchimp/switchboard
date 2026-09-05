using Switchboard.Core.Registry;
using Switchboard.Core.Screen;
using Xunit;

namespace Switchboard.Core.Tests;

public class RailPlacementTests
{
    private static MonitorBounds Monitor(int x, int y, int width = 1920, int height = 1040, double scale = 1.0) =>
        new()
        {
            WorkArea = new PixelRect(x, y, width, height),
            Bounds = new PixelRect(x, y, width, height),
            DpiScale = scale,
            IsPrimary = true,
        };

    [Fact]
    public void Left_rail_sits_flush_against_the_strip()
    {
        var rect = RailPlacement.Compute(Monitor(0, 0), ScreenEdge.Left, stripThicknessDips: 8, railSizeDips: 236);
        Assert.Equal(new PixelRect(8, 0, 236, 1040), rect);
    }

    [Fact]
    public void Right_rail_sits_inside_the_strip()
    {
        var rect = RailPlacement.Compute(Monitor(0, 0), ScreenEdge.Right, stripThicknessDips: 8, railSizeDips: 236);
        Assert.Equal(new PixelRect(1920 - 8 - 236, 0, 236, 1040), rect);
    }

    [Fact]
    public void Top_rail_drops_down_full_width()
    {
        var rect = RailPlacement.Compute(Monitor(100, 50, 2560, 1400), ScreenEdge.Top, stripThicknessDips: 8, railSizeDips: 150);
        Assert.Equal(new PixelRect(100, 58, 2560, 150), rect);
    }

    [Fact]
    public void Sizes_scale_with_monitor_dpi()
    {
        var rect = RailPlacement.Compute(Monitor(0, 0, scale: 1.5), ScreenEdge.Left, stripThicknessDips: 8, railSizeDips: 236);
        Assert.Equal(new PixelRect(12, 0, 354, 1040), rect);
    }

    [Fact]
    public void Hidden_rect_tucks_entirely_behind_the_edge()
    {
        var monitor = Monitor(0, 0);
        Assert.Equal(
            new PixelRect(8 - 236, 0, 236, 1040),
            RailPlacement.ComputeHidden(monitor, ScreenEdge.Left, 8, 236));
        Assert.Equal(
            new PixelRect(1920 - 8, 0, 236, 1040),
            RailPlacement.ComputeHidden(monitor, ScreenEdge.Right, 8, 236));
        Assert.Equal(
            new PixelRect(0, 8 - 150, 1920, 150),
            RailPlacement.ComputeHidden(monitor, ScreenEdge.Top, 8, 150));
    }

    [Fact]
    public void Zero_strip_offset_anchors_the_rail_at_the_screen_edge()
    {
        var rect = RailPlacement.Compute(Monitor(0, 0), ScreenEdge.Left, stripThicknessDips: 0, railSizeDips: 236);
        Assert.Equal(new PixelRect(0, 0, 236, 1040), rect);
    }

    [Theory]
    [InlineData(ScreenEdge.Left, true)]
    [InlineData(ScreenEdge.Right, true)]
    [InlineData(ScreenEdge.Top, false)]
    public void Vertical_only_for_side_edges(ScreenEdge edge, bool vertical)
    {
        Assert.Equal(vertical, RailPlacement.IsVertical(edge));
    }
}
