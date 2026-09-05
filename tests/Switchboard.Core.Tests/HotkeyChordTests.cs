using Switchboard.Core.Hotkeys;
using Xunit;

namespace Switchboard.Core.Tests;

public class HotkeyChordTests
{
    [Theory]
    [InlineData("Ctrl+Alt+N", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "N")]
    [InlineData("ctrl+`", HotkeyModifiers.Ctrl, "`")]
    [InlineData("Ctrl+Shift+backtick", HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "`")]
    [InlineData("Ctrl+Alt+Space", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "Space")]
    [InlineData("Ctrl+Alt+1", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "1")]
    [InlineData("Win+F12", HotkeyModifiers.Win, "F12")]
    [InlineData("Ctrl++", HotkeyModifiers.Ctrl, "+")]
    public void Parses_valid_chords(string text, HotkeyModifiers mods, string key)
    {
        var chord = HotkeyChord.Parse(text);
        Assert.Equal(mods, chord.Modifiers);
        Assert.Equal(key, chord.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("N")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Hyper+N")]
    public void Rejects_invalid_chords(string text)
    {
        Assert.False(HotkeyChord.TryParse(text, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Round_trips_through_ToString()
    {
        var chord = HotkeyChord.Parse("shift+ctrl+n");
        Assert.Equal("Ctrl+Shift+N", chord.ToString());
    }

    [Fact]
    public void Jump_builds_prefix_plus_digit()
    {
        Assert.Equal("Ctrl+Alt+7", HotkeyChord.Jump("Ctrl+Alt", 7).ToString());
    }
}
