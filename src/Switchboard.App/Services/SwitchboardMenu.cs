using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using Switchboard.App.ViewModels;
using Switchboard.Windows;

namespace Switchboard.App.Services;

/// <summary>
/// The one definition of the Switchboard menu: the strip's right-click menu, the rail's
/// right-click menu and the rail's "…" button all build from here, so they can't drift.
/// Open them through <see cref="AttachTo"/> or <see cref="OpenUnder"/>, which add the focus
/// round trip that makes a menu over a non-activating window close properly.
/// The popup is its own HWND and takes focus only while open, at the user's explicit
/// request, then hands it back; the strip and rail themselves never activate (ADR-0006/0012).
/// </summary>
public static class SwitchboardMenu
{
    // Only one menu is open at a time (UI thread), so one slot is enough.
    private static nint _restoreTo;

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
        GiveFocusBack(menu);

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

    /// <summary>Makes <paramref name="menu"/> the owner's right-click menu, with the focus round trip.</summary>
    public static void AttachTo(FrameworkElement owner, ContextMenu menu)
    {
        owner.ContextMenu = menu;

        // ContextMenuOpening fires before the menu opens, which is the order that matters.
        owner.ContextMenuOpening += (_, _) => TakeFocus(owner);
    }

    /// <summary>Opens <paramref name="menu"/> under <paramref name="target"/>, with the focus round trip.</summary>
    public static void OpenUnder(ContextMenu menu, FrameworkElement target)
    {
        TakeFocus(target);
        menu.PlacementTarget = target;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    /// <summary>
    /// The first half of the focus round trip. Windows routes clicks and keys to a menu only
    /// while its app owns the foreground; the strip and rail never do (invariant 7), so a
    /// menu over them ignores Esc and never sees a click elsewhere. The owner window takes the
    /// foreground *before* the menu opens: a foreground change cancels mouse capture, and a
    /// menu that loses capture closes, so taking it after opening shut the menu at once. The
    /// user just clicked, so the foreground lock allows it.
    /// </summary>
    private static void TakeFocus(FrameworkElement owner)
    {
        _restoreTo = WindowFocuser.CurrentForeground();
        if (Window.GetWindow(owner) is { } window && new WindowInteropHelper(window).Handle is var hwnd && hwnd != 0)
        {
            WindowFocuser.Focus(hwnd);
        }
    }

    /// <summary>
    /// The second half: when the menu closes, focus goes back to the window that had it,
    /// unless a menu item moved it somewhere real, such as the settings window.
    /// ApplicationIdle runs after the clicked item's command, so that window already holds
    /// the foreground and is left alone.
    /// </summary>
    private static void GiveFocusBack(ContextMenu menu) =>
        menu.Closed += (_, _) => menu.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            var now = WindowFocuser.CurrentForeground();
            var focusIsStranded = now == 0 || WindowFocuser.IsNonActivating(now);
            if (focusIsStranded && !WindowFocuser.IsNonActivating(_restoreTo))
            {
                WindowFocuser.Focus(_restoreTo);
            }
        });

    /// <summary>
    /// Menu text shown as typed. A string header treats "_" as an access-key marker, so a
    /// name like <c>api_server</c> would lose its underscore; doubling escapes it.
    /// </summary>
    public static string Literal(string text) => text.Replace("_", "__");
}
