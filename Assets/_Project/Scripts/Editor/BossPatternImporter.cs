using DemoLA.Boss;
using DemoLA.Content;
using DemoLA.Data;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor
{
    /// <summary>
    /// BossPatternTable(CSV) → 보스 패턴 에셋 갱신.
    /// 흐름: xlsx 수정 → python Tools/data/xlsx_to_csv.py → (CSV 저장 시 테이블 자동 재변환) → 이 메뉴로 패턴 에셋 갱신.
    /// 에셋에서 직접 고친 패턴(에셋의 TimingsMeasured 가 켜져 있고 CSV 에는 측정값이 없음)은 실행 단계를 보존한다.
    /// </summary>
    public static class BossPatternImporter
    {
        [MenuItem("DemoLA/Data/보스 패턴 테이블 → 패턴 에셋 갱신", priority = 20)]
        public static void ReimportMenu()
        {
            Debug.Log(Reimport());
        }

        public static string Reimport()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DemoLAProjectSetup.DatabasePath);
            if (database == null)
            {
                return "[DemoLA] GameDatabase 없음 — DemoLA/Setup/0. 전체 셋업 을 먼저 실행하세요.";
            }

            var tables = DemoLAProjectSetup.ImportTables();
            int created = 0;
            int updated = 0;
            int kept = 0;
            string folder = $"{DemoLAProjectSetup.DataRoot}/Boss/Patterns";
            DemoLAProjectSetup.ensureFolder(folder);

            foreach (var row in tables.BossPattern.All)
            {
                var boss = row.PatternId.StartsWith("G1") ? database.GetBoss("Gate1") : database.GetBoss("Gate2");
                if (boss == null)
                {
                    continue;
                }

                var fresh = BossPatternCsv.CreatePattern(row);
                PatternPresets.Apply(fresh);
                BossPatternCsv.ApplyMeasured(fresh, row);
                var existing = boss.GetPattern(row.PatternId);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(fresh, $"{folder}/PT_{row.PatternId}.asset");
                    boss.Patterns.Add(fresh);
                    EditorUtility.SetDirty(boss);
                    created += 1;
                    continue;
                }

                if (existing.TimingsMeasured == true && row.Measured == false)
                {
                    copyMetadata(fresh, existing);
                    kept += 1;
                }
                else
                {
                    string name = existing.name;
                    EditorUtility.CopySerialized(fresh, existing);
                    existing.name = name;
                    updated += 1;
                }

                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(fresh);
            }

            AssetDatabase.SaveAssets();
            return $"[DemoLA] 패턴 갱신: 새로 {created} · 갱신 {updated} · 에셋 측정값 보존 {kept}";
        }

        private static void copyMetadata(BossPatternDefinition InFrom, BossPatternDefinition InTo)
        {
            InTo.PhaseId = InFrom.PhaseId;
            InTo.DisplayName = InFrom.DisplayName;
            InTo.Category = InFrom.Category;
            InTo.Response = InFrom.Response;
            InTo.ShapeText = InFrom.ShapeText;
            InTo.LinkedPatternId = InFrom.LinkedPatternId;
            InTo.Confidence = InFrom.Confidence;
            InTo.DamageTier = InFrom.DamageTier;
            InTo.Notes = InFrom.Notes;
        }

        [MenuItem("DemoLA/Data/패턴 구현 현황 로그", priority = 21)]
        public static void LogStatus()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DemoLAProjectSetup.DatabasePath);
            if (database == null)
            {
                return;
            }

            foreach (var boss in database.Bosses)
            {
                int implemented = 0;
                int measured = 0;
                foreach (var pattern in boss.Patterns)
                {
                    implemented += pattern.Implemented ? 1 : 0;
                    measured += pattern.TimingsMeasured ? 1 : 0;
                }

                Debug.Log($"[DemoLA] {boss.DisplayName}: 패턴 {boss.Patterns.Count} · 구현 {implemented} · 측정값 적용 {measured}");
            }
        }
    }
}
