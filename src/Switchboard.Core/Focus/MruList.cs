namespace Switchboard.Core.Focus;

/// <summary>
/// Most-recently-used terminal names, front = most recent. Runtime-only — never persisted,
/// so a restart starts with an empty history (matching Alt-Tab expectations). Feeds both
/// cycle-recent (ADR-0015) and the overlay switcher's preselection.
/// </summary>
public sealed class MruList
{
    private readonly List<string> _order = [];

    /// <summary>Most-recent-first; index 0 is (normally) the current foreground.</summary>
    public IReadOnlyList<string> Order => _order;

    /// <summary>Moves a name to the front, inserting it if unknown.</summary>
    public void Touch(string name)
    {
        _order.Remove(name);
        _order.Insert(0, name);
    }

    /// <summary>Drops a name without disturbing the order of the others.</summary>
    public void Forget(string name) => _order.Remove(name);

    /// <summary>Follows a live rename in place — the entry keeps its recency position.</summary>
    public void Rename(string oldName, string newName)
    {
        var i = _order.IndexOf(oldName);
        if (i >= 0)
        {
            _order[i] = newName;
        }
    }

    /// <summary>Drops every name not in <paramref name="keep"/> (registry reload removed them).</summary>
    public void Prune(IReadOnlySet<string> keep) => _order.RemoveAll(n => !keep.Contains(n));
}
