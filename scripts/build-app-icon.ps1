[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Source,
    [string]$Output = (Join-Path $PSScriptRoot "..\assets\mycodex.ico")
)

# Converts the supplied artwork into one square, multi-resolution Windows icon.
# The image is only scaled and transparently padded; its content is not redrawn.
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$sourcePath = [System.IO.Path]::GetFullPath($Source)
$outputPath = [System.IO.Path]::GetFullPath($Output)
if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Icon source does not exist: $sourcePath"
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$sourceStream = [System.IO.File]::OpenRead($sourcePath)
try {
    $sourceImage = [System.Drawing.Image]::FromStream(
        $sourceStream,
        $true,
        $true)
    try {
        $frames = [System.Collections.Generic.List[byte[]]]::new()
        foreach ($size in $sizes) {
            $bitmap = [System.Drawing.Bitmap]::new(
                $size,
                $size,
                [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try {
                $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
                try {
                    $graphics.Clear([System.Drawing.Color]::Transparent)
                    $graphics.CompositingMode =
                        [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                    $graphics.CompositingQuality =
                        [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                    $graphics.InterpolationMode =
                        [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                    $graphics.PixelOffsetMode =
                        [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                    $graphics.SmoothingMode =
                        [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

                    $scale = [Math]::Min(
                        $size / $sourceImage.Width,
                        $size / $sourceImage.Height)
                    $width = [Math]::Max(
                        1,
                        [int][Math]::Round($sourceImage.Width * $scale))
                    $height = [Math]::Max(
                        1,
                        [int][Math]::Round($sourceImage.Height * $scale))
                    $left = [int][Math]::Floor(($size - $width) / 2)
                    $top = [int][Math]::Floor(($size - $height) / 2)
                    $graphics.DrawImage(
                        $sourceImage,
                        [System.Drawing.Rectangle]::new(
                            $left,
                            $top,
                            $width,
                            $height))
                }
                finally {
                    $graphics.Dispose()
                }

                $frame = [System.IO.MemoryStream]::new()
                try {
                    $bitmap.Save(
                        $frame,
                        [System.Drawing.Imaging.ImageFormat]::Png)
                    $frames.Add($frame.ToArray())
                }
                finally {
                    $frame.Dispose()
                }
            }
            finally {
                $bitmap.Dispose()
            }
        }

        $directory = [System.IO.Path]::GetDirectoryName($outputPath)
        [System.IO.Directory]::CreateDirectory($directory)
        $temporary = "$outputPath.$([Guid]::NewGuid().ToString('N')).tmp"
        try {
            $stream = [System.IO.File]::Create($temporary)
            $writer = [System.IO.BinaryWriter]::new($stream)
            try {
                $writer.Write([uint16]0)
                $writer.Write([uint16]1)
                $writer.Write([uint16]$frames.Count)
                $offset = 6 + 16 * $frames.Count
                for ($index = 0; $index -lt $frames.Count; $index++) {
                    $size = $sizes[$index]
                    $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
                    $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
                    $writer.Write([byte]0)
                    $writer.Write([byte]0)
                    $writer.Write([uint16]1)
                    $writer.Write([uint16]32)
                    $writer.Write([uint32]$frames[$index].Length)
                    $writer.Write([uint32]$offset)
                    $offset += $frames[$index].Length
                }
                foreach ($frame in $frames) {
                    $writer.Write($frame)
                }
            }
            finally {
                $writer.Dispose()
            }
            if (Test-Path -LiteralPath $outputPath) {
                [System.IO.File]::Replace($temporary, $outputPath, $null)
            }
            else {
                [System.IO.File]::Move($temporary, $outputPath)
            }
        }
        finally {
            if (Test-Path -LiteralPath $temporary) {
                Remove-Item -LiteralPath $temporary -Force
            }
        }
    }
    finally {
        $sourceImage.Dispose()
    }
}
finally {
    $sourceStream.Dispose()
}

$hash = Get-FileHash -LiteralPath $outputPath -Algorithm SHA256
[pscustomobject]@{
    Output = $outputPath
    Frames = $sizes -join ","
    Sha256 = $hash.Hash.ToLowerInvariant()
} | Format-List
