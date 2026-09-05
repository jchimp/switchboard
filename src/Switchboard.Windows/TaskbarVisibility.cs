using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Switchboard.Windows.Interop;

namespace Switchboard.Windows;

/// <summary>
/// Hides and restores taskbar buttons of windows we do not own via <c>ITaskbarList</c> (ADR-0009).
/// The window itself is untouched: still visible, still in Alt-Tab. Explorer restarts recreate
/// every button, so callers re-apply on the <see cref="TaskbarCreatedMessage"/> broadcast.
/// UI thread only: the COM object is single-threaded, which invariant 8 already guarantees.
/// </summary>
public sealed class TaskbarVisibility : IDisposable
{
    private readonly ILogger<TaskbarVisibility> _log;
    private NativeMethods.ITaskbarList? _taskbar;
    private bool _initFailed;

    public TaskbarVisibility(ILogger<TaskbarVisibility> log)
    {
        _log = log;
        TaskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");
    }

    /// <summary>The shell broadcast that means every hidden button just came back.</summary>
    public int TaskbarCreatedMessage { get; }

    public void Hide(nint hwnd) => Call(hwnd, static (t, h) => t.DeleteTab(h), nameof(Hide));

    public void Show(nint hwnd) => Call(hwnd, static (t, h) => t.AddTab(h), nameof(Show));

    private void Call(nint hwnd, Action<NativeMethods.ITaskbarList, nint> action, string operation)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
        {
            return;
        }

        var taskbar = GetOrCreate();
        if (taskbar is null)
        {
            return;
        }

        try
        {
            action(taskbar, hwnd);
        }
        catch (COMException ex)
        {
            _log.LogDebug(ex, "ITaskbarList.{Operation} failed for {Hwnd:X}", operation, hwnd);
        }
    }

    private NativeMethods.ITaskbarList? GetOrCreate()
    {
        if (_taskbar is not null || _initFailed)
        {
            return _taskbar;
        }

        try
        {
            // CS8600/nullability: GetTypeFromCLSID never returns null for a registered shell CLSID,
            // and CreateInstance of a coclass never returns null; failures throw instead.
            var type = Type.GetTypeFromCLSID(NativeMethods.CLSID_TaskbarList)!;
            _taskbar = (NativeMethods.ITaskbarList)Activator.CreateInstance(type)!;
            _taskbar.HrInit();
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or MissingMethodException)
        {
            _initFailed = true;
            _taskbar = null;
            _log.LogWarning(ex, "TaskbarList COM object unavailable; taskbar hiding disabled");
        }

        return _taskbar;
    }

    public void Dispose()
    {
        if (_taskbar is not null)
        {
            Marshal.FinalReleaseComObject(_taskbar);
            _taskbar = null;
        }
    }
}
