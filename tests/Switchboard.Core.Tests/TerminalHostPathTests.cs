using Switchboard.Core.Launch;
using Xunit;

namespace Switchboard.Core.Tests;

public class TerminalHostPathTests
{
    private const string Folder = @"C:\Users\me\AppData\Local\Switchboard\wt";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"\")]
    public void No_folder_normalizes_to_null(string? folder)
    {
        Assert.Null(TerminalHostPath.Normalize(folder));
    }

    [Theory]
    [InlineData(@"  C:\Users\me\AppData\Local\Switchboard\wt\  ")]
    [InlineData(@"C:\Users\me\AppData\Local\Switchboard\wt\\")]
    [InlineData("C:/Users/me/AppData/Local/Switchboard/wt/")]
    public void Folder_is_trimmed_and_uses_backslashes(string folder)
    {
        Assert.Equal(Folder, TerminalHostPath.Normalize(folder));
    }

    [Fact]
    public void Without_a_folder_wt_comes_from_path_and_there_is_no_settings_file()
    {
        Assert.Equal("wt.exe", TerminalHostPath.WtExecutable(null));
        Assert.Null(TerminalHostPath.SettingsFile(null));
    }

    [Fact]
    public void With_a_folder_wt_and_settings_live_inside_it()
    {
        Assert.Equal(Folder + @"\wt.exe", TerminalHostPath.WtExecutable(Folder + @"\"));
        Assert.Equal(Folder + @"\settings\settings.json", TerminalHostPath.SettingsFile(Folder));
    }

    [Fact]
    public void Without_a_folder_every_terminal_process_is_the_host()
    {
        Assert.True(TerminalHostPath.IsHostProcess(null, @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_1.25\WindowsTerminal.exe"));
        Assert.True(TerminalHostPath.IsHostProcess("", null));
    }

    [Fact]
    public void Host_process_matches_ignoring_case()
    {
        Assert.True(TerminalHostPath.IsHostProcess(Folder, @"c:\users\ME\appdata\local\switchboard\WT\windowsterminal.exe"));
    }

    [Fact]
    public void The_store_install_is_not_the_host()
    {
        Assert.False(TerminalHostPath.IsHostProcess(Folder,
            @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_1.25.3131.0_x64__8wekyb3d8bbwe\WindowsTerminal.exe"));
    }

    [Fact]
    public void A_process_path_that_could_not_be_read_is_not_the_host()
    {
        Assert.False(TerminalHostPath.IsHostProcess(Folder, null));
        Assert.False(TerminalHostPath.IsHostProcess(Folder, ""));
    }

    [Fact]
    public void A_sibling_folder_with_a_shared_prefix_is_not_the_host()
    {
        Assert.False(TerminalHostPath.IsHostProcess(Folder, Folder + @"-old\WindowsTerminal.exe"));
    }
}
