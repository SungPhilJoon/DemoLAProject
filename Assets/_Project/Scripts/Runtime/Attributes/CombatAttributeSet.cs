using System;
using DemoLA.Combat;
using DemoLA.Core;
using Framework;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Attributes
{
    /// <summary>
    /// 캐릭터/몬스터 공용 AttributeSet.
    ///  - 파생 속성 (CritRate, AttackSpeed, MoveSpeed, CooldownReduction, AttackPower) 은 PreAttributeChange 에서
    ///    원본 스탯을 읽어 더한다. 원본 스탯이 바뀌면 PostAttributeChange 가 파생 속성을 다시 계산한다.
    ///  - 메타 속성 (IncomingDamage/Heal/Stagger/Destruction) 은 PostGameplayEffectExecute 에서 실제 속성으로 옮긴다.
    /// </summary>
    public class CombatAttributeSet : AttributeSet
    {
        public event Action<DamageAppliedEvent> OnDamageTaken;
        public event Action OnDied;
        public event Action<float> OnStaggerApplied;
        public event Action<float> OnDestructionApplied;

        private static GameplayTagContainer deathWardQuery;

        public CombatAttributeSet() : base((int)EAttr.Count)
        {
        }

        public float Get(EAttr InAttr)
        {
            return GetCurrent((int)InAttr);
        }

        public float GetBaseValue(EAttr InAttr)
        {
            return GetBase((int)InAttr);
        }

        public void Set(EAttr InAttr, float InValue)
        {
            SetBase((int)InAttr, InValue);
        }

        public float HealthRatio
        {
            get
            {
                float max = Get(EAttr.MaxHealth);
                return max <= 0.0f ? 0.0f : Get(EAttr.Health) / max;
            }
        }

        public bool IsDead
        {
            get { return Get(EAttr.Health) <= 0.0f; }
        }

        /// <summary>쿨다운 배율 (ASC.CooldownScaleProvider 에 연결).</summary>
        public float CooldownScale
        {
            get { return Mathf.Clamp(1.0f - Get(EAttr.CooldownReduction) * 0.01f, 0.2f, 1.0f); }
        }

        /// <summary>기본값 일괄 설정. 최대값을 먼저 넣고 현재값을 채운다.</summary>
        public void InitializeDefaults(float InMaxHealth, float InMaxMana, float InAttackPower, float InDefense)
        {
            Set(EAttr.MaxHealth, InMaxHealth);
            Set(EAttr.Health, InMaxHealth);
            Set(EAttr.MaxMana, InMaxMana);
            Set(EAttr.Mana, InMaxMana);
            Set(EAttr.AttackPower, InAttackPower);
            Set(EAttr.Defense, InDefense);
            Set(EAttr.CritDamage, 200.0f);
            Set(EAttr.AttackSpeed, 100.0f);
            Set(EAttr.MoveSpeed, 100.0f);
            Set(EAttr.StaggerPower, 100.0f);
            Set(EAttr.MaxIdentityGauge, 100.0f);
        }

        protected override void PreAttributeChange(int InAttribute, ref float InOutNewValue)
        {
            var settings = StatFormula.Settings;
            switch ((EAttr)InAttribute)
            {
                case EAttr.Health:
                    InOutNewValue = Mathf.Clamp(InOutNewValue, 0.0f, Get(EAttr.MaxHealth));
                    break;
                case EAttr.Mana:
                    InOutNewValue = Mathf.Clamp(InOutNewValue, 0.0f, Get(EAttr.MaxMana));
                    break;
                case EAttr.StaggerGauge:
                    InOutNewValue = Mathf.Clamp(InOutNewValue, 0.0f, Get(EAttr.MaxStaggerGauge));
                    break;
                case EAttr.IdentityGauge:
                    InOutNewValue = Mathf.Clamp(InOutNewValue, 0.0f, Get(EAttr.MaxIdentityGauge));
                    break;
                case EAttr.Shield:
                    InOutNewValue = Mathf.Max(0.0f, InOutNewValue);
                    break;
                case EAttr.CritRate:
                    InOutNewValue = Mathf.Clamp(InOutNewValue + Get(EAttr.StatCrit) * settings.CritRatePerPoint, 0.0f, 100.0f);
                    break;
                case EAttr.AttackSpeed:
                    InOutNewValue = Mathf.Min(settings.SpeedCap, InOutNewValue + Get(EAttr.StatSwiftness) * settings.AttackSpeedPerSwiftness);
                    break;
                case EAttr.MoveSpeed:
                    InOutNewValue = Mathf.Min(settings.SpeedCap, InOutNewValue + Get(EAttr.StatSwiftness) * settings.MoveSpeedPerSwiftness);
                    break;
                case EAttr.CooldownReduction:
                    InOutNewValue = Mathf.Clamp(InOutNewValue + Get(EAttr.StatSwiftness) * settings.CooldownReductionPerSwiftness, 0.0f, 80.0f);
                    break;
                case EAttr.AttackPower:
                    InOutNewValue += StatFormula.AttackPower(Get(EAttr.MainStat), Get(EAttr.WeaponPower), Get(EAttr.AttackPowerPercent));
                    break;
            }
        }

        protected override void PostAttributeChange(int InAttribute, float InOldValue, float InNewValue)
        {
            switch ((EAttr)InAttribute)
            {
                case EAttr.MaxHealth:
                    Recalculate((int)EAttr.Health);
                    break;
                case EAttr.MaxMana:
                    Recalculate((int)EAttr.Mana);
                    break;
                case EAttr.MaxStaggerGauge:
                    Recalculate((int)EAttr.StaggerGauge);
                    break;
                case EAttr.MaxIdentityGauge:
                    Recalculate((int)EAttr.IdentityGauge);
                    break;
                case EAttr.StatCrit:
                    Recalculate((int)EAttr.CritRate);
                    break;
                case EAttr.StatSwiftness:
                    Recalculate((int)EAttr.AttackSpeed);
                    Recalculate((int)EAttr.MoveSpeed);
                    Recalculate((int)EAttr.CooldownReduction);
                    break;
                case EAttr.MainStat:
                case EAttr.WeaponPower:
                case EAttr.AttackPowerPercent:
                    Recalculate((int)EAttr.AttackPower);
                    break;
                case EAttr.Health:
                    if (InOldValue > 0.0f && InNewValue <= 0.0f)
                    {
                        OnDied?.Invoke();
                    }

                    break;
            }
        }

        public override void PostGameplayEffectExecute(EffectExecuteData InData)
        {
            switch ((EAttr)InData.Evaluated.Attribute)
            {
                case EAttr.IncomingDamage:
                    consumeMeta(EAttr.IncomingDamage, out float damage);
                    applyDamage(InData.Spec, damage);
                    break;
                case EAttr.IncomingHeal:
                    consumeMeta(EAttr.IncomingHeal, out float heal);
                    applyHeal(InData.Spec, heal);
                    break;
                case EAttr.IncomingStagger:
                    consumeMeta(EAttr.IncomingStagger, out float stagger);
                    if (stagger > 0.0f)
                    {
                        SetBaseRaw((int)EAttr.StaggerGauge, Get(EAttr.StaggerGauge) - stagger);
                        OnStaggerApplied?.Invoke(stagger);
                    }

                    break;
                case EAttr.IncomingDestruction:
                    consumeMeta(EAttr.IncomingDestruction, out float destruction);
                    if (destruction > 0.0f)
                    {
                        SetBaseRaw((int)EAttr.DestructionAccum, GetBase((int)EAttr.DestructionAccum) + destruction);
                        OnDestructionApplied?.Invoke(destruction);
                    }

                    break;
            }
        }

        private void consumeMeta(EAttr InAttr, out float OutValue)
        {
            OutValue = GetBase((int)InAttr);
            SetBaseRaw((int)InAttr, 0.0f);
        }

        private void applyDamage(GameplayEffectSpec InSpec, float InDamage)
        {
            if (InDamage <= 0.0f || IsDead == true || Owner == null)
            {
                return;
            }

            var evt = new DamageAppliedEvent();
            evt.Source = InSpec.Source;
            evt.Target = Owner;
            evt.RawAmount = InDamage;
            evt.IsCrit = InSpec.GetSetByCaller((int)ESetByCaller.ResultIsCrit) > 0.5f;
            evt.Positional = (EPositional)Mathf.RoundToInt(InSpec.GetSetByCaller((int)ESetByCaller.ResultPositional));
            evt.SkillId = Mathf.RoundToInt(InSpec.GetSetByCaller((int)ESetByCaller.SkillId));

            if (Owner.Tags.HasTag((int)ETag.State_Invulnerable) == true)
            {
                return;
            }

            float health = Get(EAttr.Health);
            bool wardable = InSpec.GetSetByCaller((int)ESetByCaller.ConsumesDeathWard) > 0.5f;
            if (wardable == true && Owner.Tags.HasTag((int)ETag.Buff_DeathWard) == true)
            {
                if (deathWardQuery == null)
                {
                    deathWardQuery = GameTags.Container(ETag.Buff_DeathWard);
                }

                Owner.RemoveEffectsWithGrantedTags(deathWardQuery);
                evt.DeathWardConsumed = true;
                GameplayCueManager.Execute((int)ECue.DeathWardConsumed, makeCue(InSpec, 0.0f));
                publish(evt);
                return;
            }

            float shield = Get(EAttr.Shield);
            float absorbed = Mathf.Min(shield, InDamage);
            if (absorbed > 0.0f)
            {
                SetBaseRaw((int)EAttr.Shield, shield - absorbed);
            }

            float remaining = InDamage - absorbed;
            float newHealth = Mathf.Max(0.0f, health - remaining);
            evt.ShieldAbsorbed = absorbed;
            evt.HealthLost = health - newHealth;
            evt.WasLethal = newHealth <= 0.0f;
            SetBaseRaw((int)EAttr.Health, newHealth);
            publish(evt);
        }

        private void applyHeal(GameplayEffectSpec InSpec, float InHeal)
        {
            if (InHeal <= 0.0f || IsDead == true)
            {
                return;
            }

            float health = Get(EAttr.Health);
            float healed = Mathf.Min(Get(EAttr.MaxHealth), health + InHeal) - health;
            SetBaseRaw((int)EAttr.Health, health + healed);

            var evt = new HealAppliedEvent();
            evt.Source = InSpec.Source;
            evt.Target = Owner;
            evt.Amount = healed;
            EventBus<HealAppliedEvent>.Publish(evt);
        }

        private void publish(DamageAppliedEvent InEvent)
        {
            OnDamageTaken?.Invoke(InEvent);
            EventBus<DamageAppliedEvent>.Publish(InEvent);
        }

        private GameplayCueParams makeCue(GameplayEffectSpec InSpec, float InMagnitude)
        {
            var cue = new GameplayCueParams();
            cue.Source = InSpec.Source;
            cue.Target = Owner;
            cue.Location = Owner != null ? Owner.transform.position : Vector3.zero;
            cue.Magnitude = InMagnitude;
            return cue;
        }
    }
}
