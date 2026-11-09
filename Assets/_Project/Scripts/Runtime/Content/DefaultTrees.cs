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

            addGate2ChargeRules(tree, root.Id);
            addGate2TargetingStates(tree, root.Id);
            addAttackStates(tree, root.Id, "G2-공통", true);
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

        /// <summary>
        /// 2관문 대상 선택 (SelectTargetTask · TargetRuleCondition). 기믹 상태 뒤 · 일반 패턴 공격 앞에 둔다.
        ///  - 레이저 견제: 위협 2위(탱커가 아닌 사람)에게 G2-16 추적 레이저 (40%)
        ///  - 약자 사냥: 유령 구간(G2-P3)에서 25% 확률로 체력 비율 최저인 사람을 8초 동안 어그로로 고정한 뒤 일반 패턴
        /// 확률 조건은 패턴을 고를 때마다 다시 굴린다 (패턴 사이 간격 PatternGap 2.5초).
        /// </summary>
        /// <summary>
        /// 2관문 돌진 규칙 (135줄 기준):
        ///  - 135줄 이후: 패턴이 끝날 때마다 재도약(G2-26, Cinemachine 시네마틱) — 재도약 자신 뒤에는 하지 않는다.
        ///  - 135줄 전 돌진(G2-11): 조우 직후 · 부위파괴 반격(G2-25) 착지 후 · 다른 패턴 3개 뒤. 가장 먼 대상에게.
        ///  - 135줄 이후 돌진: 무조건 카운터 가능한 돌진(G2-07). 같은 순서 규칙(다른 패턴 3개 뒤).
        /// 랜덤 패턴 공격은 돌진을 고르지 않는다 (ExcludeCharges). 부위파괴 반격은 ArmorStackGimmick 이 직접 쓴다.
        /// </summary>
        private static void addGate2ChargeRules(StateTreeAsset InTree, int InRootId)
        {
            var leap = InTree.AddState("재도약 (135줄 이후 · 패턴 끝날 때마다)", InRootId);
            leap.Color = new Color(0.45f, 0.3f, 0.65f);
            leap.EnterConditions.Add(new HpLineCondition { Op = ECompareOp.LessOrEqual, Line = 135.0f });
            leap.EnterConditions.Add(new LastPatternCondition { PatternId = "G2-26", Invert = true });
            leap.EnterConditions.Add(new PatternReadyCondition { PatternId = "G2-26" });
            leap.Tasks.Add(new UsePatternTask { PatternId = "G2-26" });

            var charge = InTree.AddState("돌진 (135줄 전: 조우 · 착지 후 · 패턴 3개마다)", InRootId);
            charge.Color = new Color(0.25f, 0.35f, 0.65f);
            charge.EnterConditions.Add(new HpLineCondition { Op = ECompareOp.Greater, Line = 135.0f });
            charge.EnterConditions.Add(new ChargeDueCondition { Patterns = 3 });
            charge.EnterConditions.Add(new PatternReadyCondition { PatternId = "G2-11" });
            charge.Tasks.Add(new SelectTargetTask { Selector = new FarthestSelector(), ApplyTo = ETargetApply.NextPattern });
            charge.Tasks.Add(new UsePatternTask { PatternId = "G2-11" });

            var counterCharge = InTree.AddState("카운터 돌진 (135줄 이후: 패턴 3개마다 G2-07)", InRootId);
            counterCharge.Color = new Color(0.25f, 0.45f, 0.75f);
            counterCharge.EnterConditions.Add(new HpLineCondition { Op = ECompareOp.LessOrEqual, Line = 135.0f });
            counterCharge.EnterConditions.Add(new ChargeDueCondition { Patterns = 3 });
            counterCharge.EnterConditions.Add(new PatternReadyCondition { PatternId = "G2-07" });
            counterCharge.Tasks.Add(new UsePatternTask { PatternId = "G2-07" });
        }

        private static void addGate2TargetingStates(StateTreeAsset InTree, int InRootId)
        {
            var laser = InTree.AddState("레이저 견제 (G2-16 → 위협 2위)", InRootId);
            laser.Color = new Color(0.25f, 0.35f, 0.65f);
            laser.EnterConditions.Add(new PatternReadyCondition { PatternId = "G2-16" });
            laser.EnterConditions.Add(new TargetRuleCondition { Selector = new HighestThreatSelector { Rank = 2 }, CompareDistance = false });
            laser.EnterConditions.Add(new RandomChanceCondition { Chance = 0.4f });
            laser.Tasks.Add(new SelectTargetTask { Selector = new HighestThreatSelector { Rank = 2 }, ApplyTo = ETargetApply.NextPattern });
            laser.Tasks.Add(new UsePatternTask { PatternId = "G2-16" });

            var hunt = InTree.AddState("약자 사냥 (G2-P3 · 체력 비율 최저 어그로 8초)", InRootId);
            hunt.Color = new Color(0.25f, 0.35f, 0.65f);
            hunt.EnterConditions.Add(new PhaseCondition { PhaseId = "G2-P3" });
            hunt.EnterConditions.Add(new RandomChanceCondition { Chance = 0.25f });
            hunt.Tasks.Add(new SelectTargetTask { Selector = new LowestHealthSelector(), ApplyTo = ETargetApply.Aggro, HoldSeconds = 8.0f, Announce = true });
            hunt.Tasks.Add(new UseRandomPatternTask { CommonPhaseId = "G2-공통" });
        }

        private static void addAttackStates(StateTreeAsset InTree, int InRootId, string InCommonPhase, bool InExcludeCharges = false)
        {
            var attack = InTree.AddState("패턴 공격", InRootId);
            attack.Color = new Color(0.55f, 0.2f, 0.2f);
            attack.Tasks.Add(new UseRandomPatternTask { CommonPhaseId = InCommonPhase, ExcludeCharges = InExcludeCharges });

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
