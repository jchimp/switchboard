using System.IO;
using Microsoft.Extensions.Logging;
using Switchboard.Core.Registry;

namespace Switchboard.App.Services;

/// <summary>
/// The new-terminal launch path: create a definition from the quick-launch defaults
/// (auto-name, auto-color), register, launch. Milestone 3 put the picker in front of the
/// primary chord; the quiet chord still comes straight here (ADR-0017).
/// </summary>
public sealed class QuickLaunchService
{
    private readonly TerminalSession _session;
    private readonly ILogger<QuickLaunchService> _log;

    public QuickLaunchService(TerminalSession session, ILogger<QuickLaunchService> log)
    {
        _session = session;
        _log = log;
    }

    public async Task<TerminalDefinition> CreateAndLaunchAsync(
        string? profile = null, ShellKind? shell = null, string? distro = null,
        bool? persistent = null, string? workingDirectory = null)
    {
        // The last-used-directory fallback applies only when actually launching; an adopt
        // seed (BuildDefinition direct) describes a window that already exists somewhere.
        var def = BuildDefinition(profile, shell, distro, persistent,
            workingDirectory ?? _session.Registry.QuickLaunch.LastDirectory);
        await _session.AddTerminalAsync(def);
        await _session.LaunchAsync(def);
        return def;
    }

    /// <summary>
    /// The quick-launch defaults as a definition, without registering or launching anything.
    /// Used by "adopt as new…": the window already exists, so nothing here should launch it.
    /// </summary>
    internal TerminalDefinition BuildDefinition(
        string? profile = null, ShellKind? shell = null, string? distro = null,
        bool? persistent = null, string? workingDirectory = null)
    {
        var q = _session.Registry.QuickLaunch;
        return new TerminalDefinition
        {
            Name = NextName(q.NamePrefix),
            Color = ColorPalette.NextUnused(_session.Registry.Terminals),
            Profile = profile ?? q.DefaultProfile,
            Shell = shell ?? q.DefaultShell,
            Distro = distro ?? q.DefaultDistro,
            Persistent = persistent ?? q.PersistByDefault,
            WorkingDirectory = UsableDirectory(workingDirectory, shell ?? q.DefaultShell),
        };
    }

    /// <summary>
    /// A remembered directory may be gone by the next launch; dropping it must never fail
    /// the launch (R21). WSL paths are shell-native and can't be checked from here.
    /// </summary>
    private string? UsableDirectory(string? directory, ShellKind shell)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        if (shell != ShellKind.Wsl && !Directory.Exists(directory))
        {
            _log.LogWarning("Launch directory '{Directory}' does not exist; using the profile default", directory);
            return null;
        }

        return directory;
    }

    internal string NextName(string prefix)
    {
        var taken = _session.Registry.Terminals.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        for (var i = 1; ; i++)
        {
            var candidate = $"{prefix}{i}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
