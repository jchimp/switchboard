using System.Text.Json.Nodes;
using Switchboard.Core.WindowsTerminal;
using Xunit;

namespace Switchboard.Core.Tests;

public class WtAppearanceImportTests
{
    // Shaped like the installed (Store) WT file: JSONC, styled defaults, one styled profile.
    private const string Installed = """
        {
            // comments and trailing commas, as WT writes them
            "theme": "dark",
            "initialCols": 140,
            "useAcrylicInTabRow": true,
            "schemes": [ { "name": "Night", "background": "#000000" } ],
            "profiles": {
                "defaults": {
                    "font": { "face": "Cascadia Code", "size": 11 },
                    "colorScheme": "Night",
                    "commandline": "evil.exe",
                    "startingDirectory": "C:\\nope",
                },
                "list": [
                    { "guid": "{AAA}", "name": "Windows PowerShell", "opacity": 80, "useAcrylic": true,
                      "commandline": "powershell.exe -NoProfile", "tabColor": "#FF0000" },
                    { "guid": "{CCC}", "name": "Only in Store", "opacity": 50 },
                ],
            },
        }
        """;

    // Shaped like a fresh portable host file: unstyled, its own extra profile and scheme.
    private const string Host = """
        {
            "defaultProfile": "{BBB}",
            "schemes": [ { "name": "HostOnly", "background": "#111111" } ],
            "profiles": {
                "defaults": {},
                "list": [
                    { "guid": "{aaa}", "name": "Windows PowerShell", "commandline": "%SystemRoot%\\powershell.exe",
                      "padding": "4" },
                    { "guid": "{BBB}", "name": "Ubuntu", "source": "Microsoft.WSL" },
                ],
            },
        }
        """;

    private static JsonObject Merged(string installed = Installed, string host = Host)
    {
        var result = WtAppearanceImport.Merge(installed, host);
        Assert.True(result.Succeeded, result.Error);
        return JsonNode.Parse(result.Json!)!.AsObject();
    }

    private static JsonObject Profile(JsonObject root, string name) =>
        root["profiles"]!["list"]!.AsArray().OfType<JsonObject>().Single(p => (string?)p["name"] == name);

    [Fact]
    public void A_fresh_host_looks_unstyled_when_the_installed_wt_has_defaults()
    {
        Assert.True(WtAppearanceImport.HostLooksUnstyled(Installed, Host));
    }

    [Fact]
    public void A_styled_host_does_not_look_unstyled()
    {
        Assert.False(WtAppearanceImport.HostLooksUnstyled(Installed, Merged().ToJsonString()));
    }

    [Fact]
    public void Defaults_get_only_appearance_keys()
    {
        var defaults = Merged()["profiles"]!["defaults"]!.AsObject();

        Assert.Equal("Cascadia Code", (string?)defaults["font"]!["face"]);
        Assert.Equal("Night", (string?)defaults["colorScheme"]);
        Assert.False(defaults.ContainsKey("commandline"));
        Assert.False(defaults.ContainsKey("startingDirectory"));
    }

    [Fact]
    public void A_profile_matched_by_guid_ignoring_case_takes_the_installed_look()
    {
        var ps = Profile(Merged(), "Windows PowerShell");

        Assert.Equal(80, (int?)ps["opacity"]);
        Assert.Equal(true, (bool?)ps["useAcrylic"]);
    }

    [Fact]
    public void Overwrite_removes_appearance_keys_the_installed_profile_does_not_have()
    {
        Assert.False(Profile(Merged(), "Windows PowerShell").ContainsKey("padding"));
    }

    [Fact]
    public void Command_lines_and_tab_colors_never_move()
    {
        var ps = Profile(Merged(), "Windows PowerShell");

        Assert.Equal(@"%SystemRoot%\powershell.exe", (string?)ps["commandline"]);
        Assert.False(ps.ContainsKey("tabColor"));
    }

    [Fact]
    public void Profiles_only_in_the_host_are_left_alone_and_none_are_added()
    {
        var merged = Merged();
        var names = merged["profiles"]!["list"]!.AsArray().Select(p => (string?)p!["name"]).ToList();

        Assert.Equal(["Windows PowerShell", "Ubuntu"], names);
        Assert.False(Profile(merged, "Ubuntu").ContainsKey("opacity"));
        Assert.Equal("{BBB}", (string?)merged["defaultProfile"]);
    }

    [Fact]
    public void A_profile_without_a_guid_match_falls_back_to_its_name()
    {
        const string host = """{ "profiles": { "list": [ { "guid": "{ZZZ}", "name": "Only in Store" } ] } }""";
        Assert.Equal(50, (int?)Profile(Merged(host: host), "Only in Store")["opacity"]);
    }

    [Fact]
    public void Schemes_merge_by_name_so_host_only_schemes_survive()
    {
        var names = Merged()["schemes"]!.AsArray().Select(s => (string?)s!["name"]).ToHashSet();
        Assert.Equal(["HostOnly", "Night"], names);
    }

    [Fact]
    public void Root_window_keys_follow_the_installed_wt()
    {
        var merged = Merged(host: """{ "initialRows": 50, "profiles": [] }""");

        Assert.Equal("dark", (string?)merged["theme"]);
        Assert.Equal(140, (int?)merged["initialCols"]);
        Assert.False(merged.ContainsKey("initialRows"));
    }

    [Fact]
    public void A_bare_profile_array_in_the_host_becomes_the_object_shape()
    {
        var merged = Merged(host: """{ "profiles": [ { "name": "Ubuntu" } ] }""");

        Assert.Equal("Night", (string?)merged["profiles"]!["defaults"]!["colorScheme"]);
        Assert.Single(merged["profiles"]!["list"]!.AsArray());
    }

    [Fact]
    public void Importing_twice_gives_the_same_file()
    {
        var once = WtAppearanceImport.Merge(Installed, Host).Json!;
        var twice = WtAppearanceImport.Merge(Installed, once).Json!;
        Assert.Equal(once, twice);
    }

    [Fact]
    public void Duplicate_keys_return_an_error_instead_of_throwing()
    {
        // JsonNode.Parse accepts duplicates and throws on first read; WT lets the last one win.
        const string duplicated = """{ "profiles": { "defaults": { "fontSize": 10, "fontSize": 12 } } }""";

        Assert.False(WtAppearanceImport.Merge(duplicated, Host).Succeeded);
        Assert.False(WtAppearanceImport.Merge(Installed, duplicated).Succeeded);
        Assert.False(WtAppearanceImport.HostLooksUnstyled(duplicated, Host));
        Assert.False(WtAppearanceImport.HostLooksUnstyled(Installed, duplicated));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("\"text\"")]
    public void A_non_object_root_is_never_unstyled_and_never_throws(string json)
    {
        Assert.False(WtAppearanceImport.HostLooksUnstyled(json, Host));
        Assert.False(WtAppearanceImport.HostLooksUnstyled(Installed, json));
        Assert.False(WtAppearanceImport.Merge(json, Host).Succeeded);
    }

    [Fact]
    public void Nameless_schemes_are_skipped_so_importing_twice_adds_nothing()
    {
        const string installed = """{ "schemes": [ { "background": "#000000" }, { "name": "Night" } ] }""";

        var once = WtAppearanceImport.Merge(installed, Host).Json!;
        var twice = WtAppearanceImport.Merge(installed, once).Json!;

        Assert.Equal(once, twice);
        Assert.All(JsonNode.Parse(twice)!["schemes"]!.AsArray(), s => Assert.NotNull(s!["name"]));
    }

    [Fact]
    public void Themes_merge_by_name_like_schemes()
    {
        const string installed = """{ "themes": [ { "name": "Mine", "tab": { "background": "#111" } } ] }""";
        const string host = """{ "themes": [ { "name": "Mine", "tab": {} }, { "name": "HostTheme" } ], "profiles": [] }""";

        var themes = Merged(installed, host)["themes"]!.AsArray().OfType<JsonObject>().ToList();

        Assert.Equal(2, themes.Count);
        Assert.Equal("#111", (string?)themes.Single(x => (string?)x["name"] == "Mine")["tab"]!["background"]);
    }

    [Fact]
    public void An_installed_wt_without_defaults_clears_the_hosts_appearance_defaults()
    {
        const string host = """{ "profiles": { "defaults": { "fontSize": 14, "historySize": 9001 }, "list": [] } }""";

        var defaults = Merged("""{ "profiles": [] }""", host)["profiles"]!["defaults"]!.AsObject();

        Assert.False(defaults.ContainsKey("fontSize"));
        Assert.Equal(9001, (int?)defaults["historySize"]);
    }

    [Fact]
    public void Untouched_text_is_written_readably_not_escaped()
    {
        const string host = """{ "profiles": [ { "name": "Café", "commandline": "cmd /k \"a && b\"" } ] }""";

        var json = WtAppearanceImport.Merge(Installed, host).Json!;

        Assert.Contains("Café", json);
        Assert.Contains("a && b", json);
    }

    [Theory]
    [InlineData("{ not json", Host)]
    [InlineData(Installed, "[1, 2")]
    public void Malformed_input_returns_an_error_instead_of_throwing(string installed, string host)
    {
        var result = WtAppearanceImport.Merge(installed, host);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
    }
}
