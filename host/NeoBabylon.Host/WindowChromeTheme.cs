using System.Runtime.InteropServices;
using NeoBabylon.Core;

namespace NeoBabylon.Host;

internal static class WindowChromeTheme
{
    private const int UseImmersiveDarkMode = 20;
    private const int CaptionColor = 35;
    private const int TextColor = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int valueSize);

    public static bool Apply(nint window, HostAppearance appearance)
    {
        if (window == 0) return false;

        var dark = appearance == HostAppearance.Dark ? 1 : 0;
        var caption = appearance == HostAppearance.Dark ? 0x001C1C1C : 0x00FFFFFF;
        var text = appearance == HostAppearance.Dark ? 0x00E8E8E8 : 0x003A2820;
        return DwmSetWindowAttribute(window, UseImmersiveDarkMode, ref dark, sizeof(int)) == 0
            && DwmSetWindowAttribute(window, CaptionColor, ref caption, sizeof(int)) == 0
            && DwmSetWindowAttribute(window, TextColor, ref text, sizeof(int)) == 0;
    }
}
