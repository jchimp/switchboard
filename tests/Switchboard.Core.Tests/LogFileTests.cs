using Switchboard.Core.Logging;
using Xunit;

namespace Switchboard.Core.Tests;

public class LogFileTests
{
    [Fact]
    public void File_name_encodes_the_day()
    {
        Assert.Equal("switchboard-20260904.log", LogFile.FileNameFor(new DateOnly(2026, 9, 4)));
    }

    [Fact]
    public void Line_contains_timestamp_level_category_and_message()
    {
        var line = LogFile.FormatLine(
            new DateTimeOffset(2026, 9, 4, 12, 33, 11, 123, TimeSpan.Zero),
            "INF", "Switchboard.App.App", "Switchboard up");

        Assert.Equal("2026-09-04 12:33:11.123 [INF] Switchboard.App.App: Switchboard up", line);
    }

    [Fact]
    public void Exception_text_is_appended_on_indented_lines()
    {
        var line = LogFile.FormatLine(
            new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero),
            "ERR", "cat", "boom", "System.Exception: kapow\r\n   at Somewhere()");

        var lines = line.Split(Environment.NewLine);
        Assert.Equal(3, lines.Length);
        Assert.Equal("    System.Exception: kapow", lines[1]);
        Assert.Equal("       at Somewhere()", lines[2]);
    }

    [Fact]
    public void Prune_keeps_the_newest_seven_days()
    {
        var today = new DateOnly(2026, 9, 4);
        var names = Enumerable.Range(0, 10)
            .Select(i => LogFile.FileNameFor(today.AddDays(-i)))
            .ToList();

        var prunable = LogFile.SelectPrunable(names, today);

        Assert.Equal(3, prunable.Count);
        Assert.DoesNotContain(LogFile.FileNameFor(today.AddDays(-6)), prunable);
        Assert.Contains(LogFile.FileNameFor(today.AddDays(-7)), prunable);
    }

    [Fact]
    public void Prune_ignores_files_it_does_not_own()
    {
        var today = new DateOnly(2026, 9, 4);
        var names = new[] { "switchboard.json", "notes.log", "switchboard-notadate.log", "switchboard-19990101.txt" };
        Assert.Empty(LogFile.SelectPrunable(names, today));
    }
}
