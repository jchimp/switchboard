using Switchboard.Core.Registry;
using Xunit;

namespace Switchboard.Core.Tests;

public sealed class JsonRegistryStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "switchboard-tests-" + Guid.NewGuid().ToString("N"));

    private string PathFor(string name) => Path.Combine(_dir, name);

    [Fact]
    public async Task Missing_file_loads_as_empty()
    {
        var store = new JsonRegistryStore(PathFor("registry.json"));
        var file = await store.LoadAsync();
        Assert.Empty(file.Terminals);
        Assert.Equal(RegistryFile.CurrentVersion, file.Version);
    }

    [Fact]
    public async Task Round_trips_and_keeps_a_backup()
    {
        var store = new JsonRegistryStore(PathFor("registry.json"));
        var first = new RegistryFile
        {
            Terminals = [new TerminalDefinition { Name = "anvil", Color = "#B07CF5", Profile = "Ubuntu", Shell = ShellKind.Wsl, Distro = "Ubuntu", JumpKey = 1 }],
        };

        await store.SaveAsync(first);
        await store.SaveAsync(first with { Terminals = [.. first.Terminals, new TerminalDefinition { Name = "docker", Color = "#22B8CF", Profile = "Ubuntu" }] });

        var loaded = await store.LoadAsync();
        Assert.Equal(2, loaded.Terminals.Count);
        Assert.Equal(ShellKind.Wsl, loaded.Terminals[0].Shell);
        Assert.True(File.Exists(PathFor("registry.json.bak")));
        Assert.False(File.Exists(PathFor("registry.json.tmp")));
    }

    [Fact]
    public async Task Old_files_without_new_settings_load_with_defaults()
    {
        var path = PathFor("registry.json");
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(path, """
            { "version": 1, "strip": { "edge": "top", "thickness": 8 }, "terminals": [] }
            """);

        var loaded = await new JsonRegistryStore(path).LoadAsync();
        Assert.False(loaded.HideFromTaskbar);
        Assert.False(loaded.Strip.AutoHide);
        Assert.Equal(2, loaded.Strip.HiddenThickness);
        Assert.Equal(800, loaded.Strip.CollapseDelayMs);
        Assert.Equal(236, loaded.Strip.RailSizeDips);
        Assert.Equal(190, loaded.Strip.RailTopHeightDips);
        Assert.True(loaded.ThumbnailMotionEnabled);
    }

    [Fact]
    public async Task Last_hwnd_round_trips_and_defaults_to_null()
    {
        var store = new JsonRegistryStore(PathFor("registry.json"));
        await store.SaveAsync(new RegistryFile
        {
            Terminals = [new TerminalDefinition { Name = "anvil", Color = "#B07CF5", Profile = "Ubuntu", LastHwnd = 0x1234 }],
        });

        var loaded = await store.LoadAsync();
        Assert.Equal(0x1234, loaded.Terminals[0].LastHwnd);
        Assert.Null(new TerminalDefinition { Name = "x", Color = "#000000", Profile = "p" }.LastHwnd);
    }

    [Fact]
    public async Task Taskbar_and_auto_hide_settings_round_trip()
    {
        var store = new JsonRegistryStore(PathFor("registry.json"));
        await store.SaveAsync(new RegistryFile
        {
            HideFromTaskbar = true,
            Strip = new StripSettings { AutoHide = true, HiddenThickness = 3, CollapseDelayMs = 500 },
        });

        var loaded = await store.LoadAsync();
        Assert.True(loaded.HideFromTaskbar);
        Assert.True(loaded.Strip.AutoHide);
        Assert.Equal(3, loaded.Strip.HiddenThickness);
        Assert.Equal(500, loaded.Strip.CollapseDelayMs);
    }

    [Fact]
    public async Task Rejects_duplicate_names()
    {
        var path = PathFor("registry.json");
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(path, """
            { "version": 1, "terminals": [
              { "name": "a", "color": "#000000", "profile": "p" },
              { "name": "a", "color": "#000000", "profile": "p" } ] }
            """);

        await Assert.ThrowsAsync<InvalidDataException>(() => new JsonRegistryStore(path).LoadAsync());
    }

    [Fact]
    public async Task Rejects_future_versions()
    {
        var path = PathFor("registry.json");
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(path, """{ "version": 99, "terminals": [] }""");

        await Assert.ThrowsAsync<InvalidDataException>(() => new JsonRegistryStore(path).LoadAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
