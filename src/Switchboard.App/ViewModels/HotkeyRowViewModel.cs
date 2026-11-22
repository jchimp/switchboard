using CommunityToolkit.Mvvm.ComponentModel;
using Switchboard.App.Services;
using Switchboard.Core.Hotkeys;

namespace Switchboard.App.ViewModels;

/// <summary>
/// One editable chord on the Hotkeys page: what it does, what it is bound to, and whether
/// Windows actually gave it to us.
/// </summary>
public sealed partial class HotkeyRowViewModel : ObservableObject
{
    private readonly Action<HotkeyRowViewModel> _changed;

    public HotkeyRowViewModel(string action, string description, string chord, bool isPrefix, Action<HotkeyRowViewModel> changed)
    {
        Action = action;
        Description = description;
        _chord = chord;
        IsPrefix = isPrefix;
        _changed = changed;
    }

    public string Action { get; }

    public string Description { get; }

    /// <summary>True for <c>jumpPrefix</c>, which is a modifier prefix rather than a whole chord.</summary>
    public bool IsPrefix { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ParseError), nameof(HasProblem), nameof(ProblemText))]
    private string _chord;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem), nameof(ProblemText))]
    private HotkeyState? _state;

    /// <summary>True while the row is waiting for the user to press a chord.</summary>
    [ObservableProperty]
    private bool _isCapturing;

    /// <summary>
    /// Validated locally so a typo shows up as you type, before it reaches the file. The
    /// prefix row is checked by building the jump chord it has to produce.
    /// </summary>
    public string? ParseError
    {
        get
        {
            var text = IsPrefix ? $"{Chord}+1" : Chord;
            return HotkeyChord.TryParse(text, out _, out var error) ? null : error;
        }
    }

    public bool HasProblem => ParseError is not null || State?.Outcome is HotkeyOutcome.Taken or HotkeyOutcome.Unparseable;

    public string ProblemText => ParseError ?? State?.Error ?? string.Empty;

    partial void OnChordChanged(string value)
    {
        if (ParseError is null)
        {
            _changed(this);
        }
    }
}
