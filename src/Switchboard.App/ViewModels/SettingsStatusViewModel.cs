using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Switchboard.App.Services;
using Switchboard.Core.Status;

namespace Switchboard.App.ViewModels;

/// <summary>
/// The Status rules page: the global rule list every terminal falls back to, plus the one
/// status source that is actually switchable.
/// </summary>
/// <remarks>
/// The mockup shows four source toggles. Only two sources are real today — title rules
/// (always on, and pointless to disable: they cost nothing and are the primary signal) and
/// thumbnail motion. Taskbar flash has no caller and the named pipe is not implemented, so
/// neither is drawn; a toggle that changes nothing is a lie about what the product does.
/// </remarks>
public sealed partial class SettingsStatusViewModel : ObservableObject
{
    private readonly TerminalSession _session;
    private readonly SettingsApplyQueue _queue;
    private bool _loading;

    public SettingsStatusViewModel(TerminalSession session, SettingsApplyQueue queue)
    {
        _session = session;
        _queue = queue;
        _session.RegistryChanged += OnRegistryChanged;
        Rules.CollectionChanged += OnRulesChanged;
        Load();
    }

    public ObservableCollection<StatusRuleRowViewModel> Rules { get; } = [];

    /// <summary>Attention is sticky until focus; Working decays on the engine's own clock.</summary>
    public string DecaySummary =>
        $"Working decays to Idle after {(int)StatusEngine.DefaultIdleAfter.TotalSeconds} s without activity. " +
        "Attention stays until you focus the terminal.";

    [ObservableProperty]
    private bool _thumbnailMotionEnabled;

    public bool HasInvalidRule => Rules.Any(r => r.HasError);

    [RelayCommand]
    private void AddRule() => Rules.Add(new StatusRuleRowViewModel(
        new StatusRule { State = TerminalState.Working, Pattern = string.Empty }));

    [RelayCommand]
    private void RemoveRule(StatusRuleRowViewModel rule) => Rules.Remove(rule);

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_loading && e.PropertyName == nameof(ThumbnailMotionEnabled))
        {
            Apply();
        }
    }

    private void OnRulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var row in e.NewItems?.OfType<StatusRuleRowViewModel>() ?? [])
        {
            row.PropertyChanged += OnRuleEdited;
        }

        foreach (var row in e.OldItems?.OfType<StatusRuleRowViewModel>() ?? [])
        {
            row.PropertyChanged -= OnRuleEdited;
        }

        if (!_loading)
        {
            Apply();
        }
    }

    private void OnRuleEdited(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasInvalidRule));
        if (!_loading)
        {
            Apply();
        }
    }

    private void Apply()
    {
        // A pattern that will not compile is dropped rather than written: the engine would
        // silently treat it as "never matches", which looks like a Switchboard bug later.
        var rules = Rules.Where(r => !r.HasError).Select(r => r.ToRule()).ToList();
        _queue.Queue(registry => registry with
        {
            ThumbnailMotionEnabled = ThumbnailMotionEnabled,
            GlobalStatusRules = rules,
        });
    }

    private void OnRegistryChanged()
    {
        if (!_queue.IsApplyingOwnEdit)
        {
            Load();
        }
    }

    private void Load()
    {
        _loading = true;
        try
        {
            foreach (var row in Rules)
            {
                row.PropertyChanged -= OnRuleEdited;
            }

            Rules.Clear();
            foreach (var rule in _session.Registry.GlobalStatusRules)
            {
                var row = new StatusRuleRowViewModel(rule);
                row.PropertyChanged += OnRuleEdited;
                Rules.Add(row);
            }

            ThumbnailMotionEnabled = _session.Registry.ThumbnailMotionEnabled;
        }
        finally
        {
            _loading = false;
        }

        OnPropertyChanged(nameof(HasInvalidRule));
    }
}
