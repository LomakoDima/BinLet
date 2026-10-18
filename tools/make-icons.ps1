# Generates the Binlet icon pair (empty / full) as multi-size PNG-compressed .ico files.
# Usage: powershell -File tools/make-icons.ps1   (writes to BinLet/Assets)
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 48, 64, 256
$outDir = Join-Path $PSScriptRoot '..\BinLet\Assets'
New-Item -ItemType Directory -Force $outDir | Out-Null

$blue  = [System.Drawing.Color]::FromArgb(255, 47, 128, 206)
$blueFill = [System.Drawing.Color]::FromArgb(70, 47, 128, 206)

function New-RoundedRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Draw-Bin([System.Drawing.Graphics]$g, [bool]$full) {
    # Coordinates are on a 32x32 grid (the graphics object is pre-scaled).
    $g.SmoothingMode = 'AntiAlias'
    $stroke = New-Object System.Drawing.Pen($blue, 2.2)
    $stroke.LineJoin = 'Round'
    $solid = New-Object System.Drawing.SolidBrush($blue)

    if ($full) {
        # Two sheets of paper poking out of the bin.
        $paper = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
        $edge = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 120, 135, 150), 1.1)
        foreach ($s in @(@(11.5, 6.5, -16), @(20.5, 6.0, 13))) {
            $state = $g.Save()
            $g.TranslateTransform($s[0], $s[1])
            $g.RotateTransform($s[2])
            $path = New-RoundedRect -4 -5.5 8 11 1
            $g.FillPath($paper, $path)
            $g.DrawPath($edge, $path)
            $g.Restore($state)
        }
    } else {
        # Lid handle.
        $h = New-RoundedRect 12 3.2 8 5 1.6
        $g.DrawPath($stroke, $h)
    }

    # Body (tapered).
    $body = New-Object System.Drawing.Drawing2D.GraphicsPath
    $body.AddPolygon(@(
        (New-Object System.Drawing.PointF(6.5, 11.5)),
        (New-Object System.Drawing.PointF(25.5, 11.5)),
        (New-Object System.Drawing.PointF(23.6, 27.6)),
        (New-Object System.Drawing.PointF(8.4, 27.6))))
    if ($full) {
        $g.FillPath($solid, $body)
        $g.DrawPath($stroke, $body)
        $ribPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(150, 255, 255, 255), 1.8)
    } else {
        $g.FillPath((New-Object System.Drawing.SolidBrush($blueFill)), $body)
        $g.DrawPath($stroke, $body)
        $ribPen = New-Object System.Drawing.Pen($blue, 1.8)
    }
    $ribPen.StartCap = 'Round'; $ribPen.EndCap = 'Round'
    foreach ($x in 12.2, 16, 19.8) { $g.DrawLine($ribPen, $x, 15.5, $x, 24.3) }

    # Lid.
    $lid = New-RoundedRect 4 7.4 24 4.2 1.8
    $g.FillPath($solid, $lid)
}

function Render([int]$size, [bool]$full) {
    $ss = 4
    $big = New-Object System.Drawing.Bitmap(($size * $ss), ($size * $ss), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($big)
    $g.ScaleTransform($size * $ss / 32.0, $size * $ss / 32.0)
    Draw-Bin $g $full
    $g.Dispose()
    $small = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g2 = [System.Drawing.Graphics]::FromImage($small)
    $g2.InterpolationMode = 'HighQualityBicubic'
    $g2.PixelOffsetMode = 'HighQuality'
    $g2.CompositingMode = 'SourceCopy'
    $g2.DrawImage($big, 0, 0, $size, $size)
    $g2.Dispose(); $big.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $small.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $small.Dispose()
    return ,$ms.ToArray()
}

function Write-Ico([string]$path, [bool]$full) {
    $images = foreach ($s in $sizes) { ,(Render $s $full) }
    $fs = [System.IO.File]::Create($path)
    $bw = New-Object System.IO.BinaryWriter($fs)
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]
        $bw.Write([byte]($s % 256)); $bw.Write([byte]($s % 256))
        $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$images[$i].Length); $bw.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($img in $images) { $bw.Write($img) }
    $bw.Dispose(); $fs.Dispose()
}

Write-Ico (Join-Path $outDir 'bin-empty.ico') $false
Write-Ico (Join-Path $outDir 'bin-full.ico') $true
Write-Host 'Icons written to' (Resolve-Path $outDir)
