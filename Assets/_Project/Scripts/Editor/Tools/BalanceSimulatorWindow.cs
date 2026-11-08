using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DemoLA.Build;
using DemoLA.Character;
using DemoLA.Content;
using DemoLA.Data;
using DemoLA.Tools;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor
{
    /// <summary>
    /// 밸런스 시뮬레이터 (DemoLA/Tools/Balance Simulator).
    /// 밸런스 툴에서 흔히 쓰는 방식을 따른다:
    ///  - 결정적 시뮬레이션: 고정 틱 · 시드 고정 · 여러 번 돌려 평균 ± 표준편차 (한 번 돌린 숫자로 판단하지 않는다)
    ///  - A/B 비교: 두 시나리오(직업/빌드/위치)를 같은 조건으로 돌려 차이를 % 로
    ///  - 파라미터 스윕: 한 값(강화 단계 · 보석 레벨)을 바꾸며 DPS 곡선을 본다 → 수확 체감/급등 구간 확인
    ///  - 결과 보관: CSV + 설정 스냅샷(JSON)을 Builds/Balance 에 시각별로 남겨 버전 간 비교
    /// 데이터는 GameDatabase(에셋) 그대로 읽으므로 CSV/에셋을 고친 뒤 다시 돌리면 바로 반영된다.
    /// </summary>
    public class BalanceSimulatorWindow : EditorWindow
    {
        private enum EBuildSource
        {
            DefaultFull = 0,
            Bare = 1,
        }

        private enum ESweep
        {
            None = 0,
            HoningLevel = 1,
            GemLevel = 2,
        }

        [Serializable]
        private class Scenario
        {
            public SimulationConfig Config = new SimulationConfig();
            public EBuildSource Source;
        }

        private const string OutputFolder = "Builds/Balance";

        private Scenario a = new Scenario();
        private Scenario b = new Scenario();
        private bool compare = true;
        private ESweep sweep;
        private SimulationResult resultA;
        private SimulationResult resultB;
        private readonly List<(int Value, float Dps)> sweepResult = new List<(int, float)>();
        private Vector2 scroll;
        private string lastExport;

        [MenuItem("DemoLA/Tools/Balance Simulator", priority = 210)]
        public static void Open()
        {
            GetWindow<BalanceSimulatorWindow>("Balance Simulator");
        }

        private void OnEnable()
        {
            a.Config.Label = "A";
            b.Config.Label = "B";
            b.Config.FromBehind = true;
        }

        private void OnGUI()
        {
            var database = loadDatabase();
            if (database == null)
            {
                EditorGUILayout.HelpBox("GameDatabase 가 없습니다. DemoLA/Setup/0. 전체 셋업 을 실행하세요.", MessageType.Warning);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUILayout.HorizontalScope())
            {
                drawScenario(a, database);
                compare = EditorGUILayout.ToggleLeft("B 비교", compare, GUILayout.Width(60));
                if (compare == true)
                {
                    drawScenario(b, database);
                }
            }

            sweep = (ESweep)EditorGUILayout.EnumPopup("파라미터 스윕 (A 기준)", sweep);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("실행", GUILayout.Height(28)))
                {
                    run(database);
                }

                if (GUILayout.Button("CSV 내보내기", GUILayout.Height(28), GUILayout.Width(120)))
                {
                    lastExport = export();
                }
            }

            if (string.IsNullOrEmpty(lastExport) == false)
            {
                EditorGUILayout.HelpBox($"내보냄: {lastExport}", MessageType.None);
            }

            drawResult(resultA);
            if (compare == true)
            {
                drawResult(resultB);
                if (resultA != null && resultB != null && resultA.MeanDps > 0.0f)
                {
                    float diff = (resultB.MeanDps / resultA.MeanDps - 1.0f) * 100.0f;
                    // 차이가 두 표준편차 합보다 작으면 우연일 수 있다.
                    bool meaningful = Mathf.Abs(resultB.MeanDps - resultA.MeanDps) > resultA.StdDps + resultB.StdDps;
                    EditorGUILayout.HelpBox($"B / A = {diff:+0.0;-0.0}%{(meaningful ? string.Empty : "  (표준편차 범위 안 — 시행 수를 늘려 보세요)")}", meaningful ? MessageType.Info : MessageType.Warning);
                }
            }

            if (sweepResult.Count > 0)
            {
                EditorGUILayout.LabelField($"스윕: {sweep}", EditorStyles.boldLabel);
                float max = sweepResult.Max(p => p.Dps);
                float previous = -1.0f;
                foreach (var point in sweepResult)
                {
                    var rect = EditorGUILayout.GetControlRect(false, 16);
                    EditorGUI.DrawRect(new Rect(rect.x + 60, rect.y + 2, (rect.width - 220) * (max > 0 ? point.Dps / max : 0), rect.height - 4), new Color(0.3f, 0.6f, 0.9f));
                    string gain = previous > 0.0f ? $"{(point.Dps / previous - 1.0f) * 100.0f:+0.0;-0.0}%" : string.Empty;
                    EditorGUI.LabelField(new Rect(rect.x, rect.y, 60, rect.height), point.Value.ToString());
                    EditorGUI.LabelField(new Rect(rect.xMax - 160, rect.y, 160, rect.height), $"{point.Dps:N0}  {gain}");
                    previous = point.Dps;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void drawScenario(Scenario InScenario, GameDatabase InDatabase)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var config = InScenario.Config;
                config.Label = EditorGUILayout.TextField("이름", config.Label);
                config.Class = (ECharacterClass)EditorGUILayout.EnumPopup("직업", config.Class);
                InScenario.Source = (EBuildSource)EditorGUILayout.EnumPopup("빌드", InScenario.Source);
                var bosses = InDatabase.Bosses.Select(x => x.BossId).ToArray();
                int bossIndex = Mathf.Max(0, Array.IndexOf(bosses, config.BossId));
                config.BossId = bosses.Length > 0 ? bosses[EditorGUILayout.Popup("대상 보스", bossIndex, bosses)] : config.BossId;
                config.Duration = EditorGUILayout.Slider("전투 시간(초)", config.Duration, 10.0f, 300.0f);
                config.Trials = EditorGUILayout.IntSlider("시행 수", config.Trials, 1, 30);
                config.Seed = EditorGUILayout.IntField("시드", config.Seed);
                config.FromBehind = EditorGUILayout.Toggle("백어택 위치", config.FromBehind);
                config.TargetGroggy = EditorGUILayout.Toggle("대상 무력화 상태", config.TargetGroggy);
                config.UseIdentity = EditorGUILayout.Toggle("아이덴티티 사용", config.UseIdentity);
                config.UseAwakening = EditorGUILayout.Toggle("각성기 사용", config.UseAwakening);
                config.Distance = EditorGUILayout.Slider("거리", config.Distance, 0.5f, 10.0f);
            }
        }

        private static void drawResult(SimulationResult InResult)
        {
            if (InResult == null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"[{InResult.Label}] DPS {InResult.MeanDps:N0} ± {InResult.StdDps:N0}   (최소 {InResult.MinDps:N0} / 최대 {InResult.MaxDps:N0})", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"평균 총 피해 {InResult.MeanTotal:N0} · 치명 {InResult.CritRate * 100.0f:0.#}% · 백어택 {InResult.BackRate * 100.0f:0.#}%");
                foreach (var pair in InResult.SkillShare)
                {
                    var rect = EditorGUILayout.GetControlRect(false, 14);
                    EditorGUI.DrawRect(new Rect(rect.x + 140, rect.y + 2, (rect.width - 200) * pair.Value, rect.height - 4), new Color(0.9f, 0.6f, 0.3f));
                    EditorGUI.LabelField(new Rect(rect.x, rect.y, 140, rect.height), pair.Key);
                    EditorGUI.LabelField(new Rect(rect.xMax - 56, rect.y, 56, rect.height), $"{pair.Value * 100.0f:0.#}%");
                }
            }
        }

        private void run(GameDatabase InDatabase)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Balance Simulator", "A 시뮬레이션", 0.1f);
                resultA = CombatSimulator.Run(InDatabase, prepare(a, InDatabase));
                resultB = null;
                if (compare == true)
                {
                    EditorUtility.DisplayProgressBar("Balance Simulator", "B 시뮬레이션", 0.4f);
                    resultB = CombatSimulator.Run(InDatabase, prepare(b, InDatabase));
                }

                sweepResult.Clear();
                if (sweep != ESweep.None)
                {
                    int max = sweep == ESweep.HoningLevel ? (InDatabase.Honing != null ? InDatabase.Honing.MaxLevel : 25) : 10;
                    int step = sweep == ESweep.HoningLevel ? 5 : 1;
                    for (int value = sweep == ESweep.HoningLevel ? 0 : 1; value <= max; value += step)
                    {
                        EditorUtility.DisplayProgressBar("Balance Simulator", $"스윕 {value}", 0.5f + 0.5f * value / max);
                        var config = prepare(a, InDatabase);
                        config.Trials = Mathf.Max(1, config.Trials / 2);
                        applySweep(config.Build, value);
                        sweepResult.Add((value, CombatSimulator.Run(InDatabase, config).MeanDps));
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static SimulationConfig prepare(Scenario InScenario, GameDatabase InDatabase)
        {
            var config = JsonUtility.FromJson<SimulationConfig>(JsonUtility.ToJson(InScenario.Config));
            config.Build = InScenario.Source == EBuildSource.Bare
                ? new CharacterBuildData { Class = config.Class }
                : DefaultContent.CreateDefaultBuild(InDatabase, config.Class);
            return config;
        }

        private void applySweep(CharacterBuildData InBuild, int InValue)
        {
            if (sweep == ESweep.HoningLevel)
            {
                foreach (var equipment in InBuild.Equipment)
                {
                    equipment.HoningLevel = InValue;
                }
            }
            else if (sweep == ESweep.GemLevel)
            {
                foreach (var gem in InBuild.Gems)
                {
                    gem.Level = InValue;
                }
            }
        }

        private string export()
        {
            Directory.CreateDirectory(OutputFolder);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string csvPath = Path.Combine(OutputFolder, $"balance-{stamp}.csv");
            var builder = new StringBuilder();
            builder.AppendLine(SimulationResult.CsvHeader);
            if (resultA != null)
            {
                builder.AppendLine(resultA.ToCsvRow());
            }

            if (resultB != null)
            {
                builder.AppendLine(resultB.ToCsvRow());
            }

            if (sweepResult.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine($"Sweep,{sweep}");
                builder.AppendLine("Value,MeanDps");
                foreach (var point in sweepResult)
                {
                    builder.AppendLine($"{point.Value},{point.Dps:0}");
                }
            }

            File.WriteAllText(csvPath, builder.ToString());
            File.WriteAllText(Path.Combine(OutputFolder, $"balance-{stamp}.config.json"), JsonUtility.ToJson(new ConfigSnapshot { A = a.Config, B = compare ? b.Config : null, Sweep = sweep.ToString() }, true));
            return csvPath;
        }

        [Serializable]
        private class ConfigSnapshot
        {
            public SimulationConfig A;
            public SimulationConfig B;
            public string Sweep;
        }

        private static GameDatabase loadDatabase()
        {
            return AssetDatabase.LoadAssetAtPath<GameDatabase>(DemoLAProjectSetup.DatabasePath);
        }
    }
}
