using Switchboard.Core.Registry;

namespace Switchboard.Core.Status;

/// <summary>
/// Turns raw observations (title changed, window flashed, window vanished, focused)
/// into a per-terminal <see cref="TerminalState"/>. Pure and deterministic: time
/// comes from an injected <see cref="TimeProvider"/> so the idle timeout is testable.
/// </summary>
/// <remarks>
/// Rules of the state machine:
/// <list type="bullet">
///   <item>Attention is sticky. Only <see cref="Focused"/> clears it.</item>
///   <item>A title that matches nothing keeps the previous state; the idle timer
///         decides when Working decays to Idle.</item>
///   <item>Closed is reported only from <see cref="WindowGone"/>, never inferred.</item>
/// </list>
/// </remarks>
public sealed class StatusEngine
{
    private readonly TitleRuleEvaluator _evaluator = new();
    private readonly TimeProvider _time;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private IReadOnlyList<StatusRule> _globalRules;

    public StatusEngine(TimeProvider time, IReadOnlyList<StatusRule> globalRules, TimeSpan? idleAfter = null)
    {
        _time = time;
        _globalRules = globalRules;
        IdleAfter = idleAfter ?? TimeSpan.FromSeconds(20);
    }

    public TimeSpan IdleAfter { get; }

    public event Action<string, TerminalState>? StateChanged;

    public void ReplaceGlobalRules(IReadOnlyList<StatusRule> rules) => _globalRules = rules;

    public TerminalState StateOf(string name) =>
        _entries.TryGetValue(name, out var e) ? e.State : TerminalState.Closed;

    public void WindowAppeared(TerminalDefinition def)
    {
        Set(def, TerminalState.Idle, touch: true);
    }

    public void WindowGone(string name)
    {
        if (_entries.Remove(name))
        {
            StateChanged?.Invoke(name, TerminalState.Closed);
        }
    }

    public void TitleChanged(TerminalDefinition def, string title)
    {
        var matched = _evaluator.Evaluate(title, def.StatusRules, _globalRules);
        if (matched is { } state)
        {
            Set(def, Escalate(def.Name, state), touch: true);
            return;
        }

        // Unmatched title change still counts as activity.
        var current = StateOf(def.Name);
        Set(def, current == TerminalState.Idle ? TerminalState.Working : current, touch: true);
    }

    /// <summary>Taskbar flash / BEL. Always escalates to Attention.</summary>
    public void Flashed(TerminalDefinition def) => Set(def, TerminalState.Attention, touch: true);

    /// <summary>Thumbnail pixels changed. Working unless already Attention.</summary>
    public void OutputObserved(TerminalDefinition def) => Set(def, Escalate(def.Name, TerminalState.Working), touch: true);

    public void Focused(string name)
    {
        if (!_entries.TryGetValue(name, out var e))
        {
            return;
        }

        if (e.State == TerminalState.Attention)
        {
            Set(e.Definition, TerminalState.Idle, touch: true);
        }
    }

    /// <summary>Call on a timer (~1 s). Decays Working to Idle after <see cref="IdleAfter"/>.</summary>
    public void Tick()
    {
        var now = _time.GetUtcNow();
        foreach (var e in _entries.Values.ToList())
        {
            if (e.State == TerminalState.Working && now - e.LastActivity >= IdleAfter)
            {
                Set(e.Definition, TerminalState.Idle, touch: false);
            }
        }
    }

    private TerminalState Escalate(string name, TerminalState proposed) =>
        StateOf(name) == TerminalState.Attention ? TerminalState.Attention : proposed;

    private void Set(TerminalDefinition def, TerminalState state, bool touch)
    {
        var now = _time.GetUtcNow();
        if (_entries.TryGetValue(def.Name, out var existing))
        {
            var changed = existing.State != state;
            existing.State = state;
            existing.Definition = def;
            if (touch)
            {
                existing.LastActivity = now;
            }

            if (changed)
            {
                StateChanged?.Invoke(def.Name, state);
            }

            return;
        }

        _entries[def.Name] = new Entry { Definition = def, State = state, LastActivity = now };
        StateChanged?.Invoke(def.Name, state);
    }

    private sealed class Entry
    {
        public required TerminalDefinition Definition { get; set; }
        public required TerminalState State { get; set; }
        public required DateTimeOffset LastActivity { get; set; }
    }
}
