using DemoLA.Attributes;
using DemoLA.Build;
using DemoLA.Character;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Effects;
using DemoLA.Skills;
using Framework.Ability;
using NUnit.Framework;
using UnityEngine;

namespace DemoLA.Tests
{
    /// <summary>AttributeSet / GameplayEffect / 트라이포드 동적 이펙트.</summary>
    public class GasTests
    {
        private TestWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void DerivedStats_FollowCombatStatPoints()
        {
            var actor = world.Combatant(ETeam.Player, Vector3.zero);
            actor.Stats.Set(EAttr.StatCrit, 1000.0f);
            Assert.AreEqual(1000.0f * StatFormula.Settings.CritRatePerPoint, actor.Stats.Get(EAttr.CritRate), 0.01f);

            actor.Stats.Set(EAttr.StatSwiftness, 1000.0f);
            Assert.AreEqual(100.0f + 1000.0f * StatFormula.Settings.AttackSpeedPerSwiftness, actor.Stats.Get(EAttr.AttackSpeed), 0.01f);
            Assert.Less(actor.Stats.CooldownScale, 1.0f);
        }

        [Test]
        public void InfiniteEffect_AddsAndRemovesModifier()
        {
            var actor = world.Combatant(ETeam.Player, Vector3.zero);
            var def = EffectBuilder.Infinite().Add(EAttr.StatCrit, 500.0f).Percent(EAttr.MaxHealth, 10.0f).Build();
            int handle = actor.ApplyEffectToSelf(def, 0);
            Assert.AreNotEqual(0, handle);
            Assert.AreEqual(110000.0f, actor.Stats.Get(EAttr.MaxHealth), 0.1f);
            Assert.Greater(actor.Stats.Get(EAttr.CritRate), 0.0f);

            actor.RemoveEffect(handle);
            Assert.AreEqual(100000.0f, actor.Stats.Get(EAttr.MaxHealth), 0.1f);
            Assert.AreEqual(0.0f, actor.Stats.Get(EAttr.CritRate), 0.001f);
        }

        [Test]
        public void SkillDamage_GoesThroughMetaAttribute_AndShield()
        {
            var source = world.Combatant(ETeam.Player, Vector3.zero, 100000.0f, 10000.0f);
            var target = world.Combatant(ETeam.Enemy, Vector3.forward, 100000.0f, 0.0f, 0.0f);
            target.Stats.Set(EAttr.Shield, 5000.0f);
            source.Stats.Set(EAttr.CritRate, 0.0f);

            var spec = source.Asc.MakeOutgoingSpec(GameEffects.SkillDamage, 1);
            spec.SetByCaller((int)ESetByCaller.Coefficient, 1.5f);
            source.Asc.ApplyGameplayEffectSpecToTarget(spec, target.Asc);

            Assert.AreEqual(0.0f, target.Stats.Get(EAttr.Shield), 0.01f);
            Assert.AreEqual(100000.0f - (15000.0f - 5000.0f), target.Stats.Get(EAttr.Health), 0.5f);
            Assert.AreEqual(0.0f, target.Stats.GetBaseValue(EAttr.IncomingDamage), "메타 속성은 0 으로 되돌린다");
        }

        [Test]
        public void IndependentDamage_MultipliesPerEffect()
        {
            var actor = world.Combatant(ETeam.Player, Vector3.zero);
            actor.ApplyEffectToSelf(EffectBuilder.Infinite().Add(EAttr.IndependentDamage, 10.0f).Build(), 0);
            actor.ApplyEffectToSelf(EffectBuilder.Infinite().Add(EAttr.IndependentDamage, 20.0f).Build(), 0);
            Assert.AreEqual(1.1f * 1.2f, SkillDamageExecution.IndependentMultiplier(actor.Asc), 0.0001f);
        }

        [Test]
        public void DeathWard_BlocksOneLethalPattern()
        {
            var boss = world.Combatant(ETeam.Enemy, Vector3.zero);
            var player = world.Combatant(ETeam.Player, Vector3.forward);
            player.ApplyEffect(GameEffects.DeathWard, boss.Asc);
            Assert.IsTrue(player.HasTag((int)ETag.Buff_DeathWard));

            for (int i = 0; i < 2; ++i)
            {
                var spec = boss.Asc.MakeOutgoingSpec(GameEffects.BossPatternDamage, 1);
                spec.SetByCaller((int)ESetByCaller.MaxHealthFraction, 1.0f);
                spec.SetByCaller((int)ESetByCaller.ConsumesDeathWard, 1.0f);
                boss.Asc.ApplyGameplayEffectSpecToTarget(spec, player.Asc);
                if (i == 0)
                {
                    Assert.IsFalse(player.IsDead, "첫 즉사기는 방어 구슬이 막는다");
                    Assert.IsFalse(player.HasTag((int)ETag.Buff_DeathWard), "방어 구슬은 소모된다");
                }
            }

            Assert.IsTrue(player.IsDead, "두 번째 즉사기에는 사망");
        }

        [Test]
        public void SuperArmor_BlocksHardCrowdControl()
        {
            var actor = world.Combatant(ETeam.Player, Vector3.zero);
            actor.AddTag((int)ETag.State_SuperArmor);
            Assert.AreEqual(0, actor.ApplyStatus(ETag.State_Airborne, 1.0f, null));
            actor.RemoveTag((int)ETag.State_SuperArmor);
            Assert.AreNotEqual(0, actor.ApplyStatus(ETag.State_Airborne, 1.0f, null));
            Assert.IsTrue(actor.IsIncapacitated);
        }

        [Test]
        public void Tripod_DynamicEffect_ChangesSkillParams_AndRemoves()
        {
            var character = world.Character(ECharacterClass.Slayer, Vector3.zero, false);
            var skill = character.Skills[0];
            float baseCooldown = skill.EffectiveCooldown;

            int damage = skill.AddRuntimeEffect(EffectBuilder.Infinite().SkillPercent(ESkillAttr.DamagePercent, 30.0f).Build(), ETripodTiming.SkillParameter, 1.0f);
            int cooldown = skill.AddRuntimeEffect(EffectBuilder.Infinite().SkillPercent(ESkillAttr.CooldownPercent, -10.0f).Build(), ETripodTiming.SkillParameter, 2.0f);
            Assert.AreEqual(130.0f, skill.Params.Get(ESkillAttr.DamagePercent), 0.01f);
            Assert.AreEqual(baseCooldown * 0.8f, skill.EffectiveCooldown, 0.01f, "트라이포드 레벨 배율 2 → -20%");

            skill.RemoveRuntimeEffect(damage);
            skill.RemoveRuntimeEffect(cooldown);
            Assert.AreEqual(100.0f, skill.Params.Get(ESkillAttr.DamagePercent), 0.01f);
            Assert.AreEqual(baseCooldown, skill.EffectiveCooldown, 0.01f);
        }

        [Test]
        public void Tripod_OwnerWhileSelected_AppliesToCharacter()
        {
            var character = world.Character(ECharacterClass.Slayer, Vector3.zero, false);
            float before = character.Stats.Get(EAttr.CritDamage);
            int handle = character.Skills[0].AddRuntimeEffect(EffectBuilder.Infinite().Add(EAttr.CritDamage, 10.0f).Build(), ETripodTiming.OwnerWhileSelected, 1.0f);
            Assert.AreEqual(before + 10.0f, character.Stats.Get(EAttr.CritDamage), 0.01f);
            character.Skills[0].RemoveRuntimeEffect(handle);
            Assert.AreEqual(before, character.Stats.Get(EAttr.CritDamage), 0.01f);
        }

        [Test]
        public void FullBuild_IncreasesAttackPower_AndAppliesGemsAndTripods()
        {
            var bare = world.Character(ECharacterClass.Slayer, Vector3.zero, false);
            var full = world.Character(ECharacterClass.Slayer, Vector3.right * 5.0f, true);
            Assert.Greater(full.Stats.Get(EAttr.AttackPower), bare.Stats.Get(EAttr.AttackPower));
            Assert.Greater(full.Stats.Get(EAttr.CritRate), bare.Stats.Get(EAttr.CritRate));

            var result = full.LastBuildResult;
            Assert.Greater(result.CharacterEffects.Count, 3);
            Assert.Greater(result.SkillEffects.Count, 0);
            Assert.Greater(result.EngravingLevels.Count, 0);
            Assert.Greater(full.Skills[0].Params.Get(ESkillAttr.GemDamagePercent), 0.0f, "보석 피해가 첫 스킬에 붙어야 한다");
        }

        [Test]
        public void Rebuild_DoesNotStackEffects()
        {
            var character = world.Character(ECharacterClass.Summoner, Vector3.zero, true);
            float attack = character.Stats.Get(EAttr.AttackPower);
            int effects = character.Effects.Count;
            character.ApplyBuild(character.Build);
            character.ApplyBuild(character.Build);
            Assert.AreEqual(attack, character.Stats.Get(EAttr.AttackPower), 0.5f);
            Assert.AreEqual(effects, character.Effects.Count);
        }

        [Test]
        public void Skill_Cooldown_And_ManaCost_Applied()
        {
            var character = world.Character(ECharacterClass.Infighter, Vector3.zero, false);
            var target = world.Combatant(ETeam.Enemy, Vector3.forward * 1.5f, 1e9f);
            var skill = character.Skills[0];
            float mana = character.Stats.Get(EAttr.Mana);
            EActivateFailReason reason;
            Assert.IsTrue(character.TryUseSkill(skill, target.Position, out reason));
            Assert.Less(character.Stats.Get(EAttr.Mana), mana);
            Assert.Greater(character.GetCooldownRemaining(skill), 0.0f);

            world.Simulate(2.0f);
            Assert.IsFalse(skill.IsActive);
            Assert.Less(target.Stats.Get(EAttr.Health), 1e9f, "근접 스킬이 대상에 맞아야 한다");
            Assert.IsFalse(character.TryUseSkill(skill, target.Position, out reason));
            Assert.AreEqual(EActivateFailReason.Cooldown, reason);
        }
    }
}
