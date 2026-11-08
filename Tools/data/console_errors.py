"""unity command console get --format json 출력(stdin)에서 컴파일 오류를 중복 없이 출력한다. dev.sh errors 가 쓴다."""
import json
import sys

data = json.load(sys.stdin)
result = (data.get("data") or {}).get("result") or {}
seen = set()
only_cs = len(sys.argv) > 1 and sys.argv[1] == "--cs"
for entry in result.get("entries", []):
    if entry.get("seeded"):
        continue
    line = entry["message"].splitlines()[0]
    if only_cs and "error CS" not in line:
        continue
    if line in seen:
        continue
    seen.add(line)
    print(line.replace("Assets\\_Project\\Scripts\\", "")[:300])
