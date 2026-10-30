param(
    [Parameter(Mandatory = $true)]
    [string]$SourceMapPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [ValidateRange(1, 100)]
    [int]$JpegQuality = 88
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sourcePath = if ([System.IO.Path]::IsPathRooted($SourceMapPath)) {
    $SourceMapPath
} else {
    Join-Path $root $SourceMapPath
}
$outputPath = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path $root $OutputDirectory
}

$map = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8 | ConvertFrom-Json
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
Add-Type -AssemblyName System.Drawing

$codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
    Where-Object MimeType -eq 'image/jpeg'
$encoderParameters = [System.Drawing.Imaging.EncoderParameters]::new(1)
$encoderParameters.Param[0] = [System.Drawing.Imaging.EncoderParameter]::new(
    [System.Drawing.Imaging.Encoder]::Quality,
    [long]$JpegQuality
)

try {
    foreach ($item in $map.items) {
        if ($item.sources.Count -ne 3) {
            throw "Group $($item.id) must contain exactly 3 source images."
        }

        for ($index = 0; $index -lt $item.sources.Count; $index++) {
            $source = [string]$item.sources[$index]
            if (-not (Test-Path -LiteralPath $source)) {
                throw "Source image was not found: $source"
            }

            $destination = Join-Path $outputPath ('{0}-{1:D2}.jpg' -f $item.id, ($index + 1))
            $image = [System.Drawing.Image]::FromFile($source)
            try {
                $image.Save($destination, $codec, $encoderParameters)
            } finally {
                $image.Dispose()
            }
        }
    }
} finally {
    $encoderParameters.Dispose()
}

$files = @(Get-ChildItem -LiteralPath $outputPath -Filter '*.jpg')
[pscustomobject]@{
    groupCount = @($map.items).Count
    imageCount = $files.Count
    outputDirectory = $outputPath
} | ConvertTo-Json -Depth 3
