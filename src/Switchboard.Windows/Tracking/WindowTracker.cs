using Microsoft.Extensions.Logging;
using Switchboard.Windows.Interop;

namespace Switchboard.Windows.Tracking;

/// <summary>
/// Watches every Windows Terminal window on the desktop via a WinEvent hook and keeps
/// a name → HWND map. Association happens at launch: the caller registers a pending
/// name, and the first terminal window to appear whose title equals that name is bound
/// to it (ADR-0002). Everything after that is tracked by HWND, so later title changes
/// are free to be used for status.
/// </summary>
/// <remarks>
/// Must be constructed and used on a thread with a message loop (the WPF UI thread).
/// WinEvent callbacks arrive on that same thread, so no locking is needed on the maps.
/// </remarks>
public sealed class WindowTracker : IDisposable
{
    private static readonly TimeSpan PendingTimeout = TimeSpan.FromSeconds(15);

    private readonly ILogger<WindowTracker> _log;
    private readonly TimeProvider _time;
    private readonly Dictionary<nint, TrackedWindow> _byHwnd = new();
    private readonly Dictionary<string, TrackedWindow> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _pending = new(StringComparer.Ordinal);

    // Keep the delegate alive; the hook holds a raw function pointer to it.
    private readonly NativeMethods.WinEventProc _callback;
    private nint _hook;

    public WindowTracker(ILogger<WindowTracker> log, TimeProvider time)
    {
        _log = log;
        _time = time;
        _callback = OnWinEvent;
    }

    public event Action<TrackedWindow>? WindowAppeared;
    public event Action<TrackedWindow, string>? TitleChanged;
    public event Action<TrackedWindow>? WindowClosed;
    public event Action<TrackedWindow?>? ForegroundChanged;

    /// <summary>Terminal windows on the desktop that are not bound to any registry name.</summary>
    public IReadOnlyList<nint> UnboundTerminalWindows() =>
        WindowInfo.EnumerateTerminalWindows().Where(h => !_byHwnd.ContainsKey(h)).ToList();

    public IReadOnlyCollection<TrackedWindow> Tracked => _byName.Values;

    public TrackedWindow? Find(string name) => _byName.GetValueOrDefault(name);

    public void Start()
    {
        if (_hook != 0)
        {
            return;
        }

        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_OBJECT_NAMECHANGE,
            0, _callback, 0, 0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        if (_hook == 0)
        {
            throw new InvalidOperationException("SetWinEventHook failed.");
        }

        _log.LogInformation("WinEvent hook installed");
    }

    /// <summary>Call right before launching <paramref name="name"/> so the next matching window binds to it.</summary>
    public void ExpectWindow(string name) => _pending[name] = _time.GetUtcNow() + PendingTimeout;

    /// <summary>
    /// Reports the current foreground window through <see cref="ForegroundChanged"/>. The hook
    /// only fires on changes, so without this the startup foreground is invisible until the
    /// user switches windows. UI thread only, like everything else here.
    /// </summary>
    public void ProbeForeground() =>
        ForegroundChanged?.Invoke(_byHwnd.GetValueOrDefault(NativeMethods.GetForegroundWindow()));

    /// <summary>Manual binding, used by "adopt this window" in the rail and by restart recovery.</summary>
    public void Bind(string name, nint hwnd)
    {
        if (!WindowInfo.IsTerminalWindow(hwnd))
        {
            throw new ArgumentException("Not a Windows Terminal window.", nameof(hwnd));
        }

        Unbind(name);
        var tw = new TrackedWindow(name, hwnd, WindowInfo.GetTitle(hwnd));
        _byHwnd[hwnd] = tw;
        _byName[name] = tw;
        _pending.Remove(name);
        _log.LogInformation("Bound {Name} → {Hwnd:X} ('{Title}')", name, hwnd, tw.LastTitle);
        WindowAppeared?.Invoke(tw);
    }

    public void Unbind(string name)
    {
        if (_byName.Remove(name, out var tw))
        {
            _byHwnd.Remove(tw.Hwnd);
        }
    }

    /// <summary>
    /// Restart recovery: bind any visible terminal window whose title *starts with* a
    /// known name. Best effort; unmatched windows stay unbound and show in the rail.
    /// </summary>
    public int RecoverByTitle(IEnumerable<string> names)
    {
        var set = names.ToHashSet(StringComparer.Ordinal);
        var bound = 0;
        foreach (var hwnd in UnboundTerminalWindows())
        {
            var title = WindowInfo.GetTitle(hwnd);
            var match = set.FirstOrDefault(n => !_byName.ContainsKey(n) &&
                (title.Equals(n, StringComparison.Ordinal) || title.StartsWith(n + " ", StringComparison.Ordinal)));
            if (match is not null)
            {
                Bind(match, hwnd);
                bound++;
            }
        }

        return bound;
    }

    public void Dispose()
    {
        if (_hook != 0)
        {
            NativeMethods.UnhookWinEvent(_hook);
            _hook = 0;
        }
    }

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != NativeMethods.OBJID_WINDOW || idChild != NativeMethods.CHILDID_SELF || hwnd == 0)
        {
            return;
        }

        switch (eventType)
        {
            case NativeMethods.EVENT_OBJECT_DESTROY:
                if (_byHwnd.Remove(hwnd, out var gone))
                {
                    _byName.Remove(gone.Name);
                    _log.LogInformation("{Name} closed", gone.Name);
                    WindowClosed?.Invoke(gone);
                }

                break;

            case NativeMethods.EVENT_OBJECT_SHOW:
            case NativeMethods.EVENT_OBJECT_NAMECHANGE:
                HandleShowOrRename(hwnd);
                break;

            case NativeMethods.EVENT_SYSTEM_FOREGROUND:
                ForegroundChanged?.Invoke(_byHwnd.GetValueOrDefault(hwnd));
                break;
        }
    }

    private void HandleShowOrRename(nint hwnd)
    {
        if (_byHwnd.TryGetValue(hwnd, out var known))
        {
            var title = WindowInfo.GetTitle(hwnd);
            if (title != known.LastTitle)
            {
                var updated = known with { LastTitle = title };
                _byHwnd[hwnd] = updated;
                _byName[known.Name] = updated;
                TitleChanged?.Invoke(updated, title);
            }

            return;
        }

        if (_pending.Count == 0 || !WindowInfo.IsTerminalWindow(hwnd))
        {
            return;
        }

        PruneExpiredPending();
        var newTitle = WindowInfo.GetTitle(hwnd);
        if (_pending.ContainsKey(newTitle))
        {
            Bind(newTitle, hwnd);
        }
    }

    private void PruneExpiredPending()
    {
        var now = _time.GetUtcNow();
        foreach (var (name, deadline) in _pending.ToList())
        {
            if (deadline < now)
            {
                _pending.Remove(name);
                _log.LogWarning("Gave up waiting for a window titled '{Name}'", name);
            }
        }
    }
}
