namespace Switchboard.Windows.Tracking;

/// <summary>A registry name bound to a live HWND.</summary>
public sealed record TrackedWindow(string Name, nint Hwnd, string LastTitle);

/// <summary>
/// A Windows Terminal window on the desktop that isn't bound to any registry name (ADR-0002).
/// Surfaced in the rail's "adopt as…" list.
/// </summary>
public sealed record UnboundWindow(nint Hwnd, string Title);
