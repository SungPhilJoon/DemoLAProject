#!/usr/bin/env bash
# DemoLA CI 스크립트 (배치 모드 Unity). 로컬 · 자체 호스트 러너 · GitHub Actions 에서 같은 단계로 쓴다.
#
#   Tools/ci/ci.sh test   [editmode|playmode|all]      → Builds/TestResults/*.xml (NUnit)
#   Tools/ci/ci.sh build  [Win64|Android|Linux64|OSX] [dev] [buildNumber] [versionName]
#   Tools/ci/ci.sh all    [target]                      → 테스트 후 빌드
#
# 환경 변수
#   UNITY_EDITOR   Unity 실행 파일 경로 (없으면 ProjectVersion.txt 버전으로 Hub 기본 위치를 찾는다)
#   UNITY_ARGS     추가 인자 (예: 라이선스 -username/-password/-serial)
#   DEMOLA_KEYSTORE_PATH / DEMOLA_KEYSTORE_PASS / DEMOLA_KEY_ALIAS / DEMOLA_KEY_PASS   Android 서명
#
# 열린 에디터가 같은 프로젝트를 잡고 있으면 배치 모드가 실패한다 → 로컬에서는 Tools/dev.sh build 를 쓴다.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LOGS="$ROOT/Builds/Logs"
RESULTS="$ROOT/Builds/TestResults"
mkdir -p "$LOGS" "$RESULTS"

VERSION="$(grep 'm_EditorVersion:' "$ROOT/ProjectSettings/ProjectVersion.txt" | awk '{print $2}')"

find_unity() {
  if [[ -n "${UNITY_EDITOR:-}" ]]; then echo "$UNITY_EDITOR"; return; fi
  local candidates=(
    "/c/Program Files/Unity/Hub/Editor/$VERSION/Editor/Unity.exe"
    "/e/Program Files/Unity/Hub/Editor/$VERSION/Editor/Unity.exe"
    "/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
    "$HOME/Unity/Hub/Editor/$VERSION/Editor/Unity"
    "/opt/unity/Editor/Unity"
  )
  for c in "${candidates[@]}"; do [[ -x "$c" || -f "$c" ]] && { echo "$c"; return; }; done
  echo "Unity $VERSION 를 찾지 못했습니다. UNITY_EDITOR 를 지정하세요." >&2; exit 2
}

UNITY="$(find_unity)"
echo "Unity: $UNITY ($VERSION)"

run_unity() {
  local log="$1"; shift
  set +e
  # shellcheck disable=SC2086
  "$UNITY" -batchmode -nographics -projectPath "$ROOT" -logFile "$log" ${UNITY_ARGS:-} "$@"
  local code=$?
  set -e
  echo "종료 코드 $code (로그: $log)"
  return $code
}

run_tests() {
  local platform="$1"
  # -runTests 는 -quit 없이 쓴다 (테스트가 끝나면 스스로 종료).
  run_unity "$LOGS/test-$platform.log" -runTests -testPlatform "$platform" -testResults "$RESULTS/$platform.xml"
}

target_flag() {
  case "$1" in
    Android) echo "Android" ;;
    Linux64) echo "StandaloneLinux64" ;;
    OSX) echo "StandaloneOSX" ;;
    *) echo "StandaloneWindows64" ;;
  esac
}

run_build() {
  local target="${1:-Win64}"; local dev="${2:-}"; local number="${3:-0}"; local version="${4:-}"
  local args=(-quit -buildTarget "$(target_flag "$target")" -executeMethod DemoLA.Editor.Build.BuildCommand.Build -demolaTarget "$target" -outputPath "Builds/$target")
  [[ "$dev" == "dev" ]] && args+=(-development)
  [[ "$number" != "0" ]] && args+=(-buildNumber "$number")
  [[ -n "$version" ]] && args+=(-versionName "$version")
  run_unity "$LOGS/build-$target.log" "${args[@]}"
  [[ -f "$ROOT/Builds/build-report.json" ]] && cat "$ROOT/Builds/build-report.json"
}

case "${1:-all}" in
  test)
    mode="${2:-all}"; rc=0
    [[ "$mode" == "editmode" || "$mode" == "all" ]] && { run_tests EditMode || rc=1; }
    [[ "$mode" == "playmode" || "$mode" == "all" ]] && { run_tests PlayMode || rc=1; }
    exit $rc ;;
  build)
    run_build "${2:-Win64}" "${3:-}" "${4:-0}" "${5:-}" ;;
  all)
    run_tests EditMode && run_tests PlayMode && run_build "${2:-Win64}" ;;
  *)
    sed -n '2,14p' "$0"; exit 1 ;;
esac
