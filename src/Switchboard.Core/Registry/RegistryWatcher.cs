namespace Switchboard.Core.Registry;

/// <summary>
/// Watches switchboard.json for out-of-band edits (ADR-0008). Events are debounced — editors
/// and our own atomic writes touch the file in bursts — and self-writes are suppressed by
/// muting around <c>SaveAsync</c>. <see cref="Changed"/> is raised on a timer thread; the
/// subscriber marshals to the UI thread.
/// </summary>
public sealed class RegistryWatcher : IDisposable
{
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(400);

    private readonly string _filePath;
    private readonly TimeProvider _time;
    private readonly WatchGate _gate = new(QuietPeriod);
    private readonly ITimer _timer;
    private readonly object _sync = new();
    private FileSystemWatcher? _watcher;

    public RegistryWatcher(string filePath, TimeProvider time)
    {
        _filePath = filePath;
        _time = time;
        _timer = time.CreateTimer(_ => OnTimer(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public event Action? Changed;

    public void Start()
    {
        if (_watcher is not null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new ArgumentException($"Registry path '{_filePath}' has no directory.");
        }

        Directory.CreateDirectory(directory);

        // Filtering to the exact file name keeps the store's .tmp/.bak churn invisible;
        // Renamed catches editors that save via atomic rename.
        _watcher = new FileSystemWatcher(directory, Path.GetFileName(_filePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        _watcher.EnableRaisingEvents = true;
    }

    /// <summary>Call before a self-write so the resulting file events are swallowed.</summary>
    public void MuteFor(TimeSpan duration)
    {
        lock (_sync)
        {
            _gate.MuteFor(_time.GetUtcNow(), duration);
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        lock (_sync)
        {
            if (_gate.Poke(_time.GetUtcNow()) is not null)
            {
                _timer.Change(QuietPeriod, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void OnTimer()
    {
        bool fire;
        lock (_sync)
        {
            fire = _gate.TryFire(_time.GetUtcNow());
        }

        if (fire)
        {
            Changed?.Invoke();
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _timer.Dispose();
    }
}

/// <summary>
/// The watcher's debounce/mute decisions, pure so they can be tested against a fake clock.
/// A poke inside the mute window is dropped; otherwise it (re)sets the due time one quiet
/// period out, and the event fires only when the due time has genuinely passed.
/// </summary>
internal sealed class WatchGate
{
    private readonly TimeSpan _quietPeriod;
    private DateTimeOffset _muteUntil = DateTimeOffset.MinValue;
    private DateTimeOffset? _dueAt;

    public WatchGate(TimeSpan quietPeriod)
    {
        _quietPeriod = quietPeriod;
    }

    /// <summary>A file event arrived. Returns the new due time, or null if muted.</summary>
    public DateTimeOffset? Poke(DateTimeOffset now)
    {
        if (now < _muteUntil)
        {
            return null;
        }

        _dueAt = now + _quietPeriod;
        return _dueAt;
    }

    /// <summary>Mutes only extend; a shorter overlapping mute never shortens a longer one.</summary>
    public void MuteFor(DateTimeOffset now, TimeSpan duration)
    {
        var until = now + duration;
        if (until > _muteUntil)
        {
            _muteUntil = until;
        }
    }

    /// <summary>True exactly once per quiet period that actually elapsed.</summary>
    public bool TryFire(DateTimeOffset now)
    {
        if (_dueAt is { } due && now >= due)
        {
            _dueAt = null;
            return true;
        }

        return false;
    }
}
