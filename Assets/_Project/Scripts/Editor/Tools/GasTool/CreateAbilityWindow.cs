using System.Linq;
using DemoLA.Boss;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor.Gas
{
    /// <summary>
    /// "새 Ability" 창: 보스 · 템플릿 · 패턴 ID · 이름 · 페이즈를 고르면 패턴 에셋을 만든다 (원본 CreateAbilityWindow 의 DemoLA 판).
    /// 에셋은 Data/Authored/Boss/Patterns 에 만들어 전체 셋업이 지우지 않고, 셋업 때 보스 · 페이즈 목록에 다시 들어간다.
    /// </summary>
    public class CreateAbilityWindow : EditorWindow
    {
        public const string PatternFolder = GasToolWindow.AuthoredRoot + "/Boss/Patterns";

        private GasToolWindow Owner;
        private int BossIndex;
        private AbilityTemplates.EKind Kind = AbilityTemplates.EKind.FrontBackCounterSlam;
        private string PatternId = string.Empty;
        private string DisplayName = "새 패턴";
        private int PhaseIndex;

        public static void Open(GasToolWindow InOwner)
        {
            var window = GetWindow<CreateAbilityWindow>(true, "새 Ability", true);
            window.Owner = InOwner;
            window.minSize = new Vector2(420.0f, 220.0f);
        }

        private void OnGUI()
        {
            var database = AssetDatabase.LoadAssetAtPath<DemoLA.Data.GameDatabase>(DemoLAProjectSetup.DatabasePath);
            if (database == null)
            {
                EditorGUILayout.HelpBox("GameDatabase 없음", MessageType.Error);
                return;
            }

            var bosses = database.Bosses.Where(b => b != null).ToList();
            BossIndex = EditorGUILayout.Popup("보스", Mathf.Clamp(BossIndex, 0, bosses.Count - 1), bosses.Select(b => b.DisplayName).ToArray());
            var boss = bosses[BossIndex];
            Kind = (AbilityTemplates.EKind)EditorGUILayout.Popup("템플릿", (int)Kind, AbilityTemplates.Labels);
            if (string.IsNullOrEmpty(PatternId) == true)
            {
                PatternId = NextPatternId(boss);
            }

            PatternId = EditorGUILayout.TextField(new GUIContent("패턴 ID", "같은 보스 안에서 겹치면 안 된다. 접두사(G1/G2)로 보스를 구분"), PatternId);
            DisplayName = EditorGUILayout.TextField("이름", DisplayName);
            var phases = boss.Phases.Select(p => $"{p.PhaseId} {p.DisplayName}").Prepend("(페이즈 목록에 넣지 않음 — StateTree 로만 사용)").ToArray();
            PhaseIndex = EditorGUILayout.Popup(new GUIContent("랜덤 패턴 후보 페이즈"), Mathf.Clamp(PhaseIndex, 0, phases.Length - 1), phases);

            bool exists = boss.GetPattern(PatternId) != null;
            if (exists == true)
            {
                EditorGUILayout.HelpBox($"{PatternId} 은(는) 이미 있습니다.", MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(exists == true || string.IsNullOrWhiteSpace(PatternId) == true))
            {
                if (GUILayout.Button("만들기", GUILayout.Height(28.0f)) == true)
                {
                    string phaseId = PhaseIndex > 0 ? boss.Phases[PhaseIndex - 1].PhaseId : string.Empty;
                    var pattern = Create(boss, Kind, PatternId.Trim(), DisplayName, phaseId);
                    if (Owner != null)
                    {
                        GasToolWindow.OpenPattern(pattern.PatternId);
                    }

                    Close();
                }
            }
        }

        /// <summary>"G2-C01" 처럼 보스 접두사 + C(자체 제작) + 번호.</summary>
        public static string NextPatternId(BossDefinition InBoss)
        {
            string prefix = InBoss.Patterns.Count > 0 && InBoss.Patterns[0] != null ? InBoss.Patterns[0].PatternId.Split('-')[0] : InBoss.BossId;
            for (int i = 1; i < 100; ++i)
            {
                string id = $"{prefix}-C{i:00}";
                if (InBoss.GetPattern(id) == null)
                {
                    return id;
                }
            }

            return $"{prefix}-C99";
        }

        public static BossPatternDefinition Create(BossDefinition InBoss, AbilityTemplates.EKind InKind, string InPatternId, string InName, string InPhaseId)
        {
            DemoLAProjectSetup.ensureFolder(PatternFolder);
            var pattern = CreateInstance<BossPatternDefinition>();
            pattern.PatternId = InPatternId;
            pattern.DisplayName = InName;
            pattern.PhaseId = InPhaseId;
            pattern.Notes = "GAS Tool 로 만든 패턴";
            pattern.ToolEdited = true;
            AbilityTemplates.Fill(pattern, InKind);
            AssetDatabase.CreateAsset(pattern, AssetDatabase.GenerateUniqueAssetPath($"{PatternFolder}/PT_{InPatternId}.asset"));

            Undo.RecordObject(InBoss, "새 패턴");
            InBoss.Patterns.Add(pattern);
            var phase = InBoss.GetPhase(InPhaseId);
            if (phase != null && phase.PatternIds.Contains(InPatternId) == false)
            {
                phase.PatternIds.Add(InPatternId);
            }

            EditorUtility.SetDirty(InBoss);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GAS Tool] 새 패턴 {InPatternId} '{InName}' ({AbilityTemplates.Labels[(int)InKind]}) → {AssetDatabase.GetAssetPath(pattern)}");
            return pattern;
        }
    }
}
