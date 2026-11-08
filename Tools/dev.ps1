<#
DemoLA 개발 루프 (Windows PowerShell 진입점). 실제 동작은 Tools/dev.sh (Git Bash) 와 같다.

  .\Tools\dev.ps1 status | compile | errors
  .\Tools\dev.ps1 test [editor|playmode|all] [필터]
  .\Tools\dev.ps1 setup [overwrite]
  .\Tools\dev.ps1 play [초] [Gate1|Gate2]
  .\Tools\dev.ps1 balance [직업] [초] [시행] [behind]
  .\Tools\dev.ps1 deps
  .\Tools\dev.ps1 build [Win64|Android] [dev]

필요: unity CLI (https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1), Git for Windows(bash), python
#>
$ErrorActionPreference = 'Stop'
$bash = Get-Command bash -ErrorAction SilentlyContinue
if ($null -eq $bash) {
    $gitBash = 'C:\Program Files\Git\bin\bash.exe'
    if (Test-Path $gitBash) { $bash = $gitBash } else { throw 'bash 를 찾지 못했습니다 (Git for Windows 설치 필요).' }
}
else {
    $bash = $bash.Source
}

$script = Join-Path $PSScriptRoot 'dev.sh'
& $bash $script @args
exit $LASTEXITCODE
