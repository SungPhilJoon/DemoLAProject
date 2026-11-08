using DemoLA.AI;
using DemoLA.Boss;
using DemoLA.Core;
using Framework.StateTree;
using UnityEngine;

namespace DemoLA.Content
{
    /// <summary>
    /// 기본 StateTree. 에디터 확장(템플릿)과 런타임 기본 콘텐츠가 같이 쓴다.
    /// 체력 줄 트리거는 xlsx 페이즈/패턴목록의 '체력 N줄 부근' 기록을 따른다.
    /// 상태 순서 = 우선순위 (위에서부터 Enter Condition 을 통과한 첫 상태).
    /// 1회성 기믹은 FlagN 으로 다시 들어가지 않게 막는다.
    /// </summary>
    public static class DefaultTrees
    {
        public static StateTreeAsset CreateGate1Boss()
        {
            var tree = create("1관문 보스 AI (xlsx G1)");
            var root = tree.AddState("Boss", 0);
            addCommonBossStates(tree, root.Id);

            var split = gimmickState(tree, root.Id, "G1-05 분열 (46줄)", 46.0f, EAIBlackboard.Flag1);
            split.Tasks.Add(new SetPhaseTask { PhaseId = "G1-P1", CompleteImmediately = false });
            split.Tasks.Add(new UsePatternTask { PatternId = "G1-05" });

            var orb30 = gimmickState(tree, root.Id, "G1-01 구슬 무력화 (30줄)", 30.0f, EAIBlackboard.Flag2);
            orb30.Tasks.Add(new StartGimmickTask { GimmickId = "G1-01" });
            phaseState(tree, root.Id, "→ G1-P2 루카스 구간", EAIBlackboard.Flag2, "G1-P1", "G1-P2");

            var orb16 = gimmickState(tree, root.Id, "G1-01 구슬 무력화 (16줄)", 16.0f, EAIBlackboard.Flag3);
            orb16.Tasks.Add(new StartGimmickTask { GimmickId = "G1-01" });
            phaseState(tree, root.Id, "→ G1-P3 합체 구간", EAIBlackboard.Flag3, "G1-P2", "G1-P3");

            addAttackStates(tree, root.Id, "G1-공통");
            return tree;
        }

        public static StateTreeAsset CreateGate2Boss()
        {
            var tree = create("2관문 보스 AI (xlsx G2)");
            var root = tree.AddState("Boss", 0);
            addCommonBossStates(tree, root.Id);

            var armor = tree.AddState("G2-01 갑옷 시작", root.Id);
            armor.EnterConditions.Add(new FlagCondition { Key = EAIBlackboard.Flag1, Invert = true });
            armor.Tasks.Add(new SetFlagTask { Key = EAIBlackboard.Flag1, CompleteImmediately = true });
            armor.Tasks.Add(new StartGimmickTask { GimmickId = "G2-01", WaitForCompletion = false });

            var ward = gimmickState(tree, root.Id, "G2-02 방어 구슬 (131줄)", 131.0f, EAIBlackboard.Flag2);
            ward.Tasks.Add(new StartGimmickTask { GimmickId = "G2-02" });

            var lethal = gimmickState(tree, root.Id, "G2-03 즉사 장판 2연속 (130줄)", 130.0f, EAIBlackboard.Flag3);
            lethal.Tasks.Add(new UsePatternTask { PatternId = "G2-03" });
            phaseState(tree, root.Id, "→ G2-P2 지형 파괴 구간", EAIBlackboard.Flag3, "G2-P1", "G2-P2");

            var collapse1 = gimmickState(tree, root.Id, "G2-05 지형 파괴 1차 (88줄)", 88.0f, EAIBlackboard.Flag4);
            collapse1.Tasks.Add(new StartGimmickTask { GimmickId = "G2-05" });

            var counter = gimmickState(tree, root.Id, "G2-07 카운터 돌진 (65줄)", 65.0f, EAIBlackboard.Flag5);
            counter.Tasks.Add(new UsePatternTask { PatternId = "G2-07" });

            var collapse2 = gimmickState(tree, root.Id, "G2-06 지형 파괴 2차 (30줄)", 30.0f, EAIBlackboard.Flag6);
            collapse2.Tasks.Add(new StartGimmickTask { GimmickId = "G2-06" });

            var chain = gimmickState(tree, root.Id, "G2-09 연속 타격 (16줄)", 16.0f, EAIBlackboard.Flag7);
            chain.Tasks.Add(new UsePatternTask { PatternId = "G2-09" });
            phaseState(tree, root.Id, "→ G2-P3 유령 구간 (40줄)", EAIBlackboard.Flag7, "G2-P2", "G2-P3");

            addAttackStates(tree, root.Id, "G2-공통");
            return tree;
        }

        public static StateTreeAsset CreatePartyAI()
        {
            var tree = create("파티 AI (NetCode 이전 아군)");
            var root = tree.AddState("Ally", 0);

            var escape = tree.AddState("장판 회피", root.Id);
            escape.Color = new Color(0.6f, 0.3f, 0.2f);
            escape.EnterConditions.Add(new FlagCondition { Key = EAIBlackboard.InDanger });
            escape.Tasks.Add(new AllyEscapeTelegraphTask());

            var counter = tree.AddState("카운터", root.Id);
            counter.EnterConditions.Add(new HasTagCondition { Tag = ETag.Boss_CounterWindow, CheckTarget = true });
            counter.Tasks.Add(new AllyCounterTask());

            var orb = tree.AddState("구슬 기믹", root.Id);
            orb.EnterConditions.Add(new GimmickStateCondition { GimmickId = "G1-01", State = EGimmickState.Running, CheckTarget = true });
            orb.Tasks.Add(new AllyPickOrbTask { GimmickId = "G1-01" });

            var fight = tree.AddState("전투", root.Id);
            fight.EnterConditions.Add(new TargetDistanceCondition { Op = ECompareOp.LessOrEqual, Distance = 60.0f });
            fight.Tasks.Add(new AllyFightTask());

            var idle = tree.AddState("대기", root.Id);
            idle.Tasks.Add(new WaitTask { Duration = 0.5f });
            return tree;
        }

        private static StateTreeAsset create(string InDescription)
        {
            var tree = ScriptableObject.CreateInstance<StateTreeAsset>();
            tree.Description = InDescription;
            tree.Evaluators.Add(new CombatSenseEvaluator());
            return tree;
        }

        private static void addCommonBossStates(StateTreeAsset InTree, int InRootId)
        {
            var groggy = InTree.AddState("무력화", InRootId);
            groggy.Color = new Color(0.2f, 0.45f, 0.6f);
            groggy.EnterConditions.Add(new HasTagCondition { Tag = ETag.State_Groggy });
            groggy.Tasks.Add(new WaitWhileGroggyTask());

            var busy = InTree.AddState("패턴/기믹 진행 중", InRootId);
            busy.EnterConditions.Add(new HasTagCondition { Tag = ETag.Boss_PatternBusy });
            busy.EnterConditions.Add(new HasTagCondition { Tag = ETag.Boss_Channeling, Operand = EConditionOperand.Or });
            busy.Tasks.Add(new WaitTask { Duration = 0.1f });
        }

        private static void addAttackStates(StateTreeAsset InTree, int InRootId, string InCommonPhase)
        {
            var attack = InTree.AddState("패턴 공격", InRootId);
            attack.Color = new Color(0.55f, 0.2f, 0.2f);
            attack.Tasks.Add(new UseRandomPatternTask { CommonPhaseId = InCommonPhase });

            var chase = InTree.AddState("추적", InRootId);
            chase.Tasks.Add(new ChaseTargetTask { StopDistance = 3.0f, Timeout = 3.0f });

            var idle = InTree.AddState("대기", InRootId);
            idle.Tasks.Add(new WaitTask { Duration = 0.4f });
        }

        private static StateTreeState gimmickState(StateTreeAsset InTree, int InRootId, string InName, float InLine, EAIBlackboard InFlag)
        {
            var state = InTree.AddState(InName, InRootId);
            state.Color = new Color(0.5f, 0.4f, 0.15f);
            state.EnterConditions.Add(new HpLineCondition { Op = ECompareOp.LessOrEqual, Line = InLine });
            state.EnterConditions.Add(new FlagCondition { Key = InFlag, Invert = true });
            state.Tasks.Add(new SetFlagTask { Key = InFlag, CompleteImmediately = false });
            return state;
        }

        private static void phaseState(StateTreeAsset InTree, int InRootId, string InName, EAIBlackboard InFlag, string InFromPhase, string InToPhase)
        {
            var state = InTree.AddState(InName, InRootId);
            state.Color = new Color(0.25f, 0.4f, 0.25f);
            state.EnterConditions.Add(new FlagCondition { Key = InFlag });
            state.EnterConditions.Add(new PhaseCondition { PhaseId = InFromPhase });
            state.Tasks.Add(new SetPhaseTask { PhaseId = InToPhase });
        }
    }
}
