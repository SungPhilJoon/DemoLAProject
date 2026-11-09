"""멀티플레이 동기화 분석: NetSyncProbe 가 남긴 host-*.jsonl 과 client-*.jsonl 을 비교한다.

  python Tools/data/net_sync_check.py Builds/NetTest [--lag 0.4]

기준 시각은 NetworkManager.ServerTime (클라이언트는 추정 서버 시각)이라 같은 축에서 비교할 수 있다.
복제는 10Hz(NetworkVariable) + 전송 지연이 있으므로, 클라이언트 값은 "같은 시각 ~ lag 초 전" 호스트 값 중 하나와 맞으면 정상으로 본다.
결과: 표준 출력 + <dir>/sync-report.md. FAIL 이 하나라도 있으면 종료 코드 1.
"""
import glob
import json
import math
import os
import sys
from bisect import bisect_left, bisect_right

# NetReplication.ReplicatedTags 와 같은 순서 (비트 번호).
TAGS = ["State_Dead", "State_Groggy", "Boss_CounterWindow", "Boss_StaggerCheck", "Buff_Armor",
        "State_Invulnerable", "State_Casting", "Buff_DeathWard", "Boss_Untargetable", "Boss_PatternBusy",
        "Boss_Channeling", "State_Airborne", "State_Frozen", "State_Stunned", "State_SuperArmor"]


def load(path):
    snaps, events = [], []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                item = json.loads(line)
            except json.JSONDecodeError:
                continue
            (snaps if item.get("type") == "snap" else events).append(item)
    snaps.sort(key=lambda s: s["t"])
    events.sort(key=lambda e: e["t"])
    return snaps, events


class Check:
    def __init__(self, name, unit, warn, fail, higher_is_worse=True):
        self.name, self.unit, self.warn, self.fail = name, unit, warn, fail
        self.samples = 0
        self.bad = 0
        self.worst = 0.0
        self.first_bad = None
        self.notes = {}

    def add(self, t, value, ok=None, note=None):
        self.samples += 1
        if value is not None and value > self.worst:
            self.worst = value
        is_bad = (not ok) if ok is not None else (value is not None and value > self.fail)
        if is_bad:
            self.bad += 1
            if self.first_bad is None:
                self.first_bad = t
            if note:
                self.notes[note] = self.notes.get(note, 0) + 1

    def ratio(self):
        return self.bad / self.samples if self.samples else 0.0

    def status(self):
        if self.samples == 0:
            return "SKIP"
        r = self.ratio()
        if r > 0.05:
            return "FAIL"
        if r > 0.0:
            return "WARN"
        return "PASS"


# 클라이언트의 ServerTime 은 추정치라 실제보다 약간 앞서 있을 수 있다 (NGO 는 지연을 보정해 서버 시각을 예측).
# 그래서 같은 시각 + LEAD 까지의 호스트 값도 허용한다.
LEAD = 0.15
# 0.1초 단위 복제로는 놓칠 수 있는 아주 짧은 태그 (시전 중 등) — 엄격 비교에서 빼고 따로 본다.
TRANSIENT = {"State_Casting"}


def window(host, times, t, lag):
    lo = bisect_left(times, t - lag)
    hi = bisect_right(times, t + LEAD)
    return host[lo:hi] if hi > lo else host[max(0, lo - 1):lo + 1]


def member_map(snap):
    return {m["id"]: m for m in snap.get("m", [])}


def compare(host_snaps, host_events, client_snaps, client_events, lag):
    times = [s["t"] for s in host_snaps]
    start = max(host_snaps[0]["t"], client_snaps[0]["t"]) + 1.0
    end = min(host_snaps[-1]["t"], client_snaps[-1]["t"]) - 0.5
    checks = {
        "boss_hp": Check("보스 체력", "%", 0.2, 0.5),
        "boss_pos": Check("보스 위치", "m", 0.5, 1.0),
        "boss_phase": Check("보스 페이즈", "", 0, 0),
        "boss_tags": Check("보스 상태 태그", "", 0, 0),
        "member_hp": Check("파티원 체력", "%", 0.5, 1.0),
        "member_pos": Check("파티원 위치", "m", 0.75, 1.5),
        "member_tags": Check("파티원 상태 태그", "", 0, 0),
        "member_dead": Check("파티원 사망", "", 0, 0),
        "esther": Check("에스더 게이지", "", 1, 2),
        "structures": Check("무너진 벽 · 기둥", "", 0, 0),
        "floor": Check("무너진 바닥", "", 0, 0),
        "pickups": Check("바닥 구슬 수", "", 0, 0),
        "telegraph_visible": Check("보이는 장판 수", "", 0, 0),
        "telegraph_pos": Check("장판 위치 (추적 포함)", "m", 0.5, 1.0),
    }
    for c in client_snaps:
        t = c["t"]
        if t < start or t > end or "boss" not in c:
            continue
        win = [h for h in window(host_snaps, times, t, lag) if "boss" in h]
        if not win:
            continue
        cb = c["boss"]
        hp_err = min(abs(cb["hp"] - h["boss"]["hp"]) / max(1.0, h["boss"]["hp"]) * 100.0 for h in win)
        checks["boss_hp"].add(t, hp_err)
        checks["boss_pos"].add(t, min(math.hypot(cb["x"] - h["boss"]["x"], cb["z"] - h["boss"]["z"]) for h in win))
        checks["boss_phase"].add(t, None, ok=any(cb["ph"] == h["boss"]["ph"] for h in win), note=cb["ph"])
        for bit, name in enumerate(TAGS):
            v = (cb["tags"] >> bit) & 1
            if not any(((h["boss"]["tags"] >> bit) & 1) == v for h in win):
                checks["boss_tags"].add(t, None, ok=False, note=f"{name} 클라이언트={v}")
                break
        else:
            checks["boss_tags"].add(t, None, ok=True)

        cm = member_map(c)
        for mid, m in cm.items():
            hms = [member_map(h).get(mid) for h in win]
            hms = [x for x in hms if x]
            if not hms:
                continue
            checks["member_hp"].add(t, min(abs(m["hp"] - x["hp"]) / max(1.0, x["hp"]) * 100.0 for x in hms))
            if not m["dead"]:
                checks["member_pos"].add(t, min(math.hypot(m["x"] - x["x"], m["z"] - x["z"]) for x in hms), note=f"id {mid}")
            checks["member_dead"].add(t, None, ok=any(m["dead"] == x["dead"] for x in hms), note=f"id {mid}")
            bad_tag = None
            for bit, name in enumerate(TAGS):
                if bit == 0 or name in TRANSIENT:
                    continue
                v = (m["tags"] >> bit) & 1
                if not any(((x["tags"] >> bit) & 1) == v for x in hms):
                    bad_tag = f"{name} 클라이언트={v}"
                    break
            checks["member_tags"].add(t, None, ok=bad_tag is None, note=bad_tag)

        checks["esther"].add(t, min(abs(c.get("es", 0) - h.get("es", 0)) for h in win))
        sd = set(filter(None, c.get("sd", "").split("|")))
        ok = any(sd == set(filter(None, h.get("sd", "").split("|"))) for h in win)
        note = None
        if not ok:
            host_sd = set(filter(None, win[-1].get("sd", "").split("|")))
            missing = sorted(host_sd - sd)
            extra = sorted(sd - host_sd)
            note = (f"클라이언트에 없음 {len(missing)}개" if missing else "") + (f" 클라이언트에만 {len(extra)}개" if extra else "")
        checks["structures"].add(t, None, ok=ok, note=note)
        checks["floor"].add(t, None, ok=any(c.get("fl") == h.get("fl") for h in win), note=f"클라이언트 {c.get('fl')} / 호스트 {win[-1].get('fl')}")
        checks["pickups"].add(t, None, ok=any(c.get("pk") == h.get("pk") for h in win), note=f"클라이언트 {c.get('pk')} / 호스트 {win[-1].get('pk')}")
        checks["telegraph_visible"].add(t, None, ok=any(c.get("tv") == h.get("tv") for h in win), note=f"클라이언트 {c.get('tv')} / 호스트 {win[-1].get('tv')}")
        # 장판 위치: 클라이언트에 보이는 장판마다 같은 시각 근처 호스트 장판 중 가장 가까운 것까지의 거리 (추적 장판이 따라가는가).
        ctp = c.get("tp") or []
        if ctp:
            hosts_tp = [p for h in win for p in (h.get("tp") or [])]
            if hosts_tp:
                worst = max(min(math.hypot(cp[0] - hp[0], cp[1] - hp[1]) for hp in hosts_tp) for cp in ctp)
                checks["telegraph_pos"].add(t, worst)

    # 장판 이벤트: 호스트 장판마다 클라이언트에 0.6초 안 · 0.5m 안의 장판이 있어야 한다.
    tele = Check("장판 RPC 전달", "", 0, 0)
    host_tele = [e for e in host_events if e["type"] == "tele" and start <= e["t"] <= end]
    client_tele = [e for e in client_events if e["type"] == "tele"]
    used = set()
    delays = []
    for e in host_tele:
        match = None
        for i, c in enumerate(client_tele):
            if i in used:
                continue
            dt = c["t"] - e["t"]
            if -LEAD - 0.1 <= dt <= 0.6 and math.hypot(c["x"] - e["x"], c["z"] - e["z"]) <= 0.5:
                match = i
                delays.append(dt)
                break
        if match is not None:
            used.add(match)
        tele.add(e["t"], None, ok=match is not None, note="클라이언트가 받지 못함")
    checks["telegraph_rpc"] = tele

    counter = Check("카운터 구간 표시 지연", "s", 0.25, 0.4)
    host_open = [e["t"] for e in host_events if e["type"] == "counter_open" and start <= e["t"] <= end]
    client_open = [e["t"] for e in client_events if e["type"] == "counter_open"]
    for ht in host_open:
        later = [ct - ht for ct in client_open if -LEAD - 0.1 <= ct - ht <= 2.0]
        counter.add(ht, later[0] if later else 9.9, note=None if later else "클라이언트에 열리지 않음")
    checks["counter_latency"] = counter
    return checks, start, end, delays


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    directory = sys.argv[1]
    lag = 0.4
    if "--lag" in sys.argv:
        lag = float(sys.argv[sys.argv.index("--lag") + 1])
    files = sorted(glob.glob(os.path.join(directory, "*.jsonl")))
    hosts = [f for f in files if os.path.basename(f).startswith("host-")]
    clients = [f for f in files if os.path.basename(f).startswith("client-")]
    if not hosts or not clients:
        print(f"host-*.jsonl 과 client-*.jsonl 이 필요합니다: {directory}")
        return 2
    host_snaps, host_events = load(hosts[-1])
    report = ["# 멀티플레이 동기화 보고서", "", f"- 호스트: `{os.path.basename(hosts[-1])}` (스냅샷 {len(host_snaps)})", f"- 허용 지연(lag): {lag}s", ""]
    failed = False
    for path in clients:
        client_snaps, client_events = load(path)
        if len(client_snaps) < 5 or len(host_snaps) < 5:
            report.append(f"## {os.path.basename(path)} — 스냅샷 부족 ({len(client_snaps)})")
            failed = True
            continue
        checks, start, end, delays = compare(host_snaps, host_events, client_snaps, client_events, lag)
        report.append(f"## {os.path.basename(path)} — 비교 구간 {start:.1f}s ~ {end:.1f}s ({end - start:.0f}초)")
        report.append("")
        report.append("| 항목 | 결과 | 어긋난 비율 | 최대 | 처음 어긋난 시각 | 주된 내용 |")
        report.append("|---|---|---|---|---|---|")
        for key, c in checks.items():
            st = c.status()
            failed = failed or st == "FAIL"
            worst = f"{c.worst:.2f}{c.unit}" if c.unit else "-"
            first = f"{c.first_bad:.1f}s" if c.first_bad is not None else "-"
            note = ", ".join(f"{k} ×{v}" for k, v in sorted(c.notes.items(), key=lambda kv: -kv[1])[:3])
            report.append(f"| {c.name} | **{st}** | {c.ratio() * 100:.1f}% ({c.bad}/{c.samples}) | {worst} | {first} | {note} |")
        if delays:
            delays.sort()
            report.append("")
            report.append(f"장판 RPC 지연: 중앙 {delays[len(delays) // 2] * 1000:.0f}ms · 최대 {delays[-1] * 1000:.0f}ms ({len(delays)}개)")
        report.append("")
    text = "\n".join(report)
    print(text)
    with open(os.path.join(directory, "sync-report.md"), "w", encoding="utf-8") as f:
        f.write(text + "\n")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
