# ADR-0013: Thumbnail motion is detected by hashing PrintWindow captures on the 1 s tick

**Status:** Accepted · 2026-09-04 · Implements PRD R19 source 3

## Context
Title rules miss the most common "working" signal: a long build or streaming output that
never touches the window title. PRD R19 lists "thumbnail motion" as a status source, but
DWM thumbnails cannot be read back — `DwmRegisterThumbnail` composites straight to the
destination window's surface, so the rail's thumbnails (ADR-0012) provide nothing to
compare. Detecting motion means capturing pixels ourselves, and that has a real CPU cost
against the "idle CPU effectively zero" non-functional.

## Decision
- On the existing `TerminalSession` 1 s tick (no new timer), sample eligible windows:
  `PrintWindow(PW_RENDERFULLCONTENT)` into a reused window-sized bitmap, `StretchBlt`
  (HALFTONE) down to a reused 24×24 sample, FNV-1a hash the bits. A changed hash since the
  last look calls `StatusEngine.OutputObserved` → Working unless already Attention.
- **Eligible**: bound, `Idle` or `Working`, not the foreground window, not minimized.
  Attention windows are skipped (sticky anyway — sampling could tell us nothing), and the
  focused window doesn't need a motion source: the user is looking at it.
- **Capped**: at most 3 windows per tick, round-robin, so a window is sampled at worst
  every ⌈n/3⌉ seconds. A few seconds of latency on a Working indicator is invisible; the
  cap keeps worst-case cost flat as terminals multiply.
- The scheduler, eligibility rule, and hash live in pure Core (`MotionSampler`) so the
  policy is unit-tested; only the GDI capture (`WindowSampler`) is Windows-bound.
- Opt-out: `thumbnailMotionEnabled` (root setting, default **true**). No version bump.
- The first sample of a window is a baseline, never motion — a fresh manager start must
  not flip every terminal to Working.

## Consequences
- This bends "idle CPU effectively zero": PrintWindow on a large terminal costs a few ms,
  so the ceiling is roughly 3 captures/s regardless of how many terminals exist. Measured
  cost goes here after hardware verification: `TBD`.
- Sampling runs whether or not the rail is open — the rail's DWM thumbnails are unrelated.
- A terminal with a blinking cursor visible in the sampled frame can ping-pong
  Working/Idle; the 24×24 downscale usually averages a cursor away, but if it proves noisy
  the fix is comparing with a small tolerance, not shrinking the sample further.
- `PW_RENDERFULLCONTENT` is required: Windows Terminal is composition-rendered and a plain
  `PrintWindow` returns black for it.
