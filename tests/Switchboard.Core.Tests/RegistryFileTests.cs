using Switchboard.Core.Registry;
using Xunit;

namespace Switchboard.Core.Tests;

public sealed class RegistryFileTests
{
    [Fact]
    public void First_run_has_four_starter_terminals_named_from_the_quick_launch_prefix()
    {
        var file = RegistryFile.FirstRun;

        Assert.Equal(["term-1", "term-2", "term-3", "term-4"], file.Terminals.Select(t => t.Name));
        Assert.All(file.Terminals, t => Assert.StartsWith(file.QuickLaunch.NamePrefix, t.Name));
    }

    [Fact]
    public void First_run_terminals_are_valid_and_take_the_quick_launch_profile()
    {
        var file = RegistryFile.FirstRun;

        Assert.All(file.Terminals, t =>
        {
            Assert.Null(t.Validate());
            Assert.Equal(file.QuickLaunch.DefaultProfile, t.Profile);
            Assert.Equal(file.QuickLaunch.DefaultShell, t.Shell);
            Assert.True(t.Persistent);
        });
    }

    [Fact]
    public void First_run_terminals_have_distinct_colors_and_jump_keys_one_to_four()
    {
        var file = RegistryFile.FirstRun;

        Assert.Equal(file.Terminals.Count, file.Terminals.Select(t => t.Color).Distinct().Count());
        Assert.Equal([1, 2, 3, 4], file.Terminals.Select(t => t.JumpKey!.Value));
    }

    [Fact]
    public void Empty_still_has_no_terminals()
    {
        Assert.Empty(RegistryFile.Empty.Terminals);
    }
}
