using System;
using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Boss.Telegraph;
using DemoLA.Combat;
using DemoLA.Core;
using Framework.StateTree;
using UnityEngine;

namespace DemoLA.AI
{
    /// <summary>매 틱 대상/거리/체력을 블랙보드에 쓴다. 보스는 어그로 대상, 아군은 가장 가까운 적.</summary>
    [Serializable]
    [StateTreeNodeInfo("DemoLA", "대상 · 거리 · 체력 · 체력 줄 · 위험 여부를 블랙보드에 기록")]
    public class CombatSenseEvaluator : StateTreeEvaluator
    {
        public float SearchRadius = 60.0f;

        public override void Tick(StateTreeContext InContext)
        {
            var actor = AIContext.Actor(InContext);
            if (actor == null || actor.Stats == null)
            {
                return;
            }

            var board = InContext.Blackboard;
            var boss = actor as BossActor;
            var target = boss != null ? boss.AggroTarget : CombatRegistry.FindNearestHostile(actor, SearchRadius);
            board.SetObject((int)EAIBlackboard.Target, target);
            board.SetFloat((int)EAIBlackboard.TargetDistance, target != null ? Vector3.Distance(HitGeometry.Flat(target.Position), HitGeometry.Flat(actor.Position)) - target.Radius : 9999.0f);
            board.SetFloat((int)EAIBlackboard.HealthPercent, actor.Stats.HealthRatio * 100.0f);
            board.SetFloat((int)EAIBlackboard.HpLine, boss != null ? boss.HpLinesExact : 0.0f);
            board.SetBool((int)EAIBlackboard.IsBusy, boss != null ? boss.IsBusy : actor.HasTag((int)ETag.State_Casting));
            board.SetBool((int)EAIBlackboard.IsGroggy, actor.HasTag((int)ETag.State_Groggy));
            board.SetBool((int)EAIBlackboard.InDanger, AIUtil.IsInTelegraph(actor.Position, actor.Radius));
        }

        public override string Describe()
        {
            return "Combat Sense";
        }
    }

    public static class AIUtil
    {
        public static bool IsInTelegraph(Vector3 InPosition, float InRadius)
        {
            var views = TelegraphView.ActiveViews;
            for (int i = 0; i < views.Count; ++i)
            {
                if (views[i] != null && views[i].gameObject.activeSelf == true && views[i].Contains(InPosition, InRadius) == true)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>장판 밖이면서 InPreferred 에 가까운 지점 (8방향 × 3거리 샘플).</summary>
        public static Vector3 FindSafePoint(Vector3 InFrom, float InRadius, Vector3 InPreferred)
        {
            var best = InFrom;
            float bestScore = float.MaxValue;
            for (int ring = 1; ring <= 3; ++ring)
            {
                for (int i = 0; i < 8; ++i)
                {
                    float angle = i * 45.0f * Mathf.Deg2Rad;
                    var point = InFrom + new Vector3(Mathf.Sin(angle), 0.0f, Mathf.Cos(angle)) * (ring * 3.5f);
                    if (IsInTelegraph(point, InRadius) == true)
                    {
                        continue;
                    }

                    if (DemoLA.World.ArenaFloor.Current != null && DemoLA.World.ArenaFloor.Current.IsSolid(point) == false)
                    {
                        continue;
                    }

                    float score = (point - InFrom).sqrMagnitude + (point - InPreferred).sqrMagnitude * 0.2f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = point;
                    }
                }
            }

            return best;
        }
    }

    // ====================================================================== conditions

    [Serializable]
    [StateTreeNodeInfo("DemoLA", "보스 체력 줄 비교 (소수점 포함)")]
    public class HpLineCondition : StateTreeCondition
    {
        public ECompareOp Op = ECompareOp.LessOrEqual;
        public float Line = 30.0f;

        protected override bool Test(StateTreeContext InContext)
        {
            return CompareUtil.Compare(InContext.Blackboard.GetFloat((int)EAIBlackboard.HpLine), Op, Line);
        }

        public override string Describe()
        {
            return $"HpLine {CompareUtil.Symbol(Op)} {Line}";
        }
    }

    [Serializable]
    [StateTreeNodeInfo("DemoLA", "체력 % 비교")]
    public class HealthPercentCondition : StateTreeCondition
    {
        public ECompareOp Op = ECompareOp.LessOrEqual;
        public float Percent = 50.0f;

        protected override bool Test(StateTreeContext InContext)
        {
            return CompareUtil.Compare(InContext.Blackboard.GetFloat((int)EAIBlackboard.HealthPercent), Op, Percent);
        }

        public override string Describe()
        {
            return $"HP% {CompareUtil.Symbol(Op)} {Percent}";
        }
    }

    [Serializable]
    [StateTreeNodeInfo("DemoLA", "대상과의 거리 비교")]
    public class TargetDistanceCondition : StateTreeCondition
    {
        public ECompareOp Op = ECompareOp.LessOrEqual;
        public float Distance = 4.0f;

        protected override bool Test(StateTreeContext InContext)
        {
            return CompareUtil.Compare(InContext.Blackboard.GetFloat((int)EAIBlackboard.TargetDistance, 9999.0f), Op, Distance);
        }

        public override string Describe()
        {
            return $"Distance {CompareUtil.Symbol(Op)} {Distance}";
        }
    }

    [Serializable]
    [StateTreeNodeInfo("DemoLA", "자신(또는 대상)이 태그를 가지고 있는가")]
    public class HasTagCondition : StateTreeCondition
    {
        public ETag Tag = ETag.State_Groggy;
        public bool CheckTarget;

        protected override bool Test(StateTreeContext InContext)
        {
            var actor = CheckTarget == true ? AIContext.Target(InContext) : AIContext.Actor(InContext);
            return actor != null && actor.HasTag((int)Tag) == true;
        }

        public override string Describe()
        {
            return $"{(CheckTarget == true ? "Target" : "Self")} has {Tag}";
        }
    }

    [Serializable]
    [StateTreeNodeInfo("DemoLA", "현재 보스 페이즈가 PhaseId 인가")]
    public class PhaseCondition : StateTreeCondition
    {
        public string PhaseId = "G1-P0";

        protected override bool Test(StateTreeContext InContext)
        {
            var boss = AIContext.Boss(InContext);
            return boss != null && boss.CurrentPhaseId == PhaseId;
        }

        public override string Describe()
        {
            return $"Phase == {PhaseId}";
        }
    }

    [Serializable]
    [StateTreeNodeInfo("DemoLA", "패턴 쿨다운이 끝났는가")]
    public class PatternReadyCondition : StateTreeCondition
    {
        public string PatternId = "G1-06";

        protected override bool Test(StateTreeContext InContext)
        {
            var boss = AIContext.Boss(InContext);
            return boss != null && boss.IsPatternReady(PatternId) == true;
        }

        public override string Describe()
        {
            return $"Ready {PatternId}";
        }
    }

    [Serializable]
    [StateTreeNodeInfo("DemoLA", "기믹 상태 비교")]
    public class GimmickStateCondition : StateTreeCondition
    {
        public string GimmickId = "G1-01";
        public EGimmickState State = EGimmickState.Running;
        [Tooltip("아군 AI 용: 자신 대신 대상(보스)의 기믹을 본다")]
        public bool CheckTarget;

        protected override bool Test(StateTreeContext InContext)
        {
            var boss = CheckTarget == true ? AIContext.Target(InContext) as BossActor : AIContext.Boss(InContext);
            var gimmick = boss != null ? boss.GetGimmick(GimmickId) : null;
            return gimmick != null && gimmick.State == State;
        }

        public override string Describe()
        {
            return $"Gimmick {GimmickId} == {State}";
        }
    }

    [Serializable]
    [StateTreeNodeInfo("DemoLA", "블랙보드 플래그 (1회성 기믹 진입 표시)")]
    public class FlagCondition : StateTreeCondition
    {
        public EAIBlackboard Key = EAIBlackboard.Flag1;

        protected override bool Test(StateTreeContext InContext)
        {
            return InContext.Blackboard.GetBool((int)Key);
        }

        public override string Describe()
        {
            return $"{(Invert == true ? "!" : string.Empty)}{Key}";
        }
    }

    [Serializable]
    [StateTreeNodeInfo("DemoLA", "확률")]
    public class RandomChanceCondition : StateTreeCondition
    {
        [Range(0.0f, 1.0f)] public float Chance = 0.5f;

        protected override bool Test(StateTreeContext InContext)
        {
            return GameRandom.Value < Chance;
        }

        public override string Describe()
        {
            return $"Chance {Chance * 100.0f:0}%";
        }
    }

    // ====================================================================== common tasks

    [Serializable]
    [StateTreeNodeInfo("DemoLA", "블랙보드 플래그 설정 후 즉시 성공")]
    public class SetFlagTask : StateTreeTask
    {
        public EAIBlackboard Key = EAIBlackboard.Flag1;
        public bool Value = true;
        public bool CompleteImmediately;

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            InContext.Blackboard.SetBool((int)Key, Value);
            return CompleteImmediately == true ? ETaskStatus.Succeeded : ETaskStatus.Running;
        }

        public override string Describe()
        {
            return $"Set {Key} = {Value}";
        }
    }

    [Serializable]
    [StateTreeNodeInfo("DemoLA", "상태가 활성인 동안 태그 부여 (전환 중 무적 등)")]
    public class AddTagWhileActiveTask : StateTreeTask
    {
        public ETag Tag = ETag.State_Invulnerable;

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            var actor = AIContext.Actor(InContext);
            if (actor != null)
            {
                actor.AddTag((int)Tag);
            }

            return ETaskStatus.Running;
        }

        public override void ExitState(StateTreeContext InContext)
        {
            var actor = AIContext.Actor(InContext);
            if (actor != null)
            {
                actor.RemoveTag((int)Tag);
            }
        }

        public override string Describe()
        {
            return $"Tag {Tag}";
        }
    }

    [Serializable]
    [StateTreeNodeInfo("DemoLA", "대상에게 다가간다. StopDistance 안이면 성공, Timeout 이면 실패")]
    public class ChaseTargetTask : StateTreeTask
    {
        public float StopDistance = 3.0f;
        public float Timeout = 4.0f;
        [NonSerialized] private float elapsed;

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            elapsed = 0.0f;
            return Tick(InContext);
        }

        public override ETaskStatus Tick(StateTreeContext InContext)
        {
            var actor = AIContext.Actor(InContext);
            var target = AIContext.Target(InContext);
            if (actor == null || target == null || actor.Motor == null)
            {
                return ETaskStatus.Failed;
            }

            elapsed += InContext.DeltaTime;
            float distance = InContext.Blackboard.GetFloat((int)EAIBlackboard.TargetDistance, 9999.0f);
            if (distance <= StopDistance)
            {
                actor.Motor.Stop();
                actor.FaceTowards(target.Position);
                return ETaskStatus.Succeeded;
            }

            if (elapsed >= Timeout)
            {
                return ETaskStatus.Failed;
            }

            actor.Motor.MoveTo(target.Position);
            return ETaskStatus.Running;
        }

        public override void ExitState(StateTreeContext InContext)
        {
            var actor = AIContext.Actor(InContext);
            if (actor != null && actor.Motor != null)
            {
                actor.Motor.Stop();
            }
        }

        public override string Describe()
        {
            return $"Chase ≤{StopDistance}m";
        }
    }
}
