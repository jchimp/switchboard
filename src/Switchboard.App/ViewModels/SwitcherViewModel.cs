using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Switchboard.App.Services;
using Switchboard.Core.Search;
using Switchboard.Core.Status;

namespace Switchboard.App.ViewModels;

/// <summary>
/// Backs the overlay switcher (ADR-0016). Cards stay in registry order — the numbers must
/// mean the same terminal as the strip's jump badges — and rows are upserted in place keyed
/// by name, never Clear()ed, for the same reason as the rail: DWM thumbnail registrations
/// hang off each row. Filtering hides rows through the collection view rather than
/// rebuilding the collection.
/// </summary>
public sealed partial class SwitcherViewModel : ObservableObject
{
    /// <summary>Grid width cap; Up/Down arrows step by the actual column count.</summary>
    public const int MaxColumns = 4;

    private readonly TerminalSession _session;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private SwitcherRowViewModel? _selected;

    public SwitcherViewModel(TerminalSession session)
    {
        _session = session;
        View = CollectionViewSource.GetDefaultView(Items);
        View.Filter = o => o is SwitcherRowViewModel { MatchesFilter: true };
        _session.StateChanged += OnStateChanged;
        _session.RegistryChanged += Sync;
        _session.ForegroundChanged += OnForegroundChanged;
        Sync();
    }

    public ObservableCollection<SwitcherRowViewModel> Items { get; } = [];

    public ICollectionView View { get; }

    /// <summary>The cards currently passing the filter, in registry order.</summary>
    public IReadOnlyList<SwitcherRowViewModel> Visible => Items.Where(i => i.MatchesFilter).ToList();

    public int Columns => Math.Clamp(Visible.Count, 1, MaxColumns);

    /// <summary>Fresh state for an open: empty filter, resynced rows, most-recent card preselected.</summary>
    public void PrepareToOpen()
    {
        Sync();
        Query = string.Empty; // OnQueryChanged refilters and fixes up the selection

        // MRU preselection: plain Ctrl+` Enter means "switch to recent" (ADR-0015/0016).
        var recent = _session.RecentOrder.FirstOrDefault(n => n != _session.ForegroundName
                                                              && Items.Any(i => i.Name == n));
        Select(Items.FirstOrDefault(i => i.Name == recent) ?? Items.FirstOrDefault());
    }

    public void Select(SwitcherRowViewModel? row)
    {
        Selected = row;
        foreach (var item in Items)
        {
            item.IsSelected = ReferenceEquals(item, row);
        }
    }

    /// <summary>Moves the selection through the filtered cards, wrapping at either end.</summary>
    public void MoveSelection(int delta)
    {
        var visible = Visible;
        if (visible.Count == 0)
        {
            return;
        }

        var at = Selected is null ? -1 : visible.ToList().FindIndex(i => ReferenceEquals(i, Selected));
        var next = at < 0 ? 0 : ((at + delta) % visible.Count + visible.Count) % visible.Count;
        Select(visible[next]);
    }

    /// <summary>Enter: the selected card, or the first visible one when the filter moved selection away.</summary>
    public Task ActivateSelectedAsync()
    {
        var target = Selected is { MatchesFilter: true } sel ? sel : Visible.FirstOrDefault();
        return target is null ? Task.CompletedTask : _session.ActivateAsync(target.Name);
    }

    public Task ActivateByJumpKeyAsync(int digit) => _session.ActivateByJumpKeyAsync(digit);

    partial void OnQueryChanged(string value)
    {
        foreach (var item in Items)
        {
            if (value.Length == 0)
            {
                item.ApplyFilter(matches: true, []);
            }
            else
            {
                var matches = FuzzyMatcher.TryMatch(value, item.Name, out var match);
                item.ApplyFilter(matches, matches ? match.Spans : []);
            }
        }

        View.Refresh();
        OnPropertyChanged(nameof(Columns));

        // A filter that hid the selected card moves selection to the first survivor.
        if (Selected is not { MatchesFilter: true })
        {
            Select(Visible.FirstOrDefault());
        }
    }

    /// <summary>Incremental resync against the registry: upsert in order, drop the missing (same discipline as the rail).</summary>
    public void Sync()
    {
        var defs = _session.Registry.Terminals;

        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (!defs.Any(d => d.Name == Items[i].Name))
            {
                Items.RemoveAt(i);
            }
        }

        for (var index = 0; index < defs.Count; index++)
        {
            var def = defs[index];
            var at = IndexOf(def.Name);
            var replace = at >= 0 && !Items[at].Definition.Equals(def);
            if (at >= 0 && !replace && Items[at].Position == index + 1)
            {
                if (at != index)
                {
                    Items.Move(at, index);
                }

                continue;
            }

            if (at >= 0)
            {
                Items.RemoveAt(at);
            }

            Items.Insert(Math.Min(index, Items.Count), new SwitcherRowViewModel(def, index + 1)
            {
                State = _session.StateOf(def.Name),
                IsActive = def.Name == _session.ForegroundName,
            });
        }
    }

    private int IndexOf(string name)
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Name == name)
            {
                return i;
            }
        }

        return -1;
    }

    private void OnStateChanged(string name, TerminalState state)
    {
        if (Items.FirstOrDefault(i => i.Name == name) is { } item)
        {
            item.State = state;
        }
        else if (_session.Definition(name) is not null)
        {
            Sync();
        }
    }

    private void OnForegroundChanged(string? name)
    {
        foreach (var item in Items)
        {
            item.IsActive = item.Name == name;
        }
    }
}
