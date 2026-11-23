# Switchboard — Product Requirements

## Problem

Jeremy runs 5–10 Windows Terminal windows at once: PowerShell for ops scripts, several WSL shells for Python/Node/Docker work, and two or three CLI AI harnesses. Three things go wrong all day:

1. **Which window is which.** Every WT window looks identical in the taskbar and in Alt-Tab. Finding "the one running the API" means cycling through them.
2. **Taskbar crowding.** Six WT entries push everything else out.
3. **Missed events.** A build finishes, a harness stops to ask a question, a compose stack dies — and nothing on screen says so until you happen to look.

## Users

One primary user (the author). Secondary: anyone who lives in Windows Terminal with more than four windows open. Not a general "terminal multiplexer" — tmux and WT's own panes cover intra-window layout. Switchboard is the layer *above* WT windows: a dock for a fixed set of terminals, kept apart from the one-off terminals the user opens from Start or the taskbar (ADR-0021).

## Goals

- Jump to any registered terminal in under a second, from any app, without touching the mouse.
- Know at a glance, without opening anything, which terminals are working / idle / waiting.
- Know, the moment a WT window is on screen, which registered terminal it is.
- Open a brand-new terminal from a global hotkey with zero prompts.
- Zero configuration inside the shells. Everything is observed from outside the WT window.

## Non-goals

- Replacing WT's tabs or panes.
- Running on macOS/Linux.
- Session persistence (restoring shell contents after reboot).
- A plugin system, themes, or a marketplace.

## Functional requirements

### Registry
- R1. Terminals are defined in `%APPDATA%\Switchboard\switchboard.json`: name, tag, color, WT profile, shell kind, distro, working dir, startup command, jump key, status rules, persistent flag.
- R2. Names are unique slugs and are used verbatim as the `wt -w` window name and initial title.
- R3. Writes are atomic and keep one backup.
- R4. Unknown or newer schema versions fail loudly; older versions migrate.

### Launch & tracking
- R5. Launching a terminal runs `wt.exe` (the terminal host's, R28) with the profile, cwd, title, tab color, and (if set) the startup command wrapped in the correct shell so the shell survives the command.
- R6. The manager binds the resulting HWND to the name and tracks it by HWND thereafter.
- R7. On manager restart, windows are re-bound by the HWND each terminal was last bound to. Windows are never adopted by title; any unbound WT window is listed in the rail's "…" menu (Adopt window) and can be adopted by hand (ADR-0020, ADR-0012).
- R8. When a bound window closes, its state becomes Closed; throwaway (non-persistent) terminals are removed from the registry.

### Color
- R9. Each terminal's color is painted on the WT window border (DWM, Win11), dimmed when unfocused, and passed as the tab color at launch.
- R10. Colors are re-applied on demand (hotkey) and automatically when a window is re-adopted.

### Strip (glance layer)
- R11. A thin strip on a configurable screen edge (top default; left/right supported) shows one colored segment per registered terminal in registry order.
- R12. Active terminal's segment glows; Attention pulses; Idle is dimmed; Closed is nearly transparent.
- R13. Clicking a segment focuses the terminal, or launches it if closed. If the terminal is already foreground and `strip.clickMinimizesFocused` is on (default), the click minimizes it instead; hotkey jumps always focus.
- R14. The strip never takes focus, never appears in Alt-Tab or the taskbar, and optionally reserves its pixels (app-bar mode).
- R15. Hovering opens the rail (M2).
- R23. Optionally (`autoHide`, off by default) the strip collapses to a thin sliver (`hiddenThickness`, default 2 px) after the cursor leaves; touching the screen edge slides it back out. Segment colors remain visible in the sliver. Ignored while app-bar mode is on (ADR-0010).
- R24. While collapsed, a terminal entering Attention pulses its segment color and slides the strip out to full thickness until that terminal is focused.

### Hotkeys
- R16. Global, configurable chords for: new terminal (picker), quiet new terminal (no UI), jump 1–9, jump to attention, cycle recent (MRU walk, ADR-0015), reapply colors, open switcher (ADR-0016), toggle rail (M2).
- R17. Chords another app already owns are skipped with a logged warning, never a crash.

### Status
- R18. States: Closed, Idle, Working, Attention. Attention is sticky until the window is focused.
- R19. Sources, in this order of delivery: title regex rules (per-terminal, then global); taskbar flash; thumbnail motion (M2); named-pipe from the shell (later, opt-in). Title rules and thumbnail motion are live; taskbar flash and the pipe are not yet wired, and the settings UI shows only the sources that exist.
- R20. Working decays to Idle after 20 s without activity.

### Taskbar
- R25. Optionally (`hideFromTaskbar`, off by default) bound windows' taskbar buttons are removed via `ITaskbarList::DeleteTab` while bound, and restored on unbind, on the flag turning off, and on exit. Unbound windows are never touched. Survives Explorer restarts via the `TaskbarCreated` broadcast. (ADR-0009)

### New terminal
- R21. A new terminal comes from the quick-launch defaults: next free `term-N` name, next unused palette hue, default profile, last-used directory (`quickLaunch.lastDirectory`). `Ctrl+Alt+Shift+N` registers and launches immediately with those defaults, no UI (ADR-0017).
- R22. `Ctrl+Alt+N` opens the picker: profile via Tab (read live from the terminal host's settings.json, or the installed WT's when no host is set), directory box, "keep" toggle (Ctrl+K), Enter launches, Esc cancels. Opens fully seeded, so a bare Enter equals the quiet launch (ADR-0017).

### Settings
- R26. A settings window edits the registry directly: general (terminal host folder, appearance import), terminals (add, edit, delete, reorder), strip, hotkeys, and global status rules. It opens on Terminals, where a splitter divides the list from the editor; the window's size and the split are remembered. Edits apply live and persist through the same atomic write as any other change, so hand-editing the file and using the window are the same operation (ADR-0018). It shows only settings the product reads; hotkey rows say when Windows refused a chord. In the terminal editor, the Windows Terminal profile is picked from the profiles WT actually has (the terminal host's when set); picking one sets the shell, and a WSL profile its distro, and a read-only panel shows what the profile runs and looks like (command line, starting folder, font, colors) with a button to open WT's settings.json. A name WT does not know is flagged, because WT would silently open its default profile.
- R27. Running terminal-host windows (R28) that are not bound to a terminal can be imported in bulk from the settings window: proposed name and color per window, edited before committing, registered and bound in one write.

### Terminal host
- R28. Optionally (`terminalHost.folder`, unset by default) Switchboard runs its terminals in a private portable Windows Terminal in that folder. Only windows whose process is that copy's `WindowsTerminal.exe` can be bound, adopted, imported, or listed as unbound; the user's own WT windows are never touched, and a Start-menu WT can never open a tab in a Switchboard window. Unset: `%LOCALAPPDATA%\Switchboard\wt` when it has `wt.exe` (ADR-0023), otherwise `wt.exe` on PATH and any WT window can be bound (ADR-0021).
- R29. Settings → Terminals can import the installed Windows Terminal's appearance (font, colors, cursor, opacity, acrylic, schemes, themes, window size) into the terminal host's settings.json, one shot, on the user's request. It overwrites the host's appearance for `profiles.defaults` and profiles matched by guid or name; it never copies command lines, starting folders, the profile list, key bindings, startup/window behaviour or `tabColor`. The original file is kept as `settings.json.switchboard.bak` (written once, by the first import). A hint offers it when the host still has WT's default look (ADR-0022).

## Non-functional

- Startup under 500 ms to strip visible.
- Idle CPU effectively zero: event-driven tracking, one 1 s timer.
- No admin rights. The release zip carries a per-user `install.ps1` (no admin, `%LOCALAPPDATA%\Programs\Switchboard`, uninstall from Settings > Apps); running the exe from the unzipped folder also works (ADR-0023).
- All logic outside Win32/WPF is unit-tested and runs on any OS.

## Success criteria for v1

- Author uses it for a full working week without reopening the taskbar to find a terminal.
- "Attention" fires for an AI harness waiting on input within 2 s, with no shell config.
