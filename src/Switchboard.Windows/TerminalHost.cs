using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Switchboard.Core.Launch;
using Switchboard.Windows.Interop;

namespace Switchboard.Windows;

/// <summary>
/// Switchboard's private Windows Terminal (ADR-0021): which <c>wt.exe</c> to launch, and
/// whether a window belongs to it. A WT window run by any other install is never ours, so the
/// user's own terminals are invisible to binding, adoption and import.
/// </summary>
/// <remarks>
/// UI thread only, like the tracker that asks. The App's <c>TerminalSession</c> sets
/// <see cref="Folder"/> whenever the registry changes. There is no pid → path cache: the check
/// runs on binds and on the rail's unbound scan, never on the per-title-change path, so one
/// process query per call is cheap and there is no pid reuse to get wrong.
/// </remarks>
public sealed class TerminalHost
{
    private readonly ILogger<TerminalHost> _log;
    private string? _folder;

    public TerminalHost(ILogger<TerminalHost> log)
    {
        _log = log;
    }

    /// <summary>
    /// The host folder with environment variables expanded, or null for "<c>wt.exe</c> on PATH,
    /// any WT window counts". Set it to the raw registry value; expansion happens here.
    /// </summary>
    public string? Folder
    {
        get => _folder;
        set
        {
            var next = TerminalHostPath.Normalize(value is null ? null : Environment.ExpandEnvironmentVariables(value));
            if (next == _folder)
            {
                return;
            }

            _folder = next;
            _log.LogInformation("Terminal host: {Folder}", next ?? "wt.exe on PATH (any WT window)");
        }
    }

    public string WtExecutable => TerminalHostPath.WtExecutable(_folder);

    /// <summary>The host's own settings.json, or null when no host folder is set.</summary>
    public string? SettingsFile => TerminalHostPath.SettingsFile(_folder);

    /// <summary>True when a host folder is set but holds no <c>wt.exe</c>; launches would fail.</summary>
    public bool IsMissing => IsMissingAt(_folder);

    /// <summary>
    /// The same check for a folder as typed (environment variables unexpanded), so the
    /// settings page can validate while the user types, before anything is saved.
    /// </summary>
    public static bool IsMissingAt(string? folder) =>
        TerminalHostPath.Normalize(folder is null ? null : Environment.ExpandEnvironmentVariables(folder)) is { } f
        && !File.Exists(TerminalHostPath.WtExecutable(f));

    /// <summary>True when <paramref name="hwnd"/> is a WT window run by the host's <c>WindowsTerminal.exe</c>.</summary>
    public bool Owns(nint hwnd)
    {
        if (!WindowInfo.IsTerminalWindow(hwnd))
        {
            return false;
        }

        return _folder is null || TerminalHostPath.IsHostProcess(_folder, ProcessPathOf(hwnd));
    }

    private string? ProcessPathOf(nint hwnd)
    {
        _ = NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
        {
            return null;
        }

        var process = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == 0)
        {
            // Typically an elevated WT. Not ours either way, but say why it was skipped.
            _log.LogWarning("Could not open process {Pid} for {Hwnd:X} (error {Error}); treating it as not ours",
                pid, hwnd, Marshal.GetLastWin32Error());
            return null;
        }

        try
        {
            var size = 1024u;
            var path = new StringBuilder((int)size);
            if (NativeMethods.QueryFullProcessImageName(process, 0, path, ref size))
            {
                return path.ToString();
            }

            _log.LogWarning("Could not read the image path of process {Pid} (error {Error})", pid, Marshal.GetLastWin32Error());
            return null;
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }
}
