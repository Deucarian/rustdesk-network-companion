[CmdletBinding()]
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\Assets\RustDeskHop.png'),
    [string]$Destination = (Join-Path $PSScriptRoot '..\obj\branding\RustDeskHop.ico'),
    [string]$LogoDestination,
    [ValidateRange(0, 0.2)]
    [double]$PaddingFraction = 0,
    [ValidateRange(1, 255)]
    [byte]$AlphaThreshold = 16
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# One master image supplies all outputs. Never write back to that master.
if (-not ('RustDeskHopIconBounds' -as [type])) {
    $drawingAssemblies = @([System.Drawing.Bitmap].Assembly.Location, [System.Drawing.Rectangle].Assembly.Location) | Select-Object -Unique
    Add-Type -ReferencedAssemblies $drawingAssemblies -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
public static class RustDeskHopIconBounds
{
    public static Bitmap Load(string path)
    {
        using (var source = new Bitmap(path))
        {
            var image = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(image)) graphics.DrawImageUnscaled(source, 0, 0);
            // Accept either a transparent master or the approved white-tile
            // preview on black. Only the connected exterior matte is decoded;
            // the rabbit and the opaque tile interior are never recolored.
            foreach (var point in new[] { new Point(0, 0), new Point(image.Width - 1, 0),
                new Point(0, image.Height - 1), new Point(image.Width - 1, image.Height - 1) })
            {
                var c = image.GetPixel(point.X, point.Y);
                if (c.A != 255 || Math.Max(c.R, Math.Max(c.G, c.B)) > 16) return image;
            }
            var visited = new bool[image.Width * image.Height];
            var queue = new Queue<int>();
            queue.Enqueue(0);
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                if (visited[index]) continue;
                visited[index] = true;
                int x = index % image.Width, y = index / image.Width;
                var c = image.GetPixel(x, y);
                int max = Math.Max(c.R, Math.Max(c.G, c.B)), min = Math.Min(c.R, Math.Min(c.G, c.B));
                if (max >= 252 || (max > 64 && max - min > 8)) continue;
                image.SetPixel(x, y, max <= 64 ? Color.Transparent : Color.FromArgb(max, 255, 255, 255));
                if (x > 0) queue.Enqueue(index - 1);
                if (x + 1 < image.Width) queue.Enqueue(index + 1);
                if (y > 0) queue.Enqueue(index - image.Width);
                if (y + 1 < image.Height) queue.Enqueue(index + image.Width);
            }
            return image;
        }
    }
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
$sourceImage = [RustDeskHopIconBounds]::Load((Resolve-Path -LiteralPath $Source).Path)
$frames = [System.Collections.Generic.List[byte[]]]::new()
try {
    if ($sourceImage.Width -ne $sourceImage.Height) { throw 'The application icon source must be square.' }
    $artBounds = [RustDeskHopIconBounds]::Measure($sourceImage, $AlphaThreshold)
    # Fill the icon frame without an added transparent margin. Retain only a
    # source-pixel guard for antialiasing; never stretch or clip the artwork.
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
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($destinationPath)) | Out-Null
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
if ($LogoDestination) {
    $logoPath = [System.IO.Path]::GetFullPath($LogoDestination)
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($logoPath)) | Out-Null
    # The preview PNG uses the exact same normalized 256px frame as the ICO.
    [System.IO.File]::WriteAllBytes($logoPath, $frames[$frames.Count - 1])
    Write-Output "Created $logoPath from the same master."
}
