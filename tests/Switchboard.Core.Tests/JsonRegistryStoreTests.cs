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
        var store = new JsonRegistryStore(PathFor("switchboard.json"));
        var file = await store.LoadAsync();
        Assert.Empty(file.Terminals);
        Assert.Equal(RegistryFile.CurrentVersion, file.Version);
    }

    [Fact]
    public async Task Missing_file_is_reported_until_the_first_save()
    {
        var store = new JsonRegistryStore(PathFor("switchboard.json"));
        var file = await store.LoadAsync();
        Assert.True(store.FoundNoFile);

        await store.SaveAsync(file);
        Assert.False(store.FoundNoFile);

        await store.LoadAsync();
        Assert.False(store.FoundNoFile);
    }

    [Fact]
    public async Task Legacy_file_is_not_a_first_run()
    {
        var legacy = PathFor("registry.json");
        await new JsonRegistryStore(legacy).SaveAsync(RegistryFile.Empty);

        var store = new JsonRegistryStore(PathFor("switchboard.json"), legacy);
        await store.LoadAsync();
        Assert.False(store.FoundNoFile);
        Assert.True(store.AdoptedLegacyFile);
    }

    [Fact]
    public async Task Round_trips_and_keeps_a_backup()
    {
        var store = new JsonRegistryStore(PathFor("switchboard.json"));
        var first = new RegistryFile
        {
            Terminals = [new TerminalDefinition { Name = "anvil", Color = "#B07CF5", Profile = "Ubuntu", Shell = ShellKind.Wsl, Distro = "Ubuntu", JumpKey = 1 }],
        };

        await store.SaveAsync(first);
        await store.SaveAsync(first with { Terminals = [.. first.Terminals, new TerminalDefinition { Name = "docker", Color = "#22B8CF", Profile = "Ubuntu" }] });

        var loaded = await store.LoadAsync();
        Assert.Equal(2, loaded.Terminals.Count);
        Assert.Equal(ShellKind.Wsl, loaded.Terminals[0].Shell);
        Assert.True(File.Exists(PathFor("switchboard.json.bak")));
        Assert.False(File.Exists(PathFor("switchboard.json.tmp")));
    }

    [Fact]
    public async Task Old_files_without_new_settings_load_with_defaults()
    {
        var path = PathFor("switchboard.json");
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
        Assert.Equal(176, loaded.Strip.RailTopHeightDips);
        Assert.Equal(TopWidthMode.Centered, loaded.Strip.TopWidth);
        Assert.True(loaded.ThumbnailMotionEnabled);
        Assert.True(loaded.Strip.ClickMinimizesFocused);
        Assert.Null(loaded.TerminalHost.Folder);
        Assert.Equal(new SettingsWindowLayout(), loaded.SettingsWindow);
    }

    [Fact]
    public async Task Settings_window_layout_round_trips()
    {
        var store = new JsonRegistryStore(PathFor("switchboard.json"));
        var layout = new SettingsWindowLayout { Width = 1500, Height = 900, EditorWidth = 520 };
        await store.SaveAsync(new RegistryFile { SettingsWindow = layout });

        Assert.Equal(layout, (await store.LoadAsync()).SettingsWindow);
    }

    [Fact]
    public async Task Terminal_host_folder_round_trips_unexpanded()
    {
        // Stored as typed: expansion happens at use, so the file stays portable across users.
        var store = new JsonRegistryStore(PathFor("switchboard.json"));
        await store.SaveAsync(new RegistryFile
        {
            TerminalHost = new TerminalHostSettings { Folder = @"%LOCALAPPDATA%\Switchboard\wt" },
        });

        var loaded = await store.LoadAsync();
        Assert.Equal(@"%LOCALAPPDATA%\Switchboard\wt", loaded.TerminalHost.Folder);
    }

    [Fact]
    public async Task Terminal_host_folder_is_written_under_terminal_host()
    {
        var path = PathFor("switchboard.json");
        await new JsonRegistryStore(path).SaveAsync(new RegistryFile
        {
            TerminalHost = new TerminalHostSettings { Folder = @"C:\wt" },
        });

        var json = await File.ReadAllTextAsync(path);
        Assert.Contains("\"terminalHost\"", json);
        Assert.Contains("\"folder\"", json);
    }

    [Fact]
    public async Task Click_minimizes_focused_can_be_turned_off_and_round_trips()
    {
        var store = new JsonRegistryStore(PathFor("switchboard.json"));
        await store.SaveAsync(new RegistryFile
        {
            Strip = new StripSettings { ClickMinimizesFocused = false },
        });

        var loaded = await store.LoadAsync();
        Assert.False(loaded.Strip.ClickMinimizesFocused);
    }

    [Fact]
    public async Task Top_width_mode_round_trips()
    {
        var store = new JsonRegistryStore(PathFor("switchboard.json"));
        await store.SaveAsync(new RegistryFile
        {
            Strip = new StripSettings { TopWidth = TopWidthMode.Full },
        });

        var loaded = await store.LoadAsync();
        Assert.Equal(TopWidthMode.Full, loaded.Strip.TopWidth);
    }

    [Fact]
    public async Task Last_hwnd_round_trips_and_defaults_to_null()
    {
        var store = new JsonRegistryStore(PathFor("switchboard.json"));
        await store.SaveAsync(new RegistryFile
        {
            Terminals = [new TerminalDefinition { Name = "anvil", Color = "#B07CF5", Profile = "Ubuntu", LastHwnd = 0x1234 }],
        });

        var loaded = await store.LoadAsync();
        Assert.Equal(0x1234, loaded.Terminals[0].LastHwnd);
        Assert.Null(new TerminalDefinition { Name = "x", Color = "#000000", Profile = "p" }.LastHwnd);
    }

    [Fact]
    public async Task Last_directory_survives_a_save_and_reload()
    {
        var store = new JsonRegistryStore(PathFor("switchboard.json"));
        await store.SaveAsync(new RegistryFile
        {
            QuickLaunch = new QuickLaunchSettings { LastDirectory = @"C:\src" },
        });

        var loaded = await store.LoadAsync();
        Assert.Equal(@"C:\src", loaded.QuickLaunch.LastDirectory);
        Assert.Null(new QuickLaunchSettings().LastDirectory);
    }

    [Fact]
    public async Task Taskbar_and_auto_hide_settings_round_trip()
    {
        var store = new JsonRegistryStore(PathFor("switchboard.json"));
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
    public async Task Legacy_registry_file_is_adopted_when_the_new_name_is_absent()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(PathFor("registry.json"), """
            { "version": 1, "terminals": [ { "name": "anvil", "color": "#B07CF5", "profile": "Ubuntu" } ] }
            """);

        var store = new JsonRegistryStore(PathFor("switchboard.json"), PathFor("registry.json"));
        var loaded = await store.LoadAsync();

        Assert.Equal("anvil", Assert.Single(loaded.Terminals).Name);
        Assert.True(store.AdoptedLegacyFile);
    }

    [Fact]
    public async Task Saving_after_adoption_writes_the_new_name_and_leaves_the_old_file()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(PathFor("registry.json"), """
            { "version": 1, "terminals": [ { "name": "anvil", "color": "#B07CF5", "profile": "Ubuntu" } ] }
            """);

        var store = new JsonRegistryStore(PathFor("switchboard.json"), PathFor("registry.json"));
        await store.SaveAsync(await store.LoadAsync());

        Assert.True(File.Exists(PathFor("switchboard.json")));
        Assert.True(File.Exists(PathFor("registry.json")));
        Assert.False(store.AdoptedLegacyFile);

        var reopened = new JsonRegistryStore(PathFor("switchboard.json"), PathFor("registry.json"));
        Assert.Equal("anvil", Assert.Single((await reopened.LoadAsync()).Terminals).Name);
        Assert.False(reopened.AdoptedLegacyFile);
    }

    [Fact]
    public async Task The_new_file_wins_when_both_exist()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(PathFor("registry.json"), """
            { "version": 1, "terminals": [ { "name": "old", "color": "#000000", "profile": "p" } ] }
            """);
        await File.WriteAllTextAsync(PathFor("switchboard.json"), """
            { "version": 1, "terminals": [ { "name": "new", "color": "#000000", "profile": "p" } ] }
            """);

        var store = new JsonRegistryStore(PathFor("switchboard.json"), PathFor("registry.json"));
        var loaded = await store.LoadAsync();

        Assert.Equal("new", Assert.Single(loaded.Terminals).Name);
        Assert.False(store.AdoptedLegacyFile);
    }

    [Fact]
    public async Task Rejects_duplicate_names()
    {
        var path = PathFor("switchboard.json");
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
        var path = PathFor("switchboard.json");
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
