using DemoLA.Attributes;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Effects;
using DemoLA.Skills;
using Framework;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Character.Identity
{
    /// <summary>
    /// X 아이덴티티. Z 아이덴티티(IdentityAbility)와 같은 자원(게이지 / 구슬)을 나눠 쓴다.
    /// 직업마다 하나 (직업 각인과 무관). 종류 키 = 직업 번호.
    /// </summary>
    public abstract class IdentitySecondaryAbility : GameplayAbility
    {
        public const int SecondaryAbilityId = 9101;

        public static IdentitySecondaryAbility Create(ECharacterClass InClass)
        {
            return KindRegistry<IdentitySecondaryAbility>.Create((int)InClass);
        }

        protected IdentitySecondaryAbility()
        {
            AbilityId = SecondaryAbilityId;
            AbilityTags.Add((int)ETag.Ability_Identity);
            var blocked = GameTags.IncapacitatedTags;
            for (int i = 0; i < blocked.Length; ++i)
            {
                ActivationBlockedTags.Add((int)blocked[i]);
            }

            CooldownEffect = GameEffects.Cooldown;
            CooldownDuration = 1.0f;
        }

        public abstract string DisplayName { get; }
        public abstract string Description { get; }

        public PartyCharacter Character
        {
            get { return Owner != null ? Owner.OwnerActor as PartyCharacter : null; }
        }

        /// <summary>같은 직업의 Z 아이덴티티 (자원 보유자).</summary>
        protected IdentityAbility Primary
        {
            get { return Character != null ? Character.Identity : null; }
        }

        /// <summary>자원 · 조건이 갖춰졌는가 (HUD 표시용).</summary>
        public abstract bool IsReady { get; }

        public override bool CanActivate(out EActivateFailReason OutReason)
        {
            if (base.CanActivate(out OutReason) == false)
            {
                return false;
            }

            if (IsReady == false)
            {
                OutReason = EActivateFailReason.Cost;
                return false;
            }

            return true;
        }

        protected override void OnActivate(AbilityActivationInfo InInfo)
        {
            if (CommitAbility() == false || Character == null || Primary == null)
            {
                EndAbility(true);
                return;
            }

            Perform(InInfo);
        }

        /// <summary>자원을 직접 쓰고 동작한다. 즉발이면 마지막에 EndAbility(false).</summary>
        protected abstract void Perform(AbilityActivationInfo InInfo);

        protected HitParams MakeHit(float InCoefficient, float InStagger, float InDestruction)
        {
            var hit = new HitParams();
            hit.SkillId = SecondaryAbilityId;
            hit.Coefficient = InCoefficient;
            hit.Stagger = InStagger;
            hit.Destruction = InDestruction;
            hit.Flags = ESkillFlags.Identity;
            return hit;
        }

        protected void FaceAim(AbilityActivationInfo InInfo)
        {
            var actor = Character;
            if (actor != null && HitGeometry.Flat(InInfo.AimPoint - actor.Position).sqrMagnitude > 0.01f)
            {
                actor.FaceTowards(InInfo.AimPoint);
            }
        }
    }

    // =========================================================================== 서머너 X: 정령 집결

    /// <summary>남은 정령 구슬을 모두 써서 3초 동안 주변을 0.5초마다 친다. 구슬 1개당 위력 증가.</summary>
    [Kind((int)ECharacterClass.Summoner)]
    public class SummonerGatherAbility : IdentitySecondaryAbility
    {
        public override string DisplayName => "정령 집결";
        public override string Description => "남은 정령 구슬을 모두 써서 3초 동안 주변 4m 를 0.5초마다 공격한다. 구슬이 많을수록 강해진다.";
        public override bool IsReady => Primary != null && Primary.Orbs > 0;

        protected override void Perform(AbilityActivationInfo InInfo)
        {
            int orbs = Primary.SpendAllOrbs();
            float elapsed = 0.0f;
            float next = 0.0f;
            AddTask(new WaitDurationTask(3.0f, (InRatio, InDelta) =>
            {
                elapsed += InDelta;
                if (elapsed < next)
                {
                    return;
                }

                next += 0.5f;
                var actor = Character;
                if (actor != null)
                {
                    SkillHitResolver.ApplyArea(actor, HitArea.Circle(4.0f), actor.Position, actor.Forward, MakeHit(0.9f * orbs, 6.0f * orbs, 0.0f));
                }
            }, () => EndAbility(false)));
        }
    }

    // =========================================================================== 인파이터 X: 연환 기공

    /// <summary>게이지 50 으로 전방 3연타 (0.15초 간격).</summary>
    [Kind((int)ECharacterClass.Infighter)]
    public class InfighterComboAbility : IdentitySecondaryAbility
    {
        private const float Cost = 50.0f;

        public override string DisplayName => "연환 기공";
        public override string Description => $"게이지 {Cost:0} 을 써서 전방에 기공을 3번 연달아 날린다. Z 보다 약하지만 자주 쓸 수 있다.";
        public override bool IsReady => Primary != null && Primary.Gauge >= Cost;

        protected override void Perform(AbilityActivationInfo InInfo)
        {
            if (Primary.TrySpendGauge(Cost) == false)
            {
                EndAbility(true);
                return;
            }

            FaceAim(InInfo);
            int count = 0;
            float elapsed = 0.0f;
            AddTask(new WaitDurationTask(0.45f, (InRatio, InDelta) =>
            {
                elapsed += InDelta;
                while (count < 3 && elapsed >= count * 0.15f)
                {
                    count += 1;
                    var actor = Character;
                    if (actor != null)
                    {
                        SkillHitResolver.ApplyArea(actor, HitArea.Fan(4.0f, 90.0f), actor.Position, actor.Forward, MakeHit(2.5f, 25.0f, 0.3f));
                    }
                }
            }, () => EndAbility(false)));
        }
    }

    // =========================================================================== 슬레이어 X: 폭주 해제 강타

    /// <summary>폭주 중에만. 폭주를 끝내며 남은 시간만큼 강해지는 강타.</summary>
    [Kind((int)ECharacterClass.Slayer)]
    public class SlayerReleaseAbility : IdentitySecondaryAbility
    {
        public override string DisplayName => "폭주 해제";
        public override string Description => "폭주 중에만 쓸 수 있다. 폭주를 즉시 끝내며 전방을 강타한다. 폭주 시간이 많이 남았을수록 강하다.";

        public override bool IsReady
        {
            get
            {
                var burst = Primary as SlayerBurstAbility;
                return burst != null && burst.ModeActive == true;
            }
        }

        protected override void Perform(AbilityActivationInfo InInfo)
        {
            var burst = Primary as SlayerBurstAbility;
            float remaining = burst != null ? burst.ModeRemainingRatio : 0.0f;
            if (burst != null)
            {
                burst.EndMode();
            }

            FaceAim(InInfo);
            var actor = Character;
            SkillHitResolver.ApplyArea(actor, HitArea.Fan(5.0f, 120.0f), actor.Position, actor.Forward, MakeHit(6.0f + 12.0f * remaining, 40.0f, 1.0f));
            EndAbility(false);
        }
    }

    // =========================================================================== 도화가 X: 해 그리기

    /// <summary>조화 구슬 2개로 주변 아군 6초 피해 감소 20%.</summary>
    [Kind((int)ECharacterClass.Artist)]
    public class ArtistSunAbility : IdentitySecondaryAbility
    {
        private static GameplayEffectDef buff;

        public override string DisplayName => "해 그리기";
        public override string Description => "조화 구슬 2개를 써서 주변 12m 아군이 6초 동안 받는 피해를 20% 줄인다.";
        public override bool IsReady => Primary != null && Primary.Orbs >= 2;

        protected override void Perform(AbilityActivationInfo InInfo)
        {
            if (Primary.TrySpendOrbs(2) == false)
            {
                EndAbility(true);
                return;
            }

            if (buff == null)
            {
                buff = EffectBuilder.Timed(6.0f).Add(EAttr.IncomingDamageReduction, 20.0f).Grant(ETag.Buff_DamageReduction).Stack(EStackPolicy.Refresh, 1).Build();
            }

            var actor = Character;
            var allies = new System.Collections.Generic.List<CombatActor>();
            CombatRegistry.FindTeam(actor.Team, allies);
            for (int i = 0; i < allies.Count; ++i)
            {
                if ((allies[i].Position - actor.Position).sqrMagnitude <= 144.0f)
                {
                    allies[i].ApplyEffect(buff, Owner);
                }
            }

            EndAbility(false);
        }
    }
}
