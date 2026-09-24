[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$NoBuild
)

# 运行入口：先停止已有实例，可选构建后启动 build\<Configuration>\ImageViewer.exe。
# 输出保持英文以兼容非 UTF-8 控制台（脚本含中文注释，文件需保存为 UTF-8 BOM）。
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot

Get-Process -Name 'ImageViewer' -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Output ("Stopping existing instance: PID {0}" -f $_.Id)
    Stop-Process -Id $_.Id -Force
}
Start-Sleep -Milliseconds 300

if (-not $NoBuild) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration $Configuration
}

$exe = Join-Path $Root ("build\{0}\ImageViewer.exe" -f $Configuration)
if (-not (Test-Path -LiteralPath $exe)) { throw ("Executable not found: {0}" -f $exe) }

Start-Process -FilePath $exe
Start-Sleep -Seconds 2
$process = Get-Process -Name 'ImageViewer' -ErrorAction SilentlyContinue
if ($process) {
    Write-Output ("Started: {0} (PID {1})" -f $exe, $process.Id)
} else {
    throw ("Application exited immediately: {0}" -f $exe)
}
