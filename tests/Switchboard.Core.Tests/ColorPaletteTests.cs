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

    [Fact]
    public void Dormant_mixes_most_of_the_way_to_grey()
    {
        // #4F8EF7 = (79,142,247), grey = (107,115,131); 80% toward grey = (101,120,154).
        Assert.Equal("#65789A", ColorPalette.Dormant("#4F8EF7"));
    }

    [Fact]
    public void Dormant_of_grey_is_grey()
    {
        Assert.Equal(ColorPalette.DormantGrey, ColorPalette.Dormant(ColorPalette.DormantGrey));
    }

    [Theory]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("#FFF")]
    [InlineData("#GGGGGG")]
    public void Dormant_of_invalid_hex_is_plain_grey(string hex)
    {
        Assert.Equal(ColorPalette.DormantGrey, ColorPalette.Dormant(hex));
    }
}
