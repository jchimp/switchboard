# ADR-0002: Associate windows by title at launch, by HWND afterwards

**Status:** Accepted · 2026-09-04

## Context
Windows Terminal exposes no API to ask a window "what is your `-w` name?". We need to bind a registry row to a live HWND, reliably, and keep that binding while the shell inside changes the title freely.

Options considered:
1. Match by process — useless; all WT windows share one process.
2. Poll for a new WT-class window immediately after launch — racy with two launches in flight.
3. Set the initial tab title to the registry name (`--title <name>`) and bind the first WT-class window whose title equals a *pending* name. Track by HWND from then on.
4. `--suppressApplicationTitle` so the title is always the name — reliable, but the title is then useless for status.

## Decision
Option 3 as the default; option 4 is available per-terminal (`lockTitle`, ADR-0003). `WindowTracker.ExpectWindow(name)` opens a 15 s window during which `EVENT_OBJECT_SHOW`/`NAMECHANGE` on a `CASCADIA_HOSTING_WINDOW_CLASS` window with an exact-title match binds it. After binding, only `EVENT_OBJECT_DESTROY` unbinds.

Restart recovery (`RecoverByTitle`) is best-effort: prefix match on the name. Unmatched windows are surfaced as "unbound" so the user can adopt them (M2).

## Consequences
- Registry names must be valid, unique, and used verbatim as titles. The name regex enforces it.
- A shell that overwrites the title within the first few ms could beat the hook; in practice WT shows the window with the `--title` value first. If this bites, `lockTitle` is the escape hatch.
- Two terminals launched in the same 15 s window with different names bind correctly; two with the *same* name is impossible (unique names).

## Amendment (2026-09-05): HWND-first restart recovery

Daily use showed title recovery failing routinely: shells retitle their windows constantly,
so after a manager restart most live terminals landed in "unbound" and needed manual
adoption. Fix: every bind writes the HWND to the terminal's registry row (`lastHwnd` —
runtime state, but the registry is the only durable store, invariant 3). Recovery now runs
two passes: `RecoverByHwnd` first (handles survive a manager restart; a stale handle after
a reboot fails `IsTerminalWindow` validation and is skipped), then `RecoverByTitle` for
whatever remains. Residual risk: a recycled HWND that happens to be a *different* WT window
mis-binds — vanishingly rare within one logon session, and one click in the rail fixes it.
