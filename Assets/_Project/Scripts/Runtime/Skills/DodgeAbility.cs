using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Effects;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Skills
{
    /// <summary>
    /// 회피기 (이동기). 시전 중인 스킬을 취소하고 조준 방향으로 이동한다.
    /// 시전 동안 무적(InvulnerableTime) · 공중/공포 상태에서 쓰면 기상기로 동작. 쿨다운/거리는 직업 정의에서 정한다.
    /// </summary>
    public class DodgeAbility : GameplayAbility
    {
        public const int DodgeAbilityId = 9000;

        public float Distance = 5.0f;
        public float Duration = 0.25f;
        public bool GrantsInvulnerability = true;
        public float InvulnerableTime = 0.3f;

        public DodgeAbility(float InCooldown)
        {
            AbilityId = DodgeAbilityId;
            AbilityTags.Add((int)ETag.Ability_Dodge);
            CancelAbilitiesWithTags.Add((int)ETag.Ability_Skill);
            ActivationOwnedTags.Add((int)ETag.State_Dashing);
            ActivationBlockedTags.Add((int)ETag.State_Dead);
            ActivationBlockedTags.Add((int)ETag.State_Stunned);
            ActivationBlockedTags.Add((int)ETag.State_Frozen);
            ActivationBlockedTags.Add((int)ETag.State_Grabbed);
            ActivationBlockedTags.Add((int)ETag.State_Falling);
            CooldownEffect = GameEffects.Cooldown;
            CooldownDuration = InCooldown;
        }

        protected override void OnActivate(AbilityActivationInfo InInfo)
        {
            var actor = Owner.OwnerActor as CombatActor;
            if (actor == null || actor.Motor == null || CommitAbility() == false)
            {
                EndAbility(true);
                return;
            }

            // 공중에 뜬 상태에서도 기상 회피처럼 쓸 수 있게 상태이상을 걷어 낸다 (임시 규칙).
            actor.Asc.RemoveEffectsWithGrantedTags(GameTags.Container(ETag.State_Airborne, ETag.State_Feared));

            var direction = HitGeometry.Flat(InInfo.AimPoint - actor.Position);
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = actor.Forward;
            }

            // 기상기: 공중/공포 상태에서도 쓸 수 있고, 무적으로 연속 피격을 끊는다.
            if (GrantsInvulnerability == true)
            {
                actor.ApplyStatus(ETag.State_Invulnerable, Mathf.Max(Duration, InvulnerableTime), Owner);
            }

            actor.Motor.Dash(direction, Distance, Duration);
            AddTask(new WaitDelayTask(Duration, () => EndAbility(false)));
        }
    }
}
