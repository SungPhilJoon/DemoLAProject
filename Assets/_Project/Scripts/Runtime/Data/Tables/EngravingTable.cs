using System;
using DemoLA.Attributes;
using DemoLA.Character;
using DemoLA.Combat;
using Framework;
using Framework.Ability;

namespace DemoLA.Data
{
    /// <summary>
    /// 각인 한 행. 레벨 1~3 값은 '|' 로 구분한 배열.
    ///  - Mod1 / Mod2 : 상시 속성 모디파이어 (GameplayEffect)
    ///  - Condition : 조건부 피해 모디파이어 (DamageCondition 이 판정, SkillDamageExecution 이 적용)
    ///  - IdentityVariant : 직업 각인 — 아이덴티티 GameplayAbility 를 다른 클래스로 교체 (0 = 기본)
    /// </summary>
    [Serializable]
    public class EngravingRow : ITableRow
    {
        public int Index;
        public string Name;
        public bool IsNegative;
        public bool IsClass;
        public ECharacterClass ClassRestriction;
        public int IdentityVariant;
        public EAttr Mod1Attr;
        public EModifierOp Mod1Op;
        public float[] Mod1Values;
        public EAttr Mod2Attr;
        public EModifierOp Mod2Op;
        public float[] Mod2Values;
        public EDamageCondition Condition;
        public float ConditionParam;
        public EDamageGroup ConditionGroup;
        public float[] ConditionValues;
        public string Description;

        public int GetKey()
        {
            return Index;
        }

        public static float At(float[] InValues, int InLevel)
        {
            if (InValues == null || InValues.Length == 0 || InLevel <= 0)
            {
                return 0.0f;
            }

            return InValues[Math.Min(InLevel, InValues.Length) - 1];
        }

        public bool HasCondition
        {
            get { return ConditionValues != null && ConditionValues.Length > 0; }
        }
    }

    public class EngravingTable : TableAsset<EngravingRow>
    {
    }
}
