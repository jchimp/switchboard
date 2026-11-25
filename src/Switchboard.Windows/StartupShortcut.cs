using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Switchboard.Windows.Interop;

namespace Switchboard.Windows;

/// <summary>What the Startup folder says about starting Switchboard at sign-in (ADR-0026).</summary>
/// <param name="Exists">A <c>Switchboard.lnk</c> is in the user's Startup folder.</param>
/// <param name="Target">The exe the shortcut starts, or null when it is missing or unreadable.</param>
/// <param name="PointsAtThisExe">The target is the exe that is running now.</param>
/// <param name="TurnedOffInWindows">
/// Windows' Startup apps page (or Task Manager) has the entry turned off, so the shortcut is
/// there but does not run.
/// </param>
public sealed record StartupShortcutStatus(bool Exists, string? Target, bool PointsAtThisExe, bool TurnedOffInWindows);

/// <summary>
/// The Startup-folder shortcut that starts Switchboard at sign-in (ADR-0026). The shortcut is
/// the state: nothing about it is kept in switchboard.json, so install.ps1 -StartAtLogin,
/// uninstall.ps1, a hand-deleted shortcut and this class always agree. Same path and name as
/// the install script. UI thread only: the ShellLink COM object is created per call.
/// </summary>
public sealed class StartupShortcut
{
    public const string FileName = "Switchboard.lnk";

    /// <summary>Where Windows records the Startup apps on/off switch for Startup-folder entries.</summary>
    private const string ApprovedKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    private readonly ILogger<StartupShortcut> _log;

    public StartupShortcut(ILogger<StartupShortcut> log) => _log = log;

    public static string LinkPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), FileName);

    /// <summary>Reads the shortcut and the Windows on/off switch. Never throws.</summary>
    public StartupShortcutStatus Read(string exePath)
    {
        var path = LinkPath;
        if (!File.Exists(path))
        {
            return new StartupShortcutStatus(false, null, false, false);
        }

        var target = ReadTarget(path);
        var pointsHere = target is not null && SamePath(target, exePath);
        return new StartupShortcutStatus(true, target, pointsHere, IsTurnedOffInWindows());
    }

    /// <summary>Writes the shortcut so it starts <paramref name="exePath"/>. False on failure (logged).</summary>
    public bool Enable(string exePath)
    {
        var path = LinkPath;
        object? link = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); // ! safe: LinkPath is a full file path.
            link = CreateShellLink();
            var shellLink = (NativeMethods.IShellLinkW)link;
            shellLink.SetPath(exePath);
            shellLink.SetWorkingDirectory(Path.GetDirectoryName(exePath) ?? string.Empty);
            shellLink.SetDescription("Switchboard");
            ((IPersistFile)link).Save(path, true);
            _log.LogInformation("Startup shortcut written: {Path} -> {Target}", path, exePath);
            return true;
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or InvalidCastException)
        {
            _log.LogWarning(ex, "Could not write the Startup shortcut {Path}", path);
            return false;
        }
        finally
        {
            Release(link);
        }
    }

    /// <summary>Deletes the shortcut, whatever it points at: the user asked for no start at sign-in.</summary>
    public bool Disable()
    {
        var path = LinkPath;
        try
        {
            File.Delete(path);
            _log.LogInformation("Startup shortcut removed: {Path}", path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.LogWarning(ex, "Could not remove the Startup shortcut {Path}", path);
            return false;
        }
    }

    private string? ReadTarget(string path)
    {
        object? link = null;
        try
        {
            link = CreateShellLink();
            ((IPersistFile)link).Load(path, NativeMethods.STGM_READ);
            var target = new StringBuilder(NativeMethods.MAX_PATH);
            ((NativeMethods.IShellLinkW)link).GetPath(target, target.Capacity, 0, 0);
            return target.Length == 0 ? null : target.ToString();
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or InvalidCastException)
        {
            _log.LogDebug(ex, "Could not read the Startup shortcut {Path}", path);
            return null;
        }
        finally
        {
            Release(link);
        }
    }

    /// <summary>
    /// The first byte of the value is odd when the entry is turned off (02 = on, 03 = off).
    /// No value means Windows has never been told, which is on.
    /// </summary>
    private bool IsTurnedOffInWindows()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            return key?.GetValue(FileName) is byte[] { Length: > 0 } value && (value[0] & 1) == 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _log.LogDebug(ex, "Could not read the Startup apps switch");
            return false;
        }
    }

    private static object CreateShellLink()
    {
        var type = Type.GetTypeFromCLSID(NativeMethods.CLSID_ShellLink, throwOnError: true)!; // ! safe: throwOnError.
        return Activator.CreateInstance(type)
            ?? throw new COMException("ShellLink could not be created.");
    }

    private static void Release(object? link)
    {
        if (link is not null && Marshal.IsComObject(link))
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
