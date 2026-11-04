using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace KayoneZSoundpad.Views;

/// <summary>
/// Windows 11 başlık çubuğunu uygulama temasına boyar. Koyu temada beyaz
/// başlık şeridi kalması, konsol görünümünü bozuyordu.
/// </summary>
internal static class WindowChromeHelper
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            bool dark = IsDarkTheme();
            int immersive = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref immersive, sizeof(int));

            int caption = dark ? Rgb(0x14, 0x11, 0x0E) : Rgb(0xF8, 0xF4, 0xED);
            int border = dark ? Rgb(0x30, 0x28, 0x20) : Rgb(0xDC, 0xD2, 0xC3);
            int text = dark ? Rgb(0xF4, 0xED, 0xE3) : Rgb(0x19, 0x15, 0x12);

            DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref caption, sizeof(int));
            DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref border, sizeof(int));
            DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref text, sizeof(int));
        }
        catch
        {
            // DWM yoksa (ör. uzak oturum) varsayılan başlık çubuğu kalır.
        }
    }

    private static bool IsDarkTheme()
    {
        try
        {
            if (System.Windows.Application.Current?.Resources["Brush.Bg"] is SolidColorBrush brush)
            {
                var color = brush.Color;
                double luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
                return luminance < 0.5;
            }
        }
        catch { }
        return true;
    }

    /// <summary>COLORREF: 0x00BBGGRR.</summary>
    private static int Rgb(int r, int g, int b) => (b << 16) | (g << 8) | r;
}
