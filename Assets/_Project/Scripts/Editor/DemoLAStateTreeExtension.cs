using System.Collections.Generic;
using DemoLA.AI;
using DemoLA.Content;
using Framework.Editor;
using Framework.StateTree;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor
{
    /// <summary>
    /// 프레임워크 StateTree 에디터(Window/Framework/StateTree Editor) 확장.
    /// 템플릿: 1관문 / 2관문 보스, 파티 AI. 블록: 체력 줄 기믹 상태, 패턴 공격 묶음, 무력화 대기.
    /// 보스 패턴을 에디터에서 모듈식으로 조합/변경하기 위한 도우미 (프로젝트 개발 사항.md).
    /// </summary>
    public class DemoLAStateTreeExtension : StateTreeEditorExtension
    {
        private static readonly string[] Templates = { "1관문 보스 (xlsx G1)", "2관문 보스 (xlsx G2)", "파티 AI" };
        private static readonly string[] Blocks =
        {
            "체력 줄 기믹 (HpLine ≤ N, 1회)", "패턴 공격 + 추적 + 대기", "무력화/패턴 진행 대기",
            "대상 지정 → 패턴 (SelectTargetTask + UsePatternTask)", "원거리 견제 (가장 먼 대상 ≥ 8m → 돌진)",
        };

        public override IReadOnlyList<string> TemplateNames
        {
            get { return Templates; }
        }

        public override StateTreeAsset CreateTemplate(int InIndex)
        {
            switch (InIndex)
            {
                case 0: return DefaultTrees.CreateGate1Boss();
                case 1: return DefaultTrees.CreateGate2Boss();
                case 2: return DefaultTrees.CreatePartyAI();
            }

            return null;
        }

        public override IReadOnlyList<string> BlockNames
        {
            get { return Blocks; }
        }

        public override void AddBlock(StateTreeAsset InAsset, int InParentId, int InIndex)
        {
            switch (InIndex)
            {
                case 0:
                    var state = InAsset.AddState("기믹 (N줄)", InParentId);
                    state.EnterConditions.Add(new HpLineCondition { Op = ECompareOp.LessOrEqual, Line = 30.0f });
                    state.EnterConditions.Add(new FlagCondition { Key = EAIBlackboard.Flag8, Invert = true });
                    state.Tasks.Add(new SetFlagTask { Key = EAIBlackboard.Flag8, CompleteImmediately = false });
                    state.Tasks.Add(new UsePatternTask { PatternId = "G1-01" });
                    break;
                case 1:
                    InAsset.AddState("패턴 공격", InParentId).Tasks.Add(new UseRandomPatternTask());
                    InAsset.AddState("추적", InParentId).Tasks.Add(new ChaseTargetTask());
                    InAsset.AddState("대기", InParentId).Tasks.Add(new WaitTask { Duration = 0.4f });
                    break;
                case 2:
                    var groggy = InAsset.AddState("무력화", InParentId);
                    groggy.EnterConditions.Add(new HasTagCondition { Tag = DemoLA.Core.ETag.State_Groggy });
                    groggy.Tasks.Add(new WaitWhileGroggyTask());
                    var busy = InAsset.AddState("패턴 진행 중", InParentId);
                    busy.EnterConditions.Add(new HasTagCondition { Tag = DemoLA.Core.ETag.Boss_PatternBusy });
                    busy.Tasks.Add(new WaitTask { Duration = 0.1f });
                    break;
                case 3:
                    var targeted = InAsset.AddState("대상 지정 → 패턴", InParentId);
                    targeted.EnterConditions.Add(new PatternReadyCondition { PatternId = "G2-24" });
                    targeted.Tasks.Add(new SelectTargetTask { Selector = new DemoLA.Boss.NearestSelector(), ApplyTo = ETargetApply.NextPattern });
                    targeted.Tasks.Add(new UsePatternTask { PatternId = "G2-24" });
                    break;
                case 4:
                    var poke = InAsset.AddState("원거리 견제 돌진", InParentId);
                    poke.EnterConditions.Add(new PatternReadyCondition { PatternId = "G2-11" });
                    poke.EnterConditions.Add(new TargetRuleCondition { Selector = new DemoLA.Boss.FarthestSelector(), Op = ECompareOp.GreaterOrEqual, Distance = 8.0f });
                    poke.Tasks.Add(new SelectTargetTask { Selector = new DemoLA.Boss.FarthestSelector(), ApplyTo = ETargetApply.NextPattern });
                    poke.Tasks.Add(new UsePatternTask { PatternId = "G2-11" });
                    break;
            }
        }

        public override void DrawContext(StateTreeEditorWindow InWindow, StateTreeAsset InAsset)
        {
            if (InAsset == null)
            {
                return;
            }

            EditorGUILayout.HelpBox("상태 순서 = 우선순위. 패턴 ID 칸은 드롭다운(◆ = 카운터 가능) + GAS 버튼으로 GAS Tool 에서 그 패턴의 단계 · 카운터 · 대상을 고칩니다. 1회성 기믹은 FlagN 으로 재진입을 막습니다.", MessageType.Info);
            drawBossContext(InAsset);
        }

        /// <summary>
        /// 이 트리를 쓰는 보스와, 트리가 직접 쓰는 패턴(UsePatternTask)의 요약 — 원본 ActionRPG 의 몬스터 패널처럼
        /// "어빌리티는 GAS Tool, 언제 쓰는지는 StateTree" 를 한 화면에서 오간다.
        /// </summary>
        private static void drawBossContext(StateTreeAsset InAsset)
        {
            var database = AssetDatabase.LoadAssetAtPath<DemoLA.Data.GameDatabase>(DemoLAProjectSetup.DatabasePath);
            if (database == null)
            {
                return;
            }

            foreach (var boss in database.Bosses)
            {
                if (boss == null || boss.Brain != InAsset)
                {
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                using (new EditorGUILayout.HorizontalScope())
                {
                    int counters = boss.Patterns.FindAll(p => p != null && p.IsCounterable).Count;
                    EditorGUILayout.LabelField($"보스: {boss.DisplayName} — 패턴 {boss.Patterns.Count} (◆ 카운터 {counters})", EditorStyles.boldLabel);
                    if (GUILayout.Button("GAS Tool: Ability Set", GUILayout.Width(150)) == true)
                    {
                        Gas.GasToolWindow.OpenAbilitySet(boss);
                    }
                }

                var used = new HashSet<string>();
                foreach (var state in InAsset.States)
                {
                    foreach (var task in state.Tasks)
                    {
                        var use = task as UsePatternTask;
                        if (use == null || used.Add(use.PatternId) == false)
                        {
                            continue;
                        }

                        var pattern = boss.GetPattern(use.PatternId);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            string summary = pattern == null ? "⚠ 보스에 없는 패턴" : $"{pattern.DisplayName} · 타격 {pattern.Steps.Count}{(pattern.IsCounterable ? $" · ◆ {pattern.Steps.FindAll(s => s.Counterable).Count}" : string.Empty)} · {DemoLA.Boss.PatternTargeting.Describe(pattern)}";
                            EditorGUILayout.LabelField($"  {use.PatternId}", summary, EditorStyles.miniLabel);
                            if (pattern != null && GUILayout.Button("GAS", EditorStyles.miniButton, GUILayout.Width(44)) == true)
                            {
                                Gas.GasToolWindow.OpenPattern(use.PatternId);
                            }
                        }
                    }
                }

                EditorGUILayout.EndVertical();
            }
        }
    }
}
