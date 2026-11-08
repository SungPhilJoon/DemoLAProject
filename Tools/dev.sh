#!/usr/bin/env bash
# DemoLA 개발 루프 (Unity CLI + com.unity.pipeline).
# 열려 있는 Unity 에디터에 명령을 보내 컴파일 · 테스트 · 플레이 확인 · 빌드를 터미널에서 돌린다.
#
#   Tools/dev.sh status                 에디터 상태
#   Tools/dev.sh compile                재컴파일 후 오류 출력 (실패 시 exit 1)
#   Tools/dev.sh test [editor|playmode|all] [필터 | cat:분류]   예) test playmode cat:Balance
#   Tools/dev.sh scenarios               시나리오 에셋 생성 (없는 것만) — Assets/_Project/Tests/Scenarios
#   Tools/dev.sh setup [overwrite]      CSV 테이블 + 데이터 에셋 + 입력 에셋 + Addressables + 레이드 씬
#   Tools/dev.sh play [초] [Gate1|Gate2] 플레이 모드 → 대기실 준비 완료 → 입장 → N초 진행 → 스크린샷 · 레이드 상태 · 딜 미터 · 오류 → 종료
#   Tools/dev.sh balance [직업] [초] [시행] [behind]   결정적 DPS 시뮬레이션
#   Tools/dev.sh deps                   Addressables 의존성 리포트
#   Tools/dev.sh build [Win64|Android] [dev]   에디터에서 플레이어 빌드 (BuildCommand)
#   Tools/dev.sh errors                 콘솔 오류
#
# 필요: unity CLI (https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh), python3
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/Builds/DevLoop"
mkdir -p "$OUT"
# Windows 의 python3 는 스토어 스텁일 수 있어 실제로 실행되는 것을 고른다.
PY=""
for c in python python3 py; do
  if command -v "$c" >/dev/null 2>&1 && "$c" -c "import json" >/dev/null 2>&1; then PY="$(command -v "$c")"; break; fi
done
[[ -z "$PY" ]] && { echo "python 이 필요합니다"; exit 1; }

uc() { unity command --project-path "$ROOT" "$@" --format json; }

# JSON 응답에서 data.result 를 꺼낸다.
result() { "$PY" -I -c 'import json,sys; d=json.load(sys.stdin); print(json.dumps((d.get("data") or {}).get("result",d) if isinstance(d,dict) else d, ensure_ascii=False))'; }
field() { "$PY" -I -c "import json,sys; d=json.load(sys.stdin); print(d.get('$1',''))"; }

wait_compile() {
  for _ in $(seq 1 60); do
    local s; s="$(uc script recompile_status 2>/dev/null | result || true)"
    local st; st="$(echo "$s" | field status 2>/dev/null || true)"
    if [[ "$st" == "completed" || "$st" == "up_to_date" ]]; then
      if echo "$s" | grep -q '"compilationFailed": true\|"failed": true'; then
        echo "$s" | "$PY" -I -c 'import json,sys; [print(e) for e in json.load(sys.stdin).get("errors",[])]'
        return 1
      fi
      return 0
    fi
    sleep 3
  done
  echo "컴파일 대기 시간 초과"; return 1
}

errors() {
  uc console get --level error --tail "${1:-30}" | result | "$PY" -I -c '
import json,sys
d=json.load(sys.stdin)
for e in d.get("entries",[]):
    if e.get("seeded"): continue
    print("-", e["message"].splitlines()[0][:300])
'
}

case "${1:-status}" in
  status)
    uc editor status | result ;;
  compile)
    # AssetDatabase 갱신 → 컴파일 요청 → 도메인 리로드 또는 이번 컴파일 오류까지 대기
    "$PY" -I "$ROOT/Tools/data/editor_compile.py" --project "$ROOT" ;;
  errors)
    errors 50 ;;
  test)
    mode="${2:-all}"; filter="${3:-}"
    run_tests() {
      local m="$1"
      local args=(test run --mode "$m" --timeout 900)
      # "cat:분류" 는 분류(Category) 필터 — 시나리오 에셋은 Category 가 분류가 된다 (예: cat:Mechanics, cat:Pattern.Gate2).
      if [[ "$filter" == cat:* ]]; then
        args+=(--filter "${filter#cat:}" --filter-type category)
      elif [[ -n "$filter" ]]; then
        args+=(--filter "$filter")
      fi
      if [[ "$m" == "playmode" ]]; then
        # 플레이 모드 진입 시 도메인 리로드가 HTTP 요청을 끊으므로 비동기 실행 후 상태를 폴링한다.
        uc "${args[@]}" --async-tests true >/dev/null
        local s=""
        for _ in $(seq 1 150); do
          sleep 4
          s="$(uc test status 2>/dev/null | result 2>/dev/null)" || continue
          echo "$s" | grep -q '"status": "completed"' && break
        done
        echo "$s"
      else
        uc "${args[@]}" | result
      fi
    }
    rc=0
    for m in $( [[ "$mode" == "all" ]] && echo "editor playmode" || echo "$mode" ); do
      run_tests "$m" | "$PY" -I -c '
import json,sys
d=json.load(sys.stdin)
s=d.get("Summary") or d.get("summary") or {}
g=lambda k: s.get(k, s.get(k.lower()))
print("[{}] 테스트 {} · 통과 {} · 실패 {} · 건너뜀 {}".format(sys.argv[1], g("Total"), g("Passed"), g("Failed"), g("Skipped")))
fails=[t for t in (d.get("Results") or d.get("results") or []) if t.get("Status") not in ("Passed","Skipped")]
for t in fails: print("FAIL", t.get("FullName"), "|", (t.get("Message") or "").strip()[:500])
sys.exit(1 if fails or not s else 0)
' "$m" || rc=1
    done
    exit $rc ;;
  scenarios)
    uc script eval --code 'return DemoLA.Editor.ScenarioAssetFactory.CreateAll(false);' | result ;;
  setup)
    # 대화창 없이 CSV 테이블 → 데이터 에셋 → 입력 에셋 → Addressables → 레이드 씬. 데이터를 다시 만들려면: setup overwrite
    uc demola setup --overwrite "$( [[ "${2:-}" == "overwrite" ]] && echo true || echo false )" | result ;;
  play)
    secs="${2:-20}"; gate="${3:-}"
    [[ -n "$gate" ]] && uc demola select_gate --gate "$gate" >/dev/null
    uc console clear >/dev/null 2>&1 || true
    uc editor.playmode set_run_in_background --enabled true >/dev/null
    uc script eval --code 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Raid.unity"); return "opened";' >/dev/null
    uc editor.playmode start >/dev/null
    # 플레이 모드 진입(도메인 리로드) 후 실제로 프레임이 도는지 기다린다.
    for _ in $(seq 1 30); do
      s="$(uc editor status 2>/dev/null | result 2>/dev/null)" || { sleep 2; continue; }
      echo "$s" | grep -q '"playerLoopTicking": true' && break
      sleep 2
    done
    # 대기실에서 준비 완료 → AI 파티원 준비(최대 4초) + 입장 카운트다운(5초) 뒤 레이드가 시작된다.
    uc demola ready >/dev/null; sleep 10
    end=$((SECONDS + secs)); while (( SECONDS < end )); do sleep 1; done
    uc capture game_view --output-path "Builds/DevLoop/play.png" | result | "$PY" -I -c 'import json,sys; print("스크린샷:", json.load(sys.stdin).get("savedPath"))'
    echo "--- 레이드 상태"; uc demola raid_status | result
    echo "--- 딜 미터"; uc demola dps | result | "$PY" -I -c 'import json,sys; print(json.load(sys.stdin).get("report",""))'
    echo "--- 오류"; errors 20
    uc editor.playmode stop >/dev/null; echo "플레이 종료" ;;
  balance)
    uc demola balance --class "${2:-Slayer}" --seconds "${3:-60}" --trials "${4:-5}" --behind "${5:-false}" | result | "$PY" -I -c 'import json,sys; print(json.load(sys.stdin).get("report",""))' ;;
  deps)
    uc demola deps | result ;;
  build)
    target="${2:-Win64}"; dev="$( [[ "${3:-}" == "dev" ]] && echo true || echo false )"
    uc demola build --target "$target" --development "$dev" --confirm true | result ;;
  *)
    sed -n '2,15p' "$0"; exit 1 ;;
esac
