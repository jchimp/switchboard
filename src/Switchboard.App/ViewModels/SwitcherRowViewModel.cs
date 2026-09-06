using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Switchboard.Core.Registry;
using Switchboard.Core.Status;

namespace Switchboard.App.ViewModels;

/// <summary>A piece of a card's name: consecutive characters that either all matched the filter or none did.</summary>
public sealed record NameSegment(string Text, bool Hit);

/// <summary>One switcher card: thumbnail slot, name (with match highlighting), badge, state.</summary>
public sealed partial class SwitcherRowViewModel : ObservableObject
{
    private IReadOnlyList<Range> _spans = [];

    public SwitcherRowViewModel(TerminalDefinition definition, int position)
    {
        Definition = definition;
        Position = position;
        // Colors were validated as #RRGGBB by the registry; the converter cannot fail.
        Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(definition.Color)!;
        Brush.Freeze();
    }

    public TerminalDefinition Definition { get; }

    /// <summary>1-based registry position; the jump fallback when no explicit key is set.</summary>
    public int Position { get; }

    public string Name => Definition.Name;

    public SolidColorBrush Brush { get; }

    /// <summary>Digit shown in the key badge, mirroring ActivateByJumpKeyAsync's resolution.</summary>
    public string JumpKeyText => (Definition.JumpKey ?? (Position <= 9 ? Position : null))?.ToString() ?? string.Empty;

    public bool HasJumpKey => JumpKeyText.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsClosed), nameof(RowOpacity))]
    private TerminalState _state = TerminalState.Closed;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isSelected;

    public bool IsClosed => State == TerminalState.Closed;

    public double RowOpacity => IsClosed ? 0.45 : 1.0;

    /// <summary>False hides the card via the collection view — the row object itself survives.</summary>
    public bool MatchesFilter { get; private set; } = true;

    public void ApplyFilter(bool matches, IReadOnlyList<Range> spans)
    {
        MatchesFilter = matches;
        _spans = spans;
        OnPropertyChanged(nameof(NameSegments));
    }

    /// <summary>The name split at match boundaries; hit segments render bold in the card.</summary>
    public IReadOnlyList<NameSegment> NameSegments
    {
        get
        {
            if (_spans.Count == 0)
            {
                return [new NameSegment(Name, Hit: false)];
            }

            var segments = new List<NameSegment>();
            var at = 0;
            foreach (var span in _spans)
            {
                var (start, end) = (span.Start.Value, span.End.Value);
                if (start > at)
                {
                    segments.Add(new NameSegment(Name[at..start], Hit: false));
                }

                segments.Add(new NameSegment(Name[start..end], Hit: true));
                at = end;
            }

            if (at < Name.Length)
            {
                segments.Add(new NameSegment(Name[at..], Hit: false));
            }

            return segments;
        }
    }
}
