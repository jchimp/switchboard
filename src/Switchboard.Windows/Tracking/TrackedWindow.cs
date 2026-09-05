namespace Switchboard.Windows.Tracking;

/// <summary>A registry name bound to a live HWND.</summary>
public sealed record TrackedWindow(string Name, nint Hwnd, string LastTitle);
