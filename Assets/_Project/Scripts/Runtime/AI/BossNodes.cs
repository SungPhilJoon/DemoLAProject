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
    [StateTreeNodeInfo("DemoLA/Boss", "PatternId 패턴 사용. 끝나면 Succeeded, 쓸 수 없으면 Failed")]
    public class UsePatternTask : StateTreeTask
    {
        public string PatternId = "G1-06";
        public bool WaitForEnd = true;
        [NonSerialized] private BossPatternAbility ability;

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            var boss = AIContext.Boss(InContext);
            if (boss == null || boss.TryActivatePattern(PatternId, AIContext.Target(InContext)) == false)
            {
                return ETaskStatus.Failed;
            }

            ability = boss.GetPattern(PatternId);
            return WaitForEnd == true ? ETaskStatus.Running : ETaskStatus.Succeeded;
        }

        public override ETaskStatus Tick(StateTreeContext InContext)
        {
            return ability == null || ability.IsActive == false ? ETaskStatus.Succeeded : ETaskStatus.Running;
        }

        public override string Describe()
        {
            return $"Pattern {PatternId}";
        }
    }

    /// <summary>
    /// 현재 페이즈(또는 PatternIds)에서 쿨다운이 끝났고 사거리 안인 패턴을 가중치로 골라 쓴다.
    /// 연결 패턴(LinkedPatternId)이나 실패 연쇄가 대기 중이면 그것을 먼저 쓴다.
    /// </summary>
    [Serializable]
    [StateTreeNodeInfo("DemoLA/Boss", "페이즈 패턴 중 가중치 랜덤 사용 (연결 패턴 우선)")]
    public class UseRandomPatternTask : StateTreeTask
    {
        [Tooltip("비우면 현재 페이즈 + '공통' 페이즈의 패턴")]
        public List<string> PatternIds = new List<string>();
        [Tooltip("현재 페이즈 이외에 함께 쓰는 공통 페이즈 (예: G1-공통)")]
        public string CommonPhaseId;
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

            var target = AIContext.Target(InContext);
            string queued = boss.ConsumeQueuedPattern();
            if (string.IsNullOrEmpty(queued) == false && boss.TryActivatePattern(queued, target) == true)
            {
                ability = boss.GetPattern(queued);
                return ETaskStatus.Running;
            }

            if (candidates == null)
            {
                candidates = new List<BossPatternAbility>();
            }

            collect(boss, InContext.Blackboard.GetFloat((int)EAIBlackboard.TargetDistance, 9999.0f));
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

                if (boss.TryActivatePattern(candidates[i].Definition.PatternId, target) == true)
                {
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

        private void collect(BossActor InBoss, float InDistance)
        {
            candidates.Clear();
            List<string> ids = PatternIds;
            if (ids == null || ids.Count == 0)
            {
                ids = new List<string>();
                var phase = InBoss.CurrentPhase;
                if (phase != null)
                {
                    ids.AddRange(phase.PatternIds);
                }

                var common = InBoss.Definition.GetPhase(CommonPhaseId);
                if (common != null)
                {
                    ids.AddRange(common.PatternIds);
                }
            }

            for (int i = 0; i < ids.Count; ++i)
            {
                var pattern = InBoss.GetPattern(ids[i]);
                if (pattern == null || pattern.Definition.RandomSelectable == false || candidates.Contains(pattern) == true)
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

                candidates.Add(pattern);
            }
        }

        public override string Describe()
        {
            return PatternIds != null && PatternIds.Count > 0 ? $"Random of {PatternIds.Count}" : $"Random (phase + {CommonPhaseId})";
        }
    }

    [Serializable]
    [StateTreeNodeInfo("DemoLA/Boss", "기믹 시작. WaitForCompletion 이면 결과에 따라 Succeeded/Failed")]
    public class StartGimmickTask : StateTreeTask
    {
        public string GimmickId = "G1-01";
        public bool WaitForCompletion = true;
        public bool ResetFirst = true;
        [NonSerialized] private BossGimmick gimmick;

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            var boss = AIContext.Boss(InContext);
            gimmick = boss != null ? boss.GetGimmick(GimmickId) : null;
            if (gimmick == null)
            {
                return ETaskStatus.Failed;
            }

            if (ResetFirst == true && gimmick.State != EGimmickState.Running)
            {
                gimmick.ResetGimmick();
            }

            if (boss.StartGimmick(GimmickId) == false)
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
            return $"Gimmick {GimmickId}{(WaitForCompletion == true ? string.Empty : " (async)")}";
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
}
