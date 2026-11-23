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

        foreach (var path in InstalledSettingsPaths())
        {
            yield return path;
        }
    }

    /// <summary>
    /// Where the installed (not the host's) Windows Terminal keeps settings.json, in the order
    /// to try. Shared with the appearance import (ADR-0022) so the two never disagree.
    /// </summary>
    public static IEnumerable<string> InstalledSettingsPaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, @"Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json");
        yield return Path.Combine(local, @"Packages\Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe\LocalState\settings.json");
        yield return Path.Combine(local, @"Microsoft\Windows Terminal\settings.json"); // unpackaged
    }

    /// <summary>Visible profiles in WT's order; empty when no settings file exists or parses.</summary>
    public IReadOnlyList<WtProfile> Load()
    {
        if (ReadSettingsFile() is not { } file)
        {
            _log.LogWarning("No Windows Terminal settings.json found; picker offers the default profile only");
            return [];
        }

        var profiles = WtSettingsReader.Parse(file.Text);
        if (profiles.Count == 0)
        {
            _log.LogWarning("WT settings at {Path} yielded no profiles", file.Path);
        }

        return profiles;
    }

    /// <summary>
    /// Profiles with what each one runs and looks like, plus the file they came from, for the
    /// terminal editor. Same file as <see cref="Load"/>: the host's when one is set.
    /// </summary>
    public WtProfileCatalog LoadDetails() =>
        ReadSettingsFile() is { } file
            ? new WtProfileCatalog(file.Path, WtProfileReader.Read(file.Text))
            : WtProfileCatalog.Empty;

    private (string Path, string Text)? ReadSettingsFile()
    {
        foreach (var path in CandidatePaths())
        {
            try
            {
                if (File.Exists(path))
                {
                    return (path, File.ReadAllText(path));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.LogWarning(ex, "Could not read WT settings at {Path}", path);
            }
        }

        return null;
    }
}

