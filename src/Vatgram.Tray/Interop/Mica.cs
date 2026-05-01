using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Vatgram.Tray.Interop;

internal static class Mica
{
    private const uint DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const uint DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const uint DWMWA_SYSTEMBACKDROP_TYPE = 38;

    public enum BackdropType { Auto = 0, None = 1, MainWindow = 2, TransientWindow = 3, TabbedWindow = 4 }
    public enum CornerPreference { Default = 0, DoNotRound = 1, Round = 2, RoundSmall = 3 }

    [DllImport("dwmapi.dll", PreserveSig = false)]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, uint attr, ref int value, int valueSize);

    public static void Apply(Window window, BackdropType backdrop = BackdropType.MainWindow, CornerPreference corner = CornerPreference.Round)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        try
        {
            int useDark = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            int c = (int)corner;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref c, sizeof(int));
            int type = (int)backdrop;
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref type, sizeof(int));
        }
        catch { /* best-effort */ }
    }
}
