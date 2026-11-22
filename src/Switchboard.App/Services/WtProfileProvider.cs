using System.IO;
using Microsoft.Extensions.Logging;
using Switchboard.Core.WindowsTerminal;
using Switchboard.Windows;

namespace Switchboard.App.Services;

/// <summary>
/// Finds and reads Windows Terminal's own settings.json for the picker's profile row
/// (ADR-0017). Read-only, re-read on every picker open — cheap, and hot edits to WT's
/// settings just work. Path resolution lives here, out of Core (invariant 1;
/// <see cref="RegistryPaths"/> is the precedent).
/// </summary>
public sealed class WtProfileProvider
{
    private readonly ILogger<WtProfileProvider> _log;
    private readonly TerminalHost _host;

    public WtProfileProvider(ILogger<WtProfileProvider> log, TerminalHost host)
    {
        _log = log;
        _host = host;
    }

    private IEnumerable<string> CandidatePaths()
    {
        // With a private host, only its profiles exist for launches; offering the Store WT's
        // would let the picker name a profile the host does not have (ADR-0021).
        if (_host.SettingsFile is { } hostSettings)
        {
            yield return hostSettings;
            yield break;
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, @"Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json");
        yield return Path.Combine(local, @"Packages\Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe\LocalState\settings.json");
        yield return Path.Combine(local, @"Microsoft\Windows Terminal\settings.json"); // unpackaged
    }

    /// <summary>Visible profiles in WT's order; empty when no settings file exists or parses.</summary>
    public IReadOnlyList<WtProfile> Load()
    {
        foreach (var path in CandidatePaths())
        {
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var profiles = WtSettingsReader.Parse(File.ReadAllText(path));
                if (profiles.Count == 0)
                {
                    _log.LogWarning("WT settings at {Path} yielded no profiles", path);
                }

                return profiles;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.LogWarning(ex, "Could not read WT settings at {Path}", path);
            }
        }

        _log.LogWarning("No Windows Terminal settings.json found; picker offers the default profile only");
        return [];
    }
}
