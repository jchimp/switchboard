using Switchboard.Core.Launch;
using Switchboard.Core.Registry;
using Xunit;

namespace Switchboard.Core.Tests;

public class WtCommandBuilderTests
{
    private static TerminalDefinition Base => new()
    {
        Name = "anvil",
        Color = "#B07CF5",
        Profile = "Ubuntu",
    };

    [Fact]
    public void Plain_shell_uses_profile_command_line()
    {
        var psi = WtCommandBuilder.Build(Base with { Shell = ShellKind.Pwsh, WorkingDirectory = @"C:\ops" });

        Assert.Equal("wt.exe", psi.FileName);
        Assert.Equal(new[] { "-w", "anvil", "new-tab", "-p", "Ubuntu", "-d", @"C:\ops", "--title", "anvil", "--tabColor", "#B07CF5" }, psi.ArgumentList);
    }

    [Fact]
    public void Pwsh_command_is_wrapped_and_keeps_shell_open()
    {
        var psi = WtCommandBuilder.Build(Base with { Shell = ShellKind.Pwsh, Command = "Get-Date" });

        Assert.Contains("--", psi.ArgumentList);
        var tail = psi.ArgumentList.SkipWhile(a => a != "--").ToList();
        Assert.Equal(new[] { "--", "pwsh.exe", "-NoLogo", "-NoExit", "-Command", "Get-Date" }, tail);
    }

    [Fact]
    public void Wsl_uses_cd_not_d_and_execs_login_shell_after_command()
    {
        var psi = WtCommandBuilder.Build(Base with
        {
            Shell = ShellKind.Wsl,
            Distro = "Ubuntu",
            WorkingDirectory = "~/anvil",
            Command = "claude --resume",
        });

        Assert.DoesNotContain("-d", psi.ArgumentList.Take(psi.ArgumentList.IndexOf("--")));
        var tail = psi.ArgumentList.SkipWhile(a => a != "--").ToList();
        Assert.Equal(new[] { "--", "wsl.exe", "-d", "Ubuntu", "--cd", "~/anvil", "--", "bash", "-lic", "claude --resume; exec bash -l" }, tail);
    }

    [Fact]
    public void Lock_title_adds_suppress_flag()
    {
        var psi = WtCommandBuilder.Build(Base with { LockTitle = true });
        Assert.Contains("--suppressApplicationTitle", psi.ArgumentList);
    }

    [Fact]
    public void Invalid_definition_throws()
    {
        Assert.Throws<ArgumentException>(() => WtCommandBuilder.Build(Base with { Name = "Bad Name" }));
    }

    [Fact]
    public void Arguments_with_spaces_are_kept_as_single_entries()
    {
        var psi = WtCommandBuilder.Build(Base with { Profile = "Windows PowerShell", WorkingDirectory = @"C:\My Stuff" });
        Assert.Contains("Windows PowerShell", psi.ArgumentList);
        Assert.Contains(@"C:\My Stuff", psi.ArgumentList);
    }

    [Fact]
    public void Executable_comes_from_the_terminal_host_and_arguments_do_not_change()
    {
        var host = WtCommandBuilder.Build(Base, @"C:\Users\me\AppData\Local\Switchboard\wt\wt.exe");
        var path = WtCommandBuilder.Build(Base);

        Assert.Equal(@"C:\Users\me\AppData\Local\Switchboard\wt\wt.exe", host.FileName);
        Assert.Equal(path.ArgumentList, host.ArgumentList);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_executable_throws(string executable)
    {
        Assert.Throws<ArgumentException>(() => WtCommandBuilder.Build(Base, executable));
    }
}
