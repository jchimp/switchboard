using Switchboard.App.Settings;

namespace Switchboard.App.Services;

/// <summary>
/// Opens the settings window on demand. A thin indirection so the strip and rail view models
/// can offer "Settings…" without holding the window itself: resolving it directly would build
/// the whole settings graph during startup, and the strip is meant to be on screen in under
/// half a second.
/// </summary>
public sealed class SettingsLauncher
{
    private readonly Func<SettingsWindow> _resolve;

    public SettingsLauncher(Func<SettingsWindow> resolve)
    {
        _resolve = resolve;
    }

    public void Open() => _resolve().Open();
}
