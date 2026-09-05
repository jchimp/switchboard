# ADR-0005: Status is inferred from outside the window

**Status:** Accepted · 2026-09-04

## Context
The strip is only worth having if "something changed" is reliable, and the PRD requires zero shell configuration. Windows Terminal publishes no activity API.

## Decision
Layer signal sources by cost, all fed into one `StatusEngine`:

1. **Title changes** (`EVENT_OBJECT_NAMECHANGE`) → per-terminal, then global regex rules; an unmatched change still counts as activity.
2. **Taskbar flash / BEL** → Attention.
3. **Thumbnail motion** (M2) → Working. Catches long builds that never touch the title.
4. **Named pipe** (M4, opt-in) → exact state from a one-line shell hook.

State machine: Attention is sticky until the window is focused; Working decays to Idle after 20 s; Closed only from window destruction.

## Consequences
- Some false "Working" on chatty prompts. Acceptable; the strip is a hint, not an alarm.
- The engine is pure and tested with `FakeTimeProvider`; sources are thin adapters.
