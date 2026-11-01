param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = $PSScriptRoot
$source = Join-Path $root 'assets\ResMode-logo.png'
$dist = $OutputDirectory
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$original = [System.Drawing.Bitmap]::new($source)
try {
    # Fit the supplied artwork without changing its colors, shape or transparency.
    $sizes = @(16,20,24,32,40,48,64,128,256)
    $images = foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size,$size,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $stream = [System.IO.MemoryStream]::new()
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingQuality = 'HighQuality'
            $graphics.InterpolationMode = 'HighQualityBicubic'
            $graphics.PixelOffsetMode = 'HighQuality'
            # The artwork sits in a wide 1536x1024 canvas with a large transparent margin; fitting the whole canvas into a
            # square left the shapes about half the icon's width. Crop to the shapes (x 352-1174, y 210-852, plus a small
            # margin) and let them fill the square, so the taskbar and tray icon read at the size of the others.
            $crop = [System.Drawing.Rectangle]::new(340,198,846,666)
            $ratio = $size * 0.94 / $crop.Width
            $width = [Math]::Max(1,[int][Math]::Round($crop.Width * $ratio))
            $height = [Math]::Max(1,[int][Math]::Round($crop.Height * $ratio))
            $target = [System.Drawing.Rectangle]::new([int][Math]::Round(($size-$width)/2),[int][Math]::Round(($size-$height)/2),$width,$height)
            $graphics.DrawImage($original,$target,$crop,[System.Drawing.GraphicsUnit]::Pixel)
            $bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
            if ($size -eq 256) { $bitmap.Save((Join-Path $dist 'ResMode-icon.png'),[System.Drawing.Imaging.ImageFormat]::Png) }
            ,$stream.ToArray()
        } finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
    $writer = [System.IO.BinaryWriter]::new([System.IO.File]::Create((Join-Path $dist 'ResMode.ico')))
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($i=0; $i -lt $sizes.Count; $i++) {
            $dim = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
            $writer.Write([byte]$dim); $writer.Write([byte]$dim); $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
            $offset += $images[$i].Length
        }
        foreach ($image in $images) { $writer.Write($image) }
    } finally { $writer.Dispose() }
} finally { $original.Dispose() }
