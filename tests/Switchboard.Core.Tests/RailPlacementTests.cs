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
    public void Top_rail_drops_down_full_width_without_a_content_length()
    {
        var rect = RailPlacement.Compute(Monitor(100, 50, 2560, 1400), ScreenEdge.Top, stripThicknessDips: 8, railSizeDips: 150);
        Assert.Equal(new PixelRect(100, 58, 2560, 150), rect);
    }

    [Fact]
    public void Top_rail_hugs_its_content_and_centers_in_the_work_area()
    {
        var rect = RailPlacement.Compute(
            Monitor(100, 50, 2560, 1400), ScreenEdge.Top, stripThicknessDips: 8, railSizeDips: 150,
            contentLengthDips: 900);
        Assert.Equal(new PixelRect(100 + (2560 - 900) / 2, 58, 900, 150), rect);
    }

    [Fact]
    public void Top_rail_content_wider_than_the_work_area_clamps_to_full_width()
    {
        var rect = RailPlacement.Compute(
            Monitor(0, 0), ScreenEdge.Top, stripThicknessDips: 8, railSizeDips: 150,
            contentLengthDips: 5000);
        Assert.Equal(new PixelRect(0, 8, 1920, 150), rect);
    }

    [Fact]
    public void Top_rail_content_length_scales_with_monitor_dpi()
    {
        var rect = RailPlacement.Compute(
            Monitor(0, 0, scale: 1.5), ScreenEdge.Top, stripThicknessDips: 0, railSizeDips: 100,
            contentLengthDips: 400);
        Assert.Equal(new PixelRect((1920 - 600) / 2, 0, 600, 150), rect);
    }

    [Fact]
    public void Top_rail_content_length_rounds_up_so_no_card_is_clipped()
    {
        // 5 cards: 5 * 216 + 46 = 1126 DIPs; at 175% that is 1970.5 px.
        var rect = RailPlacement.Compute(
            Monitor(0, 0, 3840, 2160, scale: 1.75), ScreenEdge.Top, stripThicknessDips: 0, railSizeDips: 100,
            contentLengthDips: 1126);
        Assert.Equal(1971, rect.Width);
    }

    [Fact]
    public void Hidden_top_rail_keeps_the_centered_x()
    {
        var rect = RailPlacement.ComputeHidden(
            Monitor(0, 0), ScreenEdge.Top, stripThicknessDips: 0, railSizeDips: 150,
            contentLengthDips: 900);
        Assert.Equal(new PixelRect((1920 - 900) / 2, -150, 900, 150), rect);
    }

    [Fact]
    public void Side_rails_ignore_the_content_length()
    {
        var rect = RailPlacement.Compute(
            Monitor(0, 0), ScreenEdge.Left, stripThicknessDips: 8, railSizeDips: 236,
            contentLengthDips: 900);
        Assert.Equal(new PixelRect(8, 0, 236, 1040), rect);
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
