using DemoLA.Core;
using UnityEngine;

namespace DemoLA.Combat
{
    /// <summary>피해 공식 입력 (모든 % 값은 퍼센트 단위).</summary>
    public struct DamageInput
    {
        public float AttackPower;
        /// <summary>스킬 계수 (공격력 × 계수).</summary>
        public float Coefficient;
        public float FlatDamage;
        /// <summary>스킬 파라미터 피해 % (트라이포드, 기본 100).</summary>
        public float SkillDamagePercent;
        /// <summary>보석 피해 % (곱연산).</summary>
        public float GemDamagePercent;
        /// <summary>합연산 피해 증가 %.</summary>
        public float DamageIncrease;
        /// <summary>독립 곱연산 배율의 곱 (1 = 없음).</summary>
        public float IndependentMultiplier;
        public float CritRate;
        public float CritDamage;
        public EPositional Positional;
        public float PositionalBonus;
        public float TargetDefense;
        public float DefensePenetration;
        public float TargetDamageReduction;
        public float TargetDamageAmplify;
        /// <summary>속성 상성 배율 (1 = 없음). 보스 속성 약점.</summary>
        public float ElementMultiplier;
        /// <summary>[0,1) 치명타 판정 난수. 테스트는 고정값을 넣는다.</summary>
        public float CritRoll;
    }

    public struct DamageResult
    {
        public float Damage;
        public bool IsCrit;
    }

    /// <summary>
    /// 피해 공식 (순수 함수, 테스트 대상).
    /// 최종 = (공격력 × 계수 + 고정) × 스킬% × (1+보석%) × (1+합연산%) × 독립곱 × (1+방향%) × 치명 × (1-방어감소) × (1-받피감) × (1+받피증)
    /// 실제 게임의 곱/합 그룹 분류는 확인되지 않았다 (구현 보류 사항.md "피해 공식").
    /// </summary>
    public static class DamageCalculator
    {
        public static DamageResult Calculate(in DamageInput InInput)
        {
            var result = new DamageResult();
            float baseDamage = InInput.AttackPower * InInput.Coefficient + InInput.FlatDamage;
            if (baseDamage <= 0.0f)
            {
                return result;
            }

            float skill = (InInput.SkillDamagePercent <= 0.0f ? 100.0f : InInput.SkillDamagePercent) * 0.01f;
            float gem = 1.0f + InInput.GemDamagePercent * 0.01f;
            float increase = 1.0f + InInput.DamageIncrease * 0.01f;
            float independent = InInput.IndependentMultiplier <= 0.0f ? 1.0f : InInput.IndependentMultiplier;
            float positional = InInput.Positional == EPositional.None ? 1.0f : 1.0f + InInput.PositionalBonus * 0.01f;

            result.IsCrit = InInput.CritRoll < Mathf.Clamp(InInput.CritRate, 0.0f, 100.0f) * 0.01f;
            float crit = result.IsCrit == true ? Mathf.Max(100.0f, InInput.CritDamage) * 0.01f : 1.0f;

            float defense = 1.0f - DemoLA.Attributes.StatFormula.DefenseReduction(InInput.TargetDefense, InInput.DefensePenetration);
            float reduction = 1.0f - Mathf.Clamp(InInput.TargetDamageReduction, 0.0f, 100.0f) * 0.01f;
            float amplify = 1.0f + Mathf.Max(0.0f, InInput.TargetDamageAmplify) * 0.01f;
            float element = InInput.ElementMultiplier <= 0.0f ? 1.0f : InInput.ElementMultiplier;

            result.Damage = baseDamage * skill * gem * increase * independent * positional * crit * defense * reduction * amplify * element;
            return result;
        }
    }
}
