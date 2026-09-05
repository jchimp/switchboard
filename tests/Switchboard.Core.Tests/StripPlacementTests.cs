using Switchboard.Core.Registry;
using Switchboard.Core.Screen;
using Xunit;

namespace Switchboard.Core.Tests;

public class StripPlacementTests
{
    private static MonitorBounds Monitor(
        int x, int y, int width = 1920, int height = 1040, double scale = 1.0, bool primary = false,
        PixelRect? bounds = null) =>
        new()
        {
            WorkArea = new PixelRect(x, y, width, height),
            Bounds = bounds ?? new PixelRect(x, y, width, height),
            DpiScale = scale,
            IsPrimary = primary,
        };

    [Fact]
    public void Primary_monitor_is_index_zero_regardless_of_enumeration_order()
    {
        var left = Monitor(-1920, 0);
        var primary = Monitor(0, 0, primary: true);
        Assert.Same(primary, StripPlacement.SelectMonitor([left, primary], 0));
        Assert.Same(primary, StripPlacement.SelectMonitor([primary, left], 0));
    }

    [Fact]
    public void Secondary_monitors_order_left_to_right()
    {
        var farLeft = Monitor(-3840, 0);
        var left = Monitor(-1920, 0);
        var primary = Monitor(0, 0, primary: true);
        var monitors = new[] { left, primary, farLeft };

        Assert.Same(farLeft, StripPlacement.SelectMonitor(monitors, 1));
        Assert.Same(left, StripPlacement.SelectMonitor(monitors, 2));
    }

    [Fact]
    public void Out_of_range_index_falls_back_to_primary()
    {
        var primary = Monitor(0, 0, primary: true);
        var second = Monitor(1920, 0);
        Assert.Same(primary, StripPlacement.SelectMonitor([primary, second], 7));
        Assert.Same(primary, StripPlacement.SelectMonitor([primary, second], -1));
    }

    [Fact]
    public void Top_edge_spans_work_area_width_at_scaled_thickness()
    {
        var monitor = Monitor(100, 50, 2560, 1400, scale: 1.5);
        var rect = StripPlacement.Compute(monitor, ScreenEdge.Top, thicknessDips: 8);
        Assert.Equal(new PixelRect(100, 50, 2560, 12), rect);
    }

    [Fact]
    public void Left_edge_spans_work_area_height()
    {
        var monitor = Monitor(0, 0, 1920, 1040);
        var rect = StripPlacement.Compute(monitor, ScreenEdge.Left, thicknessDips: 8);
        Assert.Equal(new PixelRect(0, 0, 8, 1040), rect);
    }

    [Fact]
    public void Right_edge_hugs_work_area_right()
    {
        var monitor = Monitor(-1920, 100, 1920, 1040, scale: 2.0);
        var rect = StripPlacement.Compute(monitor, ScreenEdge.Right, thicknessDips: 8);
        Assert.Equal(new PixelRect(-16, 100, 16, 1040), rect);
    }

    [Fact]
    public void Thickness_rounds_to_nearest_pixel()
    {
        var monitor = Monitor(0, 0, scale: 1.25);
        var rect = StripPlacement.Compute(monitor, ScreenEdge.Top, thicknessDips: 10);
        Assert.Equal(12, rect.Height); // 12.5 rounds to even
    }

    [Fact]
    public void Collapsed_sliver_is_never_rounded_below_one_pixel()
    {
        var monitor = Monitor(0, 0, scale: 0.25);
        var rect = StripPlacement.Compute(monitor, ScreenEdge.Top, thicknessDips: 1);
        Assert.Equal(1, rect.Height);
    }

    [Fact]
    public void Empty_monitor_list_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => StripPlacement.SelectMonitor([], 0));
    }

    [Fact]
    public void App_bar_band_anchors_to_full_monitor_bounds_not_work_area()
    {
        // Work area already shrunk by a 40 px taskbar at top; the proposal must ignore it (ADR-0011).
        var monitor = Monitor(0, 40, 1920, 1000, bounds: new PixelRect(0, 0, 1920, 1080));
        var rect = StripPlacement.ComputeAppBar(monitor, ScreenEdge.Top, thicknessDips: 8);
        Assert.Equal(new PixelRect(0, 0, 1920, 8), rect);
    }

    [Fact]
    public void App_bar_band_scales_thickness_per_monitor_dpi()
    {
        var monitor = Monitor(0, 0, 2560, 1400, scale: 1.5, bounds: new PixelRect(0, 0, 2560, 1440));
        var rect = StripPlacement.ComputeAppBar(monitor, ScreenEdge.Left, thicknessDips: 8);
        Assert.Equal(new PixelRect(0, 0, 12, 1440), rect);
    }

    [Fact]
    public void App_bar_band_hugs_right_bound()
    {
        var monitor = Monitor(-1920, 0, 1920, 1040, bounds: new PixelRect(-1920, 0, 1920, 1080));
        var rect = StripPlacement.ComputeAppBar(monitor, ScreenEdge.Right, thicknessDips: 8);
        Assert.Equal(new PixelRect(-8, 0, 8, 1080), rect);
    }
}
