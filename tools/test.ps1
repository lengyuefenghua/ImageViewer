[CmdletBinding()]
param(
    [string]$Filter,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Build
)

# 测试入口：默认不构建（等价 --no-build），直接运行已构建产物；-Build 时先调用 build.ps1。
# 输出保持英文以兼容非 UTF-8 控制台（脚本含中文注释，文件需保存为 UTF-8 BOM）。
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$Root = Split-Path -Parent $PSScriptRoot
$TestProject = Join-Path $Root 'tests\ImageViewer.App.Tests\ImageViewer.App.Tests.csproj'

if ($Build) {
    Write-Output '[test] -Build: running build entrypoint tools\build.ps1 first'
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration $Configuration
} else {
    Get-Process -Name 'ImageViewer' -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Output ("[test] Stopping running instance to avoid file locks: PID {0}" -f $_.Id)
        Stop-Process -Id $_.Id -Force
    }
    Start-Sleep -Milliseconds 300
}

$label = if ($Filter) { $Filter } else { '<all>' }
Write-Output ("[test] Running ({0}), filter: {1}" -f $Configuration, $label)
$a = @('test', $TestProject, '-c', $Configuration, '--no-build', '--nologo')
if ($Filter) { $a += @('--filter', $Filter) }
dotnet @a
if ($LASTEXITCODE -ne 0) {
    throw ("[test] Test failed with exit code {0}." -f $LASTEXITCODE)
}
Write-Output 'OK'
