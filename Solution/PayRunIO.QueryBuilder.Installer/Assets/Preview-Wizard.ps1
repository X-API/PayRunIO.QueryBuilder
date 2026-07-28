# Renders a preview of each wizard page by compositing the WiX text controls over the
# generated bitmaps at their real coordinates, so the layout can be checked without
# running the installer. Dialog units are converted at the standard 4/3 scale.
Add-Type -AssemblyName System.Drawing

$assets = $PSScriptRoot
# Written to the temp folder: these are a throwaway visual check, not build output.
$out    = Join-Path ([System.IO.Path]::GetTempPath()) "PayRunIO.InstallerPreview"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$scale  = 493.0 / 370.0   # bitmap width / dialog width in units

function DU { param([double]$v) return [int][math]::Round($v * $scale) }

# ---- Welcome page -------------------------------------------------------
$dlg = [System.Drawing.Image]::FromFile("$assets\DialogBanner.bmp")
$canvas = New-Object System.Drawing.Bitmap 493, 370
$g = [System.Drawing.Graphics]::FromImage($canvas)
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
$g.Clear([System.Drawing.Color]::FromArgb(240,240,240))
$g.DrawImageUnscaled($dlg, 0, 0)

# WiX draws Title and Description in the system dialog colour: near black.
$ink = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0,0,0))
$titleFont = New-Object System.Drawing.Font "Segoe UI", 12, ([System.Drawing.FontStyle]::Bold)
$bodyFont  = New-Object System.Drawing.Font "Segoe UI", 9

$titleRect = New-Object System.Drawing.RectangleF (DU 135), (DU 20), (DU 220), (DU 60)
$g.DrawString("Welcome to the PayRun.io Journal Manager Setup Wizard", $titleFont, $ink, $titleRect)

$descRect = New-Object System.Drawing.RectangleF (DU 135), (DU 80), (DU 220), (DU 60)
$g.DrawString("The Setup Wizard will install PayRun.io Journal Manager on your computer. Click Next to continue or Cancel to exit the Setup Wizard.", $bodyFont, $ink, $descRect)

$g.Dispose(); $dlg.Dispose()
$canvas.Save("$out\preview-welcome.png", [System.Drawing.Imaging.ImageFormat]::Png)
$canvas.Dispose()

# ---- Finish page --------------------------------------------------------
# Rendered separately because it carries the optional launch checkbox, which paints its
# own background from the bitmap: any off white tone shows as a pale block around it.
$dlg2 = [System.Drawing.Image]::FromFile("$assets\DialogBanner.bmp")
$canvas3 = New-Object System.Drawing.Bitmap 493, 370
$g3 = [System.Drawing.Graphics]::FromImage($canvas3)
$g3.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
$g3.Clear([System.Drawing.Color]::FromArgb(240,240,240))
$g3.DrawImageUnscaled($dlg2, 0, 0)

$ink3 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::Black)
$titleFont3 = New-Object System.Drawing.Font "Segoe UI", 12, ([System.Drawing.FontStyle]::Bold)
$bodyFont3 = New-Object System.Drawing.Font "Segoe UI", 9

$g3.DrawString("Completed the PayRun.io Journal Manager Setup Wizard", $titleFont3, $ink3,
    (New-Object System.Drawing.RectangleF (DU 135), (DU 20), (DU 220), (DU 60)))
$g3.DrawString("Click the Finish button to exit the Setup Wizard.", $bodyFont3, $ink3,
    (New-Object System.Drawing.RectangleF (DU 135), (DU 70), (DU 220), (DU 40)))

# The checkbox control at X=135 Y=190, drawn with the system control background so any
# mismatch against the bitmap is visible in the preview.
$cbX = DU 135; $cbY = DU 190
$sysBg = New-Object System.Drawing.SolidBrush ([System.Drawing.SystemColors]::Control)
$g3.FillRectangle($sysBg, $cbX, $cbY, (DU 220), 18)
$sysBg.Dispose()
$g3.DrawString("Launch PayRun.io Journal Manager", $bodyFont3, $ink3, ($cbX + 18), $cbY)
$g3.DrawRectangle([System.Drawing.Pens]::DimGray, $cbX, ($cbY + 2), 12, 12)

$titleFont3.Dispose(); $bodyFont3.Dispose(); $ink3.Dispose()
$g3.Dispose(); $dlg2.Dispose()
$canvas3.Save("$out\preview-finish.png", [System.Drawing.Imaging.ImageFormat]::Png)
$canvas3.Dispose()

# ---- Confirm page -------------------------------------------------------
$top = [System.Drawing.Image]::FromFile("$assets\TopBanner.bmp")
$canvas2 = New-Object System.Drawing.Bitmap 493, 200
$g2 = [System.Drawing.Graphics]::FromImage($canvas2)
$g2.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
$g2.Clear([System.Drawing.Color]::FromArgb(250,250,250))
$g2.DrawImageUnscaled($top, 0, 0)

$titleRect2 = New-Object System.Drawing.RectangleF (DU 15), (DU 15), (DU 300), (DU 15)
$g2.DrawString("Ready to install PayRun.io Journal Manager", $titleFont, $ink, $titleRect2)

$bodyRect2 = New-Object System.Drawing.RectangleF (DU 25), (DU 70), (DU 320), (DU 80)
$g2.DrawString("Click Install to begin the installation. Click Back to review or change any of your installation settings. Click Cancel to exit the wizard.", $bodyFont, $ink, $bodyRect2)

$titleFont.Dispose(); $bodyFont.Dispose(); $ink.Dispose()
$g2.Dispose(); $top.Dispose()
$canvas2.Save("$out\preview-confirm.png", [System.Drawing.Imaging.ImageFormat]::Png)
$canvas2.Dispose()

"previews written to $out"

