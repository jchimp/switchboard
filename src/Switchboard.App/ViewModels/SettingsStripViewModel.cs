using CommunityToolkit.Mvvm.ComponentModel;
using Switchboard.App.Services;
using Switchboard.Core.Registry;

namespace Switchboard.App.ViewModels;

/// <summary>
/// The Edge strip page. Live-apply (ADR-0018): every setter queues a debounced write, so the
/// strip re-docks as you change it and there is no Save button to forget.
/// </summary>
/// <remarks>
/// Only settings the product actually reads appear here. <c>strip.activeThickness</c> is
/// deliberately absent: it is reserved for segment growth that no code consumes yet, and a
/// control that does nothing is worse than no control.
/// </remarks>
public sealed partial class SettingsStripViewModel : ObservableObject
{
    private readonly TerminalSession _session;
    private readonly SettingsApplyQueue _queue;
    private bool _loading;

    public SettingsStripViewModel(TerminalSession session, SettingsApplyQueue queue)
    {
        _session = session;
        _queue = queue;
        _session.RegistryChanged += OnRegistryChanged;
        Load();
    }

    public static IReadOnlyList<ScreenEdge> Edges { get; } = [ScreenEdge.Left, ScreenEdge.Top, ScreenEdge.Right];

    public static IReadOnlyList<TopWidthMode> TopWidths { get; } = [TopWidthMode.Full, TopWidthMode.Centered];

    public int MinThickness => SettingsBounds.MinThickness;

    public int MaxThickness => SettingsBounds.MaxThickness;

    public int MaxCollapseDelayMs => SettingsBounds.MaxCollapseDelayMs;

    public int MaxHoverOpenDelayMs => SettingsBounds.MaxHoverOpenDelayMs;

    public int MinRailSizeDips => SettingsBounds.MinRailSizeDips;

    public int MaxRailSizeDips => SettingsBounds.MaxRailSizeDips;

    public int MinRailTopHeightDips => SettingsBounds.MinRailTopHeightDips;

    public int MaxRailTopHeightDips => SettingsBounds.MaxRailTopHeightDips;

    /// <summary>True when the top-edge-only settings apply; the rail's height follows the edge.</summary>
    public bool IsTopEdge => Edge == ScreenEdge.Top;

    /// <summary>App-bar mode always spans the whole edge, so auto-hide and top width go inert (ADR-0010/0011).</summary>
    public bool AppBarOverrides => ReserveScreenSpace;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTopEdge))]
    private ScreenEdge _edge;

    [ObservableProperty]
    private TopWidthMode _topWidth;

    [ObservableProperty]
    private int _monitor;

    [ObservableProperty]
    private int _thickness;

    [ObservableProperty]
    private bool _autoHide;

    [ObservableProperty]
    private int _hiddenThickness;

    [ObservableProperty]
    private int _collapseDelayMs;

    [ObservableProperty]
    private bool _openRailOnHover;

    [ObservableProperty]
    private int _hoverOpenDelayMs;

    [ObservableProperty]
    private int _railSizeDips;

    [ObservableProperty]
    private int _railTopHeightDips;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppBarOverrides))]
    private bool _reserveScreenSpace;

    [ObservableProperty]
    private bool _hideOverFullscreen;

    [ObservableProperty]
    private bool _showClosedTerminals;

    [ObservableProperty]
    private bool _hideFromTaskbar;

    /// <summary>Every page property lands here; one queue entry per edit, coalesced.</summary>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_loading || e.PropertyName is null or nameof(IsTopEdge) or nameof(AppBarOverrides))
        {
            return;
        }

        _queue.Queue(registry => registry with
        {
            HideFromTaskbar = HideFromTaskbar,
            Strip = registry.Strip with
            {
                Edge = Edge,
                TopWidth = TopWidth,
                Monitor = Monitor,
                Thickness = Thickness,
                AutoHide = AutoHide,
                HiddenThickness = HiddenThickness,
                CollapseDelayMs = CollapseDelayMs,
                OpenRailOnHover = OpenRailOnHover,
                HoverOpenDelayMs = HoverOpenDelayMs,
                RailSizeDips = RailSizeDips,
                RailTopHeightDips = RailTopHeightDips,
                ReserveScreenSpace = ReserveScreenSpace,
                HideOverFullscreen = HideOverFullscreen,
                ShowClosedTerminals = ShowClosedTerminals,
            },
        });
    }

    private void OnRegistryChanged()
    {
        // Our own save echoes back through here; reloading it would fight the control the
        // user is still dragging. A hand-edit or hot-reload does need to land.
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
            var registry = _session.Registry;
            var strip = registry.Strip;
            Edge = strip.Edge;
            TopWidth = strip.TopWidth;
            Monitor = strip.Monitor;
            Thickness = strip.Thickness;
            AutoHide = strip.AutoHide;
            HiddenThickness = strip.HiddenThickness;
            CollapseDelayMs = strip.CollapseDelayMs;
            OpenRailOnHover = strip.OpenRailOnHover;
            HoverOpenDelayMs = strip.HoverOpenDelayMs;
            RailSizeDips = strip.RailSizeDips;
            RailTopHeightDips = strip.RailTopHeightDips;
            ReserveScreenSpace = strip.ReserveScreenSpace;
            HideOverFullscreen = strip.HideOverFullscreen;
            ShowClosedTerminals = strip.ShowClosedTerminals;
            HideFromTaskbar = registry.HideFromTaskbar;
        }
        finally
        {
            _loading = false;
        }
    }
}
