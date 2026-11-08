using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Core;
using Framework.Ability;

namespace DemoLA.Effects
{
    /// <summary>
    /// 코드에서 GameplayEffectDef 를 만드는 빌더 (장비 강화 수치, 각인 레벨, 보석 레벨처럼 표에서 계산되는 이펙트용).
    /// </summary>
    public class EffectBuilder
    {
        private const int RuntimeIdStart = 1000000;
        private static int NextRuntimeId = RuntimeIdStart;

        private readonly GameplayEffectDef Def = new GameplayEffectDef();
        private readonly List<ModifierDef> Modifiers = new List<ModifierDef>();

        public static int AllocateId()
        {
            return NextRuntimeId++;
        }

        public EffectBuilder(int InId, EDurationPolicy InPolicy)
        {
            Def.Id = InId;
            Def.DurationPolicy = InPolicy;
        }

        public static EffectBuilder Infinite()
        {
            return new EffectBuilder(AllocateId(), EDurationPolicy.Infinite);
        }

        public static EffectBuilder Instant()
        {
            return new EffectBuilder(AllocateId(), EDurationPolicy.Instant);
        }

        public static EffectBuilder Timed(float InDuration)
        {
            return new EffectBuilder(AllocateId(), EDurationPolicy.HasDuration).Duration(InDuration);
        }

        public EffectBuilder Duration(float InDuration)
        {
            Def.Duration = InDuration;
            return this;
        }

        public EffectBuilder Period(float InPeriod, bool InExecuteOnApply)
        {
            Def.Period = InPeriod;
            Def.ExecutePeriodicOnApply = InExecuteOnApply;
            return this;
        }

        public EffectBuilder Modifier(ModifierDef InModifier)
        {
            Modifiers.Add(InModifier);
            return this;
        }

        public EffectBuilder Add(EAttr InAttr, float InValue)
        {
            return Modifier(make((int)InAttr, EModifierOp.Add, EMagnitudeType.Scalar, 0, InValue));
        }

        public EffectBuilder Percent(EAttr InAttr, float InValue)
        {
            return Modifier(make((int)InAttr, EModifierOp.MultiplyPercent, EMagnitudeType.Scalar, 0, InValue));
        }

        public EffectBuilder SkillAdd(ESkillAttr InAttr, float InValue)
        {
            return Modifier(make((int)InAttr, EModifierOp.Add, EMagnitudeType.Scalar, 0, InValue));
        }

        public EffectBuilder SkillPercent(ESkillAttr InAttr, float InValue)
        {
            return Modifier(make((int)InAttr, EModifierOp.MultiplyPercent, EMagnitudeType.Scalar, 0, InValue));
        }

        public EffectBuilder SetByCaller(EAttr InAttr, ESetByCaller InKey)
        {
            return Modifier(make((int)InAttr, EModifierOp.Add, EMagnitudeType.SetByCaller, (int)InKey, 1.0f));
        }

        public EffectBuilder Execution(EExecution InExecution)
        {
            Def.ExecutionId = (int)InExecution;
            return this;
        }

        public EffectBuilder Grant(params ETag[] InTags)
        {
            addTags(Def.GrantedTags, InTags);
            return this;
        }

        public EffectBuilder Require(params ETag[] InTags)
        {
            addTags(Def.ApplicationRequiredTags, InTags);
            return this;
        }

        public EffectBuilder Block(params ETag[] InTags)
        {
            addTags(Def.ApplicationBlockedTags, InTags);
            return this;
        }

        public EffectBuilder RemoveWith(params ETag[] InTags)
        {
            addTags(Def.RemoveEffectsWithTags, InTags);
            return this;
        }

        public EffectBuilder Stack(EStackPolicy InPolicy, int InMaxStack)
        {
            Def.StackPolicy = InPolicy;
            Def.MaxStack = InMaxStack < 1 ? 1 : InMaxStack;
            return this;
        }

        public EffectBuilder Cue(ECue InCue)
        {
            Def.CueId = (int)InCue;
            return this;
        }

        public EffectBuilder Cooldown()
        {
            Def.IsCooldown = true;
            return this;
        }

        public GameplayEffectDef Build()
        {
            Def.Modifiers = Modifiers.ToArray();
            return Def;
        }

        private static ModifierDef make(int InAttr, EModifierOp InOp, EMagnitudeType InType, int InKey, float InValue)
        {
            var modifier = new ModifierDef();
            modifier.Attribute = InAttr;
            modifier.Op = InOp;
            modifier.MagnitudeType = InType;
            modifier.Key = InKey;
            modifier.Value = InValue;
            return modifier;
        }

        private static void addTags(GameplayTagContainer InContainer, ETag[] InTags)
        {
            if (InTags == null)
            {
                return;
            }

            for (int i = 0; i < InTags.Length; ++i)
            {
                if (InTags[i] != ETag.None)
                {
                    InContainer.Add((int)InTags[i]);
                }
            }
        }
    }
}
