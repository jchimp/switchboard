using Switchboard.Core.Registry;

namespace Switchboard.App.Services;

/// <summary>
/// The new-terminal hotkey. Milestone 1 behaviour: no UI, just create a definition
/// from the quick-launch defaults, auto-name, auto-color, register, launch.
/// Milestone 3 puts the profile picker in front of this.
/// </summary>
public sealed class QuickLaunchService
{
    private readonly TerminalSession _session;

    public QuickLaunchService(TerminalSession session)
    {
        _session = session;
    }

    public async Task<TerminalDefinition> CreateAndLaunchAsync(string? profile = null, ShellKind? shell = null, string? distro = null)
    {
        var def = BuildDefinition(profile, shell, distro);
        await _session.AddTerminalAsync(def);
        await _session.LaunchAsync(def);
        return def;
    }

    /// <summary>
    /// The quick-launch defaults as a definition, without registering or launching anything.
    /// Used by "adopt as new…": the window already exists, so nothing here should launch it.
    /// </summary>
    internal TerminalDefinition BuildDefinition(string? profile = null, ShellKind? shell = null, string? distro = null, bool? persistent = null)
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
        };
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
