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
    private readonly TerminalHost _host;
    private readonly Dictionary<nint, TrackedWindow> _byHwnd = new();
    private readonly Dictionary<string, TrackedWindow> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _pending = new(StringComparer.Ordinal);

    /// <summary>
    /// The only events <see cref="OnWinEvent"/> handles, as narrow hook ranges. One hook from
    /// FOREGROUND to NAMECHANGE would also take LOCATIONCHANGE (every cursor and caret move in
    /// every app), STATECHANGE, menu and move/size events, each posted to the UI thread only to
    /// be dropped.
    /// </summary>
    private static readonly (uint Min, uint Max)[] HookRanges =
    [
        (NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND),
        (NativeMethods.EVENT_OBJECT_DESTROY, NativeMethods.EVENT_OBJECT_SHOW),
        (NativeMethods.EVENT_OBJECT_NAMECHANGE, NativeMethods.EVENT_OBJECT_NAMECHANGE),
    ];

    // Keep the delegate alive; the hooks hold a raw function pointer to it.
    private readonly NativeMethods.WinEventProc _callback;
    private readonly List<nint> _hooks = [];

    public WindowTracker(ILogger<WindowTracker> log, TimeProvider time, TerminalHost host)
    {
        _log = log;
        _time = time;
        _host = host;
        _callback = OnWinEvent;
    }

    public event Action<TrackedWindow>? WindowAppeared;
    public event Action<TrackedWindow, string>? TitleChanged;
    public event Action<TrackedWindow>? WindowClosed;
    public event Action<TrackedWindow?>? ForegroundChanged;

    /// <summary>
    /// The host's terminal windows that are not bound to any registry name. Windows from any
    /// other WT install are never listed (ADR-0021).
    /// </summary>
    public IReadOnlyList<nint> UnboundTerminalWindows() =>
        WindowInfo.EnumerateTerminalWindows().Where(h => !_byHwnd.ContainsKey(h) && _host.Owns(h)).ToList();

    /// <summary>
    /// The same set, with titles attached for display. Called on rail-open only — CLAUDE.md
    /// forbids polling EnumWindows on a timer, and this is exactly that scan.
    /// </summary>
    public IReadOnlyList<UnboundWindow> UnboundWindows() =>
        UnboundTerminalWindows().Select(h => new UnboundWindow(h, WindowInfo.GetTitle(h))).ToList();

    public IReadOnlyCollection<TrackedWindow> Tracked => _byName.Values;

    public TrackedWindow? Find(string name) => _byName.GetValueOrDefault(name);

    public void Start()
    {
        if (_hooks.Count != 0)
        {
            return;
        }

        foreach (var (min, max) in HookRanges)
        {
            var hook = NativeMethods.SetWinEventHook(
                min, max, 0, _callback, 0, 0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

            if (hook == 0)
            {
                // Half a hook set would track some events and silently miss others.
                Unhook();
                throw new InvalidOperationException($"SetWinEventHook failed for events 0x{min:X4}-0x{max:X4}.");
            }

            _hooks.Add(hook);
        }

        _log.LogInformation("WinEvent hooks installed ({Count})", _hooks.Count);
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
        if (!_host.Owns(hwnd))
        {
            throw new ArgumentException("Not a window of Switchboard's terminal host.", nameof(hwnd));
        }

        Unbind(name);
        var tw = new TrackedWindow(name, hwnd, WindowInfo.GetTitle(hwnd));
        _byHwnd[hwnd] = tw;
        _byName[name] = tw;
        _pending.Remove(name);
        _log.LogInformation("Bound {Name} → {Hwnd:X} ('{Title}')", name, hwnd, tw.LastTitle);
        WindowAppeared?.Invoke(tw);
    }

    /// <summary>
    /// Moves a live binding to a new name after a rename (ADR-0014). The HWND stays bound,
    /// so tracking never blinks; no events fire — the session coordinates the fallout.
    /// </summary>
    public void Rekey(string oldName, string newName)
    {
        if (!_byName.Remove(oldName, out var tw))
        {
            return;
        }

        var renamed = tw with { Name = newName };
        _byName[newName] = renamed;
        _byHwnd[tw.Hwnd] = renamed;
        _log.LogInformation("Rekeyed {Old} → {New} ({Hwnd:X})", oldName, newName, tw.Hwnd);
    }

    public void Unbind(string name)
    {
        if (_byName.Remove(name, out var tw))
        {
            _byHwnd.Remove(tw.Hwnd);
        }
    }

    /// <summary>
    /// Restart recovery: re-bind windows by the HWND they were bound to last run. Handles
    /// survive a manager restart while titles drift; a stale handle (reboot, the window closed,
    /// or a recycled handle now owned by another WT install) fails <see cref="TerminalHost.Owns"/>
    /// and is skipped (ADR-0002 amendment, ADR-0021). There is no title pass: it adopted the
    /// user's own one-off windows (ADR-0020).
    /// </summary>
    public int RecoverByHwnd(IEnumerable<(string Name, nint Hwnd)> lastKnown)
    {
        var bound = 0;
        foreach (var (name, hwnd) in lastKnown)
        {
            if (!_byName.ContainsKey(name) && !_byHwnd.ContainsKey(hwnd) && _host.Owns(hwnd))
            {
                Bind(name, hwnd);
                bound++;
            }
        }

        return bound;
    }

    public void Dispose() => Unhook();

    private void Unhook()
    {
        foreach (var hook in _hooks)
        {
            NativeMethods.UnhookWinEvent(hook);
        }

        _hooks.Clear();
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

        // Title match first: it is a string compare, while Owns queries the process. Only a
        // window titled exactly like a pending launch pays for the ownership check, which
        // stops a one-off Store WT window with the same title from taking the binding.
        var newTitle = WindowInfo.GetTitle(hwnd);
        if (_pending.ContainsKey(newTitle) && _host.Owns(hwnd))
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
