param(
    [switch]$Rebuild,
    [switch]$PrepareOnly,
    [switch]$CheckOnly,
    [string]$UnityPath
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$playerPath = Join-Path $projectRoot 'Builds\Windows\CampusEcho.exe'
$statePath = Join-Path $projectRoot 'Builds\Windows\.source-state.json'
$logsPath = Join-Path $projectRoot 'Logs'

function Get-SourceFiles {
    $directories = @('Assets', 'Packages', 'ProjectSettings') | ForEach-Object { Join-Path $projectRoot $_ }
    return @(Get-ChildItem -LiteralPath $directories -Recurse -File |
        Where-Object { $_.Name -notmatch '^PerformanceTestRun(Info|Settings)\.json(\.meta)?$' } |
        Sort-Object FullName)
}

function Get-SourceSignature($Files) {
    $description = New-Object Text.StringBuilder
    foreach ($file in $Files) {
        $relative = $file.FullName.Substring($projectRoot.Length + 1).Replace('\', '/')
        [void]$description.Append($relative).Append('|').Append($file.Length).Append('|').Append($file.LastWriteTimeUtc.Ticks).AppendLine()
    }
    $hash = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($description.ToString())))).Replace('-', '').ToLowerInvariant()
    } finally { $hash.Dispose() }
}

function Find-UnityEditor([string]$Version) {
    $candidates = New-Object 'Collections.Generic.List[string]'
    if ($UnityPath) { $candidates.Add($UnityPath) }
    if ($env:UNITY_EDITOR_PATH) { $candidates.Add($env:UNITY_EDITOR_PATH) }
    $editorsFile = Join-Path $env:APPDATA 'UnityHub\editors-v2.json'
    if (Test-Path -LiteralPath $editorsFile) {
        try {
            $editors = Get-Content -LiteralPath $editorsFile -Raw -Encoding utf8 | ConvertFrom-Json
            foreach ($editor in $editors.data) {
                if ($editor.version -eq $Version) {
                    foreach ($location in $editor.location) { if ($location) { $candidates.Add([string]$location) } }
                }
            }
        } catch { Write-Verbose 'Unity Hub editor list unavailable; checking standard locations.' }
    }
    foreach ($base in @($env:ProgramFiles, 'D:\', 'H:\')) {
        if (-not $base) { continue }
        $candidates.Add([IO.Path]::Combine($base, "Unity\Hub\Editor\$Version\Editor\Unity.exe"))
        $candidates.Add([IO.Path]::Combine($base, "Unity\$Version\Editor\Unity.exe"))
    }
    foreach ($candidate in $candidates) { if (Test-Path -LiteralPath $candidate -PathType Leaf) { return [IO.Path]::GetFullPath($candidate) } }
    throw "未找到 Unity $Version。请先在 Unity Hub 中安装并激活该版本，再双击启动游戏。"
}

function Assert-CompleteAssets($Files) {
    $missing = @($Files | Where-Object { $_.Length -gt 0 -and $_.Length -lt 1024 -and $_.Extension -in @('.asset','.fbx','.glb','.blend') } |
        Where-Object { [IO.File]::ReadAllText($_.FullName).StartsWith('version https://git-lfs.github.com/spec/v1') })
    if ($missing.Count -gt 0) {
        throw "还有 $($missing.Count) 个大文件资源没有下载完整。请在 GitHub Desktop 中下载完整仓库及 Git LFS 资源后重试。"
    }
}

try {
    $versionFile = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path -LiteralPath $versionFile)) { throw '项目不完整：请下载整个仓库，不要只下载启动脚本。' }
    $versionLine = Get-Content -LiteralPath $versionFile | Where-Object { $_ -match '^m_EditorVersion:\s*' } | Select-Object -First 1
    $version = ($versionLine -replace '^m_EditorVersion:\s*', '').Trim()
    $sources = Get-SourceFiles
    Assert-CompleteAssets $sources
    $signature = Get-SourceSignature $sources
    $builtSignature = ''
    if (Test-Path -LiteralPath $statePath) {
        try { $builtSignature = (Get-Content -LiteralPath $statePath -Raw -Encoding utf8 | ConvertFrom-Json).signature } catch { $builtSignature = '' }
    }
    $needsBuild = $Rebuild -or -not (Test-Path -LiteralPath $playerPath) -or $builtSignature -ne $signature
    if ($CheckOnly) {
        [pscustomobject]@{ Project = $projectRoot; Unity = (Find-UnityEditor $version); Player = $playerPath; NeedsBuild = $needsBuild; SourceFiles = $sources.Count }
        exit 0
    }
    if ($needsBuild) {
        $openEditor = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
            Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($projectRoot,[StringComparison]::OrdinalIgnoreCase) -ge 0 })
        if ($openEditor.Count -gt 0) { throw '项目正在 Unity 中打开。请先保存并关闭该项目，再双击启动游戏，以便构建最新内容。' }
        $runningPlayer = @(Get-Process CampusEcho -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $playerPath })
        if ($runningPlayer.Count -gt 0) { throw '游戏正在运行。源码已更新，请先退出游戏，再双击启动以构建最新版本。' }
        $editorPath = Find-UnityEditor $version
        New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
        $buildLog = Join-Path $logsPath 'startup-build.log'
        Write-Host '正在准备最新游戏，首次导入和构建可能需要几分钟，请不要关闭此窗口。' -ForegroundColor Cyan
        $arguments = @('-batchmode','-quit','-force-d3d11','-projectPath',('"' + $projectRoot + '"'),'-executeMethod','CampusBuilder.Build','-logFile',('"' + $buildLog + '"'))
        $build = Start-Process -FilePath $editorPath -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
        if ($build.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $playerPath)) { throw "构建没有完成。请查看日志：$buildLog" }
        $completed = Select-String -LiteralPath $buildLog -Pattern 'CAMPUS_BUILD_COMPLETE' -Quiet
        if (-not $completed) { throw "未能确认构建完成，请查看日志：$buildLog" }
        $signature = Get-SourceSignature (Get-SourceFiles)
        @{ signature = $signature; unityVersion = $version; builtAt = [DateTime]::UtcNow.ToString('o') } |
            ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
        Write-Host '游戏已准备完成。' -ForegroundColor Green
    }
    if ($PrepareOnly) { exit 0 }
    $running = @(Get-Process CampusEcho -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $playerPath })
    if ($running.Count -gt 0) {
        $shell = New-Object -ComObject WScript.Shell
        [void]$shell.AppActivate($running[0].Id)
        Write-Host '游戏已经运行，已切换到游戏窗口。'
        exit 0
    }
    New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
    $playLog = Join-Path $logsPath 'game.log'
    Write-Host '正在启动 风起 · 校园…' -ForegroundColor Cyan
    Start-Process -FilePath $playerPath -WorkingDirectory $projectRoot -ArgumentList @('-screen-width','1440','-screen-height','900','-screen-fullscreen','0','-logFile',('"' + $playLog + '"'))
} catch {
    Write-Host ('启动失败：' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
