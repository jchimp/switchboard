# ADR-0004: Startup commands are wrapped in an explicit shell

**Status:** Accepted · 2026-09-04

## Context
`wt new-tab ... -- <cmd>` *replaces* the profile's command line. Passing `claude --resume` to an Ubuntu profile runs it as a Windows process (and fails); passing it to a pwsh profile runs it and closes the window when it exits.

## Decision
`TerminalDefinition.Shell` (`pwsh`, `windowsPowerShell`, `cmd`, `wsl`) selects a wrapper in `WtCommandBuilder.ShellWrapper`:

| Shell | Wrapper |
|---|---|
| pwsh | `pwsh.exe -NoLogo -NoExit -Command <cmd>` |
| Windows PowerShell | `powershell.exe -NoLogo -NoExit -Command <cmd>` |
| cmd | `cmd.exe /k <cmd>` |
| wsl | `wsl.exe [-d distro] [--cd dir] -- bash -lic "<cmd>; exec bash -l"` |

WSL takes its cwd via `--cd` (a Linux path); the others via `wt -d` (a Windows path). Login shells (`-l`) so nvm/uv/claude on PATH resolve.

## Consequences
- The window always survives the command.
- A WSL terminal with a command needs a `distro`; validation enforces it.
- Users on fish/zsh get bash for the wrapper only; their profile shell is unaffected when no command is set. A `shellExecutable` override is a later addition if needed.
