using System;
using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Character;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Skills;
using Framework.StateTree;
using UnityEngine;

namespace DemoLA.AI
{
    /// <summary>
    /// 아군 AI 노드. 각 태스크는 짧게 행동하고 끝나서 트리가 루트부터 다시 고르게 한다
    /// (위험 회피 &gt; 카운터 &gt; 기믹 &gt; 공격 우선순위를 상태 순서로 표현).
    /// NetCode 적용 전 "Custom AI 로 파티 플레이" (프로젝트 초기 설정.md).
    /// </summary>
    [Serializable]
    [StateTreeNodeInfo("DemoLA/Ally", "장판 안이면 안전 지점으로 이동(급하면 회피기)")]
    public class AllyEscapeTelegraphTask : StateTreeTask
    {
        public bool UseDodge = true;
        public float MaxTime = 1.0f;
        [NonSerialized] private float elapsed;

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            elapsed = 0.0f;
            var party = AIContext.Party(InContext);
            if (party == null)
            {
                return ETaskStatus.Failed;
            }

            var target = AIContext.Target(InContext);
            var preferred = target != null ? target.Position : party.Position;
            var safe = AIUtil.FindSafePoint(party.Position, party.Radius, preferred);
            if (UseDodge == true && party.TryDodge(safe) == true)
            {
                return ETaskStatus.Succeeded;
            }

            if (party.Motor != null)
            {
                party.Motor.MoveTo(safe);
            }

            return ETaskStatus.Running;
        }

        public override ETaskStatus Tick(StateTreeContext InContext)
        {
            elapsed += InContext.DeltaTime;
            var party = AIContext.Party(InContext);
            if (party == null || AIUtil.IsInTelegraph(party.Position, party.Radius) == false || elapsed >= MaxTime)
            {
                return ETaskStatus.Succeeded;
            }

            return ETaskStatus.Running;
        }

        public override string Describe()
        {
            return "Escape Telegraph";
        }
    }

    [Serializable]
    [StateTreeNodeInfo("DemoLA/Ally", "대상이 카운터 가능 상태면 카운터 스킬 사용")]
    public class AllyCounterTask : StateTreeTask
    {
        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            var party = AIContext.Party(InContext);
            var target = AIContext.Target(InContext);
            if (party == null || target == null)
            {
                return ETaskStatus.Failed;
            }

            var skills = party.EquippedSkills;
            for (int i = 0; i < skills.Count; ++i)
            {
                var skill = skills[i];
                if (skill.IsCounterSkill == false || party.IsSkillReady(skill) == false)
                {
                    continue;
                }

                float distance = InContext.Blackboard.GetFloat((int)EAIBlackboard.TargetDistance, 9999.0f);
                if (distance > skill.Definition.EstimatedRange)
                {
                    if (party.Motor != null)
                    {
                        party.Motor.MoveTo(target.Position);
                    }

                    return ETaskStatus.Running;
                }

                party.FaceTowards(target.Position);
                return party.TryUseSlotSkill(skill, target.Position) == true ? ETaskStatus.Succeeded : ETaskStatus.Failed;
            }

            return ETaskStatus.Failed;
        }

        public override ETaskStatus Tick(StateTreeContext InContext)
        {
            var target = AIContext.Target(InContext);
            if (target == null || target.HasTag((int)ETag.Boss_CounterWindow) == false)
            {
                return ETaskStatus.Succeeded;
            }

            return EnterState(InContext);
        }

        public override string Describe()
        {
            return "Counter";
        }
    }

    [Serializable]
    [StateTreeNodeInfo("DemoLA/Ally", "구슬 기믹 중 직전과 다른 색의 가장 가까운 구슬로 이동")]
    public class AllyPickOrbTask : StateTreeTask
    {
        public string GimmickId = "G1-01";
        public float MaxTime = 1.5f;
        [NonSerialized] private float elapsed;

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            elapsed = 0.0f;
            return Tick(InContext);
        }

        public override ETaskStatus Tick(StateTreeContext InContext)
        {
            elapsed += InContext.DeltaTime;
            var party = AIContext.Party(InContext);
            var boss = AIContext.Target(InContext) as BossActor;
            var gimmick = boss != null ? boss.GetGimmick(GimmickId) as OrbAlternationGimmick : null;
            if (party == null || gimmick == null || gimmick.State != EGimmickState.Running || gimmick.Rule == null || elapsed >= MaxTime)
            {
                return ETaskStatus.Succeeded;
            }

            var last = gimmick.Rule.LastColor;
            RaidPickup best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < gimmick.Orbs.Count; ++i)
            {
                var orb = gimmick.Orbs[i];
                if (orb == null || (last.HasValue == true && (EOrbColor)orb.Payload == last.Value))
                {
                    continue;
                }

                float distance = (orb.transform.position - party.Position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = orb;
                }
            }

            // 여러 AI 가 동시에 같은 색을 먹지 않도록 가장 가까운 파티원만 움직인다.
            if (best == null || isClosestAlly(party, best.transform.position) == false)
            {
                return ETaskStatus.Succeeded;
            }

            if (party.Motor != null)
            {
                party.Motor.MoveTo(best.transform.position);
            }

            return ETaskStatus.Running;
        }

        private static bool isClosestAlly(PartyCharacter InSelf, Vector3 InPoint)
        {
            var allies = new List<CombatActor>();
            CombatRegistry.FindTeam(InSelf.Team, allies);
            float mine = (InSelf.Position - InPoint).sqrMagnitude;
            for (int i = 0; i < allies.Count; ++i)
            {
                var ally = allies[i] as PartyCharacter;
                if (ally == null || ally == InSelf || ally.IsPlayerControlled == true)
                {
                    continue;
                }

                if ((ally.Position - InPoint).sqrMagnitude < mine)
                {
                    return false;
                }
            }

            return true;
        }

        public override string Describe()
        {
            return $"Pick Orb ({GimmickId})";
        }
    }

    /// <summary>
    /// 기본 전투: 선호 거리/백어택 위치로 이동 → 쓸 수 있는 스킬을 우선순위(배열 순서)대로 사용.
    /// 무력화 체크 중이면 무력화 수치가 높은 스킬을 먼저 쓴다.
    /// </summary>
    [Serializable]
    [StateTreeNodeInfo("DemoLA/Ally", "위치 잡기 + 스킬 사용 (ReevaluateTime 마다 종료)")]
    public class AllyFightTask : StateTreeTask
    {
        public float ReevaluateTime = 0.4f;
        public bool UseAwakeningBelowBossHpLine = true;
        [NonSerialized] private float elapsed;

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            elapsed = 0.0f;
            act(InContext);
            return ETaskStatus.Running;
        }

        public override ETaskStatus Tick(StateTreeContext InContext)
        {
            elapsed += InContext.DeltaTime;
            if (elapsed >= ReevaluateTime)
            {
                return ETaskStatus.Succeeded;
            }

            var party = AIContext.Party(InContext);
            if (party != null && party.IsCasting == false)
            {
                act(InContext);
            }

            return ETaskStatus.Running;
        }

        /// <summary>아이덴티티: X 가 준비되면 X(슬레이어는 폭주가 거의 끝날 때), 아니면 Z 자원이 있으면 Z.</summary>
        private static bool tryIdentity(PartyCharacter InParty, Vector3 InAim)
        {
            var x = InParty.IdentityX;
            if (x != null && x.IsReady == true)
            {
                var burst = InParty.Identity as DemoLA.Character.Identity.SlayerBurstAbility;
                bool wait = burst != null && burst.ModeRemainingRatio > 0.15f;
                if (wait == false && InParty.TryUseIdentityX(InAim) == true)
                {
                    return true;
                }
            }

            var z = InParty.Identity;
            return z != null && z.HasResource == true && InParty.TryUseIdentity(InAim) == true;
        }

        private void act(StateTreeContext InContext)
        {
            var party = AIContext.Party(InContext);
            var target = AIContext.Target(InContext);
            if (party == null || target == null || party.IsCasting == true)
            {
                return;
            }

            var cls = party.ClassDefinition;
            float distance = InContext.Blackboard.GetFloat((int)EAIBlackboard.TargetDistance, 9999.0f);
            var desired = desiredPosition(party, target, cls);
            bool staggerCheck = target.HasTag((int)ETag.Boss_StaggerCheck);

            if (distance <= 6.0f && tryIdentity(party, target.Position) == true)
            {
                return;
            }

            var skill = pickSkill(party, distance, staggerCheck);
            if (skill != null)
            {
                party.FaceTowards(target.Position);
                party.TryUseSlotSkill(skill, target.Position);
                return;
            }

            if (party.Motor != null && (HitGeometry.Flat(desired - party.Position)).magnitude > 1.0f)
            {
                party.Motor.MoveTo(desired);
            }
        }

        private static SkillAbility pickSkill(PartyCharacter InParty, float InDistance, bool InStaggerCheck)
        {
            SkillAbility best = null;
            float bestScore = float.MinValue;
            var skills = InParty.EquippedSkills;
            for (int i = 0; i < skills.Count; ++i)
            {
                var skill = skills[i];
                if (InParty.IsSkillReady(skill) == false || InDistance > skill.Definition.EstimatedRange)
                {
                    continue;
                }

                if (skill.Definition.IsAwakening == true && InStaggerCheck == false)
                {
                    continue;
                }

                float score = -i;
                if (InStaggerCheck == true)
                {
                    for (int h = 0; h < skill.Hits.Count; ++h)
                    {
                        score += skill.Hits[h].Stagger;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = skill;
                }
            }

            return best;
        }

        private static Vector3 desiredPosition(PartyCharacter InParty, CombatActor InTarget, CharacterClassDefinition InClass)
        {
            float range = InClass != null ? InClass.AiPreferredRange : 3.0f;
            var direction = HitGeometry.Flat(InParty.Position - InTarget.Position);
            if (InClass != null && InClass.AiPrefersBack == true)
            {
                direction = -HitGeometry.Flat(InTarget.Forward);
            }

            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector3.back;
            }

            return InTarget.Position + direction.normalized * (range + InTarget.Radius);
        }

        public override string Describe()
        {
            return "Fight";
        }
    }

    public static class PartyCharacterAIExtensions
    {
        public static bool TryUseSlotSkill(this PartyCharacter InParty, SkillAbility InSkill, Vector3 InAim)
        {
            Framework.Ability.EActivateFailReason reason;
            return InParty.TryUseSkill(InSkill, InAim, out reason);
        }
    }
}
