using Switchboard.Core.WindowsTerminal;
using Xunit;

namespace Switchboard.Core.Tests;

public class WtSettingsReaderTests
{
    [Fact]
    public void Comments_and_trailing_commas_are_tolerated()
    {
        const string json = """
            {
                // Windows Terminal writes JSONC
                "profiles": {
                    "defaults": {},
                    "list": [
                        { "name": "PowerShell", "guid": "{guid-1}", }, // trailing comma
                    ],
                },
            }
            """;
        var profiles = WtSettingsReader.Parse(json);
        Assert.Equal([new WtProfile("PowerShell", "{guid-1}")], profiles);
    }

    [Fact]
    public void Profiles_as_a_bare_array_and_as_a_list_object_both_parse()
    {
        const string bare = """{ "profiles": [ { "name": "A" }, { "name": "B" } ] }""";
        const string wrapped = """{ "profiles": { "list": [ { "name": "A" }, { "name": "B" } ] } }""";
        Assert.Equal(WtSettingsReader.Parse(bare), WtSettingsReader.Parse(wrapped));
        Assert.Equal(2, WtSettingsReader.Parse(bare).Count);
    }

    [Fact]
    public void Hidden_profiles_are_skipped()
    {
        const string json = """
            { "profiles": [
                { "name": "Visible" },
                { "name": "Hidden one", "hidden": true },
                { "name": "Explicitly shown", "hidden": false }
            ] }
            """;
        Assert.Equal(["Visible", "Explicitly shown"], WtSettingsReader.Parse(json).Select(p => p.Name));
    }

    [Fact]
    public void Nameless_entries_are_skipped()
    {
        const string json = """{ "profiles": [ { "guid": "{g}" }, { "name": "" }, { "name": "Ok" } ] }""";
        Assert.Equal(["Ok"], WtSettingsReader.Parse(json).Select(p => p.Name));
    }

    [Fact]
    public void Garbage_json_yields_an_empty_list()
    {
        Assert.Empty(WtSettingsReader.Parse("not json at all {{{"));
        Assert.Empty(WtSettingsReader.Parse(""));
        Assert.Empty(WtSettingsReader.Parse("""{ "profiles": 42 }"""));
        Assert.Empty(WtSettingsReader.Parse("""{ "noProfiles": [] }"""));
    }

    [Fact]
    public void Order_matches_the_file()
    {
        const string json = """{ "profiles": [ { "name": "Z" }, { "name": "A" }, { "name": "M" } ] }""";
        Assert.Equal(["Z", "A", "M"], WtSettingsReader.Parse(json).Select(p => p.Name));
    }
}
