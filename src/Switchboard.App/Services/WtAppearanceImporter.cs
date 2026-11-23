using System.IO;
using Microsoft.Extensions.Logging;
using Switchboard.Core.WindowsTerminal;
using Switchboard.Windows;

namespace Switchboard.App.Services;

/// <summary>What the settings page can say about importing appearance into the terminal host.</summary>
public enum AppearanceImportState
{
    /// <summary>No host folder, no host settings.json yet, or no installed WT settings to copy from.</summary>
    Unavailable,

    /// <summary>The host has no appearance defaults but the installed WT does: offer the import.</summary>
    Unstyled,

    /// <summary>Both files exist and the host already has its own look; re-import stays available.</summary>
    Available,
}

/// <summary>
/// Copies the installed Windows Terminal's look into the terminal host's settings.json
/// (ADR-0022). File work only — the merge rules are <see cref="WtAppearanceImport"/> in Core.
/// One-shot and user-triggered: it never runs on its own, because it overwrites the host's
/// appearance. WT reloads its settings file on change, so open terminals restyle at once.
/// </summary>
public sealed class WtAppearanceImporter
{
    private readonly TerminalHost _host;
    private readonly ILogger<WtAppearanceImporter> _log;

    public WtAppearanceImporter(TerminalHost host, ILogger<WtAppearanceImporter> log)
    {
        _host = host;
        _log = log;
    }

    /// <summary>Reads both files to decide what the page offers. Cheap; never on a timer.</summary>
    public AppearanceImportState CurrentState()
    {
        if (Paths() is not { } paths)
        {
            return AppearanceImportState.Unavailable;
        }

        var (installedPath, hostPath) = paths;

        try
        {
            return WtAppearanceImport.HostLooksUnstyled(File.ReadAllText(installedPath), File.ReadAllText(hostPath))
                ? AppearanceImportState.Unstyled
                : AppearanceImportState.Available;
        }
        catch (Exception ex)
        {
            // Runs from the settings page's constructor and RegistryChanged: a throw here would
            // stop the window opening or cut the event short for later subscribers.
            _log.LogWarning(ex, "Could not read WT settings to check the host's appearance");
            return AppearanceImportState.Unavailable;
        }
    }

    /// <summary>Runs the import. Returns a line for the settings page: what happened, or why not.</summary>
    public async Task<string> ImportAsync()
    {
        if (Paths() is not { } paths)
        {
            return "Nothing to import: set a terminal host folder, and run that Windows Terminal once so it creates its settings.";
        }

        var (installedPath, hostPath) = paths;

        try
        {
            var result = WtAppearanceImport.Merge(
                await File.ReadAllTextAsync(installedPath), await File.ReadAllTextAsync(hostPath));
            if (!result.Succeeded)
            {
                _log.LogWarning("Appearance import refused: {Error}", result.Error);
                return result.Error!; // Succeeded is false only when Error is set
            }

            // Back up only once: the first import drops the file's comments, so the backup worth
            // keeping is the original, not the previous import's output. Then write beside and
            // swap in one move, so WT's file watcher never sees a half-written file.
            var backup = hostPath + ".switchboard.bak";
            var tmp = hostPath + ".switchboard.tmp";
            if (!File.Exists(backup))
            {
                File.Copy(hostPath, backup);
            }

            await File.WriteAllTextAsync(tmp, result.Json);
            File.Move(tmp, hostPath, overwrite: true);

            _log.LogInformation("Imported WT appearance from {From} into {To}; {Count} profiles matched; backup {Backup}",
                installedPath, hostPath, result.ProfilesMatched, backup);
            return $"Imported. {result.ProfilesMatched} matching profile(s) restyled; open terminals update now. " +
                   $"The original file is kept as {Path.GetFileName(backup)}.";
        }
        catch (Exception ex)
        {
            // A button handler: report every failure on the page rather than let it escape.
            _log.LogError(ex, "Appearance import failed");
            return $"Import failed: {ex.Message}";
        }
    }

    private (string Installed, string Host)? Paths()
    {
        if (_host.SettingsFile is not { } hostPath || !File.Exists(hostPath))
        {
            return null;
        }

        return WtProfileProvider.InstalledSettingsPaths().FirstOrDefault(File.Exists) is { } installedPath
            ? (installedPath, hostPath)
            : null;
    }
}
