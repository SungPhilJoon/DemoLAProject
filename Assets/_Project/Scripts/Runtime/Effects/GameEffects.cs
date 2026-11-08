using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Core;
using Framework.Ability;

namespace DemoLA.Effects
{
    /// <summary>
    /// 모든 액터가 공유하는 공용 이펙트 정의와 Execution 등록.
    /// 수치는 SetByCaller 로 싣고, 정의는 한 번만 만든다.
    /// </summary>
    public static class GameEffects
    {
        private static bool Initialized;
        private static readonly Dictionary<ETag, GameplayEffectDef> StatusDefs = new Dictionary<ETag, GameplayEffectDef>();

        public static GameplayEffectDef SkillDamage { get; private set; }
        public static GameplayEffectDef BossPatternDamage { get; private set; }
        public static GameplayEffectDef Heal { get; private set; }
        public static GameplayEffectDef Cooldown { get; private set; }
        public static GameplayEffectDef ManaCost { get; private set; }
        public static GameplayEffectDef DeathWard { get; private set; }
        public static GameplayEffectDef ManaRegen { get; private set; }
        public static GameplayEffectDef Shield { get; private set; }

        public static void Initialize()
        {
            if (Initialized == true)
            {
                return;
            }

            Initialized = true;
            GameTags.RegisterAll();
            ExecutionRegistry.Register((int)EExecution.SkillDamage, new SkillDamageExecution());
            ExecutionRegistry.Register((int)EExecution.BossPatternDamage, new BossPatternDamageExecution());
            ExecutionRegistry.Register((int)EExecution.Heal, new HealExecution());

            SkillDamage = EffectBuilder.Instant().Execution(EExecution.SkillDamage).Cue(ECue.Hit).Build();
            BossPatternDamage = EffectBuilder.Instant().Execution(EExecution.BossPatternDamage).Cue(ECue.Hit).Build();
            Heal = EffectBuilder.Instant().Execution(EExecution.Heal).Cue(ECue.Heal).Build();
            Cooldown = new EffectBuilder(EffectBuilder.AllocateId(), EDurationPolicy.HasDuration).Cooldown().Build();
            ManaCost = EffectBuilder.Instant().SetByCaller(EAttr.Mana, ESetByCaller.Cost).Build();
            DeathWard = EffectBuilder.Infinite().Grant(ETag.Buff_DeathWard).Stack(EStackPolicy.Refresh, 1).Build();
            Shield = EffectBuilder.Instant().SetByCaller(EAttr.Shield, ESetByCaller.Magnitude).Cue(ECue.Shield).Build();
            // 초당 마나 회복 (임시 값, 구현 보류 사항.md)
            ManaRegen = EffectBuilder.Infinite().Period(1.0f, false).Add(EAttr.Mana, 15.0f).Build();
        }

        /// <summary>태그 하나를 부여하는 시간제 상태이상 (지속시간은 Spec.DurationOverride 로).</summary>
        public static GameplayEffectDef Status(ETag InTag)
        {
            GameplayEffectDef def;
            if (StatusDefs.TryGetValue(InTag, out def) == true)
            {
                return def;
            }

            def = new EffectBuilder(EffectBuilder.AllocateId(), EDurationPolicy.HasDuration)
                .Duration(1.0f).Grant(InTag).Stack(EStackPolicy.Refresh, 1).Cue(ECue.StatusGeneric).Build();
            StatusDefs.Add(InTag, def);
            return def;
        }

        /// <summary>테스트 격리용.</summary>
        public static void ResetForTests()
        {
            Initialized = false;
            StatusDefs.Clear();
            ExecutionRegistry.Clear();
            Initialize();
        }
    }
}
