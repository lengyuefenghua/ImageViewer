[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Output = Join-Path $Root 'build\Release'
$Temp = Join-Path $env:TEMP ('ImageViewer-startup-test-' + [guid]::NewGuid().ToString('N'))
$Log = Join-Path $env:APPDATA ('ImageViewer\Logs\' + (Get-Date -Format 'yyyy-MM-dd') + '.log')

try {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release
    New-Item -ItemType Directory -Path $Temp | Out-Null
    Copy-Item (Join-Path $Output '*') $Temp -Recurse
    $config = '<?xml version="1.0" encoding="utf-8"?><configuration><appSettings><add key="Logging.MinimumLevel" value="Debug"/><add key="Preferences.DefaultViewerPromptDismissed" value="true"/></appSettings></configuration>'
    Set-Content -Path (Join-Path $Temp 'ImageViewer.exe.config') -Value $config -Encoding UTF8

    Add-Type -AssemblyName System.Drawing
    $bitmap = New-Object System.Drawing.Bitmap 640, 480
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::SteelBlue)
    $imageName = 'startup-order-' + [guid]::NewGuid().ToString('N') + '.png'
    $imagePath = Join-Path $Temp $imageName
    $bitmap.Save($imagePath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()

    $parentName = 'startup-parent-' + [guid]::NewGuid().ToString('N')
    $parentPath = Join-Path $Temp $parentName
    New-Item -ItemType Directory -Path $parentPath | Out-Null
    Move-Item -LiteralPath $imagePath -Destination (Join-Path $parentPath $imageName)
    $imagePath = Join-Path $parentPath $imageName

    $previousLineCount = if (Test-Path $Log) { (Get-Content -Path $Log -Encoding UTF8 | Measure-Object -Line).Lines } else { 0 }
    $process = Start-Process -FilePath (Join-Path $Temp 'ImageViewer.exe') -ArgumentList ('"' + $imagePath + '"') -PassThru
    $deadline = (Get-Date).AddSeconds(20)
    $lines = @()
    $windowLoaded = $null
    $sizePreload = $null
    $sizePreloadComplete = $null
    do {
        if (Test-Path $Log) {
            $allLines = Get-Content -Path $Log -Encoding UTF8
            $lines = @($allLines | Select-Object -Skip $previousLineCount)
        }
        $opened = $lines | Where-Object { $_ -match [regex]::Escape($imageName) -and $_ -match '\|查看器已打开：' } | Select-Object -Last 1
        $decoded = $lines | Where-Object { $_ -match '启动首图解码完成' } | Select-Object -Last 1
        $scanned = $lines | Where-Object { $_ -match [regex]::Escape($parentName) -and $_ -match '\|查看器目录扫描开始：' } | Select-Object -Last 1
        $windowLoaded = $lines | Where-Object { $_ -match '\|查看器窗口 Loaded$' } | Select-Object -Last 1
        $sizePreload = $lines | Where-Object { $_ -match '\|首图尺寸预读开始$' } | Select-Object -Last 1
        $sizePreloadComplete = $lines | Where-Object { $_ -match '\|首图尺寸预读完成$' } | Select-Object -Last 1
        if ($opened -and $decoded -and $scanned -and $windowLoaded -and $sizePreload -and $sizePreloadComplete) { break }
        Start-Sleep -Milliseconds 25
    } while ((Get-Date) -lt $deadline)

    if (!$opened) { throw '图片启动未记录“查看器已打开”事件。' }
    if (!$decoded) { throw '图片启动未记录首图解码完成事件。' }
    if (!$scanned) { throw '图片启动未记录目录扫描开始事件。' }
    if (!$windowLoaded) { throw '图片启动未记录窗口 Loaded 事件。' }
    if (!$sizePreload) { throw '图片启动未记录首图尺寸预读开始事件。' }
    if (!$sizePreloadComplete) { throw '图片启动未记录首图尺寸预读完成事件。' }

    $imageLoadStartedTime = [datetime]::ParseExact([regex]::Match([string]$decoded, '^(?<time>[^|]+)\|').Groups['time'].Value, 'yyyy-MM-dd HH:mm:ss.ffff', [Globalization.CultureInfo]::InvariantCulture)
    $scanTime = [datetime]::ParseExact([regex]::Match([string]$scanned, '^(?<time>[^|]+)\|').Groups['time'].Value, 'yyyy-MM-dd HH:mm:ss.ffff', [Globalization.CultureInfo]::InvariantCulture)
    $loadedTime = [datetime]::ParseExact([regex]::Match([string]$windowLoaded, '^(?<time>[^|]+)\|').Groups['time'].Value, 'yyyy-MM-dd HH:mm:ss.ffff', [Globalization.CultureInfo]::InvariantCulture)
    $preloadTime = [datetime]::ParseExact([regex]::Match([string]$sizePreload, '^(?<time>[^|]+)\|').Groups['time'].Value, 'yyyy-MM-dd HH:mm:ss.ffff', [Globalization.CultureInfo]::InvariantCulture)
    $preloadCompleteTime = [datetime]::ParseExact([regex]::Match([string]$sizePreloadComplete, '^(?<time>[^|]+)\|').Groups['time'].Value, 'yyyy-MM-dd HH:mm:ss.ffff', [Globalization.CultureInfo]::InvariantCulture)
    Write-Output ("[startup-test] size-preload-start={0:O}; window-loaded={1:O}; first-image-decoded={2:O}; directory-scan-start={3:O}" -f $preloadTime, $loadedTime, $imageLoadStartedTime, $scanTime)
    if ($preloadTime -ge $loadedTime) { throw 'RED: 首图尺寸预读没有在窗口 Loaded 前开始。' }
    if ($preloadCompleteTime -lt $preloadTime) { throw '首图尺寸预读完成时间早于开始时间。' }
    if ($imageLoadStartedTime -ge $scanTime) {
        throw 'RED: 目录扫描在首图就绪之前开始。'
    }
    Write-Output 'GREEN: 首图就绪后才启动目录扫描。'
}
finally {
    if ($process -and !$process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (!$process.WaitForExit(3000)) { Stop-Process -Id $process.Id -Force }
    }
    if (Test-Path $Temp) { Remove-Item -LiteralPath $Temp -Recurse -Force }
}
