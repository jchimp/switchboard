namespace Switchboard.Core.Status;

/// <summary>
/// Policy half of the thumbnail-motion status source (ADR-0013): which windows are worth
/// sampling, which few are due this tick, and whether a sampled hash means motion.
/// Pure so every rule is unit-tested; the GDI capture lives in the Windows layer.
/// </summary>
public sealed class MotionSampler
{
    /// <summary>Capture cost ceiling: at most this many windows are sampled per tick.</summary>
    public const int MaxPerTick = 3;

    private readonly Dictionary<string, ulong> _baselines = new(StringComparer.Ordinal);
    private int _cursor;

    /// <summary>
    /// Attention is sticky (sampling could tell us nothing), the focused window is being
    /// looked at, and Closed has no pixels. Idle and Working windows are the ones whose
    /// output nobody is watching.
    /// </summary>
    public static bool IsEligible(TerminalState state, bool focused) =>
        !focused && state is TerminalState.Idle or TerminalState.Working;

    /// <summary>The up-to-<see cref="MaxPerTick"/> names due this tick, round-robin.</summary>
    public IReadOnlyList<string> NextBatch(IReadOnlyList<string> eligible)
    {
        if (eligible.Count == 0)
        {
            return [];
        }

        var count = Math.Min(MaxPerTick, eligible.Count);
        var batch = new List<string>(count);
        _cursor %= eligible.Count;
        for (var i = 0; i < count; i++)
        {
            batch.Add(eligible[(_cursor + i) % eligible.Count]);
        }

        _cursor = (_cursor + count) % eligible.Count;
        return batch;
    }

    /// <summary>
    /// Records a sampled hash. True only when it differs from the previous one — the first
    /// sample is a baseline, never motion (a fresh start must not flip everything Working).
    /// </summary>
    public bool Observe(string name, ulong hash)
    {
        var moved = _baselines.TryGetValue(name, out var previous) && previous != hash;
        _baselines[name] = hash;
        return moved;
    }

    /// <summary>Drops a window's baseline; its next sample starts fresh (window closed/relaunched).</summary>
    public void Forget(string name) => _baselines.Remove(name);

    /// <summary>FNV-1a, 64-bit: tiny, allocation-free, and plenty for change detection.</summary>
    public static ulong Fnv1a(ReadOnlySpan<byte> data)
    {
        var hash = 14695981039346656037UL;
        foreach (var b in data)
        {
            hash = (hash ^ b) * 1099511628211UL;
        }

        return hash;
    }
}
