using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Switchboard.Core.Registry;

namespace Switchboard.App.Services;

/// <summary>
/// The settings window's live-apply path (ADR-0018). Edits are folded into one pending
/// registry and written after a short quiet period, so dragging a slider is a couple of
/// saves rather than one per pixel.
/// </summary>
/// <remarks>
/// <para>
/// Every write fires <see cref="TerminalSession.RegistryChanged"/>, which the settings pages
/// also listen to so a hand-edit reaches them. <see cref="IsApplyingOwnEdit"/> tells a page
/// the change it is hearing is the echo of its own save — without that check, the round trip
/// would overwrite the text box the user is still typing in.
/// </para>
/// <para>
/// Shared by the root view model and every page rather than owned by one of them: the pages
/// would otherwise need a reference back to their parent purely to reach this.
/// </para>
/// </remarks>
public sealed class SettingsApplyQueue
{
    /// <summary>Long enough to swallow a slider drag, short enough that the file feels live.</summary>
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(400);

    private readonly TerminalSession _session;
    private readonly ILogger<SettingsApplyQueue> _log;
    private readonly DispatcherTimer _timer;
    private RegistryFile? _pending;

    public SettingsApplyQueue(TerminalSession session, ILogger<SettingsApplyQueue> log)
    {
        _session = session;
        _log = log;
        _timer = new DispatcherTimer { Interval = QuietPeriod };
        _timer.Tick += async (_, _) => await FlushAsync();
    }

    /// <summary>True while a save started here is fanning out; pages skip their re-sync.</summary>
    public bool IsApplyingOwnEdit { get; private set; }

    /// <summary>Folds an edit into the pending registry and restarts the quiet period.</summary>
    public void Queue(Func<RegistryFile, RegistryFile> edit)
    {
        _pending = edit(_pending ?? _session.Registry);
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>Writes any pending edit now. Called when the window hides, so nothing is lost.</summary>
    public async Task FlushAsync()
    {
        _timer.Stop();
        if (_pending is not { } pending)
        {
            return;
        }

        _pending = null;
        IsApplyingOwnEdit = true;
        try
        {
            await _session.ApplySettingsAsync(pending);
        }
        catch (Exception ex)
        {
            // A failed write must not take the window down; the file keeps its last good state.
            _log.LogError(ex, "Applying settings failed");
        }
        finally
        {
            IsApplyingOwnEdit = false;
        }
    }
}
