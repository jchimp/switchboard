# CLAUDE.md — working on Switchboard

Read this before touching the code. It is the contract between the human and any agent working in this repo.

## What this is

A WPF/.NET 8 utility that names, colors, tracks and switches between Windows Terminal windows. Product intent is in `PRD.md`, sequencing in `ROADMAP.md`, and the *why* behind every non-obvious shape in `docs/adr/`. The UI is built from the mockups in `docs/design/` — open them in a browser before changing any visual.

## Commands

```powershell
dotnet build                      # whole solution, warnings are errors
dotnet test                       # Core tests; cross-platform, must stay that way
dotnet run --project src/Switchboard.App
dotnet format                     # before every commit
```

Registry lives at `%APPDATA%\Switchboard\switchboard.json`. A sample is in `docs/switchboard.example.json`.

## Architecture in one paragraph

`Switchboard.Core` is pure logic and knows nothing about Windows or WPF; anything that can live there must. `Switchboard.Windows` is the Win32 surface — every P/Invoke is in `Interop/NativeMethods.cs`, nowhere else. `Switchboard.App` composes the graph in `App.xaml.cs`; `Services/TerminalSession` is the only object that sees the registry, the tracker, the status engine and the colorizer together, and the UI talks only to it. Windows are found by title at launch and tracked by HWND after that (ADR-0002).

## Invariants — do not break these

1. **Core stays platform-neutral.** No `System.Windows`, no `DllImport`, no `%APPDATA%` in Core. If a test can't run on Linux, it's in the wrong project.
2. **One P/Invoke file.** New Win32 calls go in `NativeMethods.cs` with the constant block they belong to.
3. **The registry is the state.** Nothing durable lives anywhere else. Writes go through `IRegistryStore.SaveAsync` (atomic, backed up). Never hand-write JSON.
4. **Name == wt window name == initial title.** Changing that triple breaks window association. See ADR-0002 before proposing otherwise.
5. **`wt` arguments use `ArgumentList`.** Never build a command string. Add a test in `WtCommandBuilderTests` for any new argument.
6. **Attention is sticky until focused.** Don't add code paths that clear it for convenience.
7. **The strip never activates.** `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`, `ShowActivated=False`, `Focusable=False`. It must never steal focus from a terminal.
8. **WinEvent callbacks run on the UI thread.** The tracker's dictionaries are unsynchronised on purpose; don't marshal them elsewhere.
9. **User regexes are hostile input.** Compile with a timeout, swallow `ArgumentException`, never let a bad rule crash the tracker.
10. **Motion only for attention.** The pulse is the single unprompted animation. Everything else moves only in response to the user.

## Conventions

- C# 12, file-scoped namespaces, `sealed` by default, records for data, `required` for mandatory init.
- Nullable is on and enforced. `!` needs a comment saying why it's safe.
- Logging via `ILogger<T>`; no `Console.WriteLine`, no `Debug.WriteLine`.
- Async: `Task` all the way; `async void` only on event handlers and it must try/catch.
- Names are product words: `TerminalDefinition`, `StripWindow`, `Attention`. Not `Item`, `Manager2`, `Helper`.
- Tests: xunit, one class per unit, method names read as sentences (`Attention_is_sticky_until_focused`).
- Commits: imperative subject, body explains *why*. Reference the ADR or ROADMAP item.

## When you change behaviour

- New setting → add to the record in `RegistryFile.cs` with a default, bump `CurrentVersion` only if old files can't load, add a `Migrate` step, add a store test.
- New hotkey → `HotkeySettings` + `HotkeyBindings.RegisterAll` + a `HotkeyChordTests` case if the chord shape is new.
- New status source → feed it into `StatusEngine` via a new public method; add an engine test; document it in PRD R19.
- New Win32 call → `NativeMethods.cs`, then a small typed wrapper (`FrameColorizer`, `WindowFocuser` are the pattern).
- Anything that changes the shape of the product → an ADR first, in `docs/adr/`, numbered, with Status/Context/Decision/Consequences.

## Things not to do

- Don't add a settings UI before M4; edit the JSON.
- Don't reach for `--suppressApplicationTitle` globally to "fix" tracking — it kills title-based status. It's per-terminal (`lockTitle`) for a reason (ADR-0003).
- Don't add WinUI 3. ADR-0001 explains why WPF.
- Don't introduce a message bus, mediator, or event aggregator. `TerminalSession` events are enough at this size.
- Don't poll `EnumWindows` on a timer for tracking. The WinEvent hook is the design; polling is only for restart recovery.
- Don't hand a real registry file to a test. Tests use a temp path.

## Current milestone

M0 → M1. The code has not been compiled on a Windows machine yet. The first job is `dotnet build`, fix what the compiler finds, `dotnet test`, then run it and confirm a colored WT window appears on `Ctrl+Alt+N`.
