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
            $ratio = [Math]::Min($size / $original.Width, $size / $original.Height)
            $width = [Math]::Max(1,[int][Math]::Round($original.Width * $ratio))
            $height = [Math]::Max(1,[int][Math]::Round($original.Height * $ratio))
            $graphics.DrawImage($original,[int](($size-$width)/2),[int](($size-$height)/2),$width,$height)
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
