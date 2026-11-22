using CommunityToolkit.Mvvm.ComponentModel;
using Switchboard.Core.Status;

namespace Switchboard.App.ViewModels;

/// <summary>
/// One editable "title regex → state" rule. Used by both the global rules page and the
/// per-terminal rules list in the terminal editor.
/// </summary>
public sealed partial class StatusRuleRowViewModel : ObservableObject
{
    public StatusRuleRowViewModel(StatusRule rule)
    {
        _state = rule.State;
        _pattern = rule.Pattern;
    }

    /// <summary>The states a rule may assert. Closed is excluded — it comes only from a window dying.</summary>
    public static IReadOnlyList<TerminalState> States { get; } =
        [TerminalState.Working, TerminalState.Attention, TerminalState.Idle];

    [ObservableProperty]
    private TerminalState _state;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(HasError))]
    private string _pattern;

    /// <summary>Compile error for this pattern, or null. Invariant 9 — checked before it is saved.</summary>
    public string? Error => StatusRule.ValidatePattern(Pattern);

    public bool HasError => Error is not null;

    public StatusRule ToRule() => new() { State = State, Pattern = Pattern };
}
