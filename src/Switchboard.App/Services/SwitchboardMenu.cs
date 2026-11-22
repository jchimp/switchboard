using System.Windows.Controls;
using Switchboard.App.ViewModels;

namespace Switchboard.App.Services;

/// <summary>
/// The one definition of the Switchboard menu: the strip's right-click menu, the rail's
/// right-click menu and the rail's "…" button all build from here, so they can't drift.
/// The popup is its own HWND and takes focus only while open, at the user's explicit
/// request; the strip and rail themselves never activate (ADR-0006/0012).
/// </summary>
public static class SwitchboardMenu
{
    /// <summary>Builds the menu.</summary>
    /// <param name="commands">The view model whose commands the items run.</param>
    /// <param name="beforeNewTerminal">
    /// Runs before "New terminal" launches; the rail closes itself here so the new window
    /// doesn't appear under it.
    /// </param>
    /// <param name="adopt">Optional "Adopt window" submenu, placed after "New terminal".</param>
    public static ContextMenu Build(
        ISwitchboardMenuCommands commands, Action? beforeNewTerminal = null, MenuItem? adopt = null)
    {
        var menu = new ContextMenu();

        // Command, not a Click that calls Execute: the item then greys out while a launch is
        // running. MenuItem raises Click before it runs the command, so the hook still runs first.
        var newTerminal = new MenuItem { Header = "New terminal", Command = commands.NewTerminalCommand };
        if (beforeNewTerminal is not null)
        {
            newTerminal.Click += (_, _) => beforeNewTerminal();
        }

        menu.Items.Add(newTerminal);

        if (adopt is not null)
        {
            menu.Items.Add(adopt);
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Settings…", Command = commands.OpenSettingsCommand });
        menu.Items.Add(new MenuItem { Header = "Reapply colors", Command = commands.ReapplyColorsCommand });
        menu.Items.Add(new MenuItem { Header = "Reload registry", Command = commands.ReloadRegistryCommand });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Quit Switchboard", Command = commands.QuitCommand });
        return menu;
    }

    /// <summary>
    /// Menu text shown as typed. A string header treats "_" as an access-key marker, so a
    /// name like <c>api_server</c> would lose its underscore; doubling escapes it.
    /// </summary>
    public static string Literal(string text) => text.Replace("_", "__");
}
