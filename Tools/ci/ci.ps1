<#
DemoLA CI 스크립트 (Windows PowerShell). Tools/ci/ci.sh 와 같은 단계.

  .\Tools\ci\ci.ps1 test  [-Mode editmode|playmode|all]
  .\Tools\ci\ci.ps1 build [-Target Win64|Android|Linux64|OSX] [-Development] [-BuildNumber 42] [-VersionName 0.3.0]
  .\Tools\ci\ci.ps1 all   [-Target Win64]

환경 변수: UNITY_EDITOR (Unity.exe 경로), UNITY_ARGS (추가 인자), DEMOLA_KEYSTORE_* (Android 서명)
열린 에디터가 프로젝트를 잡고 있으면 배치 모드가 실패한다 → 로컬은 Tools\dev.ps1 build 를 쓴다.
#>
param(
    [Parameter(Position = 0)][ValidateSet('test', 'build', 'all')][string]$Step = 'all',
    [ValidateSet('editmode', 'playmode', 'all')][string]$Mode = 'all',
    [ValidateSet('Win64', 'Android', 'Linux64', 'OSX')][string]$Target = 'Win64',
    [switch]$Development,
    [int]$BuildNumber = 0,
    [string]$VersionName = ''
)

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Logs = Join-Path $Root 'Builds\Logs'
$Results = Join-Path $Root 'Builds\TestResults'
New-Item -ItemType Directory -Force $Logs, $Results | Out-Null

$Version = ((Get-Content (Join-Path $Root 'ProjectSettings\ProjectVersion.txt') | Select-String 'm_EditorVersion:').ToString() -split ':\s*')[1].Trim()

function Find-Unity {
    if ($env:UNITY_EDITOR) { return $env:UNITY_EDITOR }
    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\$Version\Editor\Unity.exe",
        "E:\Program Files\Unity\Hub\Editor\$Version\Editor\Unity.exe",
        "D:\Program Files\Unity\Hub\Editor\$Version\Editor\Unity.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    throw "Unity $Version 를 찾지 못했습니다. UNITY_EDITOR 를 지정하세요."
}

$Unity = Find-Unity
Write-Host "Unity: $Unity ($Version)"

function Invoke-Unity([string]$Log, [string[]]$Arguments) {
    $all = @('-batchmode', '-nographics', '-projectPath', $Root, '-logFile', $Log) + $Arguments
    if ($env:UNITY_ARGS) { $all += ($env:UNITY_ARGS -split ' ') }
    $process = Start-Process -FilePath $Unity -ArgumentList $all -Wait -PassThru -NoNewWindow
    Write-Host "종료 코드 $($process.ExitCode) (로그: $Log)"
    return $process.ExitCode
}

function Invoke-Tests([string]$Platform) {
    return Invoke-Unity (Join-Path $Logs "test-$Platform.log") @('-runTests', '-testPlatform', $Platform, '-testResults', (Join-Path $Results "$Platform.xml"))
}

function Invoke-Build {
    $flag = switch ($Target) { 'Android' { 'Android' } 'Linux64' { 'StandaloneLinux64' } 'OSX' { 'StandaloneOSX' } default { 'StandaloneWindows64' } }
    $arguments = @('-quit', '-buildTarget', $flag, '-executeMethod', 'DemoLA.Editor.Build.BuildCommand.Build', '-demolaTarget', $Target, '-outputPath', "Builds/$Target")
    if ($Development) { $arguments += '-development' }
    if ($BuildNumber -gt 0) { $arguments += @('-buildNumber', "$BuildNumber") }
    if ($VersionName) { $arguments += @('-versionName', $VersionName) }
    $code = Invoke-Unity (Join-Path $Logs "build-$Target.log") $arguments
    $report = Join-Path $Root 'Builds\build-report.json'
    if (Test-Path $report) { Get-Content $report }
    return $code
}

$exit = 0
switch ($Step) {
    'test' {
        if ($Mode -in 'editmode', 'all') { if ((Invoke-Tests 'EditMode') -ne 0) { $exit = 1 } }
        if ($Mode -in 'playmode', 'all') { if ((Invoke-Tests 'PlayMode') -ne 0) { $exit = 1 } }
    }
    'build' { $exit = Invoke-Build }
    'all' {
        if ((Invoke-Tests 'EditMode') -ne 0) { exit 1 }
        if ((Invoke-Tests 'PlayMode') -ne 0) { exit 1 }
        $exit = Invoke-Build
    }
}
exit $exit
