using Switchboard.Core.Registry;
using Xunit;

namespace Switchboard.Core.Tests;

public class ColorPaletteTests
{
    private static TerminalDefinition With(string color) => new() { Name = "x", Color = color, Profile = "p" };

    [Fact]
    public void First_unused_hue_wins()
    {
        var next = ColorPalette.NextUnused([With(ColorPalette.Hues[0]), With(ColorPalette.Hues[1])]);
        Assert.Equal(ColorPalette.Hues[2], next);
    }

    [Fact]
    public void Comparison_is_case_insensitive()
    {
        var next = ColorPalette.NextUnused([With(ColorPalette.Hues[0].ToLowerInvariant())]);
        Assert.Equal(ColorPalette.Hues[1], next);
    }

    [Fact]
    public void Wraps_when_exhausted()
    {
        var all = ColorPalette.Hues.Select(With).ToList();
        Assert.Contains(ColorPalette.NextUnused(all), ColorPalette.Hues);
    }
}
