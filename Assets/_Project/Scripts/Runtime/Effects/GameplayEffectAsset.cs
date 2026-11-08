using System;
using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Core;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Effects
{
    [Serializable]
    public struct EffectModifierEntry
    {
        [Tooltip("체크하면 SkillAttribute(스킬 파라미터)를, 아니면 Attribute(캐릭터 속성)를 바꾼다")]
        public bool IsSkillAttribute;
        public EAttr Attribute;
        public ESkillAttr SkillAttribute;
        public EModifierOp Op;
        public EMagnitudeType MagnitudeType;
        [Tooltip("SetByCaller 키 또는 Source/Target 속성 인덱스")]
        public int Key;
        public float Value;

        public static EffectModifierEntry Char(EAttr InAttr, EModifierOp InOp, float InValue)
        {
            var entry = new EffectModifierEntry();
            entry.Attribute = InAttr;
            entry.Op = InOp;
            entry.Value = InValue;
            return entry;
        }

        public static EffectModifierEntry Skill(ESkillAttr InAttr, EModifierOp InOp, float InValue)
        {
            var entry = new EffectModifierEntry();
            entry.IsSkillAttribute = true;
            entry.SkillAttribute = InAttr;
            entry.Op = InOp;
            entry.Value = InValue;
            return entry;
        }

        public ModifierDef ToDef()
        {
            var def = new ModifierDef();
            def.Attribute = IsSkillAttribute == true ? (int)SkillAttribute : (int)Attribute;
            def.Op = Op;
            def.MagnitudeType = MagnitudeType;
            def.Key = Key;
            def.Value = Value;
            return def;
        }
    }

    /// <summary>
    /// 에디터에서 만드는 GameplayEffect 정의 (언리얼 UGameplayEffect 블루프린트 대응).
    /// 런타임에는 GetDef() 로 프레임워크의 GameplayEffectDef 를 한 번 만들어 공유한다.
    /// 장비/각인/카드/보석/트라이포드/보스 디버프가 모두 이 에셋 또는 EffectBuilder 로 만든 정의를 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "DemoLA/GAS/Gameplay Effect", fileName = "GE_New")]
    public class GameplayEffectAsset : ScriptableObject
    {
        [Tooltip("0 이면 런타임에 자동 할당")]
        public int Id;
        [TextArea] public string Description;
        public EDurationPolicy DurationPolicy = EDurationPolicy.Infinite;
        public float Duration;
        public float Period;
        public bool ExecutePeriodicOnApply;
        public List<EffectModifierEntry> Modifiers = new List<EffectModifierEntry>();
        public EExecution Execution;
        public ETag[] GrantedTags = new ETag[0];
        public ETag[] ApplicationRequiredTags = new ETag[0];
        public ETag[] ApplicationBlockedTags = new ETag[0];
        public ETag[] RemoveEffectsWithTags = new ETag[0];
        public EStackPolicy StackPolicy;
        public int MaxStack = 1;
        public ECue Cue;

        [NonSerialized] private GameplayEffectDef CachedDef;

        public GameplayEffectDef GetDef()
        {
            if (CachedDef != null)
            {
                return CachedDef;
            }

            var builder = new EffectBuilder(Id != 0 ? Id : EffectBuilder.AllocateId(), DurationPolicy);
            builder.Duration(Duration).Period(Period, ExecutePeriodicOnApply).Execution(Execution)
                .Grant(GrantedTags).Require(ApplicationRequiredTags).Block(ApplicationBlockedTags)
                .RemoveWith(RemoveEffectsWithTags).Stack(StackPolicy, MaxStack).Cue(Cue);
            for (int i = 0; i < Modifiers.Count; ++i)
            {
                builder.Modifier(Modifiers[i].ToDef());
            }

            CachedDef = builder.Build();
            return CachedDef;
        }

        private void OnValidate()
        {
            CachedDef = null;
        }
    }
}
