# Generates app.ico with multiple sizes from discord_icon.png
# Small sizes = BMP entries (max compatibility), 128/256 = PNG entries.
Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\White\Desktop\2\discord_icon.png"
$outPath = Join-Path $PSScriptRoot "..\app.ico"

function New-Resized([System.Drawing.Image]$src, [int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($src, 0, 0, $s, $s)
    $g.Dispose()
    return ,$bmp
}

# XOR (BGRA bottom-up) + empty AND mask for one size
function Get-XorMask([System.Drawing.Bitmap]$bmp) {
    $s = $bmp.Width
    $rect = New-Object System.Drawing.Rectangle(0, 0, $s, $s)
    $bd = $bmp.LockBits($rect,
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $raw = New-Object byte[] ($s * $s * 4)
    [System.Runtime.InteropServices.Marshal]::Copy($bd.Scan0, $raw, 0, $raw.Length)
    $bmp.UnlockBits($bd)

    # swap RGBA -> BGRA happens automatically? No: memory is BGRA already in Format32bppArgb.
    # Rows must go bottom-up for ICO XOR data:
    $stride = $s * 4
    $xor = New-Object byte[] $raw.Length
    for ($y = 0; $y -lt $s; $y++) {
        $srcOff = $y * $stride
        $dstOff = ($s - 1 - $y) * $stride
        [Array]::Copy($raw, $srcOff, $xor, $dstOff, $stride)
    }

    # AND mask: 1bpp, rows padded to 32 bits, all zero = use alpha channel
    $maskStride = (($s + 31) -shr 5) -shl 2
    $mask = New-Object byte[] ($maskStride * $s)

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    # BITMAPINFOHEADER, height doubled (XOR+AND)
    $bw.Write([uint32]40); $bw.Write([int32]$s); $bw.Write([int32]($s * 2))
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]0)
    $bw.Write([uint32]($xor.Length + $mask.Length)); $bw.Write([int32]0)
    $bw.Write([int32]0); $bw.Write([uint32]0); $bw.Write([uint32]0)
    $bw.Write($xor); $bw.Write($mask)
    $bw.Flush()
    return ,$ms.ToArray()
}

function Get-PngData([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return ,$ms.ToArray()
}

$src = [System.Drawing.Image]::FromFile($srcPath)
$entries = @()   # arrays: w, h, data(bytes), isPng

foreach ($s in @(16, 24, 32, 48, 64)) {
    $b = New-Resized $src $s
    $entries += ,@($s, $s, (Get-XorMask $b), $false)
    $b.Dispose()
}
foreach ($s in @(128, 256)) {
    $b = New-Resized $src $s
    $entries += ,@($s, $s, (Get-PngData $b), $true)
    $b.Dispose()
}
$src.Dispose()

$fs = [IO.File]::Create($outPath)
$bw = New-Object IO.BinaryWriter($fs)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$entries.Count)

$offset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
    $dim = if ($e[0] -ge 256) { 0 } else { $e[0] }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim)
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]($(if ($e[3]) { 32 } else { 32 })))
    $bw.Write([uint32]$e[2].Length)
    $bw.Write([uint32]$offset)
    $offset += $e[2].Length
}
foreach ($e in $entries) { $bw.Write([byte[]]$e[2]) }
$bw.Flush(); $bw.Close()

Write-Host ("OK: {0} ({1} bytes, {2} sizes)" -f $outPath, (Get-Item $outPath).Length, $entries.Count)

