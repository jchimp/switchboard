using System.Text.Json;
using Switchboard.Core.Registry;

namespace Switchboard.Core.WindowsTerminal;

/// <summary>
/// What a Windows Terminal profile runs and looks like, as the terminal editor shows it.
/// Appearance values are effective (the profile's own, else <c>profiles.defaults</c>); the
/// command line is the profile's own only, since WT layers generated profiles' command lines
/// in ways the file does not show. Null means WT decides.
/// </summary>
public sealed record WtProfileDetail(
    string Name,
    string? Guid,
    string? Source,
    string? Commandline,
    string? StartingDirectory,
    string? FontFace,
    double? FontSize,
    string? ColorScheme,
    bool Hidden = false);

/// <summary>The profiles in one WT settings.json, and where that file is.</summary>
public sealed record WtProfileCatalog(string? SettingsPath, IReadOnlyList<WtProfileDetail> Profiles)
{
    public static WtProfileCatalog Empty { get; } = new(null, []);

    /// <summary>What the editor's dropdown offers: hidden profiles are WT's to show, not ours.</summary>
    public IReadOnlyList<string> VisibleNames => Profiles.Where(p => !p.Hidden).Select(p => p.Name).ToList();

    /// <summary>
    /// The profile <c>wt -p</c> would open for <paramref name="nameOrGuid"/>: by exact name
    /// (WT matches names exactly), or by guid, which <c>-p</c> also accepts. Hidden ones count.
    /// </summary>
    public WtProfileDetail? Find(string nameOrGuid)
    {
        var key = nameOrGuid.Trim();
        return key.Length == 0
            ? null
            : Profiles.FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.Ordinal))
              ?? Profiles.FirstOrDefault(p => string.Equals(p.Guid, key, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Reads profile details from WT's settings.json and decides the shell and WSL distro a
/// profile implies. Pure (invariant 1); hostile input yields an empty list, never an exception.
/// </summary>
public static class WtProfileReader
{
    // WT's fixed guids for its two built-in profiles; they carry no "source" to go by.
    private const string WindowsPowerShellGuid = "{61c54bbd-c2c6-5271-96e7-009a87ff44bf}";
    private const string CommandPromptGuid = "{0caa0dad-35be-5f56-a8ff-afceeeaa6101}";

    private static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>All profiles in file order, hidden ones flagged, with effective details.</summary>
    public static IReadOnlyList<WtProfileDetail> Read(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, Options);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("profiles", out var profiles))
            {
                return [];
            }

            var defaults = profiles.ValueKind == JsonValueKind.Object
                           && profiles.TryGetProperty("defaults", out var d) && d.ValueKind == JsonValueKind.Object
                ? d
                : default;
            var list = profiles.ValueKind switch
            {
                JsonValueKind.Array => profiles,
                JsonValueKind.Object when profiles.TryGetProperty("list", out var l)
                                          && l.ValueKind == JsonValueKind.Array => l,
                _ => default,
            };

            if (list.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<WtProfileDetail>();
            foreach (var entry in list.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object || Text(entry, "name") is not { } name)
                {
                    continue;
                }

                result.Add(new WtProfileDetail(
                    name,
                    Text(entry, "guid"),
                    Text(entry, "source"),
                    Text(entry, "commandline"),
                    StartingDirectory(entry) ?? StartingDirectory(defaults),
                    FontFace(entry) ?? FontFace(defaults),
                    FontSize(entry) ?? FontSize(defaults),
                    ColorScheme(entry) ?? ColorScheme(defaults),
                    entry.TryGetProperty("hidden", out var hidden) && hidden.ValueKind == JsonValueKind.True));
            }

            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// The shell a profile runs, or null when it is none of the four Switchboard wraps commands
    /// for (Git Bash, Azure, a custom program), so the user's own choice stands.
    /// </summary>
    public static ShellKind? InferShell(WtProfileDetail profile)
    {
        if (IsWslSource(profile.Source))
        {
            return ShellKind.Wsl;
        }

        if (profile.Source == "Windows.Terminal.PowershellCore")
        {
            return ShellKind.Pwsh;
        }

        if (profile.Commandline is { } cmd)
        {
            var (exe, _) = SplitCommandline(cmd);
            var file = exe[(exe.LastIndexOfAny(['\\', '/']) + 1)..].ToLowerInvariant();
            return file switch
            {
                "pwsh" or "pwsh.exe" => ShellKind.Pwsh,
                "powershell" or "powershell.exe" => ShellKind.WindowsPowerShell,
                "cmd" or "cmd.exe" => ShellKind.Cmd,
                "wsl" or "wsl.exe" => ShellKind.Wsl,
                _ => null,
            };
        }

        return profile.Guid?.ToLowerInvariant() switch
        {
            WindowsPowerShellGuid => ShellKind.WindowsPowerShell,
            CommandPromptGuid => ShellKind.Cmd,
            _ => null,
        };
    }

    /// <summary>
    /// The WSL distro a profile runs: <c>-d</c>/<c>--distribution</c> on its command line first,
    /// else, for a WSL-generated profile, its name (WT names those after the distro; a user
    /// rename breaks that, which is why the command line wins). Null = WSL's default distro.
    /// </summary>
    public static string? WslDistro(WtProfileDetail profile)
    {
        if (profile.Commandline is { } cmd)
        {
            var args = SplitCommandline(cmd).Args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] is "-d" or "--distribution")
                {
                    return args[i + 1].Trim('"');
                }
            }
        }

        return IsWslSource(profile.Source) ? profile.Name : null;
    }

    /// <summary>
    /// The shell and distro the editor shows for <paramref name="profile"/>, given the user's own
    /// choice. Derived every time from that baseline, never accumulated: typing through a name
    /// that briefly matches a profile (WPF auto-complete does this) and on to one that matches
    /// nothing lands back on the user's values.
    /// </summary>
    public static (ShellKind Shell, string? Distro) Resolve(
        WtProfileDetail? profile, ShellKind userShell, string? userDistro)
    {
        if (profile is null || InferShell(profile) is not { } shell)
        {
            return (userShell, userDistro);
        }

        return shell == ShellKind.Wsl ? (shell, WslDistro(profile)) : (shell, userDistro);
    }

    private static bool IsWslSource(string? source) => source is "Microsoft.WSL" or "Windows.Terminal.Wsl";

    /// <summary>The program and the rest: a quoted program runs to its closing quote (paths like
    /// <c>C:\Program Files\…</c> contain spaces), else to the first space.</summary>
    private static (string Exe, string Args) SplitCommandline(string commandline)
    {
        var s = commandline.Trim();
        if (s.StartsWith('"'))
        {
            var close = s.IndexOf('"', 1);
            return close < 0 ? (s[1..], string.Empty) : (s[1..close], s[(close + 1)..]);
        }

        var space = s.IndexOf(' ');
        return space < 0 ? (s, string.Empty) : (s[..space], s[(space + 1)..]);
    }

    private static string? Text(JsonElement obj, string key) =>
        obj.ValueKind == JsonValueKind.Object
        && obj.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
        && v.GetString() is { Length: > 0 } s
            ? s
            : null;

    /// <summary>An explicit <c>null</c> means "the launching process's folder" in WT.</summary>
    private static string? StartingDirectory(JsonElement obj) =>
        obj.ValueKind == JsonValueKind.Object
        && obj.TryGetProperty("startingDirectory", out var v) && v.ValueKind == JsonValueKind.Null
            ? "(inherited from the launching process)"
            : Text(obj, "startingDirectory");

    /// <summary>Newer WT allows a light/dark pair as well as a single scheme name.</summary>
    private static string? ColorScheme(JsonElement obj)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty("colorScheme", out var v))
        {
            return null;
        }

        return v.ValueKind == JsonValueKind.Object
            ? $"{Text(v, "light") ?? "?"} (light) / {Text(v, "dark") ?? "?"} (dark)"
            : Text(obj, "colorScheme");
    }

    /// <summary>WT 1.10+ nests font settings; older files use flat fontFace/fontSize.</summary>
    private static string? FontFace(JsonElement obj) =>
        (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty("font", out var font) ? Text(font, "face") : null)
        ?? Text(obj, "fontFace");

    private static double? FontSize(JsonElement obj)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (obj.TryGetProperty("font", out var font) && font.ValueKind == JsonValueKind.Object
            && font.TryGetProperty("size", out var nested) && nested.ValueKind == JsonValueKind.Number)
        {
            return nested.GetDouble();
        }

        return obj.TryGetProperty("fontSize", out var flat) && flat.ValueKind == JsonValueKind.Number
            ? flat.GetDouble()
            : null;
    }
}
