[CmdletBinding()]
param(
    # 版本标签（如 v1.0.0）。省略时取最近的 git tag。
    [string]$Tag,
    # 缺失标签时是否自动创建（默认不创建）。
    [switch]$CreateTag,
    [string]$ProjectName = 'ImageViewer',
    [string]$OutputDirectory
)

# 发布入口：版本号来自 git tag（形如 v1.0.0 / 1.0.0），Release x64 构建后打包为
# <项目名>-<版本号>-win-x64.zip。输出保持英文以兼容非 UTF-8 控制台（脚本含中文注释，文件需保存为 UTF-8 BOM）。
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$Root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $Root 'dist' }
$PublishDirectory = Join-Path $env:TEMP ('opencode\iv-release-' + [Guid]::NewGuid().ToString('N'))

function Invoke-Git {
    param([string[]]$Arguments)
    # git 会把正常诊断写到 stderr；临时放宽 ErrorActionPreference，改用 $LASTEXITCODE 判断。
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & git -C $Root @Arguments 2>&1
        return @{ ExitCode = $LASTEXITCODE; Output = (($output | Out-String).Trim()) }
    } finally {
        $ErrorActionPreference = $previous
    }
}

# 1. 解析版本标签
$resolvedTag = $Tag
if (-not $resolvedTag) {
    $latest = Invoke-Git @('describe', '--tags', '--abbrev=0')
    if ($latest.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($latest.Output)) {
        throw '[release] No git tag found. Create one first (e.g. git tag v1.0.0) or pass -Tag v1.0.0.'
    }
    $resolvedTag = $latest.Output
}

$exists = Invoke-Git @('rev-parse', '--verify', ('refs/tags/' + $resolvedTag))
if ($exists.ExitCode -ne 0) {
    if (-not $CreateTag) {
        throw ("[release] Tag not found: {0}. Pass -CreateTag to create it." -f $resolvedTag)
    }
    Write-Output ("[release] Creating tag: {0}" -f $resolvedTag)
    & git -C $Root tag $resolvedTag
    if ($LASTEXITCODE -ne 0) { throw ("[release] Failed to create tag {0}." -f $resolvedTag) }
}

$version = $resolvedTag.TrimStart('v', 'V')
if ($version -notmatch '^\d+(\.\d+){0,3}$') {
    throw ("[release] Tag '{0}' does not look like a version (expected vX / vX.Y / vX.Y.Z)." -f $resolvedTag)
}

# 2. 停止运行中的应用，避免输出目录被锁
Get-Process -Name 'ImageViewer' -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Output ("[release] Stopping running instance: PID {0}" -f $_.Id)
    Stop-Process -Id $_.Id -Force
}
Start-Sleep -Milliseconds 300

# 3. Release x64 构建（构建产物直接输出到仓库根 build\Release）
$Solution = Join-Path $Root 'src\ImageViewer.sln'
$ReleaseOutput = Join-Path $Root 'build\Release'
if (Test-Path -LiteralPath $ReleaseOutput) { Remove-Item -LiteralPath $ReleaseOutput -Recurse -Force }
Write-Output ("[release] Building Release x64: {0} (version {1})" -f $ProjectName, $version)
dotnet build $Solution -c Release -v quiet
if ($LASTEXITCODE -ne 0) { throw ("[release] Build failed with exit code {0}." -f $LASTEXITCODE) }

# 4. 暂存发布内容：排除运行期数据目录与压缩包
if (Test-Path -LiteralPath $PublishDirectory) { Remove-Item -LiteralPath $PublishDirectory -Recurse -Force }
New-Item -ItemType Directory -Path $PublishDirectory | Out-Null
$excluded = @('Configuration', 'Logs', 'dist')
Get-ChildItem -LiteralPath $ReleaseOutput | Where-Object { $excluded -notcontains $_.Name -and $_.Extension -ne '.zip' } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $PublishDirectory -Recurse -Force
}

# 5. 打包 <项目名>-<版本号>-win-x64.zip
if (-not (Test-Path -LiteralPath $OutputDirectory)) { New-Item -ItemType Directory -Path $OutputDirectory | Out-Null }
$archive = Join-Path $OutputDirectory ("{0}-{1}-win-x64.zip" -f $ProjectName, $version)
if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
Compress-Archive -Path (Join-Path $PublishDirectory '*') -DestinationPath $archive -CompressionLevel Optimal
Remove-Item -LiteralPath $PublishDirectory -Recurse -Force

Write-Output ("[release] Tag: {0}" -f $resolvedTag)
Write-Output ("[release] Package: {0}" -f $archive)
Write-Output 'OK'
