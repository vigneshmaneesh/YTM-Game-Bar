[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$Source = (Join-Path $PSScriptRoot '..\Artwork\YouTubeMusicLogo.png'),

    [ValidateRange(0.5, 1.0)]
    [double]$FillRatio = 0.88
)

$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourcePath = [System.IO.Path]::GetFullPath($Source)
$assetsRoot = Join-Path $projectRoot 'YouTubeMusicGameBar\Assets'
$gameBarIconRoot = Join-Path $assetsRoot 'GameBar\YouTubeMusicWidget\Icons'

if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Logo source was not found: $sourcePath"
}

Add-Type -AssemblyName System.Drawing

function Write-LogoAsset {
    param(
        [Parameter(Mandatory = $true)]
        [System.Drawing.Image]$Image,

        [Parameter(Mandatory = $true)]
        [int]$Size,

        [Parameter(Mandatory = $true)]
        [string]$Destination,

        [Parameter(Mandatory = $true)]
        [double]$Scale
    )

    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmap.SetResolution(96, 96)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

        $drawSize = [Math]::Max(1, [int][Math]::Round($Size * $Scale))
        $offset = [int][Math]::Floor(($Size - $drawSize) / 2)
        $destinationRectangle = New-Object System.Drawing.Rectangle $offset, $offset, $drawSize, $drawSize
        $sourceRectangle = New-Object System.Drawing.Rectangle 0, 0, $Image.Width, $Image.Height

        $attributes = New-Object System.Drawing.Imaging.ImageAttributes
        try {
            $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
            $graphics.DrawImage(
                $Image,
                $destinationRectangle,
                $sourceRectangle.X,
                $sourceRectangle.Y,
                $sourceRectangle.Width,
                $sourceRectangle.Height,
                [System.Drawing.GraphicsUnit]::Pixel,
                $attributes)
        }
        finally {
            $attributes.Dispose()
        }

        $destinationDirectory = Split-Path -Parent $Destination
        [System.IO.Directory]::CreateDirectory($destinationDirectory) | Out-Null
        $bitmap.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Write-MediaThumbnail {
    param(
        [Parameter(Mandatory = $true)]
        [System.Drawing.Image]$Image,

        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    $size = 512
    $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmap.SetResolution(96, 96)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        # Supply an explicit high-resolution media thumbnail so Windows uses the
        # logo's real alpha channel instead of creating a plated tile fallback.
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

        $drawSize = 380
        $offset = [int](($size - $drawSize) / 2)
        $destinationRectangle = New-Object System.Drawing.Rectangle $offset, $offset, $drawSize, $drawSize
        $sourceRectangle = New-Object System.Drawing.Rectangle 0, 0, $Image.Width, $Image.Height

        $attributes = New-Object System.Drawing.Imaging.ImageAttributes
        try {
            $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
            $graphics.DrawImage(
                $Image,
                $destinationRectangle,
                $sourceRectangle.X,
                $sourceRectangle.Y,
                $sourceRectangle.Width,
                $sourceRectangle.Height,
                [System.Drawing.GraphicsUnit]::Pixel,
                $attributes)
        }
        finally {
            $attributes.Dispose()
        }

        $bitmap.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$sourceImage = [System.Drawing.Image]::FromFile($sourcePath)

try {
    if ($sourceImage.Width -ne $sourceImage.Height) {
        throw "The source logo must be square. Received $($sourceImage.Width)x$($sourceImage.Height)."
    }

    $packageAssets = @(
        @{ Name = 'Square150x150Logo.png'; Size = 150 },
        @{ Name = 'Square44x44Logo.png'; Size = 44 },
        @{ Name = 'StoreLogo.png'; Size = 50 }
    )

    foreach ($asset in $packageAssets) {
        Write-LogoAsset -Image $sourceImage -Size $asset.Size -Destination (Join-Path $assetsRoot $asset.Name) -Scale $FillRatio
    }

    Write-MediaThumbnail -Image $sourceImage -Destination (Join-Path $assetsRoot 'MediaThumbnail.png')

    # Windows shell surfaces (including audio/session controls) can request a
    # target-size, unplated app icon instead of the base Square44x44Logo asset.
    # Supply the complete current target-size family for both shell themes.
    foreach ($size in @(16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256)) {
        $shellNames = @(
            "Square44x44Logo.targetsize-$size.png",
            "Square44x44Logo.targetsize-$($size)_altform-unplated.png",
            "Square44x44Logo.targetsize-$($size)_altform-lightunplated.png"
        )

        foreach ($name in $shellNames) {
            Write-LogoAsset -Image $sourceImage -Size $size -Destination (Join-Path $assetsRoot $name) -Scale $FillRatio
        }
    }

    foreach ($size in @(16, 20, 24, 32, 44, 256)) {
        foreach ($name in @("icon.targetsize-$size.png", "icon.light.targetsize-$size.png")) {
            Write-LogoAsset -Image $sourceImage -Size $size -Destination (Join-Path $gameBarIconRoot $name) -Scale $FillRatio
        }
    }
}
finally {
    $sourceImage.Dispose()
}

Write-Host "Generated YouTube Music package and Game Bar icons from $sourcePath"
