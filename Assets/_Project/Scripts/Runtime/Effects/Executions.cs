using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Combat;
using DemoLA.Core;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Effects
{
    /// <summary>플레이어/아군 스킬 피해 (DamageCalculator 에 시전자 스냅샷 + 대상 현재 속성을 넣는다).</summary>
    public sealed class SkillDamageExecution : IGameplayEffectExecution
    {
        public void Execute(GameplayEffectSpec InSpec, AbilitySystemComponent InTarget, List<EvaluatedModifier> OutModifiers)
        {
            var input = new DamageInput();
            input.AttackPower = InSpec.GetSourceAttribute((int)EAttr.AttackPower);
            input.Coefficient = InSpec.GetSetByCaller((int)ESetByCaller.Coefficient);
            input.FlatDamage = InSpec.GetSetByCaller((int)ESetByCaller.FlatDamage);
            input.SkillDamagePercent = 100.0f;
            input.GemDamagePercent = InSpec.GetSetByCaller((int)ESetByCaller.GemDamagePercent);
            input.DamageIncrease = InSpec.GetSourceAttribute((int)EAttr.DamageIncrease);
            input.IndependentMultiplier = IndependentMultiplier(InSpec.Source);
            input.CritRate = InSpec.GetSourceAttribute((int)EAttr.CritRate) + InSpec.GetSetByCaller((int)ESetByCaller.CritRateBonus);
            input.CritDamage = InSpec.GetSourceAttribute((int)EAttr.CritDamage) + InSpec.GetSetByCaller((int)ESetByCaller.CritDamageBonus);

            var positional = (EPositional)Mathf.RoundToInt(InSpec.GetSetByCaller((int)ESetByCaller.PositionalType));
            input.Positional = positional;
            if (positional == EPositional.BackAttack)
            {
                // 백어택의 추가 치명타 적중률 보너스 여부는 미정 (구현 보류 사항.md) — 피해 % 만 반영한다.
                input.PositionalBonus = StatFormula.Settings.BaseBackAttackBonus + InSpec.GetSourceAttribute((int)EAttr.BackAttackDamage);
            }
            else if (positional == EPositional.HeadAttack)
            {
                input.PositionalBonus = StatFormula.Settings.BaseHeadAttackBonus + InSpec.GetSourceAttribute((int)EAttr.HeadAttackDamage);
            }

            input.PositionalBonus += InSpec.GetSetByCaller((int)ESetByCaller.PositionalBonus);

            var sourceActor = InSpec.Source != null ? InSpec.Source.OwnerActor as CombatActor : null;
            var targetActor = InTarget.OwnerActor as CombatActor;
            if (positional == EPositional.BackAttack)
            {
                input.CritRate += StatFormula.Settings.BackAttackCritBonus;
            }

            if (sourceActor != null)
            {
                var flags = (DemoLA.Skills.ESkillFlags)Mathf.RoundToInt(InSpec.GetSetByCaller((int)ESetByCaller.SkillFlags));
                var context = new DamageContext();
                context.Source = sourceActor;
                context.Target = targetActor;
                context.Positional = positional;
                context.Distance = targetActor != null ? Vector3.Distance(HitGeometry.Flat(sourceActor.Position), HitGeometry.Flat(targetActor.Position)) - targetActor.Radius : 0.0f;
                context.IsAwakening = (flags & DemoLA.Skills.ESkillFlags.Awakening) != 0;
                context.IsIdentity = (flags & DemoLA.Skills.ESkillFlags.Identity) != 0;
                context.SkillCooldown = InSpec.GetSetByCaller((int)ESetByCaller.SkillCooldown);
                DamageConditions.Accumulate(sourceActor.DamageModifiers, context, ref input.IndependentMultiplier, ref input.DamageIncrease, ref input.CritRate, ref input.CritDamage);

                var element = sourceActor.AttackElement != EElement.None ? sourceActor.AttackElement : (EElement)Mathf.RoundToInt(InSpec.GetSetByCaller((int)ESetByCaller.Element));
                var boss = targetActor as DemoLA.Boss.BossActor;
                input.ElementMultiplier = boss != null && boss.Definition != null ? boss.Definition.ElementMultiplier(element) : 1.0f;
            }

            var targetAttributes = InTarget.Attributes;
            input.TargetDefense = targetAttributes.GetCurrent((int)EAttr.Defense);
            input.TargetDamageReduction = targetAttributes.GetCurrent((int)EAttr.IncomingDamageReduction);
            input.TargetDamageAmplify = targetAttributes.GetCurrent((int)EAttr.IncomingDamageAmplify);
            input.CritRoll = GameRandom.Value;

            var result = DamageCalculator.Calculate(input);
            InSpec.SetByCaller((int)ESetByCaller.ResultIsCrit, result.IsCrit == true ? 1.0f : 0.0f);
            InSpec.SetByCaller((int)ESetByCaller.ResultPositional, (float)positional);

            // 무력화 / 부위 파괴는 피해보다 먼저 넣어 사망 처리 전에 게이지가 반영되게 한다.
            float stagger = InSpec.GetSetByCaller((int)ESetByCaller.StaggerValue) * InSpec.GetSourceAttribute((int)EAttr.StaggerPower) * 0.01f;
            if (stagger > 0.0f)
            {
                OutModifiers.Add(evaluated(EAttr.IncomingStagger, stagger));
            }

            float destruction = InSpec.GetSetByCaller((int)ESetByCaller.DestructionValue);
            if (destruction > 0.0f)
            {
                OutModifiers.Add(evaluated(EAttr.IncomingDestruction, destruction));
            }

            // 밸런스: 플레이어 피해 전체 배율 (StatFormula.PlayerDamageScale).
            OutModifiers.Add(evaluated(EAttr.IncomingDamage, result.Damage * StatFormula.Settings.PlayerDamageScale));
        }

        /// <summary>
        /// IndependentDamage 를 건드리는 활성 이펙트마다 (1 + 값/100) 을 곱한다.
        /// AttributeSet 의 기본 집계는 합연산이므로, 곱연산 그룹은 이펙트 단위로 직접 계산한다.
        /// </summary>
        public static float IndependentMultiplier(AbilitySystemComponent InSource)
        {
            if (InSource == null)
            {
                return 1.0f;
            }

            float multiplier = 1.0f;
            var effects = InSource.Effects;
            for (int i = 0; i < effects.Count; ++i)
            {
                var effect = effects[i];
                var modifiers = effect.Spec.Def.Modifiers;
                for (int m = 0; m < modifiers.Length; ++m)
                {
                    if (modifiers[m].Attribute != (int)EAttr.IndependentDamage)
                    {
                        continue;
                    }

                    float magnitude = effect.Magnitudes != null && m < effect.Magnitudes.Length ? effect.Magnitudes[m] : modifiers[m].Value;
                    multiplier *= 1.0f + magnitude * effect.StackCount * 0.01f;
                }
            }

            return multiplier;
        }

        private static EvaluatedModifier evaluated(EAttr InAttr, float InMagnitude)
        {
            var modifier = new EvaluatedModifier();
            modifier.Attribute = (int)InAttr;
            modifier.Op = EModifierOp.Add;
            modifier.Magnitude = InMagnitude;
            return modifier;
        }
    }

    /// <summary>
    /// 보스 패턴 피해 = 대상 최대 체력 × 비율 (xlsx 의 '피해 추정 (최대 체력 대비)').
    /// 비율 1 이상은 즉사급으로 보고 피해 감소를 무시한다.
    /// </summary>
    public sealed class BossPatternDamageExecution : IGameplayEffectExecution
    {
        public void Execute(GameplayEffectSpec InSpec, AbilitySystemComponent InTarget, List<EvaluatedModifier> OutModifiers)
        {
            var attributes = InTarget.Attributes;
            float fraction = InSpec.GetSetByCaller((int)ESetByCaller.MaxHealthFraction);
            float maxHealth = attributes.GetCurrent((int)EAttr.MaxHealth);
            float damage;
            if (fraction >= 1.0f)
            {
                damage = maxHealth * fraction + attributes.GetCurrent((int)EAttr.Shield);
            }
            else
            {
                float reduction = 1.0f - Mathf.Clamp(attributes.GetCurrent((int)EAttr.IncomingDamageReduction), 0.0f, 100.0f) * 0.01f;
                float amplify = 1.0f + Mathf.Max(0.0f, attributes.GetCurrent((int)EAttr.IncomingDamageAmplify)) * 0.01f;
                damage = maxHealth * fraction * reduction * amplify;
            }

            var modifier = new EvaluatedModifier();
            modifier.Attribute = (int)EAttr.IncomingDamage;
            modifier.Op = EModifierOp.Add;
            modifier.Magnitude = damage;
            OutModifiers.Add(modifier);
        }
    }

    /// <summary>회복 = 시전자 공격력 × 계수 × (1 + 치유 증가%) + 고정.</summary>
    public sealed class HealExecution : IGameplayEffectExecution
    {
        public void Execute(GameplayEffectSpec InSpec, AbilitySystemComponent InTarget, List<EvaluatedModifier> OutModifiers)
        {
            float amount = InSpec.GetSourceAttribute((int)EAttr.AttackPower) * InSpec.GetSetByCaller((int)ESetByCaller.Coefficient);
            amount *= 1.0f + InSpec.GetSourceAttribute((int)EAttr.OutgoingHealing) * 0.01f;
            amount += InSpec.GetSetByCaller((int)ESetByCaller.FlatDamage);

            var modifier = new EvaluatedModifier();
            modifier.Attribute = (int)EAttr.IncomingHeal;
            modifier.Op = EModifierOp.Add;
            modifier.Magnitude = amount;
            OutModifiers.Add(modifier);
        }
    }
}
