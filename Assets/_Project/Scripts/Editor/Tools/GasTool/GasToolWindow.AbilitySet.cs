using System.Linq;
using DemoLA.Boss;
using DemoLA.Character;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor.Gas
{
    /// <summary>
    /// [Ability Set] 탭: 보스가 가진 패턴 목록(BossDefinition.Patterns) · 직업이 가진 스킬 목록 — 언리얼 Lyra 의 AbilitySet 대응.
    /// 보스는 생성 시 이 순서대로 패턴 어빌리티를 부여(GiveAbility)한다. 페이즈 칸 = 랜덤 패턴 공격이 고르는 후보 (BossPhaseDefinition.PatternIds).
    /// AI 가 언제 어떤 패턴을 쓰는지는 StateTree Editor 에서 정한다.
    /// </summary>
    public partial class GasToolWindow
    {
        [SerializeField] private ScriptableObject SelectedSetOwner;

        private void drawSetList()
        {
            listHeader("보스");
            foreach (var boss in Database.Bosses.Where(b => b != null))
            {
                string label = $"{boss.BossId} — {boss.DisplayName} ({boss.Patterns.Count})";
                if (matchesFilter(label) == true && listButton(SelectedSetOwner == boss, label, false) == true)
                {
                    SelectedSetOwner = boss;
                    GUI.FocusControl(null);
                }
            }

            listHeader("직업");
            foreach (var cls in Database.Classes.Where(c => c != null))
            {
                string label = $"{cls.DisplayName} ({cls.Skills.Count})";
                if (matchesFilter(label) == true && listButton(SelectedSetOwner == cls, label, false) == true)
                {
                    SelectedSetOwner = cls;
                    GUI.FocusControl(null);
                }
            }
        }

        private void drawSetEditor()
        {
            var boss = SelectedSetOwner as BossDefinition;
            if (boss != null)
            {
                drawBossSet(boss);
                return;
            }

            var cls = SelectedSetOwner as CharacterClassDefinition;
            if (cls != null)
            {
                EditorGUILayout.LabelField($"{cls.DisplayName} — Ability Set (스킬)", EditorStyles.largeLabel);
                foreach (var skill in cls.Skills.Where(s => s != null))
                {
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        GUILayout.Label($"[{skill.SkillId}] {skill.DisplayName}", GUILayout.Width(260));
                        GUILayout.Label($"쿨 {skill.Cooldown:0.#}s · 마나 {skill.ManaCost:0}", EditorStyles.miniLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Ability", EditorStyles.miniButton, GUILayout.Width(60)) == true)
                        {
                            Tab = 0;
                            SelectedSkill = skill;
                            ShowSkills = true;
                        }
                    }
                }

                return;
            }

            EditorGUILayout.HelpBox("왼쪽에서 보스나 직업을 고르세요. Ability Set = 생성될 때 받는 어빌리티 목록입니다.", MessageType.Info);
        }

        private void drawBossSet(BossDefinition InBoss)
        {
            EditorGUILayout.LabelField($"{InBoss.DisplayName} — Ability Set (패턴 {InBoss.Patterns.Count})", EditorStyles.largeLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("페이즈 칸 = 그 페이즈에서 랜덤 패턴 공격 후보. ◆ = 카운터 가능한 타격이 있음", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (InBoss.Brain != null && GUILayout.Button("StateTree Editor", GUILayout.Width(120)) == true)
                {
                    Framework.Editor.StateTreeEditorWindow.Open(InBoss.Brain);
                }
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("패턴", GUILayout.Width(300));
                foreach (var phase in InBoss.Phases)
                {
                    GUILayout.Label(new GUIContent(phase.PhaseId, phase.DisplayName), EditorStyles.miniLabel, GUILayout.Width(70));
                }
            }

            BossPatternDefinition remove = null;
            foreach (var pattern in InBoss.Patterns.Where(p => p != null).OrderBy(p => p.PatternId))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string label = $"[{pattern.PatternId}] {pattern.DisplayName}{(pattern.IsCounterable ? " ◆" : string.Empty)}";
                    if (GUILayout.Button(new GUIContent(label, "GAS Tool Ability 탭에서 열기"), EditorStyles.label, GUILayout.Width(300)) == true)
                    {
                        Tab = 0;
                        SelectedPatternId = pattern.PatternId;
                        SelectedSkill = null;
                    }

                    foreach (var phase in InBoss.Phases)
                    {
                        bool on = phase.PatternIds.Contains(pattern.PatternId);
                        bool next = EditorGUILayout.Toggle(on, GUILayout.Width(70));
                        if (next != on)
                        {
                            Undo.RecordObject(InBoss, "페이즈 패턴 목록");
                            if (next == true)
                            {
                                phase.PatternIds.Add(pattern.PatternId);
                            }
                            else
                            {
                                phase.PatternIds.Remove(pattern.PatternId);
                            }

                            EditorUtility.SetDirty(InBoss);
                        }
                    }

                    if (AssetDatabase.GetAssetPath(pattern).StartsWith(AuthoredRoot) == true && GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)) == true)
                    {
                        remove = pattern;
                    }
                }
            }

            if (remove != null && EditorUtility.DisplayDialog("GAS Tool", $"{remove.PatternId} 을(를) {InBoss.DisplayName} 에서 빼고 에셋을 지울까요?", "지우기", "취소") == true)
            {
                Undo.RecordObject(InBoss, "패턴 제거");
                InBoss.Patterns.Remove(remove);
                foreach (var phase in InBoss.Phases)
                {
                    phase.PatternIds.Remove(remove.PatternId);
                }

                EditorUtility.SetDirty(InBoss);
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(remove));
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.HelpBox("페이즈 칸은 CSV(BossPhaseTable.csv) 에서 만들어지며, 전체 셋업 시 CSV 값으로 돌아갑니다. GAS Tool 로 만든 패턴(Data/Authored)은 셋업 때 자기 PhaseId 에 다시 들어갑니다.", MessageType.None);
        }
    }
}
