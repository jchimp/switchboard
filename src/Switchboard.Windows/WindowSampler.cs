using Microsoft.Extensions.Logging;
using Switchboard.Core.Status;
using Switchboard.Windows.Interop;

namespace Switchboard.Windows;

/// <summary>
/// The capture half of thumbnail-motion detection (ADR-0013): renders a window with
/// PrintWindow, downscales to a tiny sample, and returns a hash of the pixels. GDI objects
/// are created lazily and reused across ticks (the full-size scratch bitmap grows to the
/// largest window seen). Every failure degrades to "no sample", never an exception.
/// UI thread only, like everything else here.
/// </summary>
public sealed class WindowSampler : IDisposable
{
    private const int SampleSize = 24;
    private const int BytesPerPixel = 4;

    private readonly ILogger<WindowSampler> _log;
    private readonly byte[] _bits = new byte[SampleSize * SampleSize * BytesPerPixel];
    private nint _captureDc;
    private nint _captureBitmap;
    private int _captureWidth;
    private int _captureHeight;
    private nint _sampleDc;
    private nint _sampleBitmap;

    public WindowSampler(ILogger<WindowSampler> log)
    {
        _log = log;
    }

    /// <summary>The window's current pixel hash, or null when it can't be captured right now.</summary>
    public ulong? SampleHash(nint hwnd)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd) || NativeMethods.IsIconic(hwnd))
        {
            return null; // minimized windows render stale content; skip rather than lie
        }

        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return null;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0 || !EnsureSurfaces(width, height))
        {
            return null;
        }

        if (!NativeMethods.PrintWindow(hwnd, _captureDc, NativeMethods.PW_RENDERFULLCONTENT))
        {
            return null;
        }

        if (!NativeMethods.StretchBlt(
                _sampleDc, 0, 0, SampleSize, SampleSize,
                _captureDc, 0, 0, width, height, NativeMethods.SRCCOPY))
        {
            return null;
        }

        return NativeMethods.GetBitmapBits(_sampleBitmap, _bits.Length, _bits) > 0
            ? MotionSampler.Fnv1a(_bits)
            : null;
    }

    private bool EnsureSurfaces(int width, int height)
    {
        var screenDc = NativeMethods.GetDC(0);
        if (screenDc == 0)
        {
            return false;
        }

        try
        {
            if (_sampleDc == 0)
            {
                _sampleDc = NativeMethods.CreateCompatibleDC(screenDc);
                _sampleBitmap = NativeMethods.CreateCompatibleBitmap(screenDc, SampleSize, SampleSize);
                if (_sampleDc == 0 || _sampleBitmap == 0)
                {
                    _log.LogWarning("GDI sample surface creation failed; motion sampling disabled this session");
                    return false;
                }

                NativeMethods.SelectObject(_sampleDc, _sampleBitmap);
                NativeMethods.SetStretchBltMode(_sampleDc, NativeMethods.HALFTONE); // averages, so a cursor blink mostly disappears
            }

            if (_captureDc == 0 || width > _captureWidth || height > _captureHeight)
            {
                if (_captureBitmap != 0)
                {
                    NativeMethods.DeleteObject(_captureBitmap);
                }

                if (_captureDc == 0)
                {
                    _captureDc = NativeMethods.CreateCompatibleDC(screenDc);
                }

                _captureBitmap = NativeMethods.CreateCompatibleBitmap(screenDc, width, height);
                if (_captureDc == 0 || _captureBitmap == 0)
                {
                    return false;
                }

                NativeMethods.SelectObject(_captureDc, _captureBitmap);
                _captureWidth = width;
                _captureHeight = height;
            }

            return true;
        }
        finally
        {
            NativeMethods.ReleaseDC(0, screenDc);
        }
    }

    public void Dispose()
    {
        if (_captureBitmap != 0)
        {
            NativeMethods.DeleteObject(_captureBitmap);
            _captureBitmap = 0;
        }

        if (_captureDc != 0)
        {
            NativeMethods.DeleteDC(_captureDc);
            _captureDc = 0;
        }

        if (_sampleBitmap != 0)
        {
            NativeMethods.DeleteObject(_sampleBitmap);
            _sampleBitmap = 0;
        }

        if (_sampleDc != 0)
        {
            NativeMethods.DeleteDC(_sampleDc);
            _sampleDc = 0;
        }
    }
}
