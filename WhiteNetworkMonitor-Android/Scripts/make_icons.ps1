Add-Type -AssemblyName System.Drawing

$outRoot = Join-Path $PSScriptRoot "..\app\src\main\res"
$sizes = @{ "mdpi" = 48; "hdpi" = 72; "xhdpi" = 96; "xxhdpi" = 144; "xxxhdpi" = 192 }

function New-Icon([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $k = $s / 64.0   # масштаб от базовых 64px координат

    # фиолетовый градиентный круг
    $rect = New-Object System.Drawing.Rectangle(0, 0, $s, $s)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 157, 107, 255),
        [System.Drawing.Color]::FromArgb(255, 84, 33, 158), 45.0)
    $g.FillEllipse($bg, (1*$k), (1*$k), (62*$k), (62*$k))

    # светлая окантовка
    $ring = New-Object System.Drawing.Pen(
        [System.Drawing.Color]::FromArgb(80, 255, 255, 255), (1.6*$k))
    $g.DrawEllipse($ring, (2*$k), (2*$k), (60*$k), (60*$k))

    # монограмма WT
    $f = New-Object System.Drawing.Font("Segoe UI", (17*$k),
        [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $tr = New-Object System.Drawing.RectangleF(0, (6*$k), $s, (24*$k))
    $g.DrawString("WT", $f, [System.Drawing.Brushes]::White, $tr, $sf)

    # Wi-Fi арки
    $pen = New-Object System.Drawing.Pen(
        [System.Drawing.Color]::FromArgb(235, 240, 235, 255), (2.6*$k))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $cx = 32*$k; $cy = 45*$k
    foreach ($r in @(6, 11.5, 17)) {
        $rr = $r * $k
        $g.DrawArc($pen, ($cx-$rr), ($cy-$rr), (2*$rr), (2*$rr), 215, 130)
    }
    $dotR = 3*$k
    $g.FillEllipse([System.Drawing.Brushes]::White, ($cx-$dotR), ($cy-$dotR), (2*$dotR), (2*$dotR))

    # зелёный бейдж со статусом
    $badge = New-Object System.Drawing.SolidBrush(
        [System.Drawing.Color]::FromArgb(255, 42, 160, 67))
    $bs = 18*$k; $bx = 44*$k; $by = 44*$k
    $g.FillEllipse($badge, $bx, $by, $bs, $bs)
    $dark = New-Object System.Drawing.Pen(
        [System.Drawing.Color]::FromArgb(120, 10, 8, 24), (2*$k))
    $g.DrawEllipse($dark, $bx, $by, $bs, $bs)
    $w = New-Object System.Drawing.Pen([System.Drawing.Color]::White, (2.3*$k))
    $w.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $w.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($w, (48.4*$k), (53.4*$k), (51.6*$k), (56.6*$k))
    $g.DrawLine($w, (51.6*$k), (56.6*$k), (57.6*$k), (50.0*$k))

    $g.Dispose()
    return $bmp
}

foreach ($dpi in $sizes.Keys) {
    $dir = Join-Path $outRoot "mipmap-$dpi"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $bmp = New-Icon $sizes[$dpi]
    $bmp.Save((Join-Path $dir "ic_launcher.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host ("ic_launcher.png -> mipmap-{0} ({1}px)" -f $dpi, $sizes[$dpi])
}
Write-Host "OK"
