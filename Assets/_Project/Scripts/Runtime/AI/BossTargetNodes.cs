using System;
using DemoLA.Boss;
using DemoLA.Combat;
using DemoLA.Core;
using Framework.StateTree;
using UnityEngine;

namespace DemoLA.AI
{
    /// <summary>SelectTargetTask 가 고른 대상을 어디에 쓰는가.</summary>
    public enum ETargetApply
    {
        /// <summary>다음에 쓰는 패턴 1회의 대상 (블랙보드 PatternTarget). 패턴 규칙보다 우선한다.</summary>
        NextPattern = 0,
        /// <summary>보스의 어그로 대상 자체를 바꾼다 (추적 · 이후 모든 패턴의 기본 대상). HoldSeconds 동안 자동 재선정 안 함.</summary>
        Aggro = 1,
    }

    /// <summary>
    /// 대상 선택 규칙(PatternTargetSelector)으로 플레이어 1명을 골라 다음 패턴 대상 또는 보스 어그로로 지정한다.
    /// 예: [패턴 공격] 상태 Tasks = SelectTargetTask(Farthest) → UsePatternTask(G2-11 돌진).
    /// 도발 중에는 고른 대상과 상관없이 도발한 대상을 노린다.
    /// </summary>
    [Serializable]
    [StateTreeNodeInfo("DemoLA/Boss", "규칙으로 대상을 골라 다음 패턴 대상 / 어그로로 지정. 못 고르면 Failed (FailIfNone)")]
    public class SelectTargetTask : StateTreeTask
    {
        [SerializeReference, SubclassPicker] public PatternTargetSelector Selector = new FarthestSelector();
        public ETargetApply ApplyTo = ETargetApply.NextPattern;
        [Tooltip("ApplyTo = Aggro: 이 시간(초) 동안 어그로 자동 재선정을 하지 않는다. 0 = 보스 RetargetInterval")]
        public float HoldSeconds;
        [Tooltip("고를 대상이 없으면 Failed (끄면 아무것도 바꾸지 않고 Succeeded)")]
        public bool FailIfNone = true;
        [Tooltip("고른 대상 이름을 레이드 메시지로 띄운다 (디버그 · 기믹 안내)")]
        public bool Announce;
        [Tooltip("false 면 Running 으로 남아 같은 상태의 다른 태스크(UsePatternTask 등)가 끝날 때까지 기다린다. " +
                 "StateTree 는 상태 안의 태스크 하나라도 끝나면 상태를 끝내므로, 패턴 태스크와 함께 둘 때는 false 로 둔다")]
        public bool CompleteImmediately;

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            var boss = AIContext.Boss(InContext);
            var chosen = boss != null && Selector != null ? Selector.Select(boss, boss.AlivePlayers()) : null;
            if (chosen == null || chosen.IsDead == true)
            {
                return FailIfNone == true ? ETaskStatus.Failed : done();
            }

            switch (ApplyTo)
            {
                case ETargetApply.Aggro:
                    boss.ForceAggro(chosen, HoldSeconds);
                    InContext.Blackboard.SetObject((int)EAIBlackboard.Target, boss.AggroTarget);
                    break;
                default:
                    InContext.Blackboard.SetObject((int)EAIBlackboard.PatternTarget, chosen);
                    break;
            }

            if (Announce == true)
            {
                DemoLA.World.Raid.Message($"{chosen.GetName()} 지정!", 1.5f, false);
            }

            return done();
        }

        private ETaskStatus done()
        {
            return CompleteImmediately == true ? ETaskStatus.Succeeded : ETaskStatus.Running;
        }

        public override string Describe()
        {
            string rule = Selector != null ? Selector.Describe() : "(규칙 없음)";
            return ApplyTo == ETargetApply.Aggro ? $"Aggro ← {rule}" : $"Next Pattern ← {rule}";
        }
    }

    /// <summary>
    /// 규칙으로 고른 대상이 있는가 (+ 그 대상까지 거리 비교). 상태 진입 조건 · 전환 조건에 쓴다.
    /// 예: "가장 먼 플레이어가 10m 이상" 이면 돌진 상태로, "정면 60° 안에 누가 있으면" 부채꼴 상태로.
    /// </summary>
    [Serializable]
    [StateTreeNodeInfo("DemoLA/Boss", "규칙으로 고른 대상이 있는가 (+ 거리 비교)")]
    public class TargetRuleCondition : StateTreeCondition
    {
        [SerializeReference, SubclassPicker] public PatternTargetSelector Selector = new FarthestSelector();
        public bool CompareDistance = true;
        public ECompareOp Op = ECompareOp.GreaterOrEqual;
        [Tooltip("보스 중심 ~ 대상 몸 가장자리 평면 거리 (m)")]
        public float Distance = 10.0f;

        protected override bool Test(StateTreeContext InContext)
        {
            var boss = AIContext.Boss(InContext);
            var chosen = boss != null && Selector != null ? Selector.Select(boss, boss.AlivePlayers()) : null;
            if (chosen == null || chosen.IsDead == true)
            {
                return false;
            }

            if (CompareDistance == false)
            {
                return true;
            }

            float distance = Vector3.Distance(HitGeometry.Flat(chosen.Position), HitGeometry.Flat(boss.Position)) - chosen.Radius;
            return CompareUtil.Compare(distance, Op, Distance);
        }

        public override string Describe()
        {
            string rule = Selector != null ? Selector.Describe() : "(규칙 없음)";
            return CompareDistance == true ? $"{rule} {CompareUtil.Symbol(Op)} {Distance}m" : $"Has {rule}";
        }
    }

    /// <summary>
    /// 돌진 차례인가: 마지막 돌진 뒤 다른 패턴이 Patterns 개 이상 끝났다 (BossActor.PatternsSinceCharge).
    /// 전투 시작 직후(조우)와 RequestCharge 뒤(부위파괴 반격 착지 후)에는 바로 참.
    /// </summary>
    [Serializable]
    [StateTreeNodeInfo("DemoLA/Boss", "돌진 차례인가 (마지막 돌진 뒤 다른 패턴 N개 · 조우 직후 · 반격 착지 후)")]
    public class ChargeDueCondition : StateTreeCondition
    {
        public int Patterns = 3;

        protected override bool Test(StateTreeContext InContext)
        {
            var boss = AIContext.Boss(InContext);
            return boss != null && boss.PatternsSinceCharge >= Patterns;
        }

        public override string Describe()
        {
            return $"Charge due (≥{Patterns} patterns)";
        }
    }

    /// <summary>마지막으로 끝난 패턴이 PatternId 인가 (Invert 로 "아닌가"). 아직 끝난 패턴이 없으면 false (Invert 면 RequireAnyPattern 에 따라).</summary>
    [Serializable]
    [StateTreeNodeInfo("DemoLA/Boss", "마지막으로 끝난 패턴 비교 (재도약처럼 패턴이 끝날 때마다 끼워 넣기)")]
    public class LastPatternCondition : StateTreeCondition
    {
        [DemoLA.Boss.PatternId] public string PatternId = "G2-26";
        [Tooltip("끝난 패턴이 하나도 없으면 false (전투 시작 직후 제외)")]
        public bool RequireAnyPattern = true;

        protected override bool Test(StateTreeContext InContext)
        {
            var boss = AIContext.Boss(InContext);
            if (boss == null || string.IsNullOrEmpty(boss.LastEndedPatternId) == true)
            {
                // Invert 가 결과를 뒤집으므로, "끝난 패턴 없음" 을 항상 거짓으로 만들려면 Invert 를 미리 반영한다.
                return RequireAnyPattern == true ? Invert : false;
            }

            return boss.LastEndedPatternId == PatternId;
        }

        public override string Describe()
        {
            return $"Last pattern {(Invert ? "!=" : "==")} {PatternId}";
        }
    }
}
