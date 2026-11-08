using System;
using DemoLA.Attributes;
using DemoLA.Core;
using Framework;
using UnityEngine;

namespace DemoLA.Combat
{
    /// <summary>조건부 피해 모디파이어 1개 (각인 · 카드 · 버프가 액터에 붙인다).</summary>
    [Serializable]
    public struct DamageModifierEntry
    {
        public EDamageCondition Condition;
        public float Param;
        public EDamageGroup Group;
        public float Value;
        public string Source;
    }

    /// <summary>조건 판정에 필요한 피해 1건의 맥락.</summary>
    public struct DamageContext
    {
        public CombatActor Source;
        public CombatActor Target;
        public EPositional Positional;
        public float Distance;
        public bool IsAwakening;
        public bool IsIdentity;
        public float SkillCooldown;
    }

    /// <summary>
    /// 조건 판정 인터페이스. SkillDamageExecution(IGameplayEffectExecution) 이 피해 계산 중에 부른다.
    /// 반환값 = 이번 피해에 실제로 더할 값 (조건 불충족이면 0). 비례형 조건은 값을 스스로 계산한다.
    /// 새 조건 = enum 값 + [Kind] 클래스 하나.
    /// </summary>
    public interface IDamageCondition
    {
        float Evaluate(in DamageContext InContext, float InParam, float InValue);
    }

    public static class DamageConditions
    {
        public static IDamageCondition Of(EDamageCondition InCondition)
        {
            return KindRegistry<DamageConditionBase>.Get((int)InCondition);
        }

        /// <summary>InModifiers 를 판정해 그룹별 합계를 낸다 (Independent 는 곱).</summary>
        public static void Accumulate(System.Collections.Generic.IReadOnlyList<DamageModifierEntry> InModifiers, in DamageContext InContext,
            ref float InOutIndependent, ref float InOutAdditive, ref float InOutCritRate, ref float InOutCritDamage)
        {
            if (InModifiers == null)
            {
                return;
            }

            for (int i = 0; i < InModifiers.Count; ++i)
            {
                var modifier = InModifiers[i];
                var condition = Of(modifier.Condition);
                if (condition == null)
                {
                    continue;
                }

                float value = condition.Evaluate(InContext, modifier.Param, modifier.Value);
                if (value == 0.0f)
                {
                    continue;
                }

                switch (modifier.Group)
                {
                    case EDamageGroup.Independent:
                        InOutIndependent *= 1.0f + value * 0.01f;
                        break;
                    case EDamageGroup.Additive:
                        InOutAdditive += value;
                        break;
                    case EDamageGroup.CritRate:
                        InOutCritRate += value;
                        break;
                    case EDamageGroup.CritDamage:
                        InOutCritDamage += value;
                        break;
                }
            }
        }
    }

    public abstract class DamageConditionBase : IDamageCondition
    {
        public virtual float Evaluate(in DamageContext InContext, float InParam, float InValue)
        {
            return Test(InContext, InParam) == true ? InValue : 0.0f;
        }

        protected abstract bool Test(in DamageContext InContext, float InParam);

        protected static float healthPercent(CombatActor InActor)
        {
            return InActor != null && InActor.Stats != null ? InActor.Stats.HealthRatio * 100.0f : 0.0f;
        }
    }

    [Kind(EDamageCondition.Always)]
    public sealed class AlwaysCondition : DamageConditionBase
    {
        protected override bool Test(in DamageContext InContext, float InParam) => true;
    }

    [Kind(EDamageCondition.TargetIsBoss)]
    public sealed class TargetIsBossCondition : DamageConditionBase
    {
        protected override bool Test(in DamageContext InContext, float InParam) => InContext.Target is DemoLA.Boss.BossActor;
    }

    [Kind(EDamageCondition.BackAttack)]
    public sealed class BackAttackCondition : DamageConditionBase
    {
        protected override bool Test(in DamageContext InContext, float InParam) => InContext.Positional == EPositional.BackAttack;
    }

    [Kind(EDamageCondition.HeadAttack)]
    public sealed class HeadAttackCondition : DamageConditionBase
    {
        protected override bool Test(in DamageContext InContext, float InParam) => InContext.Positional == EPositional.HeadAttack;
    }

    [Kind(EDamageCondition.SelfHealthAbove)]
    public sealed class SelfHealthAboveCondition : DamageConditionBase
    {
        protected override bool Test(in DamageContext InContext, float InParam) => healthPercent(InContext.Source) >= InParam;
    }

    [Kind(EDamageCondition.SelfHealthBelow)]
    public sealed class SelfHealthBelowCondition : DamageConditionBase
    {
        protected override bool Test(in DamageContext InContext, float InParam) => healthPercent(InContext.Source) <= InParam;
    }

    [Kind(EDamageCondition.TargetHealthBelow)]
    public sealed class TargetHealthBelowCondition : DamageConditionBase
    {
        protected override bool Test(in DamageContext InContext, float InParam) => healthPercent(InContext.Target) <= InParam;
    }

    [Kind(EDamageCondition.SelfShielded)]
    public sealed class SelfShieldedCondition : DamageConditionBase
    {
        protected override bool Test(in DamageContext InContext, float InParam) => InContext.Source != null && InContext.Source.Stats.Get(EAttr.Shield) > 0.0f;
    }

    [Kind(EDamageCondition.TargetGroggy)]
    public sealed class TargetGroggyCondition : DamageConditionBase
    {
        protected override bool Test(in DamageContext InContext, float InParam) => InContext.Target != null && InContext.Target.HasTag((int)ETag.State_Groggy);
    }

    [Kind(EDamageCondition.TargetWithin)]
    public sealed class TargetWithinCondition : DamageConditionBase
    {
        protected override bool Test(in DamageContext InContext, float InParam) => InContext.Distance <= InParam;
    }

    [Kind(EDamageCondition.TargetBeyond)]
    public sealed class TargetBeyondCondition : DamageConditionBase
    {
        protected override bool Test(in DamageContext InContext, float InParam) => InContext.Distance >= InParam;
    }

    [Kind(EDamageCondition.AwakeningSkill)]
    public sealed class AwakeningSkillCondition : DamageConditionBase
    {
        protected override bool Test(in DamageContext InContext, float InParam) => InContext.IsAwakening;
    }

    [Kind(EDamageCondition.IdentitySkill)]
    public sealed class IdentitySkillCondition : DamageConditionBase
    {
        protected override bool Test(in DamageContext InContext, float InParam) => InContext.IsIdentity;
    }

    [Kind(EDamageCondition.CooldownAtLeast)]
    public sealed class CooldownAtLeastCondition : DamageConditionBase
    {
        protected override bool Test(in DamageContext InContext, float InParam) => InContext.SkillCooldown >= InParam;
    }

    /// <summary>이동속도 초과분(%) × 값. 조건이 아니라 비례형이라 Evaluate 를 직접 구현한다.</summary>
    [Kind(EDamageCondition.MoveSpeedScaled)]
    public sealed class MoveSpeedScaledCondition : DamageConditionBase
    {
        protected override bool Test(in DamageContext InContext, float InParam) => true;

        public override float Evaluate(in DamageContext InContext, float InParam, float InValue)
        {
            if (InContext.Source == null || InContext.Source.Stats == null)
            {
                return 0.0f;
            }

            return Mathf.Max(0.0f, InContext.Source.Stats.Get(EAttr.MoveSpeed) - 100.0f) * InValue;
        }
    }
}
