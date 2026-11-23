using Switchboard.Core.Registry;
using Xunit;

namespace Switchboard.Core.Tests;

public class SettingsBoundsTests
{
    [Fact]
    public void The_default_settings_layout_is_left_alone()
    {
        var layout = new SettingsWindowLayout();
        Assert.Equal(layout, SettingsBounds.Clamp(layout));
    }

    [Fact]
    public void A_settings_window_smaller_than_its_minimum_is_grown()
    {
        var clamped = SettingsBounds.Clamp(new SettingsWindowLayout { Width = 100, Height = 50 });

        Assert.Equal(SettingsBounds.MinSettingsWidth, clamped.Width);
        Assert.Equal(SettingsBounds.MinSettingsHeight, clamped.Height);
    }

    [Fact]
    public void A_wide_editor_is_kept_for_the_window_to_cap_against_its_real_grid()
    {
        // A maximized user's wide split must survive the save; the live window caps it.
        Assert.Equal(1500, SettingsBounds.Clamp(new SettingsWindowLayout { Width = 1240, EditorWidth = 1500 }).EditorWidth);
    }

    [Fact]
    public void Clamping_twice_changes_nothing_so_an_unchanged_layout_is_never_rewritten()
    {
        var once = SettingsBounds.Clamp(new SettingsWindowLayout { Width = 10, Height = 99999, EditorWidth = -5 });
        Assert.Equal(once, SettingsBounds.Clamp(once));
    }

    [Fact]
    public void The_editor_never_goes_below_its_own_minimum()
    {
        Assert.Equal(SettingsBounds.MinEditorWidth,
            SettingsBounds.Clamp(new SettingsWindowLayout { EditorWidth = 10 }).EditorWidth);
    }

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
