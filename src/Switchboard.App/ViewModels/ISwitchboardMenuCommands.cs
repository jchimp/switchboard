using CommunityToolkit.Mvvm.Input;

namespace Switchboard.App.ViewModels;

/// <summary>
/// The commands behind the shared Switchboard menu. The strip and the rail both implement it,
/// so their menus are built by one <see cref="Services.SwitchboardMenu"/> and can't drift.
/// </summary>
public interface ISwitchboardMenuCommands
{
    IAsyncRelayCommand NewTerminalCommand { get; }

    IRelayCommand OpenSettingsCommand { get; }

    IRelayCommand ReapplyColorsCommand { get; }

    IAsyncRelayCommand ReloadRegistryCommand { get; }

    IRelayCommand QuitCommand { get; }
}
