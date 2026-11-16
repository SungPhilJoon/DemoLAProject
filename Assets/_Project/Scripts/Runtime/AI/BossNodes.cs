using System;
using System.Collections.Generic;
using DemoLA.Boss;
using DemoLA.Combat;
using DemoLA.Core;
using Framework.StateTree;
using UnityEngine;

namespace DemoLA.AI
{
    /// <summary>지정 패턴을 쓰고 끝날 때까지 기다린다.</summary>
    [Serializable]
    [StateTreeNodeInfo("DemoLA/Boss", "PatternTag 패턴 사용. 끝나면 Succeeded, 쓸 수 없으면 Failed")]
    public class UsePatternTask : StateTreeTask
    {
        [PatternTag] public ETag PatternTag = ETag.Pattern_G1_BlinkDoubleClaw;
        public bool WaitForEnd = true;
        [NonSerialized] private BossPatternAbility ability;

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            var boss = AIContext.Boss(InContext);
            if (boss == null || boss.TryActivatePattern(PatternTag, AIContext.PatternTarget(InContext)) == false)
            {
                return ETaskStatus.Failed;
            }

            AIContext.ClearPatternTarget(InContext);
            ability = boss.GetPattern(PatternTag);
            return WaitForEnd == true ? ETaskStatus.Running : ETaskStatus.Succeeded;
        }

        public override ETaskStatus Tick(StateTreeContext InContext)
        {
            return ability == null || ability.IsActive == false ? ETaskStatus.Succeeded : ETaskStatus.Running;
        }

        public override string Describe()
        {
            return $"Pattern {GameTags.ToTagString(PatternTag)}";
        }
    }

    /// <summary>
    /// 현재 페이즈(또는 PatternTags)에서 쿨다운이 끝났고 사거리 안인 패턴을 가중치로 골라 쓴다.
    /// 연결 패턴(LinkedPatternTag)이나 실패 연쇄가 대기 중이면 그것을 먼저 쓴다.
    /// </summary>
    [Serializable]
    [StateTreeNodeInfo("DemoLA/Boss", "페이즈 패턴 중 가중치 랜덤 사용 (연결 패턴 우선)")]
    public class UseRandomPatternTask : StateTreeTask
    {
        [Tooltip("비우면 현재 페이즈 + '공통' 페이즈의 패턴")]
        [PatternTag] public List<ETag> PatternTags = new List<ETag>();
        [Tooltip("현재 페이즈 이외에 함께 쓰는 공통 페이즈 (예: G1-공통)")]
        public string CommonPhaseId;
        [Tooltip("돌진 패턴은 랜덤으로 고르지 않는다 (2관문: 돌진은 ChargeDueCondition 상태가 정해진 순서로만 쓴다)")]
        public bool ExcludeCharges;
        [NonSerialized] private BossPatternAbility ability;
        [NonSerialized] private List<BossPatternAbility> candidates;

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            ability = null;
            var boss = AIContext.Boss(InContext);
            if (boss == null)
            {
                return ETaskStatus.Failed;
            }

            // 지정 대상(SelectTargetTask)이 있으면 그 대상 기준으로 사거리를 본다. 없으면 null 로 넘겨 패턴 규칙 · 어그로에 맡긴다.
            var target = AIContext.PatternTarget(InContext);
            ETag queued;
            if (boss.TryActivateQueuedPattern(target, out queued) == true)
            {
                AIContext.ClearPatternTarget(InContext);
                ability = boss.GetPattern(queued);
                return ETaskStatus.Running;
            }

            if (queued != ETag.None)
            {
                // 이어질 패턴이 있는데 지금 못 쓴다 → 다른 패턴으로 새치기하지 않고 다음 판단을 기다린다.
                return ETaskStatus.Failed;
            }

            if (candidates == null)
            {
                candidates = new List<BossPatternAbility>();
            }

            float distance = RandomPatternCandidates.Distance(InContext, boss, target);
            RandomPatternCandidates.Collect(boss, PatternTags, CommonPhaseId, ExcludeCharges, distance, candidates);
            float total = 0.0f;
            for (int i = 0; i < candidates.Count; ++i)
            {
                total += Mathf.Max(0.01f, candidates[i].Definition.Weight);
            }

            float roll = GameRandom.Value * total;
            for (int i = 0; i < candidates.Count; ++i)
            {
                roll -= Mathf.Max(0.01f, candidates[i].Definition.Weight);
                if (roll > 0.0f && i < candidates.Count - 1)
                {
                    continue;
                }

                if (boss.TryActivatePattern(candidates[i].Definition.PatternTag, target) == true)
                {
                    AIContext.ClearPatternTarget(InContext);
                    ability = candidates[i];
                    return ETaskStatus.Running;
                }
            }

            return ETaskStatus.Failed;
        }

        public override ETaskStatus Tick(StateTreeContext InContext)
        {
            return ability == null || ability.IsActive == false ? ETaskStatus.Succeeded : ETaskStatus.Running;
        }

        public override string Describe()
        {
            return PatternTags != null && PatternTags.Count > 0 ? $"Random of {PatternTags.Count}" : $"Random (phase + {CommonPhaseId})";
        }
    }

    /// <summary>UseRandomPatternTask 와 HasRandomPatternCondition 이 같은 규칙으로 후보를 고른다.</summary>
    public static class RandomPatternCandidates
    {
        public static float Distance(StateTreeContext InContext, BossActor InBoss, CombatActor InTarget)
        {
            return InTarget != null
                ? Vector3.Distance(HitGeometry.Flat(InTarget.Position), HitGeometry.Flat(InBoss.Position)) - InTarget.Radius
                : InContext.Blackboard.GetFloat((int)EAIBlackboard.TargetDistance, 9999.0f);
        }

        /// <summary>쿨다운이 끝났고 사거리(MinRange~MaxRange) 안인 랜덤 선택 가능 패턴.</summary>
        [ThreadStatic] private static List<ETag> phaseTags;

        public static void Collect(BossActor InBoss, List<ETag> InPatternTags, string InCommonPhaseId, bool InExcludeCharges, float InDistance, List<BossPatternAbility> OutCandidates)
        {
            OutCandidates.Clear();
            List<ETag> ids = InPatternTags;
            if (ids == null || ids.Count == 0)
            {
                ids = phaseTags ??= new List<ETag>();
                ids.Clear();
                var phase = InBoss.CurrentPhase;
                if (phase != null)
                {
                    ids.AddRange(phase.PatternTags);
                }

                var common = InBoss.Definition.GetPhase(InCommonPhaseId);
                if (common != null)
                {
                    ids.AddRange(common.PatternTags);
                }
            }

            for (int i = 0; i < ids.Count; ++i)
            {
                var pattern = InBoss.GetPattern(ids[i]);
                if (pattern == null || pattern.Definition.RandomSelectable == false || OutCandidates.Contains(pattern) == true)
                {
                    continue;
                }

                if (InExcludeCharges == true && pattern.Definition.IsCharge == true)
                {
                    continue;
                }

                if (InDistance < pattern.Definition.MinRange || InDistance > pattern.Definition.MaxRange)
                {
                    continue;
                }

                if (InBoss.IsPatternReady(ids[i]) == false)
                {
                    continue;
                }

                OutCandidates.Add(pattern);
            }
        }
    }

    /// <summary>
    /// 지금 위치에서 쓸 수 있는 랜덤 패턴이 있는가 (UseRandomPatternTask 와 같은 규칙).
    /// "패턴 공격" 상태에 붙여, 사거리 안 패턴이 없으면 다음 상태("추적")로 넘어가 대상에게 다가가게 한다.
    /// (조건이 없으면 실패한 패턴 공격 상태만 매 프레임 다시 골라 추적 상태에 영영 닿지 않는다.)
    /// </summary>
    [Serializable]
    [StateTreeNodeInfo("DemoLA/Boss", "쓸 수 있는(쿨다운 · 사거리) 랜덤 패턴이 있는가")]
    public class HasRandomPatternCondition : StateTreeCondition
    {
        [Tooltip("비우면 현재 페이즈 + CommonPhaseId 페이즈의 패턴")]
        [PatternTag] public List<ETag> PatternTags = new List<ETag>();
        public string CommonPhaseId;
        public bool ExcludeCharges;
        [NonSerialized] private List<BossPatternAbility> buffer;

        protected override bool Test(StateTreeContext InContext)
        {
            var boss = AIContext.Boss(InContext);
            if (boss == null)
            {
                return false;
            }

            buffer ??= new List<BossPatternAbility>();
            float distance = RandomPatternCandidates.Distance(InContext, boss, AIContext.PatternTarget(InContext));
            RandomPatternCandidates.Collect(boss, PatternTags, CommonPhaseId, ExcludeCharges, distance, buffer);
            return buffer.Count > 0;
        }

        public override string Describe()
        {
            return PatternTags != null && PatternTags.Count > 0 ? $"Has random of {PatternTags.Count}" : $"Has random (phase + {CommonPhaseId})";
        }
    }

    [Serializable]
    [StateTreeNodeInfo("DemoLA/Boss", "기믹 시작. WaitForCompletion 이면 결과에 따라 Succeeded/Failed")]
    public class StartGimmickTask : StateTreeTask
    {
        [Tooltip("기믹 = 그 기믹 패턴의 태그")]
        [PatternTag] public ETag GimmickTag = ETag.Pattern_G1_OrbStagger;
        public bool WaitForCompletion = true;
        public bool ResetFirst = true;
        [NonSerialized] private BossGimmick gimmick;

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            var boss = AIContext.Boss(InContext);
            gimmick = boss != null ? boss.GetGimmick(GimmickTag) : null;
            if (gimmick == null)
            {
                return ETaskStatus.Failed;
            }

            if (ResetFirst == true && gimmick.State != EGimmickState.Running)
            {
                gimmick.ResetGimmick();
            }

            if (boss.StartGimmick(GimmickTag) == false)
            {
                return ETaskStatus.Failed;
            }

            return WaitForCompletion == true ? ETaskStatus.Running : ETaskStatus.Succeeded;
        }

        public override ETaskStatus Tick(StateTreeContext InContext)
        {
            if (gimmick == null)
            {
                return ETaskStatus.Failed;
            }

            switch (gimmick.State)
            {
                case EGimmickState.Succeeded:
                    return ETaskStatus.Succeeded;
                case EGimmickState.Failed:
                    return ETaskStatus.Failed;
            }

            return ETaskStatus.Running;
        }

        public override string Describe()
        {
            return $"Gimmick {GameTags.ToTagString(GimmickTag)}{(WaitForCompletion == true ? string.Empty : " (async)")}";
        }
    }

    [Serializable]
    [StateTreeNodeInfo("DemoLA/Boss", "보스 페이즈 변경 후 즉시 성공")]
    public class SetPhaseTask : StateTreeTask
    {
        public string PhaseId = "G1-P1";
        [Tooltip("false 면 Running 으로 남아 같은 상태의 다른 태스크가 끝날 때까지 기다린다")]
        public bool CompleteImmediately = true;

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            var boss = AIContext.Boss(InContext);
            if (boss == null)
            {
                return ETaskStatus.Failed;
            }

            boss.SetPhase(PhaseId);
            return CompleteImmediately == true ? ETaskStatus.Succeeded : ETaskStatus.Running;
        }

        public override string Describe()
        {
            return $"Phase → {PhaseId}";
        }
    }

    [Serializable]
    [StateTreeNodeInfo("DemoLA/Boss", "무력화(그로기) 동안 대기")]
    public class WaitWhileGroggyTask : StateTreeTask
    {
        public override ETaskStatus Tick(StateTreeContext InContext)
        {
            var actor = AIContext.Actor(InContext);
            return actor != null && actor.HasTag((int)ETag.State_Groggy) == true ? ETaskStatus.Running : ETaskStatus.Succeeded;
        }

        public override string Describe()
        {
            return "Wait Groggy";
        }
    }

    /// <summary>
    /// 이어질 패턴이 대기 중이고 지금 쓸 수 있는가 (FollowUpPatternTags · LinkedPatternTag).
    /// 패턴 간격(PatternGap)이 지났고 그로기가 아닐 때만 참 — 그로기가 풀린 뒤 이어서 쓴다.
    /// </summary>
    [Serializable]
    [StateTreeNodeInfo("DemoLA/Boss", "이어질 패턴(연결 · 후속)이 대기 중이고 지금 쓸 수 있는가")]
    public class HasQueuedPatternCondition : StateTreeCondition
    {
        protected override bool Test(StateTreeContext InContext)
        {
            var boss = AIContext.Boss(InContext);
            ETag next = boss != null ? boss.PeekQueuedPattern() : ETag.None;
            return next != ETag.None && boss.IsPatternGapElapsed == true && boss.HasTag((int)ETag.State_Groggy) == false;
        }

        public override string Describe()
        {
            return "Has queued pattern";
        }
    }

    /// <summary>대기 중인 다음 패턴을 쓰고 끝날 때까지 기다린다. 못 쓰면 Failed (대기열은 그대로 — 다음에 다시 시도).</summary>
    [Serializable]
    [StateTreeNodeInfo("DemoLA/Boss", "대기 중인 다음 패턴(연결 · 후속) 사용")]
    public class UseQueuedPatternTask : StateTreeTask
    {
        [NonSerialized] private BossPatternAbility ability;

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            ability = null;
            var boss = AIContext.Boss(InContext);
            ETag next;
            if (boss == null || boss.TryActivateQueuedPattern(AIContext.PatternTarget(InContext), out next) == false)
            {
                return ETaskStatus.Failed;
            }

            AIContext.ClearPatternTarget(InContext);
            ability = boss.GetPattern(next);
            return ETaskStatus.Running;
        }

        public override ETaskStatus Tick(StateTreeContext InContext)
        {
            return ability == null || ability.IsActive == false ? ETaskStatus.Succeeded : ETaskStatus.Running;
        }

        public override string Describe()
        {
            return "Use queued pattern";
        }
    }
}
