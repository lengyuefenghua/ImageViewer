[CmdletBinding()]
param(
    # 版本标签（如 v1.0.0）。省略时取最近的 git tag。
    [string]$Tag,
    # 缺失标签时是否自动创建（默认不创建）。
    [switch]$CreateTag,
    [string]$ProjectName = 'ImageViewer',
    [string]$OutputDirectory
)

# 发布入口：版本号来自 git tag；Release x64 构建后同时生成兼容 ZIP 与 Inno Setup 安装程序。
# 输出保持英文以兼容非 UTF-8 控制台（脚本含中文注释，文件需保存为 UTF-8 BOM）。
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$Root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $Root 'dist' }
$PublishDirectory = Join-Path $env:TEMP ('opencode\iv-release-' + [Guid]::NewGuid().ToString('N'))
$PackageDirectory = $null

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

try {
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

# 3. 验证 Inno Setup 编译器，避免缺少打包工具时删除旧 Release 产物。
$installerScript = Join-Path $PSScriptRoot 'ImageViewer.iss'
$compilerCandidates = @(
    (Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
    $(if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe' }),
    $(if ($env:ProgramFiles) { Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe' })
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
if (-not (Test-Path -LiteralPath $installerScript)) { throw ("[release] Installer script not found: {0}" -f $installerScript) }
if (-not $compilerCandidates) { throw '[release] Inno Setup 6 compiler not found. Install Inno Setup 6 or add ISCC.exe to PATH.' }
$compiler = $compilerCandidates | Select-Object -First 1
Write-Output ("[release] Building installer with: {0}" -f $compiler)

$Solution = Join-Path $Root 'ImageViewer.sln'
$ReleaseOutput = Join-Path $Root 'build\Release'
if (Test-Path -LiteralPath $ReleaseOutput) { Remove-Item -LiteralPath $ReleaseOutput -Recurse -Force }
Write-Output ("[release] Building Release x64: {0} (version {1})" -f $ProjectName, $version)
dotnet build $Solution -c Release -v quiet
if ($LASTEXITCODE -ne 0) { throw ("[release] Build failed with exit code {0}." -f $LASTEXITCODE) }

# 4a. 暂存发布内容：排除运行期数据目录与压缩包
if (Test-Path -LiteralPath $PublishDirectory) { Remove-Item -LiteralPath $PublishDirectory -Recurse -Force }
New-Item -ItemType Directory -Path $PublishDirectory | Out-Null
$excluded = @('Configuration', 'Logs', 'dist')
Get-ChildItem -LiteralPath $ReleaseOutput | Where-Object { $excluded -notcontains $_.Name -and $_.Extension -ne '.zip' } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $PublishDirectory -Recurse -Force
}

# 4b. 裁掉 Wpf.Ui.dll 内未使用的两个 Fluent 图标字体（约占发布体积 4.8MB）；只改暂存副本，
#     不动 build\Release，详见 docs/changes/trim-wpfui-icon-fonts。
$WpfUi = Join-Path $PublishDirectory 'Wpf.Ui.dll'
if (Test-Path -LiteralPath $WpfUi) {
    $Stripper = Join-Path $Root 'tools\WpfUiFontStripper\WpfUiFontStripper.csproj'
    Write-Output '[release] Trimming unused icon fonts from Wpf.Ui.dll'
    dotnet run --project $Stripper -c Release -- $WpfUi
    if ($LASTEXITCODE -ne 0) { throw ("[release] Font trimming failed with exit code {0}." -f $LASTEXITCODE) }
}

# 4. 产出 ZIP 与 Inno Setup 安装器。
if (-not (Test-Path -LiteralPath $OutputDirectory)) { New-Item -ItemType Directory -Path $OutputDirectory | Out-Null }
$archive = Join-Path $OutputDirectory ("{0}-{1}-win-x64.zip" -f $ProjectName, $version)
$installer = Join-Path $OutputDirectory ("{0}-{1}-win-x64-setup.exe" -f $ProjectName, $version)
$PackageDirectory = Join-Path $env:TEMP ('opencode\iv-package-' + [Guid]::NewGuid().ToString('N'))
$tempArchive = Join-Path $PackageDirectory ("{0}-{1}-win-x64.zip" -f $ProjectName, $version)
$tempInstaller = Join-Path $PackageDirectory ("{0}-{1}-win-x64-setup.exe" -f $ProjectName, $version)
New-Item -ItemType Directory -Path $PackageDirectory | Out-Null
Compress-Archive -Path (Join-Path $PublishDirectory '*') -DestinationPath $tempArchive -CompressionLevel Optimal
& $compiler "/DAppVersion=$version" "/DSourceDir=$PublishDirectory" "/DOutputDirectory=$PackageDirectory" $installerScript
if ($LASTEXITCODE -ne 0) { throw ("[release] Inno Setup compilation failed with exit code {0}." -f $LASTEXITCODE) }
if (-not (Test-Path -LiteralPath $tempInstaller)) { throw ("[release] Installer was not produced: {0}" -f $tempInstaller) }

# 正式文件只在两种产物都成功后更新，失败时保留此前发布版本。
Move-Item -LiteralPath $tempArchive -Destination $archive -Force
Move-Item -LiteralPath $tempInstaller -Destination $installer -Force
Remove-Item -LiteralPath $PackageDirectory -Recurse -Force
Remove-Item -LiteralPath $PublishDirectory -Recurse -Force

Write-Output ("[release] Tag: {0}" -f $resolvedTag)
Write-Output ("[release] ZIP: {0}" -f $archive)
Write-Output ("[release] Installer: {0}" -f $installer)
Write-Output 'OK'
} finally {
    if ($PackageDirectory -and (Test-Path -LiteralPath $PackageDirectory)) {
        Remove-Item -LiteralPath $PackageDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $PublishDirectory) {
        Remove-Item -LiteralPath $PublishDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}
