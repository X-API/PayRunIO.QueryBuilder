# Regenerates the WiX installer artwork from the PayRun.io logo.
#
# Run this only when the branding changes; the generated bitmaps are committed alongside
# it so an ordinary build needs neither this script nor PowerShell image support.
#
#     pwsh .\Generate-Banners.ps1
#
# Both images are written as 24 bit BMP, which is what Windows Installer requires: it will
# not render a 32bpp bitmap with an alpha channel.
Add-Type -AssemblyName System.Drawing

$outDir   = $PSScriptRoot
$logoPath = Join-Path $outDir "PayRunIO_logo.png"

if (-not (Test-Path $logoPath)) {
    throw "Source logo not found: $logoPath"
}

# Brand colours sampled from the supplied logo.
$slate = [System.Drawing.Color]::FromArgb(51, 59, 65)    # #333B41 logo background
$green = [System.Drawing.Color]::FromArgb(0, 176, 0)     # #00B000 PayRun green

function Save-Bmp24 {
    param([System.Drawing.Bitmap]$Bitmap, [string]$Path)

    # Force 24bpp: Windows Installer will not render 32bpp bitmaps with an alpha channel.
    # The canvas is already 24bpp and fully painted, so it is copied as is rather than
    # cleared first, which would discard the gradient.
    $flat = New-Object System.Drawing.Bitmap $Bitmap.Width, $Bitmap.Height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($flat)
    $g.DrawImageUnscaled($Bitmap, 0, 0)
    $g.Dispose()
    $flat.Save($Path, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $flat.Dispose()
}

function New-Canvas {
    param([int]$Width, [int]$Height)

    $bmp = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    # Required so the keyed out logo blends with the panel rather than punching a hole.
    $g.CompositingMode   = [System.Drawing.Drawing2D.CompositingMode]::SourceOver

    # Flat brand colour. A gradient reads as muddy at this size, particularly where it
    # meets the light text area, so the panel is a solid block with a green edge instead.
    $brush = New-Object System.Drawing.SolidBrush $slate
    $g.FillRectangle($brush, 0, 0, $Width, $Height)
    $brush.Dispose()

    return @{ Bitmap = $bmp; Graphics = $g }
}

$logoRaw = [System.Drawing.Image]::FromFile($logoPath)

# The supplied logo is flattened onto its own #333B41 background. Drawing it directly
# leaves a visible rectangle wherever the panel gradient has darkened, so the background
# is keyed out to transparency and the artwork composited over the gradient instead.
function Get-TransparentLogo {
    param([System.Drawing.Image]$Source)

    # Cloned into 32bpp ARGB so the alpha written below persists. Drawing into a blank
    # ARGB surface instead leaves parts of it untouched and fully transparent, which then
    # reads as artwork and defeats the crop.
    $full = New-Object System.Drawing.Rectangle 0, 0, $Source.Width, $Source.Height
    $flat = New-Object System.Drawing.Bitmap $Source
    $bmp = $flat.Clone($full, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $flat.Dispose()

    # Tolerance covers the anti aliased fringe around the lettering. The bounds of the
    # surviving artwork are tracked at the same time: the supplied file has the logo in
    # the top left of a larger canvas, so scaling it whole would shrink the mark.
    $minX = $bmp.Width; $minY = $bmp.Height; $maxX = -1; $maxY = -1

    for ($y = 0; $y -lt $bmp.Height; $y++) {
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            $p = $bmp.GetPixel($x, $y)
            $dr = [math]::Abs($p.R - 51); $dg = [math]::Abs($p.G - 59); $db = [math]::Abs($p.B - 65)
            if ($dr -le 20 -and $dg -le 20 -and $db -le 20) {
                $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 51, 59, 65))
            }
            else {
                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    # Crop to the artwork so the logo fills the space it is given.
    $pad = 2
    $minX = [math]::Max(0, $minX - $pad); $minY = [math]::Max(0, $minY - $pad)
    $maxX = [math]::Min($bmp.Width - 1, $maxX + $pad); $maxY = [math]::Min($bmp.Height - 1, $maxY + $pad)

    $cropRect = New-Object System.Drawing.Rectangle $minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1)
    $cropped = $bmp.Clone($cropRect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bmp.Dispose()

    Write-Host ("  logo cropped to {0} x {1}" -f $cropped.Width, $cropped.Height)

    return $cropped
}

$logo = Get-TransparentLogo -Source $logoRaw
$logoRaw.Dispose()

# ---------------------------------------------------------------------------
# Side banner: 493 x 312, shown on the Welcome and Finish pages.
#
# This bitmap spans the WHOLE dialog, not just a left hand strip. WiX overlays the
# Title and Description text controls at X=135..355 in dialog units, which on this
# 493px wide bitmap is x=180..473. That text is drawn by Windows in dark ink on
# whatever the bitmap provides, so the right hand side must be left light and empty:
# artwork placed there is both overlapped and unreadable.
#
#   x 0                  164 |180                              493
#   +----------------------+--+---------------------------------+
#   | branded panel (logo) |gap| white, must stay clear for text |
#   +----------------------+--+---------------------------------+
#
# The panel stops at 164 rather than 180 so the green edge is not flush against the
# wizard text, and the light zone is pure white to match the dialog body.
# ---------------------------------------------------------------------------
# The panel ends short of the text controls so the green edge is not flush against the
# wizard text. The controls start at x=180, so the edge is pulled back to leave a gap.
$textZoneX = 164
$textGap   = 16

$dlg = New-Canvas -Width 493 -Height 312
$g = $dlg.Graphics

# Pure white, matching the dialog body exactly. The ExitDialog checkbox at X=135 (x=180px)
# paints its own background from this bitmap, so anything off white shows as a pale block
# around the tick box.
$lightBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$g.FillRectangle($lightBrush, $textZoneX, 0, (493 - $textZoneX), 312)
$lightBrush.Dispose()

# Thin green rule marking the edge of the branded panel. A clean line rather than a
# soft blend: the fade looked washed out where it met the light area.
$edgeBrush = New-Object System.Drawing.SolidBrush $green
$g.FillRectangle($edgeBrush, ($textZoneX - 2), 0, 2, 312)
$edgeBrush.Dispose()

# Logo sized to the branded column, which is 162px wide once the text gap is allowed for.
$logoW = 120
$logoH = [int]($logo.Height * ($logoW / $logo.Width))
$logoX = 20
$logoY = 40
$g.DrawImage($logo, $logoX, $logoY, $logoW, $logoH)

# Green rule and strapline beneath the mark, kept inside the branded column.
$pen = New-Object System.Drawing.Pen $green, 2
$g.DrawLine($pen, $logoX, ($logoY + $logoH + 12), ($logoX + $logoW), ($logoY + $logoH + 12))
$pen.Dispose()

$font = New-Object System.Drawing.Font "Segoe UI", 8, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Point)
$textBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(205, 210, 215))
$layout = New-Object System.Drawing.RectangleF $logoX, ($logoY + $logoH + 20), $logoW, 60
$g.DrawString("Payroll and auto`nenrolment API", $font, $textBrush, $layout)
$font.Dispose()
$textBrush.Dispose()

# No decorative swoosh: a translucent curve reads as texture over a gradient but as a
# stray diagonal on a flat panel. The solid block, green edge and logo carry the branding.

Save-Bmp24 -Bitmap $dlg.Bitmap -Path (Join-Path $outDir "DialogBanner.bmp")
$g.Dispose(); $dlg.Bitmap.Dispose()

# ---------------------------------------------------------------------------
# Top banner: 493 x 58, shown on the Confirm and Progress pages.
#
# WiX places the dialog title at X=15, Y=15 in dialog units, which is the TOP LEFT of
# this bitmap (x=20, y=20 in pixels), drawn in dark text. The left must therefore stay
# light and clear, with the logo moved to the right hand end.
#
# The title control is 300 units wide starting at X=15, so it can extend to x=420 in
# pixels. The logo is therefore kept small and pinned to the right edge, clear of it.
#
#   x 0                                              400        493
#   +------------------------------------------------+----------+
#   | light, dialog title drawn here                  | logo     |
#   +------------------------------------------------+----------+
# ---------------------------------------------------------------------------
$logoZoneX = 400

$top = New-Canvas -Width 493 -Height 58
$g2 = $top.Graphics

# Title area painted pure white to match the dialog body and keep the title legible.
$lightBrush2 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$g2.FillRectangle($lightBrush2, 0, 0, $logoZoneX, 58)
$lightBrush2.Dispose()

# Thin green rule where the light title area meets the branded block, matching the
# vertical edge on the side panel.
$edgeBrush2 = New-Object System.Drawing.SolidBrush $green
$g2.FillRectangle($edgeBrush2, $logoZoneX, 0, 2, 58)
$edgeBrush2.Dispose()

$tLogoW = 74
$tLogoH = [int]($logo.Height * ($tLogoW / $logo.Width))
$g2.DrawImage($logo, (493 - $tLogoW - 12), [int]((58 - $tLogoH) / 2), $tLogoW, $tLogoH)

# Green rule along the bottom edge to tie the banner to the side panel.
$pen2 = New-Object System.Drawing.Pen $green, 2
$g2.DrawLine($pen2, 0, 56, 493, 56)
$pen2.Dispose()

Save-Bmp24 -Bitmap $top.Bitmap -Path (Join-Path $outDir "TopBanner.bmp")
$g2.Dispose(); $top.Bitmap.Dispose()

$logo.Dispose()

foreach ($f in @("DialogBanner.bmp", "TopBanner.bmp")) {
    $p = Join-Path $outDir $f
    $i = [System.Drawing.Image]::FromFile($p)
    "  {0,-18} {1} x {2}  {3}  {4} KB" -f $f, $i.Width, $i.Height, $i.PixelFormat, [math]::Round((Get-Item $p).Length / 1KB)
    $i.Dispose()
}
