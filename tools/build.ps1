[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

# 构建入口：运行中的 ImageViewer 会锁住 build\<Configuration>，默认先停止该进程再构建。
# 输出保持英文以兼容非 UTF-8 控制台（脚本含中文注释，文件需保存为 UTF-8 BOM）。
$ErrorActionPreference = 'Stop'
# 统一 dotnet CLI 输出语言为英文，避免中文输出在不同控制台编码下乱码
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$Root = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $Root 'ImageViewer.sln'

Get-Process -Name 'ImageViewer' -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Output ("[build] Stopping running instance to avoid file locks: PID {0}" -f $_.Id)
    Stop-Process -Id $_.Id -Force
}
Start-Sleep -Milliseconds 300

Write-Output ("[build] Building ({0}): ImageViewer.sln" -f $Configuration)
dotnet build $Solution -c $Configuration -v quiet
if ($LASTEXITCODE -ne 0) {
    throw ("[build] Build failed with exit code {0}." -f $LASTEXITCODE)
}
Write-Output ("[build] Build succeeded ({0})." -f $Configuration)
Write-Output 'OK'
