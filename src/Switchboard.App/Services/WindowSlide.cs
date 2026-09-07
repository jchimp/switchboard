using System.Windows.Media;
using Switchboard.Core.Screen;
using Switchboard.Windows;

namespace Switchboard.App.Services;

/// <summary>
/// Drives one window's slide animation from <see cref="CompositionTarget.Rendering"/>
/// instead of a DispatcherTimer. A default-priority timer ticks at Background and gets
/// starved by layout and render work — which is exactly when the slide is running — so a
/// nominal 16 ms interval degrades to visible jitter. Rendering fires once per WPF frame,
/// vsync-aligned, and the time-based easing absorbs whatever the real frame spacing is.
/// Position-only slides (rail: open and hidden rects are the same size) skip the layered
/// window's per-frame surface reallocation via <see cref="WindowPlacer.MoveWithoutResizing"/>.
/// </summary>
public sealed class WindowSlide
{
    private readonly Func<nint> _hwnd;
    private readonly int _durationMs;
    private readonly Action<PixelRect>? _applied;
    private PixelRect _from;
    private PixelRect _to;
    private long _startTicks;
    private Action? _completed;

    /// <param name="hwnd">Resolved per frame; a zero handle stops the slide.</param>
    /// <param name="durationMs">Slide duration; easing is ease-out cubic.</param>
    /// <param name="applied">Optional per-frame observer of the rect just applied.</param>
    public WindowSlide(Func<nint> hwnd, int durationMs, Action<PixelRect>? applied = null)
    {
        _hwnd = hwnd;
        _durationMs = durationMs;
        _applied = applied;
    }

    public bool IsRunning { get; private set; }

    /// <summary>The rect most recently applied; where a mid-slide reversal should start from.</summary>
    public PixelRect? Current { get; private set; }

    /// <summary>Starts (or retargets) the slide. The completion callback replaces any pending one.</summary>
    public void Start(PixelRect from, PixelRect to, Action? completed = null)
    {
        _from = from;
        _to = to;
        _completed = completed;
        _startTicks = Environment.TickCount64;
        if (!IsRunning)
        {
            IsRunning = true;
            CompositionTarget.Rendering += OnRendering;
        }
    }

    /// <summary>Detaches without running the completion callback.</summary>
    public void Stop()
    {
        if (IsRunning)
        {
            IsRunning = false;
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var hwnd = _hwnd();
        if (hwnd == 0)
        {
            Stop();
            return;
        }

        var t = Math.Clamp((Environment.TickCount64 - _startTicks) / (double)_durationMs, 0.0, 1.0);
        var eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic, the product's one slide feel
        var rect = new PixelRect(
            Lerp(_from.X, _to.X, eased),
            Lerp(_from.Y, _to.Y, eased),
            Lerp(_from.Width, _to.Width, eased),
            Lerp(_from.Height, _to.Height, eased));

        if (_from.Width == _to.Width && _from.Height == _to.Height)
        {
            WindowPlacer.MoveWithoutResizing(hwnd, rect.X, rect.Y);
        }
        else
        {
            WindowPlacer.MoveWithoutActivating(hwnd, rect);
        }

        Current = rect;
        _applied?.Invoke(rect);

        if (t >= 1.0)
        {
            Stop();
            _completed?.Invoke();
        }
    }

    private static int Lerp(int from, int to, double t) => from + (int)Math.Round((to - from) * t);
}
