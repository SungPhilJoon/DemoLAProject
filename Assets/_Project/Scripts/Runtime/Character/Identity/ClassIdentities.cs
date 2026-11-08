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
    // =========================================================================== 서머너: 정령 구슬 → 정령 소환

    /// <summary>구슬 1개 → Duration 동안 정령이 Interval 마다 가장 가까운 적을 친다.</summary>
    [Kind(0)]
    public class SummonerSpiritAbility : IdentityAbility
    {
        protected virtual float Duration => 6.0f;
        protected virtual float Interval => 0.75f;
        protected virtual float Coefficient => 1.2f;

        public override string DisplayName => "정령";
        public override int MaxOrbs => 2;
        public override string Description => $"정령 구슬 1개를 써서 {Duration:0}초 동안 정령을 부른다. 정령은 {Interval:0.##}초마다 가장 가까운 적을 공격한다.";

        protected override void Perform(AbilityActivationInfo InInfo)
        {
            float elapsed = 0.0f;
            float next = 0.0f;
            AddTask(new WaitDurationTask(Duration, (InRatio, InDelta) =>
            {
                elapsed += InDelta;
                if (elapsed < next)
                {
                    return;
                }

                next += Interval;
                var actor = Character;
                var target = actor != null ? CombatRegistry.FindNearestHostile(actor, 20.0f) : null;
                if (target != null)
                {
                    SkillHitResolver.ApplyArea(actor, HitArea.Circle(2.5f), target.Position, Vector3.forward, MakeHit(Coefficient, 4.0f, 0.0f));
                }
            }, () => EndAbility(false)));
        }
    }

    /// <summary>직업 각인 1: 정령 지속시간 증가.</summary>
    [Kind(1)]
    public class SummonerResonanceAbility : SummonerSpiritAbility
    {
        protected override float Duration => 10.0f;
        public override string DisplayName => "정령 공명";
    }

    /// <summary>직업 각인 2: 정령을 즉시 폭발시킨다.</summary>
    [Kind(2)]
    public class SummonerBurstAbility : IdentityAbility
    {
        public override string DisplayName => "정령 폭발";
        public override int MaxOrbs => 2;
        public override string Description => "정령 구슬 1개를 써서 조준 지점에 정령을 터뜨린다.";

        protected override void Perform(AbilityActivationInfo InInfo)
        {
            SkillHitResolver.ApplyArea(Character, HitArea.Circle(4.0f), InInfo.AimPoint, Vector3.forward, MakeHit(6.0f, 30.0f, 1.0f));
            EndAbility(false);
        }
    }

    // =========================================================================== 인파이터: 게이지 → 충격 해방

    [Kind(10)]
    public class InfighterShockAbility : IdentityAbility
    {
        public override string DisplayName => "충격 해방";
        public override string Description => "게이지 100 을 써서 전방에 강한 충격파를 날린다. 무력화가 높다.";
        protected virtual HitArea Area => HitArea.Fan(5.0f, 120.0f);
        protected virtual float Coefficient => 8.0f;
        protected virtual float Stagger => 120.0f;

        protected override void Perform(AbilityActivationInfo InInfo)
        {
            FaceAim(InInfo);
            var actor = Character;
            SkillHitResolver.ApplyArea(actor, Area, actor.Position, actor.Forward, MakeHit(Coefficient, Stagger, 1.0f));
            EndAbility(false);
        }
    }

    /// <summary>직업 각인 1: 범위 대신 단일 직선 강타.</summary>
    [Kind(11)]
    public class InfighterFocusAbility : InfighterShockAbility
    {
        public override string DisplayName => "충격 수련";
        protected override HitArea Area => HitArea.Rect(6.0f, 2.0f);
        protected override float Coefficient => 11.0f;
        protected override float Stagger => 150.0f;
    }

    /// <summary>직업 각인 2: 게이지를 소모해 10초간 피해 증가.</summary>
    [Kind(12)]
    public class InfighterSpiritAbility : IdentityAbility
    {
        private static GameplayEffectDef buff;

        public override string DisplayName => "투지 해방";
        public override string Description => "게이지 100 을 써서 10초 동안 주는 피해가 15% (독립) 증가한다.";

        protected override void Perform(AbilityActivationInfo InInfo)
        {
            if (buff == null)
            {
                buff = EffectBuilder.Timed(10.0f).Add(EAttr.IndependentDamage, 15.0f).Stack(EStackPolicy.Refresh, 1).Build();
            }

            Owner.ApplyGameplayEffectSpecToSelf(Owner.MakeOutgoingSpec(buff, AbilityId));
            EndAbility(false);
        }
    }

    // =========================================================================== 슬레이어: 게이지 → 폭주

    [Kind(20)]
    public class SlayerBurstAbility : IdentityAbility
    {
        private GameplayEffectDef modeEffect;

        public override string DisplayName => "폭주";
        public override string Description => $"게이지 100 을 써서 {Duration:0}초 동안 폭주한다 (피해 · 공격 속도 · 이동 속도 증가). 폭주 중 X 로 폭주를 끝내며 강타할 수 있다.";
        protected virtual float Duration => 12.0f;

        private int modeHandle;
        private float modeStart = -1.0f;

        /// <summary>폭주 중인가 (X: 폭주 해제 강타의 조건).</summary>
        public bool ModeActive
        {
            get { return modeStart >= 0.0f && Time.time < modeStart + Duration; }
        }

        /// <summary>폭주 남은 비율 1 → 0.</summary>
        public float ModeRemainingRatio
        {
            get { return ModeActive == true ? 1.0f - (Time.time - modeStart) / Duration : 0.0f; }
        }

        public override string StatusText
        {
            get { return ModeActive == true ? $"{DisplayName} 중 {Duration * ModeRemainingRatio:0.0}초" : base.StatusText; }
        }

        /// <summary>폭주를 즉시 끝낸다.</summary>
        public void EndMode()
        {
            if (modeHandle != 0 && Owner != null)
            {
                Owner.RemoveActiveEffect(modeHandle);
            }

            modeHandle = 0;
            modeStart = -1.0f;
        }

        protected virtual GameplayEffectDef BuildMode()
        {
            return EffectBuilder.Timed(Duration).Add(EAttr.DamageIncrease, 20.0f).Add(EAttr.AttackSpeed, 15.0f).Add(EAttr.MoveSpeed, 15.0f).Build();
        }

        protected override void Perform(AbilityActivationInfo InInfo)
        {
            if (modeEffect == null)
            {
                modeEffect = BuildMode();
            }

            modeHandle = Owner.ApplyGameplayEffectSpecToSelf(Owner.MakeOutgoingSpec(modeEffect, AbilityId));
            modeStart = Time.time;
            EndAbility(false);
        }

        public override bool CanActivate(out EActivateFailReason OutReason)
        {
            // 폭주 중에는 다시 쓸 수 없다.
            if (ModeActive == true)
            {
                OutReason = EActivateFailReason.Custom;
                return false;
            }

            return base.CanActivate(out OutReason);
        }
    }

    /// <summary>직업 각인 1: 폭주 지속 연장 · 치명타 피해.</summary>
    [Kind(21)]
    public class SlayerMadnessAbility : SlayerBurstAbility
    {
        public override string DisplayName => "광기";
        protected override float Duration => 16.0f;

        protected override GameplayEffectDef BuildMode()
        {
            return EffectBuilder.Timed(Duration).Add(EAttr.DamageIncrease, 20.0f).Add(EAttr.AttackSpeed, 15.0f).Add(EAttr.CritDamage, 30.0f).Build();
        }
    }

    /// <summary>직업 각인 2: 폭주 대신 게이지를 모두 써서 강타 (백어택).</summary>
    [Kind(22)]
    public class SlayerExecutionAbility : IdentityAbility
    {
        public override string DisplayName => "처단";
        public override string Description => "게이지 100 을 써서 전방을 강타한다. 백어택 판정이 붙는다.";

        protected override void Perform(AbilityActivationInfo InInfo)
        {
            FaceAim(InInfo);
            var actor = Character;
            var hit = MakeHit(14.0f, 40.0f, 1.0f);
            hit.PositionalType = ESkillPositionalType.BackAttack;
            SkillHitResolver.ApplyArea(actor, HitArea.Fan(4.5f, 90.0f), actor.Position, actor.Forward, hit);
            EndAbility(false);
        }
    }

    // =========================================================================== 도화가: 조화 구슬 → 아군 지원

    [Kind(30)]
    public class ArtistHarmonyAbility : IdentityAbility
    {
        public override string DisplayName => "조화 구슬";
        public override int MaxOrbs => 3;
        protected virtual float HealPercent => 15.0f;
        public override string Description => $"조화 구슬 1개를 써서 주변 12m 아군의 생명력을 {HealPercent:0}% 회복한다.";

        protected override void Perform(AbilityActivationInfo InInfo)
        {
            ForEachAlly(12.0f, InAlly =>
            {
                var heal = Owner.MakeOutgoingSpec(GameEffects.Heal, AbilityId);
                heal.SetByCaller((int)ESetByCaller.FlatDamage, InAlly.Stats.Get(EAttr.MaxHealth) * HealPercent * 0.01f);
                Owner.ApplyGameplayEffectSpecToTarget(heal, InAlly.Asc);
            });
            EndAbility(false);
        }
    }

    /// <summary>직업 각인 1: 회복량 증가.</summary>
    [Kind(31)]
    public class ArtistBloomAbility : ArtistHarmonyAbility
    {
        public override string DisplayName => "만개";
        protected override float HealPercent => 25.0f;
    }

    /// <summary>직업 각인 2: 회복 대신 아군 피해 증가.</summary>
    [Kind(32)]
    public class ArtistMarginAbility : IdentityAbility
    {
        private static GameplayEffectDef buff;

        public override string DisplayName => "여백";
        public override int MaxOrbs => 3;
        public override string Description => "조화 구슬 1개를 써서 주변 아군의 피해를 8초 동안 10% 올린다.";

        protected override void Perform(AbilityActivationInfo InInfo)
        {
            if (buff == null)
            {
                buff = EffectBuilder.Timed(8.0f).Add(EAttr.DamageIncrease, 10.0f).Stack(EStackPolicy.Refresh, 1).Build();
            }

            ForEachAlly(12.0f, InAlly => InAlly.ApplyEffect(buff, Owner));
            EndAbility(false);
        }
    }
}
