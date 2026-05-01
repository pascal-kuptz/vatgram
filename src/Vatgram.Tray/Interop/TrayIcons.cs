using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SystemBitmap = System.Drawing.Bitmap;
using DrawingIcon = System.Drawing.Icon;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace Vatgram.Tray.Interop;

internal static class TrayIcons
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static DrawingIcon Brand(bool connected = true) => Render(16, connected);

    private static DrawingIcon Render(int size, bool connected)
    {
        var topColor = connected ? WpfColor.FromRgb(0x38, 0xA3, 0xF0) : WpfColor.FromRgb(0x6B, 0x6B, 0x73);
        var bottomColor = connected ? WpfColor.FromRgb(0x14, 0x6E, 0xC8) : WpfColor.FromRgb(0x46, 0x46, 0x4E);

        var dv = new DrawingVisual();
        using (var ctx = dv.RenderOpen())
        {
            var rect = new Rect(0, 0, size, size);
            var radius = size * 0.25;
            var fill = new LinearGradientBrush(topColor, bottomColor, 45);
            ctx.DrawRoundedRectangle(fill, null, rect, radius, radius);

            var ft = new FormattedText("v",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                size * 0.62, WpfBrushes.White, 96);
            ft.SetFontWeight(FontWeights.Bold);
            ctx.DrawText(ft, new WpfPoint((size - ft.Width) / 2, (size - ft.Height) / 2 - size * 0.04));
        }

        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        ms.Position = 0;
        using var bmp = new SystemBitmap(ms);
        var hIcon = bmp.GetHicon();
        try { return (DrawingIcon)DrawingIcon.FromHandle(hIcon).Clone(); }
        finally { DestroyIcon(hIcon); }
    }

    public static BitmapSource Logo(int size)
    {
        var topColor = WpfColor.FromRgb(0x38, 0xA3, 0xF0);
        var bottomColor = WpfColor.FromRgb(0x14, 0x6E, 0xC8);
        var dv = new DrawingVisual();
        using (var ctx = dv.RenderOpen())
        {
            var rect = new Rect(0, 0, size, size);
            var radius = size * 0.25;
            var fill = new LinearGradientBrush(topColor, bottomColor, 45);
            ctx.DrawRoundedRectangle(fill, null, rect, radius, radius);

            var ft = new FormattedText("v",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                size * 0.62, WpfBrushes.White, 96);
            ft.SetFontWeight(FontWeights.Bold);
            ctx.DrawText(ft, new WpfPoint((size - ft.Width) / 2, (size - ft.Height) / 2 - size * 0.04));
        }
        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }
}
