namespace Switchboard.Core.Launch;

/// <summary>
/// Launches that have started but whose window has not bound yet, each with a deadline. Two
/// jobs: a second activation of the same name inside the window is dropped instead of opening a
/// duplicate WT window, and the window that does appear is brought forward once it binds.
/// </summary>
/// <remarks>
/// UI thread only, like the session that owns it. The timeout should match the tracker's
/// pending-title timeout: after that the window will not bind, so the name may launch again.
/// </remarks>
public sealed class PendingLaunches
{
    private readonly TimeProvider _time;
    private readonly TimeSpan _timeout;
    private readonly Dictionary<string, DateTimeOffset> _deadlines = new(StringComparer.Ordinal);

    public PendingLaunches(TimeProvider time, TimeSpan timeout)
    {
        _time = time;
        _timeout = timeout;
    }

    /// <summary>Records a launch of <paramref name="name"/> starting now.</summary>
    public void Start(string name) => _deadlines[name] = _time.GetUtcNow() + _timeout;

    /// <summary>True while a launch of <paramref name="name"/> is waiting for its window.</summary>
    public bool IsInFlight(string name) =>
        _deadlines.TryGetValue(name, out var deadline) && deadline >= _time.GetUtcNow();

    /// <summary>
    /// The window for <paramref name="name"/> bound. Returns true when it was a launch still
    /// inside its deadline, i.e. the caller should focus it; recovery and adopt binds get false.
    /// </summary>
    public bool Complete(string name) =>
        _deadlines.Remove(name, out var deadline) && deadline >= _time.GetUtcNow();

    /// <summary>The launch failed or the terminal went away; the name may launch again at once.</summary>
    public void Cancel(string name) => _deadlines.Remove(name);
}
