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
        private static readonly string[] Blocks = { "체력 줄 기믹 (HpLine ≤ N, 1회)", "패턴 공격 + 추적 + 대기", "무력화/패턴 진행 대기" };

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
            }
        }

        public override void DrawContext(StateTreeEditorWindow InWindow, StateTreeAsset InAsset)
        {
            if (InAsset == null)
            {
                return;
            }

            EditorGUILayout.HelpBox("상태 순서 = 우선순위. 패턴 ID 는 BossDefinition.Patterns 의 PatternId (xlsx 패턴 ID) 와 같아야 합니다. 1회성 기믹은 FlagN 으로 재진입을 막습니다.", MessageType.Info);
        }
    }
}
