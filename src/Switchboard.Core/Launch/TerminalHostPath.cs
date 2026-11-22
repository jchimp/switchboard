namespace Switchboard.Core.Launch;

/// <summary>
/// Path rules for the private terminal host (ADR-0021). Plain string work on Windows paths,
/// not <see cref="System.IO.Path"/>, so the rules test the same on any OS (invariant 1). The
/// caller expands environment variables before passing a folder in.
/// </summary>
public static class TerminalHostPath
{
    /// <summary>The WT process every window of an install belongs to.</summary>
    public const string HostProcessFile = "WindowsTerminal.exe";

    /// <summary>The folder trimmed of whitespace and trailing separators, or null when unset.</summary>
    public static string? Normalize(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        var trimmed = folder.Trim().Replace('/', '\\').TrimEnd('\\');
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>The <c>wt.exe</c> to run: the host's when a folder is set, else the one on PATH.</summary>
    public static string WtExecutable(string? folder) =>
        Normalize(folder) is { } f ? $@"{f}\{WtCommandBuilder.Executable}" : WtCommandBuilder.Executable;

    /// <summary>A portable WT keeps its settings beside the binaries; null when no folder is set.</summary>
    public static string? SettingsFile(string? folder) =>
        Normalize(folder) is { } f ? $@"{f}\settings\settings.json" : null;

    /// <summary>
    /// True when the process at <paramref name="processPath"/> is the host's WT. With no folder
    /// set, every WT process qualifies; with one set, an unknown path never does — a window
    /// we cannot verify is not ours.
    /// </summary>
    public static bool IsHostProcess(string? folder, string? processPath)
    {
        if (Normalize(folder) is not { } f)
        {
            return true;
        }

        return !string.IsNullOrEmpty(processPath) && string.Equals(
            processPath.Replace('/', '\\'), $@"{f}\{HostProcessFile}", StringComparison.OrdinalIgnoreCase);
    }
}
