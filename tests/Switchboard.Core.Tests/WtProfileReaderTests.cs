using Switchboard.Core.Registry;
using Switchboard.Core.WindowsTerminal;
using Xunit;

namespace Switchboard.Core.Tests;

public class WtProfileReaderTests
{
    private const string Settings = """
        {
            // JSONC, like WT writes it
            "profiles": {
                "defaults": { "font": { "face": "Cascadia Code", "size": 11 }, "colorScheme": "Night",
                              "commandline": "should-not-leak.exe" },
                "list": [
                    { "guid": "{61c54bbd-c2c6-5271-96e7-009a87ff44bf}", "name": "Windows PowerShell",
                      "commandline": "%SystemRoot%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe",
                      "fontSize": 14, "colorScheme": "Campbell" },
                    { "guid": "{574e775e}", "name": "PowerShell", "source": "Windows.Terminal.PowershellCore",
                      "startingDirectory": null },
                    { "guid": "{2c4de342}", "name": "Ubuntu", "source": "Microsoft.WSL", "startingDirectory": "~",
                      "colorScheme": { "light": "One Half Light", "dark": "One Half Dark" } },
                    { "guid": "{hid}", "name": "Hidden one", "hidden": true },
                ],
            },
        }
        """;

    private static readonly WtProfileCatalog Catalog = new("settings.json", WtProfileReader.Read(Settings));

    private static WtProfileDetail Get(string name) => Catalog.Find(name)!;

    private static WtProfileDetail Profile(string? commandline = null, string? source = null, string name = "x") =>
        new(name, null, source, commandline, null, null, null, null);

    [Fact]
    public void Hidden_profiles_are_read_but_not_offered()
    {
        Assert.Equal(4, Catalog.Profiles.Count);
        Assert.Equal(["Windows PowerShell", "PowerShell", "Ubuntu"], Catalog.VisibleNames);
        Assert.True(Get("Hidden one").Hidden);
    }

    [Fact]
    public void Find_matches_exact_names_or_guids_and_ignores_blank()
    {
        Assert.Equal("Ubuntu", Catalog.Find("  Ubuntu ")!.Name);
        Assert.Null(Catalog.Find("ubuntu"));
        Assert.Equal("Ubuntu", Catalog.Find("{2C4DE342}")!.Name);
        Assert.Null(Catalog.Find(""));
    }

    [Fact]
    public void Appearance_values_take_the_profile_first_then_the_defaults()
    {
        var ps = Get("Windows PowerShell");
        Assert.Equal("Campbell", ps.ColorScheme);
        Assert.Equal(14, ps.FontSize);
        Assert.Equal("Cascadia Code", ps.FontFace);

        var pwsh = Get("PowerShell");
        Assert.Equal("Night", pwsh.ColorScheme);
        Assert.Equal(11, pwsh.FontSize);
    }

    [Fact]
    public void The_command_line_never_comes_from_the_defaults()
    {
        Assert.Null(Get("PowerShell").Commandline);
    }

    [Fact]
    public void A_light_dark_scheme_and_a_null_starting_directory_are_shown_as_such()
    {
        Assert.Equal("One Half Light (light) / One Half Dark (dark)", Get("Ubuntu").ColorScheme);
        Assert.Equal("(inherited from the launching process)", Get("PowerShell").StartingDirectory);
    }

    [Fact]
    public void Flat_font_keys_and_a_bare_profile_array_still_read()
    {
        var profiles = WtProfileReader.Read("""{ "profiles": [ { "name": "Old", "fontFace": "Consolas", "fontSize": 10 } ] }""");

        Assert.Equal("Consolas", profiles[0].FontFace);
        Assert.Equal(10, profiles[0].FontSize);
    }

    [Fact]
    public void Shells_are_inferred_from_source_command_line_or_built_in_guid()
    {
        Assert.Equal(ShellKind.WindowsPowerShell, WtProfileReader.InferShell(Get("Windows PowerShell")));
        Assert.Equal(ShellKind.Pwsh, WtProfileReader.InferShell(Get("PowerShell")));
        Assert.Equal(ShellKind.Wsl, WtProfileReader.InferShell(Get("Ubuntu")));
        Assert.Equal(ShellKind.Wsl, WtProfileReader.InferShell(Profile(source: "Windows.Terminal.Wsl")));

        var cmdByGuid = new WtProfileDetail("Command Prompt", "{0CAA0DAD-35BE-5F56-A8FF-AFCEEEAA6101}", null, null, null, null, null, null);
        Assert.Equal(ShellKind.Cmd, WtProfileReader.InferShell(cmdByGuid));
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\PowerShell\\7\\pwsh.exe\" -NoLogo", ShellKind.Pwsh)]
    [InlineData("cmd.exe /k vcvars64.bat", ShellKind.Cmd)]
    [InlineData("wsl.exe -d Debian", ShellKind.Wsl)]
    public void Command_lines_with_paths_quotes_and_arguments_still_infer(string commandline, ShellKind expected)
    {
        Assert.Equal(expected, WtProfileReader.InferShell(Profile(commandline)));
    }

    [Fact]
    public void Unknown_shells_infer_nothing()
    {
        Assert.Null(WtProfileReader.InferShell(Profile("C:\\Program Files\\Git\\bin\\bash.exe", "Git", "Git Bash")));
    }

    [Theory]
    [InlineData("wsl.exe -d Debian", null, "Debian")]
    [InlineData("wsl.exe --distribution \"Ubuntu-22.04\" --cd ~", null, "Ubuntu-22.04")]
    [InlineData(null, "Microsoft.WSL", "Work Ubuntu")]
    [InlineData("wsl.exe", null, null)]
    public void The_distro_comes_from_the_command_line_first_then_a_wsl_profile_name(
        string? commandline, string? source, string? expected)
    {
        Assert.Equal(expected, WtProfileReader.WslDistro(Profile(commandline, source, "Work Ubuntu")));
    }

    [Fact]
    public void Resolve_without_a_profile_keeps_the_users_choice()
    {
        Assert.Equal((ShellKind.Cmd, "Arch"), WtProfileReader.Resolve(null, ShellKind.Cmd, "Arch"));
    }

    [Fact]
    public void Resolve_with_an_unknown_shell_keeps_the_users_choice()
    {
        Assert.Equal((ShellKind.Wsl, "Arch"),
            WtProfileReader.Resolve(Profile("bash.exe", "Git"), ShellKind.Wsl, "Arch"));
    }

    [Fact]
    public void Resolve_with_a_wsl_profile_sets_its_distro_and_clears_a_stale_one()
    {
        Assert.Equal((ShellKind.Wsl, "Debian"),
            WtProfileReader.Resolve(Profile("wsl.exe -d Debian"), ShellKind.Wsl, "Ubuntu"));
        Assert.Equal((ShellKind.Wsl, (string?)null),
            WtProfileReader.Resolve(Profile("wsl.exe"), ShellKind.Wsl, "Ubuntu"));
    }

    [Fact]
    public void Typing_through_a_match_to_a_non_match_lands_back_on_the_users_values()
    {
        // WPF auto-complete: "W" becomes "Windows PowerShell", then "WS" matches nothing.
        var (passing, _) = WtProfileReader.Resolve(Get("Windows PowerShell"), ShellKind.Cmd, null);
        var final = WtProfileReader.Resolve(Catalog.Find("WSL-dev"), ShellKind.Cmd, null);

        Assert.Equal(ShellKind.WindowsPowerShell, passing);
        Assert.Equal((ShellKind.Cmd, (string?)null), final);
    }

    [Theory]
    [InlineData("not json {{")]
    [InlineData("[]")]
    [InlineData("""{ "profiles": 42 }""")]
    public void Bad_input_yields_an_empty_list(string json)
    {
        Assert.Empty(WtProfileReader.Read(json));
    }
}
