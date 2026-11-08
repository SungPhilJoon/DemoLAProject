using Framework.Ability;

namespace DemoLA.Attributes
{
    /// <summary>
    /// 스킬 하나의 파라미터. ASC 없이 AttributeSet 의 모디파이어 집계만 쓴다.
    /// 트라이포드/보석은 GameplayEffectDef 의 모디파이어를 이 세트에 핸들 단위로 붙였다 뗀다 (SkillAbility.AddRuntimeEffect).
    /// </summary>
    public class SkillAttributeSet : AttributeSet
    {
        public SkillAttributeSet() : base((int)ESkillAttr.Count)
        {
            SetBase((int)ESkillAttr.DamagePercent, 100.0f);
            SetBase((int)ESkillAttr.CooldownPercent, 100.0f);
            SetBase((int)ESkillAttr.RangePercent, 100.0f);
            SetBase((int)ESkillAttr.CastSpeedPercent, 100.0f);
            SetBase((int)ESkillAttr.StaggerPercent, 100.0f);
            SetBase((int)ESkillAttr.ManaCostPercent, 100.0f);
        }

        public float Get(ESkillAttr InAttr)
        {
            return GetCurrent((int)InAttr);
        }

        public bool GetFlag(ESkillAttr InAttr)
        {
            return GetCurrent((int)InAttr) > 0.5f;
        }

        /// <summary>시전 시간 배율 (시전 속도 % 와 캐릭터 공격 속도 % 를 함께 반영).</summary>
        public float TimeScale(float InAttackSpeedPercent)
        {
            float speed = Get(ESkillAttr.CastSpeedPercent) * 0.01f * UnityEngine.Mathf.Max(0.1f, InAttackSpeedPercent * 0.01f);
            return 1.0f / UnityEngine.Mathf.Max(0.1f, speed);
        }

        public void AddEffectModifiers(GameplayEffectDef InDef, int InHandle, float InScale)
        {
            for (int i = 0; i < InDef.Modifiers.Length; ++i)
            {
                var modifier = InDef.Modifiers[i];
                AddModifier(modifier.Attribute, InHandle, modifier.Op, modifier.Value * InScale);
            }
        }
    }
}
