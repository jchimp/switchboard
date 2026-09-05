using Microsoft.Extensions.Logging;
using Switchboard.Core.Launch;
using Switchboard.Core.Registry;
using Switchboard.Core.Status;
using Switchboard.Windows;
using Switchboard.Windows.Tracking;

namespace Switchboard.App.Services;

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

    private readonly IRegistryStore _store;
    private readonly ITerminalLauncher _launcher;
    private readonly WindowTracker _tracker;
    private readonly FrameColorizer _colorizer;
    private readonly TaskbarVisibility _taskbar;
    private readonly RegistryWatcher _watcher;
    private readonly ILogger<TerminalSession> _log;
    private readonly TimeProvider _time;
    private readonly System.Windows.Threading.Dispatcher _dispatcher;
    private readonly System.Windows.Threading.DispatcherTimer _tick;
    private readonly Dictionary<string, DateTimeOffset> _focusOnAppear = new(StringComparer.Ordinal);
    private StatusEngine _status;
    private string? _foregroundName;
    private string? _previousForegroundName;

    public TerminalSession(
        IRegistryStore store, ITerminalLauncher launcher, WindowTracker tracker,
        FrameColorizer colorizer, TaskbarVisibility taskbar, RegistryWatcher watcher,
        TimeProvider time, ILogger<TerminalSession> log)
    {
        _store = store;
        _launcher = launcher;
        _tracker = tracker;
        _colorizer = colorizer;
        _taskbar = taskbar;
        _watcher = watcher;
        _log = log;
        _time = time;
        _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        _status = new StatusEngine(time, StatusRule.Defaults);
        _tick = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tick.Tick += (_, _) => _status.Tick();
    }

    public RegistryFile Registry { get; private set; } = RegistryFile.Empty;

    /// <summary>Fired on the UI thread whenever a terminal's state or presence changes.</summary>
    public event Action<string, TerminalState>? StateChanged;

    public event Action? RegistryChanged;

    /// <summary>Fired when a tracked terminal gains or loses the foreground (null = none of ours).</summary>
    public event Action<string?>? ForegroundChanged;

    public string? ForegroundName => _foregroundName;

    public async Task InitializeAsync()
    {
        Registry = await _store.LoadAsync();
        _status = new StatusEngine(_time, Registry.GlobalStatusRules);
        _status.StateChanged += (name, state) => StateChanged?.Invoke(name, state);

        _tracker.WindowAppeared += OnWindowAppeared;
        _tracker.TitleChanged += OnTitleChanged;
        _tracker.WindowClosed += OnWindowClosed;
        _tracker.ForegroundChanged += OnForegroundChanged;
        _tracker.Start();

        var recovered = _tracker.RecoverByTitle(Registry.Terminals.Select(t => t.Name));
        _log.LogInformation("Recovered {Count} windows from a previous run", recovered);
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

    public Task CycleRecentAsync() =>
        _previousForegroundName is { } prev ? ActivateAsync(prev) : Task.CompletedTask;

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

    public async Task RemoveTerminalAsync(string name)
    {
        Registry = Registry with { Terminals = Registry.Terminals.Where(t => t.Name != name).ToList() };
        if (_tracker.Find(name) is { } live)
        {
            _taskbar.Show(live.Hwnd); // the window outlives us; give its button back
        }

        _tracker.Unbind(name);
        await PersistAsync();
        RegistryChanged?.Invoke();
    }

    /// <summary>
    /// Re-reads registry.json and applies adds, removes, and edits to live state (ADR-0008).
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
            _log.LogWarning(ex, "registry.json changed but did not load; keeping the current registry");
            return;
        }

        var removed = Registry.Terminals.Select(t => t.Name)
            .Except(fresh.Terminals.Select(t => t.Name), StringComparer.Ordinal)
            .ToList();

        Registry = fresh;
        _status.ReplaceGlobalRules(fresh.GlobalStatusRules);

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

        _status.WindowAppeared(def);

        if (focusRequested)
        {
            WindowFocuser.Focus(tw.Hwnd);
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

        if (previous is not null && previous != _foregroundName)
        {
            _previousForegroundName = previous;
            if (_tracker.Find(previous) is { } prevWin && Definition(previous) is { } prevDef)
            {
                _colorizer.Apply(prevWin.Hwnd, prevDef.Color, focused: false);
            }
        }

        if (tw is not null && Definition(tw.Name) is { } def)
        {
            _colorizer.Apply(tw.Hwnd, def.Color, focused: true);
            _status.Focused(tw.Name);
        }

        ForegroundChanged?.Invoke(_foregroundName);
    }

    public void Dispose()
    {
        _tick.Stop();
        ResetColors();
        foreach (var tw in _tracker.Tracked)
        {
            _taskbar.Show(tw.Hwnd); // buttons must not stay hidden after we're gone
        }

        _tracker.Dispose();
    }
}
