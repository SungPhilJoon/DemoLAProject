"""
pattern_analysis_valtan.xlsx → Assets/_Project/Data/CSV/BossPatternTable.csv, BossPhaseTable.csv

xlsx 를 고친 뒤 이 스크립트를 다시 돌리면 CSV 가 갱신되고, Unity 의 TableImportPostprocessor 가 테이블 에셋을 다시 만든다.
패턴목록의 '전조 시간(초) / 측정 평균', '후딜(초) / 측정 평균' 칸에 숫자가 있으면 TelegraphTime / Recovery 로 옮기고 Measured=1 로 표시한다.
xlsx 의 패턴 번호(G1-01)는 분석 문서의 행 번호일 뿐이고, 게임 데이터의 패턴 ID 는 GameplayTag(Pattern.G1.OrbStagger)다.
번호 → 태그 대응은 아래 PATTERN_TAGS 에 있다 (GameTags.cs 의 ETag 와 같아야 한다). xlsx 에 새 패턴 행을 넣으면 여기와 ETag 에 같이 추가한다.
(외부 패키지 없이 xlsx 의 XML 을 직접 읽는다.)

사용: python Tools/data/xlsx_to_csv.py [xlsx 경로]
"""
import csv
import io
import os
import re
import sys
import xml.etree.ElementTree as ET
import zipfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DEFAULT_XLSX = os.path.join(ROOT, "Documentation", "md파일", "프로젝트 설정", "pattern_analysis_valtan.xlsx")
OUT_DIR = os.path.join(ROOT, "Assets", "_Project", "Data", "CSV")
NS = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}


def col_index(ref):
    letters = re.match(r"[A-Z]+", ref).group(0)
    n = 0
    for ch in letters:
        n = n * 26 + ord(ch) - 64
    return n - 1


def read_sheets(path):
    z = zipfile.ZipFile(path)
    shared = []
    if "xl/sharedStrings.xml" in z.namelist():
        for si in ET.fromstring(z.read("xl/sharedStrings.xml")).findall("m:si", NS):
            shared.append("".join(t.text or "" for t in si.iter("{%s}t" % NS["m"])))
    names = [s.get("name") for s in ET.fromstring(z.read("xl/workbook.xml")).find("m:sheets", NS)]
    files = sorted([n for n in z.namelist() if n.startswith("xl/worksheets/sheet")], key=lambda x: int(re.findall(r"\d+", x)[0]))
    sheets = {}
    for name, f in zip(names, files):
        rows = []
        for row in ET.fromstring(z.read(f)).iter("{%s}row" % NS["m"]):
            values = {}
            for c in row.findall("m:c", NS):
                v = c.find("m:v", NS)
                t = c.get("t")
                if t == "s" and v is not None:
                    val = shared[int(v.text)]
                elif t == "inlineStr":
                    val = "".join(x.text or "" for x in c.iter("{%s}t" % NS["m"]))
                else:
                    val = (v.text if v is not None else "") or ""
                values[col_index(c.get("r"))] = val.replace("\n", " ").strip()
            if values:
                rows.append([values.get(i, "") for i in range(max(values) + 1)])
        sheets[name] = rows
    return sheets


# xlsx 패턴 번호 → GameplayTag (GameTags.cs ETag.Pattern_G1_* / Pattern_G2_* 와 같아야 한다).
PATTERN_TAGS = {
    "G1-01": "Pattern.G1.OrbStagger", "G1-02": "Pattern.G1.RivalIntrusion", "G1-03": "Pattern.G1.ChasingWhirlwind",
    "G1-04": "Pattern.G1.FearExecution", "G1-05": "Pattern.G1.Split", "G1-06": "Pattern.G1.BlinkDoubleClaw",
    "G1-07": "Pattern.G1.TripleClaw", "G1-08": "Pattern.G1.FanFrenzyClaw", "G1-09": "Pattern.G1.BleedClaw",
    "G1-10": "Pattern.G1.LeapDoubleClaw", "G1-11": "Pattern.G1.SpinKick", "G1-12": "Pattern.G1.CrouchBurst",
    "G1-13": "Pattern.G1.SonicCharge", "G1-14": "Pattern.G1.LineGrab", "G1-15": "Pattern.G1.BladeThrow",
    "G1-16": "Pattern.G1.SpinSlashOmen", "G1-17": "Pattern.G1.GravityZone", "G1-18": "Pattern.G1.FreezeTrap",
    "G1-19": "Pattern.G1.ExplosiveTrap", "G1-20": "Pattern.G1.DetonateProjectile", "G1-21": "Pattern.G1.RandomLaunchField",
    "G1-22": "Pattern.G1.StaggerFailChain", "G1-23": "Pattern.G1.FogPrisonGrab",
    "G2-01": "Pattern.G2.ArmorBreak", "G2-02": "Pattern.G2.WardOrb", "G2-03": "Pattern.G2.LethalFieldDouble",
    "G2-04": "Pattern.G2.ChasingLethalField", "G2-05": "Pattern.G2.TerrainBreakHalf", "G2-06": "Pattern.G2.TerrainBreakCenter",
    "G2-07": "Pattern.G2.CounterCharge", "G2-08": "Pattern.G2.TripleCounter", "G2-09": "Pattern.G2.FootFieldBarrage",
    "G2-10": "Pattern.G2.CloneCounter", "G2-11": "Pattern.G2.Charge", "G2-12": "Pattern.G2.LeapWedgeVortex",
    "G2-13": "Pattern.G2.SwingSlamBurst", "G2-14": "Pattern.G2.LeapAxeRain", "G2-15": "Pattern.G2.InOutField",
    "G2-16": "Pattern.G2.TrackingLaser", "G2-17": "Pattern.G2.BeamRockChain", "G2-18": "Pattern.G2.ChaseFieldSmash",
    "G2-19": "Pattern.G2.SphereStagger", "G2-20": "Pattern.G2.GhostGrab", "G2-21": "Pattern.G2.Whirlwind",
    "G2-22": "Pattern.G2.TripleSlash", "G2-23": "Pattern.G2.LeapSmashWhirlwind", "G2-24": "Pattern.G2.AxeChopCounter",
    "G2-25": "Pattern.G2.Skyfall", "G2-26": "Pattern.G2.ReLeap", "G2-27": "Pattern.G2.PillarCoverDrop",
}


def pattern_tag(number):
    number = number.strip()
    if not number:
        return ""
    if number not in PATTERN_TAGS:
        sys.exit(f"xlsx 패턴 번호 {number} 의 태그가 없습니다 — Tools/data/xlsx_to_csv.py PATTERN_TAGS 와 GameTags.cs ETag 에 추가하세요")
    return PATTERN_TAGS[number]


def number_or(value, default):
    try:
        return str(round(float(value), 3))
    except (TypeError, ValueError):
        return default


def pattern_index(pattern_id):
    m = re.match(r"G(\d)-(\d+)", pattern_id)
    return int(m.group(1)) * 100 + int(m.group(2))


def write_csv(path, header, rows):
    buf = io.StringIO()
    writer = csv.writer(buf, lineterminator="\n")
    writer.writerow(header)
    writer.writerows(rows)
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(buf.getvalue())


def main():
    xlsx = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_XLSX
    sheets = read_sheets(xlsx)
    os.makedirs(OUT_DIR, exist_ok=True)

    patterns = []
    for r in sheets["패턴목록"]:
        if not r or not re.match(r"G\d-\d\d", r[0]):
            continue
        r = r + [""] * (20 - len(r))
        telegraph = number_or(r[6], "-1")
        recovery = number_or(r[8], "-1")
        measured = "1" if telegraph != "-1" or recovery != "-1" else "0"
        patterns.append([pattern_index(r[0]), pattern_tag(r[0]), r[1], r[2], r[3], r[4], r[10], pattern_tag(r[11]), r[12], r[18], r[17], telegraph, recovery, "-1", measured, r[16]])
    write_csv(os.path.join(OUT_DIR, "BossPatternTable.csv"),
              ["Index", "PatternTag", "PhaseId", "Name", "Category", "Shape", "Response", "LinkedPatternTag", "Confidence", "DamageTier", "DamageText", "TelegraphTime", "Recovery", "AreaScale", "Measured", "Notes"],
              patterns)

    phases = []
    for i, r in enumerate([r for r in sheets["페이즈"] if r and re.match(r"G\d-", r[0])]):
        r = r + [""] * (10 - len(r))
        ids = "|".join(pattern_tag(x) for x in r[6].split(",") if x.strip())
        phases.append([i + 1, r[0], r[1], r[2], ids, r[7], r[9]])
    write_csv(os.path.join(OUT_DIR, "BossPhaseTable.csv"),
              ["Index", "PhaseId", "Name", "EnterCondition", "PatternTags", "EndCondition", "Notes"],
              phases)
    print(f"patterns {len(patterns)}, phases {len(phases)} → {OUT_DIR}")


if __name__ == "__main__":
    main()
