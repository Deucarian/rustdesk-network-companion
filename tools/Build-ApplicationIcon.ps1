[CmdletBinding()]
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\Assets\RustDeskHop.png'),
    [string]$Destination = (Join-Path $PSScriptRoot '..\Assets\RustDeskHop.ico'),
    [ValidateRange(0, 0.2)]
    [double]$PaddingFraction = 0.015625,
    [ValidateRange(1, 255)]
    [byte]$AlphaThreshold = 16
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Ignore near-transparent export speckles when finding the artwork's bounds.
# The approved PNG is never written, redrawn, stretched, or recolored.
if (-not ('RustDeskHopIconBounds' -as [type])) {
    $drawingAssemblies = @([System.Drawing.Bitmap].Assembly.Location, [System.Drawing.Rectangle].Assembly.Location) | Select-Object -Unique
    Add-Type -ReferencedAssemblies $drawingAssemblies -TypeDefinition @'
using System;
using System.Drawing;
public static class RustDeskHopIconBounds
{
    public static Rectangle Measure(Bitmap image, byte alphaThreshold)
    {
        int left = image.Width, top = image.Height, right = -1, bottom = -1;
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            if (image.GetPixel(x, y).A < alphaThreshold) continue;
            left = Math.Min(left, x); top = Math.Min(top, y);
            right = Math.Max(right, x); bottom = Math.Max(bottom, y);
        }
        if (right < left) throw new InvalidOperationException("The icon artwork is fully transparent.");
        return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }
}
'@
}

# Only crop transparent padding and uniformly resize/encode; never redraw or recolor.
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$sourceImage = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $Source))
$frames = [System.Collections.Generic.List[byte[]]]::new()
try {
    if ($sourceImage.Width -ne $sourceImage.Height) { throw 'The application icon source must be square.' }
    $artBounds = [RustDeskHopIconBounds]::Measure($sourceImage, $AlphaThreshold)
    # Retain a source-pixel guard around the visible edge for antialiasing.
    $artBounds.Inflate(1, 1)
    $artBounds.Intersect([System.Drawing.Rectangle]::new(0, 0, $sourceImage.Width, $sourceImage.Height))
    Write-Output "Visible source bounds: $artBounds. Uniform frame padding: $($PaddingFraction * 100)% per side."
    foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $png = [System.IO.MemoryStream]::new()
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $available = $size * (1 - 2 * $PaddingFraction)
            $scale = [Math]::Min($available / $artBounds.Width, $available / $artBounds.Height)
            $width = $artBounds.Width * $scale
            $height = $artBounds.Height * $scale
            $target = [System.Drawing.RectangleF]::new(($size - $width) / 2, ($size - $height) / 2, $width, $height)
            $graphics.DrawImage($sourceImage, $target, [System.Drawing.RectangleF]$artBounds, [System.Drawing.GraphicsUnit]::Pixel)
            $bitmap.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add($png.ToArray())
        } finally {
            $png.Dispose()
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }
} finally {
    $sourceImage.Dispose()
}

$destinationPath = [System.IO.Path]::GetFullPath($Destination)
$output = [System.IO.File]::Create($destinationPath)
$writer = [System.IO.BinaryWriter]::new($output)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$index].Length)
        $writer.Write([uint32]$offset)
        $offset += $frames[$index].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally {
    $writer.Dispose()
    $output.Dispose()
}

Write-Output "Created $destinationPath with sizes $($sizes -join ', ')."
