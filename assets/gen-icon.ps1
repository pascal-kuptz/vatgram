# Generates assets\vatgram.ico from the brand recipe (gradient roundsquare + bold "v"),
# multi-resolution: 16/32/48/64/128/256.
#
# Uses BMP DIB encoded entries for max compatibility — Windows Explorer, MSBuild's
# ApplicationIcon embedding, and System.Drawing.Icon all read these reliably.
# (PNG-encoded ICO entries work in modern Windows but trip up older tooling
# including the apphost icon patcher used during dotnet publish.)

Add-Type -AssemblyName System.Drawing

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out  = Join-Path $here 'vatgram.ico'

function New-FrameBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.TextRenderingHint  = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $rect = New-Object System.Drawing.RectangleF 0, 0, $size, $size
    $top    = [System.Drawing.Color]::FromArgb(255, 0x38, 0xA3, 0xF0)
    $bottom = [System.Drawing.Color]::FromArgb(255, 0x14, 0x6E, 0xC8)
    $brush  = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $top, $bottom, 45.0

    # Rounded rectangle path
    $r = [float]($size * 0.25)
    $d = $r * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0,            0,            $d, $d, 180, 90)
    $path.AddArc($size - $d,   0,            $d, $d, 270, 90)
    $path.AddArc($size - $d,   $size - $d,   $d, $d,   0, 90)
    $path.AddArc(0,            $size - $d,   $d, $d,  90, 90)
    $path.CloseFigure()
    $g.FillPath($brush, $path)

    # Bold "v" centered
    $font = New-Object System.Drawing.Font 'Segoe UI', ([float]($size * 0.55)), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $sf   = New-Object System.Drawing.StringFormat
    $sf.Alignment     = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString('v', $font, [System.Drawing.Brushes]::White, $rect, $sf)

    $g.Dispose()
    $brush.Dispose()
    $path.Dispose()
    $font.Dispose()
    $sf.Dispose()
    return $bmp
}

function Get-BmpDibEntry([System.Drawing.Bitmap]$bmp) {
    $sz = $bmp.Width
    $bmpRect = New-Object System.Drawing.Rectangle 0, 0, $sz, $sz
    $data    = $bmp.LockBits($bmpRect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride  = $data.Stride
    $raw     = New-Object byte[] ($stride * $sz)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $raw, 0, $raw.Length)
    $bmp.UnlockBits($data)

    # ICO BMP DIB stores pixels bottom-up
    $pixels = New-Object byte[] $raw.Length
    for ($y = 0; $y -lt $sz; $y++) {
        [Array]::Copy($raw, $y * $stride, $pixels, ($sz - 1 - $y) * $stride, $stride)
    }

    # AND mask: 1 bpp, rows padded to 4 bytes, top-down. Empty (alpha already in BGRA).
    $maskRowBytes = [math]::Ceiling($sz / 32) * 4
    $mask = New-Object byte[] ($maskRowBytes * $sz)

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([uint32]40)                  # biSize
    $bw.Write([int32]$sz)                  # biWidth
    $bw.Write([int32]($sz * 2))            # biHeight (doubled for AND mask, per ICO spec)
    $bw.Write([uint16]1)                   # biPlanes
    $bw.Write([uint16]32)                  # biBitCount
    $bw.Write([uint32]0)                   # biCompression = BI_RGB
    $bw.Write([uint32]$pixels.Length)      # biSizeImage
    $bw.Write([int32]0)                    # biXPelsPerMeter
    $bw.Write([int32]0)                    # biYPelsPerMeter
    $bw.Write([uint32]0)                   # biClrUsed
    $bw.Write([uint32]0)                   # biClrImportant
    $bw.Write($pixels)
    $bw.Write($mask)
    $bw.Close()
    return ,$ms.ToArray()
}

$sizes = @(16, 32, 48, 64, 128, 256)
$entries = @()
foreach ($sz in $sizes) {
    $bmp = New-FrameBitmap $sz
    $entries += , @{ Size = $sz; Data = Get-BmpDibEntry $bmp }
    $bmp.Dispose()
}

$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter $fs
try {
    $bw.Write([uint16]0)              # reserved
    $bw.Write([uint16]1)              # type = 1 (ICO)
    $bw.Write([uint16]$entries.Count) # image count

    $offset = 6 + (16 * $entries.Count)
    foreach ($e in $entries) {
        $w = if ($e.Size -ge 256) { 0 } else { [byte]$e.Size }
        $bw.Write([byte]$w)             # width  (0 = 256)
        $bw.Write([byte]$w)             # height (0 = 256)
        $bw.Write([byte]0)              # color count
        $bw.Write([byte]0)              # reserved
        $bw.Write([uint16]1)            # planes
        $bw.Write([uint16]32)           # bit count
        $bw.Write([uint32]$e.Data.Length)
        $bw.Write([uint32]$offset)
        $offset += $e.Data.Length
    }
    foreach ($e in $entries) { $bw.Write($e.Data) }
}
finally {
    $bw.Close()
    $fs.Close()
}

Write-Host "OK   $out  ($([math]::Round((Get-Item $out).Length / 1KB, 1)) KB, $($sizes.Count) frames)"
