# ADR-0003: `lockTitle` is a per-terminal opt-in, not a global default

**Status:** Accepted · 2026-09-04

## Context
`--suppressApplicationTitle` makes tracking bulletproof but freezes the title, and titles are the cheapest status signal we have (ADR-0005). AI harnesses in particular set descriptive titles ("waiting for input") that are exactly what the strip should react to.

## Decision
Default off. Per-terminal `lockTitle: true` for terminals whose shell mangles titles in unhelpful ways, or where the user cares more about the label than about status.

## Consequences
- Title-based rules don't fire on locked terminals; flash and thumbnail-motion sources still do.
- The settings UI (M4) must make the trade-off visible next to the toggle.
