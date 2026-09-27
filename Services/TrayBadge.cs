using System.Globalization;
using Microsoft.UI;
using TrayDo.Tray;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using WinUIEx;

namespace TrayDo.Services;

/// <summary>
/// Shows the number of open hit list tasks as the tray icon. Owns the generated icon handle: the
/// shell copies the image when the tray icon is updated, so the previous one is freed after.
/// </summary>
internal sealed class TrayBadge
{
    private const uint Blue = 0xFF005FB8;
    private const uint White = 0xFFFFFFFF;
    private const uint IconFormatVersion = 0x00030000; // Required by CreateIconFromResourceEx.
    private const int MaxShown = 99; // Two characters is all that reads at 16 px.

    private HICON _icon;

    /// <summary>Draws <paramref name="count"/> on the tray icon. Returns false if the icon couldn't be made.</summary>
    public bool Show(TrayIcon trayIcon, int count)
    {
        int size = PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXSMICON, PInvoke.GetDpiForSystem());
        int shown = Math.Clamp(count, 0, MaxShown);
        string text = shown.ToString(CultureInfo.InvariantCulture);
        byte[] ico = BadgeFont.Render(text, size) is { } coverage
            ? BadgeIcon.Create(coverage, size, Blue, White)
            : BadgeIcon.Create(shown, size, Blue, White);

        HICON icon;
        nint handle;
        unsafe
        {
            fixed (byte* bits = ico)
            {
                icon = PInvoke.CreateIconFromResourceEx(
                    bits + BadgeIcon.ImageOffset,
                    (uint)(ico.Length - BadgeIcon.ImageOffset),
                    true,
                    IconFormatVersion,
                    size,
                    size,
                    IMAGE_FLAGS.LR_DEFAULTCOLOR);
            }

            handle = (nint)icon.Value;
        }

        if (handle == 0)
        {
            return false;
        }

        trayIcon.SetIcon(Win32Interop.GetIconIdFromIcon(handle));
        Release();
        _icon = icon;
        return true;
    }

    /// <summary>Frees the badge; call after the tray icon has been switched back to the app icon.</summary>
    public void Release()
    {
        if (!_icon.IsNull)
        {
            PInvoke.DestroyIcon(_icon);
            _icon = default;
        }
    }
}
