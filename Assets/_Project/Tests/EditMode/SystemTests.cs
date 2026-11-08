using System.Linq;
using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Build;
using DemoLA.Character;
using DemoLA.Character.Identity;
using DemoLA.Combat;
using DemoLA.Content;
using DemoLA.Core;
using DemoLA.Data;
using DemoLA.Player;
using DemoLA.Tools;
using NUnit.Framework;
using UnityEngine;

namespace DemoLA.Tests
{
    /// <summary>2차 구현 (구현 보류 사항 답변) 검증.</summary>
    public class SystemTests
    {
        private TestWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld(21);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        // ------------------------------------------------------------------ data

        [Test]
        public void Tables_AllCsvParse_WithoutErrors()
        {
            string errors;
            var tables = DemoLATables.FromCsv(TablePaths.ReadCsv, out errors);
            Assert.IsEmpty(errors);
            // xlsx 43개 + 자체 추가 3개 (BossPatternTable.csv 의 '자체 추가' 행).
            Assert.AreEqual(43 + 3, tables.BossPattern.Count);
            Assert.AreEqual(9, tables.BossPhase.Count);
            Assert.Greater(tables.Engraving.Count, 20);
            Assert.AreEqual(10, tables.Gem.Count);
            Assert.Greater(tables.BattleItem.Count, 3);
            Assert.AreEqual(9, tables.BossPhase.All.First(p => p.PhaseId == "G2-P2").PatternIds.Length, "xlsx G2-P2 패턴 9개 ('|' 배열)");
        }

        [Test]
        public void MeasuredRow_OverridesPresetTimings()
        {
            var row = new BossPatternRow { PatternId = "G1-11", Name = "t", Category = "일반 공격", TelegraphTime = 2.0f, Recovery = 0.4f, AreaScale = 2.0f, Measured = true };
            var pattern = BossPatternCsv.CreatePattern(row);
            PatternPresets.Apply(pattern);
            float radius = pattern.Steps[0].Area.Radius;
            BossPatternCsv.ApplyMeasured(pattern, row);
            Assert.AreEqual(2.0f, pattern.Steps[0].TelegraphTime, 0.001f);
            Assert.AreEqual(0.4f, pattern.Recovery, 0.001f);
            Assert.AreEqual(radius * 2.0f, pattern.Steps[0].Area.Radius, 0.001f);
            Assert.IsTrue(pattern.TimingsMeasured);
            Object.DestroyImmediate(pattern);
        }

        [Test]
        public void InputAsset_HasEveryGameplayAction()
        {
            var asset = InputBindings.CreateDefaultAsset();
            var map = asset.FindActionMap(InputBindings.GameplayMap);
            foreach (var entry in InputBindings.DefaultBindings)
            {
                Assert.IsNotNull(map.FindAction(entry.Action), entry.Action);
            }

            Object.DestroyImmediate(asset);
        }

        // ------------------------------------------------------------------ build rules

        [Test]
        public void Gems_NoDuplicates_NoAwakening()
        {
            var db = TestWorld.Database;
            var cls = db.GetClass(ECharacterClass.Slayer);
            var build = new CharacterBuildData { Class = ECharacterClass.Slayer };
            build.Gems.Add(new GemSlotState { Type = EGemType.Damage, Level = 10, SkillId = cls.Skills[0].SkillId });
            build.Gems.Add(new GemSlotState { Type = EGemType.Damage, Level = 10, SkillId = cls.Skills[0].SkillId });
            build.Gems.Add(new GemSlotState { Type = EGemType.Cooldown, Level = 10, SkillId = cls.Awakenings[0].SkillId });
            var result = BuildCalculator.Compute(build, db);
            Assert.AreEqual(1, result.SkillEffects.Count(e => e.Label.Contains("보석")), "중복 · 각성기 보석은 무시");
            Assert.AreEqual(2, result.Warnings.Count);
        }

        [Test]
        public void ConditionalEngraving_AppliesOnlyWhenConditionMet()
        {
            var character = world.Character(ECharacterClass.Slayer, Vector3.zero, false);
            var boss = world.Boss("Gate1", Vector3.forward * 2.0f, Vector3.zero);
            var context = new DamageContext { Source = character, Target = boss, Positional = EPositional.None };
            character.DamageModifiers.Add(new DamageModifierEntry { Condition = EDamageCondition.BackAttack, Group = EDamageGroup.Independent, Value = 20.0f });
            character.DamageModifiers.Add(new DamageModifierEntry { Condition = EDamageCondition.TargetIsBoss, Group = EDamageGroup.Independent, Value = 10.0f });

            float independent = 1.0f;
            float additive = 0.0f;
            float critRate = 0.0f;
            float critDamage = 0.0f;
            DamageConditions.Accumulate(character.DamageModifiers, context, ref independent, ref additive, ref critRate, ref critDamage);
            Assert.AreEqual(1.1f, independent, 0.0001f, "보스 대상만 충족");

            context.Positional = EPositional.BackAttack;
            independent = 1.0f;
            DamageConditions.Accumulate(character.DamageModifiers, context, ref independent, ref additive, ref critRate, ref critDamage);
            Assert.AreEqual(1.1f * 1.2f, independent, 0.0001f, "백어택 + 보스 모두 충족 → 곱연산");
        }

        [Test]
        public void ClassEngraving_SwapsIdentityAbilityClass()
        {
            var db = TestWorld.Database;
            var character = world.Character(ECharacterClass.Slayer, Vector3.zero, false);
            Assert.IsInstanceOf<SlayerBurstAbility>(character.Identity);

            var build = new CharacterBuildData { Class = ECharacterClass.Slayer };
            build.Books.Add(new EngravingBookState { EngravingId = 122, Points = 12 });
            build.Accessories.Add(new AccessoryState { EngravingA = 122, PointsA = 3 });
            character.ApplyBuild(build);
            Assert.IsInstanceOf<SlayerExecutionAbility>(character.Identity, "직업 각인 2 → 처단 어빌리티");
        }

        [Test]
        public void CardSet_ConvertsElement_AndBossIsWeakToHoly()
        {
            var db = TestWorld.Database;
            var build = DefaultContent.CreateDefaultBuild(db, ECharacterClass.Slayer);
            var result = BuildCalculator.Compute(build, db);
            Assert.AreEqual(EElement.Holy, result.AttackElement, "기본 빌드 = 성속성 변환 세트 6장 각성 30");
            Assert.Greater(db.GetBoss("Gate1").ElementMultiplier(EElement.Holy), 1.0f);
            Assert.AreEqual(1.0f, db.GetBoss("Gate1").ElementMultiplier(EElement.Dark));
        }

        [Test]
        public void Awakening_TwoChoices_Selectable()
        {
            var character = world.Character(ECharacterClass.Infighter, Vector3.zero, false);
            var first = character.Awakening.Definition;
            var build = character.Build.Clone();
            build.SelectedAwakening = 1;
            character.ApplyBuild(build);
            Assert.AreNotEqual(first, character.Awakening.Definition);
            Assert.AreEqual(1, character.Skills.Count(s => s.Definition.IsAwakening), "각성기는 하나만 부여");
        }

        // ------------------------------------------------------------------ abilities

        [Test]
        public void Identity_OrbsFromGauge_AndConsume()
        {
            var artist = world.Character(ECharacterClass.Artist, Vector3.zero, false);
            Assert.IsFalse(artist.Identity.HasResource);
            artist.Stats.Set(EAttr.IdentityGauge, 100.0f);
            artist.Identity.TickResource();
            Assert.AreEqual(1, artist.Identity.Orbs);
            Assert.IsTrue(artist.TryUseIdentity(artist.Position));
            Assert.AreEqual(0, artist.Identity.Orbs);
        }

        [Test]
        public void BattleItem_CountAndCooldown()
        {
            var character = world.Character(ECharacterClass.Slayer, Vector3.zero, false);
            var potion = character.BattleItems[0];
            Assert.AreEqual(EBattleItemType.HealPotion, potion.Row.Type);
            character.Stats.Set(EAttr.Health, character.Stats.Get(EAttr.MaxHealth) * 0.5f);
            Assert.IsTrue(character.TryUseBattleItem(0, character.Position));
            Assert.Greater(character.Stats.HealthRatio, 0.5f);
            Assert.AreEqual(potion.Row.MaxCount - 1, potion.Remaining);
            Assert.IsFalse(character.TryUseBattleItem(0, character.Position), "쿨다운 중");
        }

        [Test]
        public void BattleItem_TimeStop_GrantsInvulnerability()
        {
            var character = world.Character(ECharacterClass.Slayer, Vector3.zero, false);
            int slot = character.BattleItems.ToList().FindIndex(i => i.Row.Type == EBattleItemType.TimeStop);
            Assert.GreaterOrEqual(slot, 0);
            Assert.IsTrue(character.TryUseBattleItem(slot, character.Position));
            Assert.IsTrue(character.HasTag((int)ETag.State_Invulnerable));
            Assert.IsTrue(character.HasTag((int)ETag.State_Frozen));
        }

        [Test]
        public void Dodge_GrantsInvulnerability()
        {
            var character = world.Character(ECharacterClass.Slayer, Vector3.zero, false);
            character.ApplyStatus(ETag.State_Airborne, 2.0f, null);
            Assert.IsTrue(character.TryDodge(Vector3.forward), "공중에서도 기상기로 사용");
            Assert.IsTrue(character.HasTag((int)ETag.State_Invulnerable));
            Assert.IsFalse(character.HasTag((int)ETag.State_Airborne));
        }

        // ------------------------------------------------------------------ boss

        [Test]
        public void PartyScale_FourPlayers_HalvesGimmickCounts()
        {
            var go = new GameObject("ScaleBoss");
            go.AddComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
            go.AddComponent<ActorMotor>();
            var orbs = go.AddComponent<OrbAlternationGimmick>();
            orbs.GimmickId = "G1-01";
            var boss = go.AddComponent<BossActor>();
            var def = TestWorld.Database.GetBoss("Gate1");
            boss.Initialize(def, 4);
            Assert.AreEqual(0.5f, boss.PartyScale, 0.001f);
            Assert.AreEqual(4, orbs.OrbCount);
            Assert.AreEqual(def.HpPerLine * 0.5f, boss.HpPerLine, 1.0f);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Aggro_NearestChance_Distribution()
        {
            var boss = world.Boss("Gate1", Vector3.zero, Vector3.forward);
            var near = world.Combatant(ETeam.Player, Vector3.forward * 2.0f);
            world.Combatant(ETeam.Player, Vector3.forward * 20.0f);
            world.Combatant(ETeam.Player, Vector3.back * 20.0f);
            int nearCount = 0;
            const int rolls = 600;
            for (int i = 0; i < rolls; ++i)
            {
                boss.Retarget();
                nearCount += boss.AggroTarget == near ? 1 : 0;
            }

            // 기대값: 0.7 + 0.3 × (1/3) = 0.8
            Assert.AreEqual(0.8f, nearCount / (float)rolls, 0.06f);
        }

        [Test]
        public void Aggro_Taunt_Overrides()
        {
            var boss = world.Boss("Gate1", Vector3.zero, Vector3.forward);
            world.Combatant(ETeam.Player, Vector3.forward * 2.0f);
            var far = world.Combatant(ETeam.Player, Vector3.forward * 20.0f);
            boss.Taunt(far, 5.0f);
            Assert.AreEqual(far, boss.AggroTarget);
        }

        // ------------------------------------------------------------------ tools

        [Test]
        public void Simulator_IsDeterministic_AndBackAttackHelpsSlayer()
        {
            var db = TestWorld.Database;
            var front = new SimulationConfig { Class = ECharacterClass.Slayer, Duration = 20.0f, Trials = 2, Seed = 5 };
            var again = CombatSimulator.Run(db, front);
            var first = CombatSimulator.Run(db, front);
            Assert.AreEqual(first.MeanDps, again.MeanDps, 0.01f, "같은 시드 = 같은 결과");

            var behind = new SimulationConfig { Class = ECharacterClass.Slayer, Duration = 20.0f, Trials = 2, Seed = 5, FromBehind = true };
            var back = CombatSimulator.Run(db, behind);
            Debug.Log(CombatSimulator.Describe(first) + CombatSimulator.Describe(back));
            Assert.Greater(back.BackRate, 0.0f);
            Assert.Greater(back.MeanDps, first.MeanDps);
        }
    }
}
