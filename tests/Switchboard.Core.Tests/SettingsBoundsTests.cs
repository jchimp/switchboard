using Switchboard.Core.Registry;
using Xunit;

namespace Switchboard.Core.Tests;

public class SettingsBoundsTests
{
    [Fact]
    public void Defaults_are_left_alone()
    {
        var strip = new StripSettings();
        Assert.Equal(strip, SettingsBounds.Clamp(strip));
    }

    [Fact]
    public void A_zero_thickness_strip_is_pushed_back_to_visible()
    {
        var clamped = SettingsBounds.Clamp(new StripSettings { Thickness = 0 });
        Assert.Equal(SettingsBounds.MinThickness, clamped.Thickness);
    }

    [Fact]
    public void The_sliver_is_never_thicker_than_the_strip_it_hides_into()
    {
        var clamped = SettingsBounds.Clamp(new StripSettings { Thickness = 6, HiddenThickness = 20 });
        Assert.Equal(6, clamped.HiddenThickness);
    }

    [Fact]
    public void The_sliver_is_never_invisible()
    {
        var clamped = SettingsBounds.Clamp(new StripSettings { HiddenThickness = 0 });
        Assert.Equal(SettingsBounds.MinHiddenThickness, clamped.HiddenThickness);
    }

    [Fact]
    public void Active_thickness_never_falls_below_the_resting_thickness()
    {
        var clamped = SettingsBounds.Clamp(new StripSettings { Thickness = 20, ActiveThickness = 4 });
        Assert.Equal(20, clamped.ActiveThickness);
    }

    [Fact]
    public void Delays_are_capped()
    {
        var clamped = SettingsBounds.Clamp(new StripSettings { CollapseDelayMs = 99999, HoverOpenDelayMs = 99999 });
        Assert.Equal(SettingsBounds.MaxCollapseDelayMs, clamped.CollapseDelayMs);
        Assert.Equal(SettingsBounds.MaxHoverOpenDelayMs, clamped.HoverOpenDelayMs);
    }

    [Fact]
    public void Rail_sizes_stay_within_range()
    {
        var clamped = SettingsBounds.Clamp(new StripSettings { RailSizeDips = 4, RailTopHeightDips = 4000 });
        Assert.Equal(SettingsBounds.MinRailSizeDips, clamped.RailSizeDips);
        Assert.Equal(SettingsBounds.MaxRailTopHeightDips, clamped.RailTopHeightDips);
    }

    [Fact]
    public void A_negative_monitor_index_falls_back_to_primary()
    {
        Assert.Equal(0, SettingsBounds.Clamp(new StripSettings { Monitor = -3 }).Monitor);
    }
}
