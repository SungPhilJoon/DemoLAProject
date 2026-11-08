"""
pattern_analysis_valtan.xlsx → Assets/_Project/Data/CSV/BossPatternTable.csv, BossPhaseTable.csv

xlsx 를 고친 뒤 이 스크립트를 다시 돌리면 CSV 가 갱신되고, Unity 의 TableImportPostprocessor 가 테이블 에셋을 다시 만든다.
패턴목록의 '전조 시간(초) / 측정 평균', '후딜(초) / 측정 평균' 칸에 숫자가 있으면 TelegraphTime / Recovery 로 옮기고 Measured=1 로 표시한다.
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
        patterns.append([pattern_index(r[0]), r[0], r[1], r[2], r[3], r[4], r[10], r[11], r[12], r[18], r[17], telegraph, recovery, "-1", measured, r[16]])
    write_csv(os.path.join(OUT_DIR, "BossPatternTable.csv"),
              ["Index", "PatternId", "PhaseId", "Name", "Category", "Shape", "Response", "LinkedPatternId", "Confidence", "DamageTier", "DamageText", "TelegraphTime", "Recovery", "AreaScale", "Measured", "Notes"],
              patterns)

    phases = []
    for i, r in enumerate([r for r in sheets["페이즈"] if r and re.match(r"G\d-", r[0])]):
        r = r + [""] * (10 - len(r))
        ids = "|".join(x.strip() for x in r[6].split(",") if x.strip())
        phases.append([i + 1, r[0], r[1], r[2], ids, r[7], r[9]])
    write_csv(os.path.join(OUT_DIR, "BossPhaseTable.csv"),
              ["Index", "PhaseId", "Name", "EnterCondition", "PatternIds", "EndCondition", "Notes"],
              phases)
    print(f"patterns {len(patterns)}, phases {len(phases)} → {OUT_DIR}")


if __name__ == "__main__":
    main()
