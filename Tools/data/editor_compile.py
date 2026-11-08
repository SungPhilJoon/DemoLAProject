"""
열린 Unity 에디터에 AssetDatabase 갱신 + 스크립트 컴파일을 요청하고, 끝날 때까지 기다린 뒤 이번 컴파일의 오류만 출력한다.
에디터가 포커스 없이 떠 있어도 동작한다 (unity CLI + com.unity.pipeline).

사용: python Tools/data/editor_compile.py [--project <path>] [--timeout 600]
종료 코드: 0 = 성공(도메인 리로드 완료), 1 = 컴파일 오류, 2 = 시간 초과
"""
import argparse
import datetime
import json
import os
import subprocess
import sys
import time

REFRESH = ('UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport); '
           'UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(); return "ok";')


def unity(project, *args):
    cmd = ["unity", "command", "--project-path", project, *args, "--format", "json"]
    try:
        out = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", timeout=120).stdout
        return json.loads(out)
    except Exception:
        return None


def result(data):
    if not isinstance(data, dict):
        return {}
    return (data.get("data") or {}).get("result") or {}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", default=os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))
    parser.add_argument("--timeout", type=int, default=600)
    args = parser.parse_args()

    status = result(unity(args.project, "editor", "status"))
    load_count = status.get("codeLoadCount", -1)
    started = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%S")
    unity(args.project, "script", "eval", "--code", REFRESH)

    deadline = time.time() + args.timeout
    saw_compiling = False
    while time.time() < deadline:
        time.sleep(4)
        status = result(unity(args.project, "editor", "status"))
        if not status:
            continue  # 도메인 리로드 중에는 응답이 없다.
        if status.get("compiling"):
            saw_compiling = True
            continue
        if status.get("codeLoadCount", -1) > load_count:
            print(f"컴파일 성공 (codeLoadCount {load_count} → {status.get('codeLoadCount')})")
            return 0
        console = result(unity(args.project, "console", "get", "--level", "error", "--tail", "200"))
        fresh = []
        for entry in console.get("entries", []):
            if entry.get("timestampUtc", "") >= started and "error CS" in entry.get("message", ""):
                line = entry["message"].splitlines()[0]
                if line not in fresh:
                    fresh.append(line)
        if fresh:
            for line in fresh:
                print(line.replace("Assets\\_Project\\", ""))
            return 1
        if saw_compiling and (console.get("groundTruth") or {}).get("compilationFailed") is False:
            print("컴파일 성공 (변경 없음)")
            return 0
    print("시간 초과")
    return 2


if __name__ == "__main__":
    sys.exit(main())
