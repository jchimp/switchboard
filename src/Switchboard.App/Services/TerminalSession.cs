using Microsoft.Extensions.Logging;
using Switchboard.Core.Focus;
using Switchboard.Core.Launch;
using Switchboard.Core.Registry;
using Switchboard.Core.Status;
using Switchboard.Windows;
using Switchboard.Windows.Tracking;

namespace Switchboard.App.Services;

/// <summary>One row of an import: a definition to register, and the live window to bind it to (R27).</summary>
public sealed record ImportRequest(TerminalDefinition Definition, nint Hwnd);

/// <summary>An import row that could not land, with a message fit to show beside it.</summary>
public sealed record ImportFailure(string Name, string Reason);

/// <summary>
/// The one object that knows about the registry, the live windows, and their status
/// at the same time. Everything UI-facing reads from here; nothing UI-facing talks to
/// the tracker or colorizer directly.
/// </summary>
public sealed class TerminalSession : IDisposable
{
    // Mirrors the tracker's pending-window timeout: a focus request older than this is stale.
    private static readonly TimeSpan FocusOnAppearTimeout = TimeSpan.FromSeconds(15);

    // Covers the store's tmp-write → bak-copy → rename burst so we don't reload our own saves.
    private static readonly TimeSpan SelfWriteMute = TimeSpan.FromSeconds(2);

    // Taps closer together than this walk deeper into the same cycle session (ADR-0015).
    private static readonly TimeSpan CycleSettle = TimeSpan.FromMilliseconds(1500);

    private readonly IRegistryStore _store;
    private readonly ITerminalLauncher _launcher;
    private readonly WindowTracker _tracker;
    private readonly FrameColorizer _colorizer;
    private readonly TaskbarVisibility _taskbar;
    private readonly RegistryWatcher _watcher;
    private readonly WindowSampler _sampler;
    private readonly MotionSampler _motion = new();
    private readonly ILogger<TerminalSession> _log;
    private readonly TimeProvider _time;
    private readonly System.Windows.Threading.Dispatcher _dispatcher;
    private readonly System.Windows.Threading.DispatcherTimer _tick;
    private readonly Dictionary<string, DateTimeOffset> _focusOnAppear = new(StringComparer.Ordinal);
    private readonly MruList _mru = new();
    private readonly System.Windows.Threading.DispatcherTimer _cycleTimer;
    private StatusEngine _status;
    private string? _foregroundName;
    private List<string>? _cycleSnapshot;
    private int _cycleIndex;
    private string? _cycleTarget;

    public TerminalSession(
        IRegistryStore store, ITerminalLauncher launcher, WindowTracker tracker,
        FrameColorizer colorizer, TaskbarVisibility taskbar, RegistryWatcher watcher,
        WindowSampler sampler, TimeProvider time, ILogger<TerminalSession> log)
    {
        _store = store;
        _launcher = launcher;
        _tracker = tracker;
        _colorizer = colorizer;
        _taskbar = taskbar;
        _watcher = watcher;
        _sampler = sampler;
        _log = log;
        _time = time;
        _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        _status = new StatusEngine(time, StatusRule.Defaults);
        _tick = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tick.Tick += (_, _) =>
        {
            _status.Tick();
            SampleMotion(); // rides the existing 1 s tick — no new timer (ADR-0013)
        };
        _cycleTimer = new System.Windows.Threading.DispatcherTimer { Interval = CycleSettle };
        _cycleTimer.Tick += (_, _) => CommitCycle();
    }

    public RegistryFile Registry { get; private set; } = RegistryFile.Empty;

    /// <summary>Fired on the UI thread whenever a terminal's state or presence changes.</summary>
    public event Action<string, TerminalState>? StateChanged;

    public event Action? RegistryChanged;

    /// <summary>Fired when a tracked terminal gains or loses the foreground (null = none of ours).</summary>
    public event Action<string?>? ForegroundChanged;

    public string? ForegroundName => _foregroundName;

    /// <summary>
    /// Terminal names most-recent-first; runtime-only, empty after a restart. The overlay
    /// switcher preselects from here and cycle-recent walks it (ADR-0015).
    /// </summary>
    public IReadOnlyList<string> RecentOrder => _mru.Order;

    /// <summary>
    /// When this returns true, motion sampling skips the tick. The composition root wires
    /// it to "a slide animation is running": PrintWindow costs a few ms on the UI thread
    /// and would stutter the 16 ms animation frames (ADR-0013).
    /// </summary>
    public Func<bool>? MotionSamplingPaused { get; set; }

    public async Task InitializeAsync()
    {
        Registry = await _store.LoadAsync();

        // The store reads the pre-rename file name once; convert it now rather than at the
        // next save, so the watcher below has the current file to watch from the first run.
        if (_store.AdoptedLegacyFile)
        {
            await PersistAsync();
            _log.LogInformation("Adopted the legacy registry file; wrote switchboard.json");
        }

        _status = new StatusEngine(_time, Registry.GlobalStatusRules);
        _status.StateChanged += (name, state) => StateChanged?.Invoke(name, state);

        _tracker.WindowAppeared += OnWindowAppeared;
        _tracker.TitleChanged += OnTitleChanged;
        _tracker.WindowClosed += OnWindowClosed;
        _tracker.ForegroundChanged += OnForegroundChanged;
        _tracker.Start();

        // HWND first: handles survive a manager restart while titles drift; stale handles
        // fail validation and fall through to the title pass (ADR-0002 amendment).
        var byHwnd = _tracker.RecoverByHwnd(Registry.Terminals
            .Select(t => (t.Name, Hwnd: (nint)t.LastHwnd.GetValueOrDefault()))
            .Where(p => p.Hwnd != 0)
            .ToList());
        var byTitle = _tracker.RecoverByTitle(Registry.Terminals.Select(t => t.Name));
        _log.LogInformation("Recovered {ByHwnd} windows by handle, {ByTitle} by title", byHwnd, byTitle);
        _tracker.ProbeForeground();
        ReapplyTaskbarVisibility();

        // Watcher events arrive on a timer thread; everything downstream (tracker maps,
        // status engine) is UI-thread-only (invariant 8), so marshal before reloading.
        _watcher.Changed += () => _dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await ReloadRegistryAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Registry hot-reload failed");
            }
        });
        _watcher.Start();
        _tick.Start();
    }

    public TerminalDefinition? Definition(string name) =>
        Registry.Terminals.FirstOrDefault(t => t.Name == name);

    /// <summary>The bound window's HWND, or 0 when the terminal isn't running. Rail thumbnails key on this.</summary>
    public nint HwndOf(string name) => _tracker.Find(name)?.Hwnd ?? 0;

    /// <summary>
    /// Windows Terminal windows on the desktop not bound to any registry name. Call only
    /// when the rail actually needs the list (on open) — this is an EnumWindows scan.
    /// </summary>
    public IReadOnlyList<UnboundWindow> UnboundWindows() => _tracker.UnboundWindows();

    /// <summary>
    /// Registered terminals the rail can offer as an adopt target: not already bound to a
    /// live window (R7). A closed, persistent terminal is exactly this; a running one isn't.
    /// </summary>
    public IReadOnlyList<TerminalDefinition> AdoptableTerminals() =>
        Registry.Terminals.Where(t => _tracker.Find(t.Name) is null).ToList();

    /// <summary>
    /// Binds an unbound window to an existing registry name. Never steals focus — this is
    /// the tracker's manual-bind path, the same one restart recovery uses (ADR-0002).
    /// </summary>
    public bool AdoptAsync(string name, nint hwnd)
    {
        try
        {
            _tracker.Bind(name, hwnd);
            return true;
        }
        catch (ArgumentException ex)
        {
            // The window closed between the rail listing it and the click landing.
            _log.LogWarning(ex, "Adopt failed for '{Name}'", name);
            return false;
        }
    }

    /// <summary>Registers a brand-new definition and binds it to an already-running window in one step.</summary>
    public async Task<bool> AdoptAsNewAsync(TerminalDefinition def, nint hwnd)
    {
        await AddTerminalAsync(def);
        return AdoptAsync(def.Name, hwnd);
    }

    public TerminalState StateOf(string name) => _status.StateOf(name);

    /// <summary>Focus if running, launch if not. The single entry point for "jump to X".</summary>
    public async Task ActivateAsync(string name)
    {
        var def = Definition(name);
        if (def is null)
        {
            _log.LogWarning("Activate: '{Name}' is not in the registry", name);
            return;
        }

        if (_tracker.Find(name) is { } live)
        {
            WindowFocuser.Focus(live.Hwnd);
            return;
        }

        await LaunchAsync(def);
    }

    public async Task LaunchAsync(TerminalDefinition def)
    {
        _tracker.ExpectWindow(def.Name);

        // A freshly spawned wt.exe has no foreground rights and opens behind us; bring it
        // forward once it binds. Only launches ask for this — recovery/adopt binds must not
        // steal focus.
        _focusOnAppear[def.Name] = _time.GetUtcNow() + FocusOnAppearTimeout;
        await _launcher.LaunchAsync(def);
    }

    public Task ActivateByJumpKeyAsync(int digit)
    {
        var def = Registry.Terminals.FirstOrDefault(t => t.JumpKey == digit)
               ?? Registry.Terminals.ElementAtOrDefault(digit - 1);
        return def is null ? Task.CompletedTask : ActivateAsync(def.Name);
    }

    public Task ActivateAttentionAsync()
    {
        var def = Registry.Terminals.FirstOrDefault(t => _status.StateOf(t.Name) == TerminalState.Attention);
        return def is null ? Task.CompletedTask : ActivateAsync(def.Name);
    }

    /// <summary>
    /// One tap switches to the previous terminal; taps within <see cref="CycleSettle"/> walk
    /// deeper into a snapshot frozen at the first tap, so the list doesn't reshuffle under
    /// the walk. Only the terminal the walk lands on gets touched, when the session settles
    /// (ADR-0015).
    /// </summary>
    public Task CycleRecentAsync()
    {
        if (_cycleSnapshot is null)
        {
            if (_mru.Order.Count == 0)
            {
                return Task.CompletedTask;
            }

            _cycleSnapshot = [.. _mru.Order];
            // From a tracked terminal the front entry is the terminal itself, so the first
            // tap must land one deeper; from anywhere else it lands on the front entry.
            _cycleIndex = _cycleSnapshot[0] == _foregroundName ? 0 : -1;
        }

        // Closed or unregistered entries are skipped rather than relaunched: a walk must
        // never stall behind a 15 s launch-and-bind.
        for (var step = 0; step < _cycleSnapshot.Count; step++)
        {
            _cycleIndex = (_cycleIndex + 1) % _cycleSnapshot.Count;
            var name = _cycleSnapshot[_cycleIndex];
            if (Definition(name) is null || _tracker.Find(name) is null)
            {
                continue;
            }

            _cycleTimer.Stop();
            _cycleTimer.Start();
            _cycleTarget = name;
            return ActivateAsync(name);
        }

        AbortCycle();
        return Task.CompletedTask;
    }

    private void CommitCycle()
    {
        _cycleTimer.Stop();
        if (_cycleTarget is { } landed)
        {
            _mru.Touch(landed);
        }

        _cycleSnapshot = null;
        _cycleTarget = null;
    }

    private void AbortCycle()
    {
        _cycleTimer.Stop();
        _cycleSnapshot = null;
        _cycleTarget = null;
    }

    public async Task AddTerminalAsync(TerminalDefinition def)
    {
        if (def.Validate() is { } error)
        {
            throw new ArgumentException(error, nameof(def));
        }

        Registry = Registry with { Terminals = [.. Registry.Terminals, def] };
        await PersistAsync();
        RegistryChanged?.Invoke();
    }

    /// <summary>
    /// Applies a rename/recolor from the rail (ADR-0014). The window stays bound by HWND
    /// through a rename: tracker rekeyed, status entry moved (Attention survives), frame
    /// repainted. Throws <see cref="ArgumentException"/> with a user-facing message when
    /// validation fails; the popup shows it inline.
    /// </summary>
    public async Task UpdateTerminalAsync(string oldName, TerminalDefinition updated)
    {
        if (Definition(oldName) is null)
        {
            return; // removed underneath the popup (hot-reload); nothing to update
        }

        if (updated.Validate() is { } error)
        {
            throw new ArgumentException(error);
        }

        if (updated.Name != oldName && Registry.Terminals.Any(t => t.Name == updated.Name))
        {
            throw new ArgumentException($"Name '{updated.Name}' is already in use.");
        }

        Registry = Registry with
        {
            Terminals = Registry.Terminals.Select(t => t.Name == oldName ? updated : t).ToList(),
        };

        if (updated.Name != oldName)
        {
            _tracker.Rekey(oldName, updated.Name);
            _status.Renamed(oldName, updated);
            _motion.Forget(oldName); // the new name starts from a fresh pixel baseline
            if (_foregroundName == oldName)
            {
                _foregroundName = updated.Name;
            }

            _mru.Rename(oldName, updated.Name);
            if (_cycleSnapshot is { } snapshot && snapshot.IndexOf(oldName) is >= 0 and var at)
            {
                snapshot[at] = updated.Name; // a rename mid-walk must not break the chain
            }

            if (_cycleTarget == oldName)
            {
                _cycleTarget = updated.Name;
            }
        }

        if (_tracker.Find(updated.Name) is { } live)
        {
            _colorizer.Apply(live.Hwnd, updated.Color, focused: updated.Name == _foregroundName);
        }

        await PersistAsync();
        RegistryChanged?.Invoke();
        _log.LogInformation("Updated terminal '{Old}' → '{New}'", oldName, updated.Name);
    }

    public async Task RemoveTerminalAsync(string name)
    {
        Registry = Registry with { Terminals = Registry.Terminals.Where(t => t.Name != name).ToList() };
        _mru.Forget(name);
        if (_tracker.Find(name) is { } live)
        {
            _taskbar.Show(live.Hwnd); // the window outlives us; give its button back
        }

        _tracker.Unbind(name);
        await PersistAsync();
        RegistryChanged?.Invoke();
    }

    /// <summary>
    /// Applies everything on the settings pages except the terminal list (ADR-0018): strip,
    /// hotkeys, global status rules, and the two root flags. Terminals are deliberately
    /// carried over from the current registry rather than taken from <paramref name="updated"/>,
    /// so a settings edit can never stomp a concurrent rename or import.
    /// </summary>
    /// <remarks>
    /// Numbers are clamped through <see cref="SettingsBounds"/> before they land: a slider can
    /// otherwise produce a strip with nothing left to hover.
    /// </remarks>
    public async Task ApplySettingsAsync(RegistryFile updated)
    {
        Registry = updated with
        {
            Terminals = Registry.Terminals,
            Strip = SettingsBounds.Clamp(updated.Strip),
        };

        _status.ReplaceGlobalRules(Registry.GlobalStatusRules);
        await PersistAsync();
        RegistryChanged?.Invoke();
    }

    /// <summary>
    /// Reorders the registry, which is the order the strip, rail and switcher render in.
    /// Jump keys are an explicit field and do not move with a row.
    /// </summary>
    /// <remarks>
    /// Names not in <paramref name="namesInOrder"/> keep their relative order at the end, so a
    /// terminal added underneath this call (hot-reload, quick launch) survives the reorder.
    /// </remarks>
    public async Task ReorderTerminalsAsync(IReadOnlyList<string> namesInOrder)
    {
        var rank = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < namesInOrder.Count; i++)
        {
            rank[namesInOrder[i]] = i;
        }

        var reordered = Registry.Terminals
            .Select((t, index) => (Terminal: t, Key: rank.TryGetValue(t.Name, out var r) ? r : namesInOrder.Count + index))
            .OrderBy(x => x.Key)
            .Select(x => x.Terminal)
            .ToList();

        if (reordered.Select(t => t.Name).SequenceEqual(Registry.Terminals.Select(t => t.Name), StringComparer.Ordinal))
        {
            return;
        }

        Registry = Registry with { Terminals = reordered };
        await PersistAsync();
        RegistryChanged?.Invoke();
    }

    /// <summary>
    /// Registers and binds several already-running windows in one step (R27). One save and one
    /// <see cref="RegistryChanged"/> for the whole batch, unlike calling
    /// <see cref="AdoptAsNewAsync"/> per window.
    /// </summary>
    /// <returns>
    /// The rows that could not be imported, with a user-facing reason. A window that closed
    /// between the scan and the click fails alone; the rest of the batch still lands.
    /// </returns>
    public async Task<IReadOnlyList<ImportFailure>> ImportAsync(IReadOnlyList<ImportRequest> batch)
    {
        var failures = new List<ImportFailure>();
        var accepted = new List<ImportRequest>();
        var taken = Registry.Terminals.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var request in batch)
        {
            if (request.Definition.Validate() is { } error)
            {
                failures.Add(new ImportFailure(request.Definition.Name, error));
                continue;
            }

            if (!taken.Add(request.Definition.Name))
            {
                failures.Add(new ImportFailure(request.Definition.Name, $"Name '{request.Definition.Name}' is already in use."));
                continue;
            }

            accepted.Add(request);
        }

        if (accepted.Count == 0)
        {
            return failures;
        }

        // Register every accepted row first: Bind raises WindowAppeared, and the handler needs
        // to find the definition to colorize and hide the taskbar button.
        Registry = Registry with { Terminals = [.. Registry.Terminals, .. accepted.Select(a => a.Definition)] };

        var bound = new List<string>();
        foreach (var request in accepted)
        {
            try
            {
                _tracker.Bind(request.Definition.Name, request.Hwnd);
                bound.Add(request.Definition.Name);
            }
            catch (ArgumentException ex)
            {
                _log.LogWarning(ex, "Import failed for '{Name}'", request.Definition.Name);
                failures.Add(new ImportFailure(request.Definition.Name, "That window is gone; refresh the list."));
            }
        }

        // A window that vanished mid-import leaves no registry row behind.
        var lost = accepted.Select(a => a.Definition.Name).Except(bound, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        if (lost.Count > 0)
        {
            Registry = Registry with { Terminals = Registry.Terminals.Where(t => !lost.Contains(t.Name)).ToList() };
        }

        await PersistAsync();
        RegistryChanged?.Invoke();
        _log.LogInformation("Imported {Count} window(s), {Failed} failed", bound.Count, failures.Count);
        return failures;
    }

    /// <summary>
    /// Re-reads switchboard.json and applies adds, removes, and edits to live state (ADR-0008).
    /// A file that fails to load keeps the current in-memory registry — hand-edits are
    /// hostile input and must never take the session down.
    /// </summary>
    public async Task ReloadRegistryAsync()
    {
        RegistryFile fresh;
        try
        {
            fresh = await _store.LoadAsync();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "switchboard.json changed but did not load; keeping the current registry");
            return;
        }

        var removed = Registry.Terminals.Select(t => t.Name)
            .Except(fresh.Terminals.Select(t => t.Name), StringComparer.Ordinal)
            .ToList();

        Registry = fresh;
        _status.ReplaceGlobalRules(fresh.GlobalStatusRules);
        _mru.Prune(fresh.Terminals.Select(t => t.Name).ToHashSet(StringComparer.Ordinal));

        foreach (var name in removed)
        {
            if (_tracker.Find(name) is { } live)
            {
                _taskbar.Show(live.Hwnd);
            }

            _tracker.Unbind(name);
            _status.WindowGone(name);
        }

        _tracker.RecoverByTitle(fresh.Terminals.Select(t => t.Name));
        ReapplyColors();
        ReapplyTaskbarVisibility();
        RegistryChanged?.Invoke();
        _log.LogInformation("Registry reloaded: {Count} terminals", fresh.Terminals.Count);
    }

    /// <summary>
    /// Remembers the picker's launch directory (R21, ADR-0017). Quiet-save like
    /// <see cref="RememberHwnd"/>: no RegistryChanged — nothing UI-visible changed, and the
    /// registry stays the only durable store (invariant 3).
    /// </summary>
    public Task RememberLastDirectoryAsync(string directory)
    {
        if (Registry.QuickLaunch.LastDirectory == directory)
        {
            return Task.CompletedTask;
        }

        Registry = Registry with { QuickLaunch = Registry.QuickLaunch with { LastDirectory = directory } };
        return SaveQuietlyAsync();
    }

    private Task PersistAsync()
    {
        _watcher.MuteFor(SelfWriteMute);
        return _store.SaveAsync(Registry);
    }

    /// <summary>Hands every tracked window its default frame color back; used on quit.</summary>
    public void ResetColors()
    {
        foreach (var tw in _tracker.Tracked)
        {
            _colorizer.Reset(tw.Hwnd);
        }
    }

    /// <summary>
    /// Hides or restores every tracked window's taskbar button per the current setting (ADR-0009).
    /// Called on init, on registry reload (covers toggling the flag), and when Explorer restarts.
    /// </summary>
    public void ReapplyTaskbarVisibility()
    {
        foreach (var tw in _tracker.Tracked)
        {
            if (Registry.HideFromTaskbar)
            {
                _taskbar.Hide(tw.Hwnd);
            }
            else
            {
                _taskbar.Show(tw.Hwnd);
            }
        }
    }

    /// <summary>An Explorer restart wipes DWM border colors and recreates taskbar buttons; redo both.</summary>
    public void OnTaskbarRecreated()
    {
        ReapplyColors();
        ReapplyTaskbarVisibility();
    }

    public void ReapplyColors()
    {
        foreach (var tw in _tracker.Tracked)
        {
            if (Definition(tw.Name) is { } def)
            {
                _colorizer.Apply(tw.Hwnd, def.Color, focused: tw.Name == _foregroundName);
            }
        }
    }

    /// <summary>
    /// The thumbnail-motion status source (ADR-0013): sample up to three unfocused
    /// Idle/Working windows this tick; a changed pixel hash means output happened.
    /// </summary>
    private void SampleMotion()
    {
        if (!Registry.ThumbnailMotionEnabled || MotionSamplingPaused?.Invoke() == true)
        {
            return;
        }

        var eligible = Registry.Terminals
            .Where(t => _tracker.Find(t.Name) is not null &&
                        MotionSampler.IsEligible(_status.StateOf(t.Name), t.Name == _foregroundName))
            .Select(t => t.Name)
            .ToList();

        foreach (var name in _motion.NextBatch(eligible))
        {
            if (_tracker.Find(name) is not { } tw || _sampler.SampleHash(tw.Hwnd) is not { } hash)
            {
                continue;
            }

            if (_motion.Observe(name, hash) && Definition(name) is { } def)
            {
                _status.OutputObserved(def);
            }
        }
    }

    private void OnWindowAppeared(TrackedWindow tw)
    {
        var focusRequested = _focusOnAppear.Remove(tw.Name, out var deadline)
                             && deadline >= _time.GetUtcNow();

        if (Definition(tw.Name) is not { } def)
        {
            return;
        }

        _colorizer.Apply(tw.Hwnd, def.Color, focused: false);
        if (Registry.HideFromTaskbar)
        {
            _taskbar.Hide(tw.Hwnd);
        }

        RememberHwnd(tw);
        _status.WindowAppeared(def);

        if (focusRequested)
        {
            WindowFocuser.Focus(tw.Hwnd);
        }
    }

    /// <summary>
    /// Persists the bound HWND so the next manager restart can recover this window even if
    /// its title has drifted (ADR-0002 amendment). No RegistryChanged: nothing UI-visible
    /// changed, and firing it would rebuild the strip and rebind hotkeys for nothing.
    /// </summary>
    private void RememberHwnd(TrackedWindow tw)
    {
        if (Definition(tw.Name) is not { } def || def.LastHwnd == tw.Hwnd)
        {
            return;
        }

        Registry = Registry with
        {
            Terminals = Registry.Terminals
                .Select(t => t.Name == tw.Name ? t with { LastHwnd = tw.Hwnd } : t)
                .ToList(),
        };
        _ = SaveQuietlyAsync();
    }

    private async Task SaveQuietlyAsync()
    {
        try
        {
            await PersistAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to persist last-known window handles");
        }
    }

    private void OnTitleChanged(TrackedWindow tw, string title)
    {
        if (Definition(tw.Name) is { } def)
        {
            _status.TitleChanged(def, title);

            // Focus is the one sanctioned clear (invariant 6), and this window already
            // has it — without this, attention raised while focused would pulse until
            // the user focused away and back.
            if (tw.Name == _foregroundName)
            {
                _status.Focused(tw.Name);
            }
        }
    }

    private async void OnWindowClosed(TrackedWindow tw)
    {
        _status.WindowGone(tw.Name);
        _motion.Forget(tw.Name); // a relaunch under this name starts from a fresh baseline
        _mru.Forget(tw.Name); // recency is a live-window notion; a relaunch re-earns its place
        if (Definition(tw.Name) is { Persistent: false })
        {
            try
            {
                await RemoveTerminalAsync(tw.Name);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to drop throwaway terminal {Name}", tw.Name);
            }
        }
    }

    private void OnForegroundChanged(TrackedWindow? tw)
    {
        var previous = _foregroundName;
        _foregroundName = tw?.Name;

        if (previous is not null && previous != _foregroundName
            && _tracker.Find(previous) is { } prevWin && Definition(previous) is { } prevDef)
        {
            _colorizer.Apply(prevWin.Hwnd, prevDef.Color, focused: false);
        }

        if (tw is not null && Definition(tw.Name) is { } def)
        {
            _colorizer.Apply(tw.Hwnd, def.Color, focused: true);
            _status.Focused(tw.Name);
        }

        // Recency: the walk's own activations wait for the session to settle; any other
        // foreground change ends a live walk without committing it (ADR-0015).
        if (tw is not null && tw.Name == _cycleTarget && _cycleSnapshot is not null)
        {
            // no touch — CommitCycle will touch the terminal the walk lands on
        }
        else
        {
            if (_cycleSnapshot is not null)
            {
                AbortCycle();
            }

            if (tw is not null)
            {
                _mru.Touch(tw.Name);
            }
        }

        ForegroundChanged?.Invoke(_foregroundName);
    }

    public void Dispose()
    {
        _tick.Stop();
        _cycleTimer.Stop();
        ResetColors();
        foreach (var tw in _tracker.Tracked)
        {
            _taskbar.Show(tw.Hwnd); // buttons must not stay hidden after we're gone
        }

        _tracker.Dispose();
    }
}
