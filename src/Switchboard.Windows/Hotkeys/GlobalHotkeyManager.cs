using Microsoft.Extensions.Logging;
using Switchboard.Core.Hotkeys;
using Switchboard.Windows.Interop;

namespace Switchboard.Windows.Hotkeys;

/// <summary>
/// Registers system-wide hotkeys against a message-only or UI window and dispatches
/// <c>WM_HOTKEY</c>. The host must call <see cref="HandleMessage"/> from its window
/// procedure (WPF: <c>HwndSource.AddHook</c>).
/// </summary>
public sealed class GlobalHotkeyManager : IDisposable
{
    private readonly ILogger<GlobalHotkeyManager> _log;
    private readonly Dictionary<int, (HotkeyChord Chord, Action Handler)> _bindings = new();
    private nint _hwnd;
    private int _nextId = 1;

    public GlobalHotkeyManager(ILogger<GlobalHotkeyManager> log)
    {
        _log = log;
    }

    public void Attach(nint hwnd)
    {
        if (_hwnd != 0)
        {
            throw new InvalidOperationException("Already attached.");
        }

        _hwnd = hwnd;
    }

    /// <summary>Registers a chord. Returns false (and logs) when Windows refuses it, e.g. another app owns it.</summary>
    public bool Register(HotkeyChord chord, Action handler)
    {
        EnsureAttached();

        if (!VirtualKeys.TryResolve(chord.Key, out var vk))
        {
            _log.LogWarning("Hotkey {Chord}: unknown key '{Key}'", chord, chord.Key);
            return false;
        }

        var id = _nextId++;
        if (!NativeMethods.RegisterHotKey(_hwnd, id, ToNative(chord.Modifiers) | NativeMethods.MOD_NOREPEAT, vk))
        {
            _log.LogWarning("Hotkey {Chord} is already taken by another application", chord);
            return false;
        }

        _bindings[id] = (chord, handler);
        _log.LogDebug("Registered hotkey {Chord} (id {Id})", chord, id);
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _bindings.Keys.ToList())
        {
            NativeMethods.UnregisterHotKey(_hwnd, id);
        }

        _bindings.Clear();
    }

    /// <summary>Wire into the window procedure. Returns true when the message was a hotkey we own.</summary>
    public bool HandleMessage(int msg, nint wParam)
    {
        if (msg != NativeMethods.WM_HOTKEY || !_bindings.TryGetValue((int)wParam, out var binding))
        {
            return false;
        }

        try
        {
            binding.Handler();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Hotkey handler for {Chord} threw", binding.Chord);
        }

        return true;
    }

    public void Dispose() => UnregisterAll();

    private void EnsureAttached()
    {
        if (_hwnd == 0)
        {
            throw new InvalidOperationException("Call Attach(hwnd) before registering hotkeys.");
        }
    }

    private static uint ToNative(HotkeyModifiers m)
    {
        uint n = 0;
        if (m.HasFlag(HotkeyModifiers.Ctrl))
        {
            n |= NativeMethods.MOD_CONTROL;
        }

        if (m.HasFlag(HotkeyModifiers.Alt))
        {
            n |= NativeMethods.MOD_ALT;
        }

        if (m.HasFlag(HotkeyModifiers.Shift))
        {
            n |= NativeMethods.MOD_SHIFT;
        }

        if (m.HasFlag(HotkeyModifiers.Win))
        {
            n |= NativeMethods.MOD_WIN;
        }

        return n;
    }
}
