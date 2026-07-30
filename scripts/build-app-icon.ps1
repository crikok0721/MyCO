[CmdletBinding()]
param(
    [string]$Source = (Join-Path $PSScriptRoot "..\assets\MyCO-source.ico"),
    [string]$Output = (Join-Path $PSScriptRoot "..\assets\MyCO.ico"),
    [string]$PngOutput = (Join-Path $PSScriptRoot "..\assets\MyCO-logo.png"),
    [double]$CornerRadiusRatio = 0.18
)

# Generates the packaged UI PNG and multi-resolution Windows ICO from the
# repository-owned canonical artwork. Derived frames receive one deterministic,
# anti-aliased rounded-rectangle mask; the portrait pixels are never redrawn.
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Remove-IconRenderDirectory {
    param([string]$Directory)

    if ([string]::IsNullOrWhiteSpace($Directory)) {
        return
    }
    $resolvedRender = [System.IO.Path]::GetFullPath($Directory)
    $resolvedRoot = [System.IO.Path]::GetFullPath(
        (Join-Path ([System.IO.Path]::GetTempPath()) "MyCOIcon"))
    if ($resolvedRender.StartsWith(
            $resolvedRoot + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase) -and
        [System.IO.Directory]::Exists($resolvedRender)) {
        [System.IO.Directory]::Delete($resolvedRender, $true)
    }
}

function Convert-BitmapToIconDib {
    param([System.Drawing.Bitmap]$Bitmap)

    $width = $Bitmap.Width
    $height = $Bitmap.Height
    $xorBytes = $width * $height * 4
    $maskStride = [int]([Math]::Ceiling($width / 32.0) * 4)
    $maskBytes = $maskStride * $height
    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        # BITMAPINFOHEADER. ICO stores XOR and AND masks in one doubled-height DIB.
        $writer.Write([uint32]40)
        $writer.Write([int32]$width)
        $writer.Write([int32]($height * 2))
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]0)
        $writer.Write([uint32]$xorBytes)
        $writer.Write([int32]0)
        $writer.Write([int32]0)
        $writer.Write([uint32]0)
        $writer.Write([uint32]0)

        # ICO DIB scanlines are bottom-up and pixels are BGRA.
        for ($y = $height - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $width; $x++) {
                $color = $Bitmap.GetPixel($x, $y)
                $writer.Write([byte]$color.B)
                $writer.Write([byte]$color.G)
                $writer.Write([byte]$color.R)
                $writer.Write([byte]$color.A)
            }
        }

        # Preserve transparency for legacy icon consumers through the AND mask.
        for ($y = $height - 1; $y -ge 0; $y--) {
            $mask = [byte[]]::new($maskStride)
            for ($x = 0; $x -lt $width; $x++) {
                if ($Bitmap.GetPixel($x, $y).A -eq 0) {
                    $byteIndex = [int][Math]::Floor($x / 8)
                    $bit = 7 - ($x % 8)
                    $mask[$byteIndex] =
                        $mask[$byteIndex] -bor [byte](1 -shl $bit)
                }
            }
            $writer.Write($mask)
        }
        $writer.Flush()
        return $stream.ToArray()
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function New-RoundedRectanglePath {
    param(
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $diameter = [Math]::Max(1.0, $Radius * 2.0)
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.StartFigure()
    $path.AddArc(
        [System.Drawing.RectangleF]::new(0, 0, $diameter, $diameter),
        180,
        90)
    $path.AddArc(
        [System.Drawing.RectangleF]::new(
            $Width - $diameter,
            0,
            $diameter,
            $diameter),
        270,
        90)
    $path.AddArc(
        [System.Drawing.RectangleF]::new(
            $Width - $diameter,
            $Height - $diameter,
            $diameter,
            $diameter),
        0,
        90)
    $path.AddArc(
        [System.Drawing.RectangleF]::new(
            0,
            $Height - $diameter,
            $diameter,
            $diameter),
        90,
        90)
    $path.CloseFigure()
    return $path
}

function Set-RoundedRectangleAlpha {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [double]$RadiusRatio
    )

    $supersample = 4
    $maskSize = $Bitmap.Width * $supersample
    $maskLarge = [System.Drawing.Bitmap]::new(
        $maskSize,
        $maskSize,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $mask = [System.Drawing.Bitmap]::new(
        $Bitmap.Width,
        $Bitmap.Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($maskLarge)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode =
                [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.SmoothingMode =
                [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $radius = [float]($maskSize * $RadiusRatio)
            $path = New-RoundedRectanglePath `
                -Width ([float]$maskSize) `
                -Height ([float]$maskSize) `
                -Radius $radius
            try {
                $graphics.FillPath([System.Drawing.Brushes]::White, $path)
            }
            finally {
                $path.Dispose()
            }
        }
        finally {
            $graphics.Dispose()
        }

        $graphics = [System.Drawing.Graphics]::FromImage($mask)
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
            $graphics.DrawImage(
                $maskLarge,
                [System.Drawing.Rectangle]::new(
                    0,
                    0,
                    $Bitmap.Width,
                    $Bitmap.Height))
        }
        finally {
            $graphics.Dispose()
        }

        for ($y = 0; $y -lt $Bitmap.Height; $y++) {
            for ($x = 0; $x -lt $Bitmap.Width; $x++) {
                $source = $Bitmap.GetPixel($x, $y)
                $maskAlpha = $mask.GetPixel($x, $y).A
                if ($maskAlpha -lt 255) {
                    $alpha = [byte][Math]::Round(
                        $source.A * $maskAlpha / 255.0)
                    $Bitmap.SetPixel(
                        $x,
                        $y,
                        [System.Drawing.Color]::FromArgb(
                            $alpha,
                            $source.R,
                            $source.G,
                            $source.B))
                }
            }
        }
    }
    finally {
        $mask.Dispose()
        $maskLarge.Dispose()
    }
}

$sourcePath = [System.IO.Path]::GetFullPath($Source)
$outputPath = [System.IO.Path]::GetFullPath($Output)
$pngOutputPath = if ([string]::IsNullOrWhiteSpace($PngOutput)) {
    $null
}
else {
    [System.IO.Path]::GetFullPath($PngOutput)
}
if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Icon source does not exist: $sourcePath"
}
if ($sourcePath.Equals($outputPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The canonical icon source and generated ICO output must be different files."
}
if ($CornerRadiusRatio -lt 0.08 -or $CornerRadiusRatio -gt 0.30) {
    throw "CornerRadiusRatio must be between 0.08 and 0.30."
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$rasterSourcePath = $sourcePath
$renderDirectory = $null
if ([System.IO.Path]::GetExtension($sourcePath).Equals(
        ".svg",
        [System.StringComparison]::OrdinalIgnoreCase)) {
    $edgeCandidates = @(
        (Join-Path $env:ProgramFiles "Microsoft\Edge\Application\msedge.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft\Edge\Application\msedge.exe")
    )
    $edgePath = $edgeCandidates |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
    if (-not $edgePath) {
        throw "Microsoft Edge is required to render the SVG icon source."
    }

    $renderRoot = Join-Path ([System.IO.Path]::GetTempPath()) "MyCOIcon"
    $renderDirectory = Join-Path $renderRoot ([Guid]::NewGuid().ToString("N"))
    [void][System.IO.Directory]::CreateDirectory($renderDirectory)
    $rasterSourcePath = Join-Path $renderDirectory "source-1024.png"
    $edgeProfile = Join-Path $renderDirectory "edge-profile"
    $sourceUri = [System.Uri]::new($sourcePath).AbsoluteUri
    try {
        & $edgePath `
            "--headless=new" `
            "--disable-gpu" `
            "--hide-scrollbars" `
            "--force-device-scale-factor=1" `
            "--window-size=1024,1024" `
            "--user-data-dir=$edgeProfile" `
            "--screenshot=$rasterSourcePath" `
            $sourceUri | Out-Null
        if ($LASTEXITCODE -ne 0 -or
            -not (Test-Path -LiteralPath $rasterSourcePath -PathType Leaf)) {
            throw "The SVG icon source could not be rendered."
        }
    }
    catch {
        Remove-IconRenderDirectory -Directory $renderDirectory
        throw
    }
}

$sourceStream = [System.IO.File]::OpenRead($rasterSourcePath)
$sourceIcon = $null
try {
    if ([System.IO.Path]::GetExtension($rasterSourcePath).Equals(
            ".ico",
            [System.StringComparison]::OrdinalIgnoreCase)) {
        $sourceIcon = [System.Drawing.Icon]::new($sourceStream)
        $sourceImage = $sourceIcon.ToBitmap()
    }
    else {
        $sourceImage = [System.Drawing.Image]::FromStream(
            $sourceStream,
            $true,
            $true)
    }
    try {
        $frames = [System.Collections.Generic.List[byte[]]]::new()
        $pngFrame = $null
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

                    if ($sourceImage.Width -eq $size -and
                        $sourceImage.Height -eq $size) {
                        $graphics.DrawImageUnscaled($sourceImage, 0, 0)
                    }
                    else {
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
                }
                finally {
                    $graphics.Dispose()
                }

                Set-RoundedRectangleAlpha `
                    -Bitmap $bitmap `
                    -RadiusRatio $CornerRadiusRatio
                $frames.Add((Convert-BitmapToIconDib -Bitmap $bitmap))
                if ($size -eq 256) {
                    $pngStream = [System.IO.MemoryStream]::new()
                    try {
                        $bitmap.Save(
                            $pngStream,
                            [System.Drawing.Imaging.ImageFormat]::Png)
                        $pngFrame = $pngStream.ToArray()
                    }
                    finally {
                        $pngStream.Dispose()
                    }
                }
            }
            finally {
                $bitmap.Dispose()
            }
        }

        $directory = [System.IO.Path]::GetDirectoryName($outputPath)
        [void][System.IO.Directory]::CreateDirectory($directory)
        $temporary = "$outputPath.$([Guid]::NewGuid().ToString('N')).tmp"
        $backup = "$outputPath.$([Guid]::NewGuid().ToString('N')).bak"
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
                [System.IO.File]::Replace($temporary, $outputPath, $backup)
            }
            else {
                [System.IO.File]::Move($temporary, $outputPath)
            }
        }
        finally {
            if (Test-Path -LiteralPath $temporary) {
                Remove-Item -LiteralPath $temporary -Force
            }
            if (Test-Path -LiteralPath $backup) {
                Remove-Item -LiteralPath $backup -Force
            }
        }

        if ($pngOutputPath) {
            if (-not $pngFrame) {
                throw "The 256px UI PNG frame was not generated."
            }
            $pngDirectory = [System.IO.Path]::GetDirectoryName($pngOutputPath)
            [void][System.IO.Directory]::CreateDirectory($pngDirectory)
            $pngTemporary =
                "$pngOutputPath.$([Guid]::NewGuid().ToString('N')).tmp"
            $pngBackup =
                "$pngOutputPath.$([Guid]::NewGuid().ToString('N')).bak"
            try {
                [System.IO.File]::WriteAllBytes(
                    $pngTemporary,
                    $pngFrame)
                if (Test-Path -LiteralPath $pngOutputPath) {
                    [System.IO.File]::Replace(
                        $pngTemporary,
                        $pngOutputPath,
                        $pngBackup)
                }
                else {
                    [System.IO.File]::Move($pngTemporary, $pngOutputPath)
                }
            }
            finally {
                if (Test-Path -LiteralPath $pngTemporary) {
                    Remove-Item -LiteralPath $pngTemporary -Force
                }
                if (Test-Path -LiteralPath $pngBackup) {
                    Remove-Item -LiteralPath $pngBackup -Force
                }
            }
        }
    }
    finally {
        $sourceImage.Dispose()
        if ($sourceIcon) {
            $sourceIcon.Dispose()
        }
    }
}
finally {
    $sourceStream.Dispose()
    Remove-IconRenderDirectory -Directory $renderDirectory
}

$hash = Get-FileHash -LiteralPath $outputPath -Algorithm SHA256
[pscustomobject]@{
    Output = $outputPath
    PngOutput = $pngOutputPath
    Frames = $sizes -join ","
    Sha256 = $hash.Hash.ToLowerInvariant()
} | Format-List
