using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Character;
using DemoLA.Content;
using DemoLA.Core;
using NUnit.Framework;
using UnityEngine;

namespace DemoLA.Tests
{
    /// <summary>
    /// 딜량 수치 / 보스 패턴 시나리오 (문서: Unity Test Framework 로 딜량과 시나리오 확인).
    /// EditMode 에서 ASC 를 고정 간격으로 돌려 결정적으로 계산한다.
    /// </summary>
    public class ScenarioTests
    {
        private TestWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld(11);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        /// <summary>스킬 바를 순서대로 돌리며 InSeconds 동안 허수아비에게 준 피해.</summary>
        private float runRotation(ECharacterClass InClass, bool InFullBuild, float InSeconds, out int OutHits)
        {
            var character = world.Character(InClass, Vector3.zero, InFullBuild);
            var dummy = world.Boss("Gate1", Vector3.forward * 2.5f, Vector3.zero);
            using (var meter = new DamageMeter(character.Asc))
            {
                world.Simulate(InSeconds, 0.05f, InTime =>
                {
                    character.Stats.Set(EAttr.Mana, character.Stats.Get(EAttr.MaxMana));
                    if (character.IsCasting == true)
                    {
                        return;
                    }

                    for (int i = 0; i < character.Skills.Count; ++i)
                    {
                        if (character.Skills[i].Definition.IsAwakening == false && character.TryUseSlot(i, dummy.Position) == true)
                        {
                            return;
                        }
                    }
                });

                OutHits = meter.Hits;
                Debug.Log($"[DPS] {InClass} {(InFullBuild ? "풀세팅" : "맨몸")} {InSeconds}s: 총 {meter.Total:N0} / 초당 {meter.Total / InSeconds:N0} / 타격 {meter.Hits} / 치명 {meter.Crits}");
                return meter.Total;
            }
        }

        [TestCase(ECharacterClass.Summoner)]
        [TestCase(ECharacterClass.Infighter)]
        [TestCase(ECharacterClass.Slayer)]
        [TestCase(ECharacterClass.Artist)]
        public void Dps_EveryClassDealsDamage(ECharacterClass InClass)
        {
            int hits;
            float total = runRotation(InClass, true, 30.0f, out hits);
            Assert.Greater(hits, 5);
            Assert.Greater(total, 0.0f);
        }

        [Test]
        public void Dps_FullBuildBeatsBareBuild()
        {
            int hits;
            float bare = runRotation(ECharacterClass.Slayer, false, 30.0f, out hits);
            world.Dispose();
            world = new TestWorld(11);
            float full = runRotation(ECharacterClass.Slayer, true, 30.0f, out hits);
            Assert.Greater(full, bare * 1.2f, $"풀세팅 {full:N0} vs 맨몸 {bare:N0}");
        }

        [Test]
        public void Pattern_CircleAroundBoss_DealsTierDamage()
        {
            var player = world.Character(ECharacterClass.Slayer, Vector3.forward * 2.0f, false);
            var boss = world.Boss("Gate1", Vector3.zero, Vector3.forward);
            float max = player.Stats.Get(EAttr.MaxHealth);

            Assert.IsTrue(boss.TryActivatePattern("G1-11", player));
            world.Simulate(3.0f);
            float lost = max - player.Stats.Get(EAttr.Health);
            Assert.AreEqual(max * DamageTierUtil.Fraction(EDamageTier.Normal), lost, max * 0.01f);
            Assert.IsTrue(player.HasTag((int)ETag.State_Airborne) == false, "에어본은 1초 뒤 풀린다");
        }

        [Test]
        public void Pattern_OutOfRange_NoDamage()
        {
            var player = world.Character(ECharacterClass.Slayer, Vector3.forward * 15.0f, false);
            var boss = world.Boss("Gate1", Vector3.zero, Vector3.forward);
            Assert.IsTrue(boss.TryActivatePattern("G1-11", player));
            world.Simulate(3.0f);
            Assert.AreEqual(player.Stats.Get(EAttr.MaxHealth), player.Stats.Get(EAttr.Health), 0.1f);
        }

        [Test]
        public void Pattern_Counter_CancelsAndGroggies()
        {
            var player = world.Character(ECharacterClass.Infighter, Vector3.forward * 2.5f, false);
            var boss = world.Boss("Gate2", Vector3.zero, Vector3.forward);
            player.FaceTowards(boss.Position);
            DemoLA.Skills.SkillAbility counterSkill = null;
            for (int i = 0; i < player.Skills.Count; ++i)
            {
                if (player.Skills[i].IsCounterSkill == true)
                {
                    counterSkill = player.Skills[i];
                }
            }

            Assert.IsNotNull(counterSkill, "인파이터는 카운터 스킬을 가진다");

            Assert.IsTrue(boss.TryActivatePattern("G2-07", player));
            bool used = false;
            world.Simulate(4.0f, 0.05f, InTime =>
            {
                if (used == false && boss.HasTag((int)ETag.Boss_CounterWindow) == true)
                {
                    Framework.Ability.EActivateFailReason reason;
                    used = player.TryUseSkill(counterSkill, boss.Position, out reason);
                }
            });

            Assert.IsTrue(used, "카운터 구간이 열려야 한다");
            Assert.IsFalse(player.IsDead, "카운터 성공 시 즉사 돌진은 취소된다");
            Assert.IsNull(boss.ActivePattern);
        }

        [Test]
        public void Pattern_Counter_FailIsLethal()
        {
            var player = world.Character(ECharacterClass.Infighter, Vector3.forward * 2.5f, false);
            var boss = world.Boss("Gate2", Vector3.zero, Vector3.forward);
            Assert.IsTrue(boss.TryActivatePattern("G2-07", player));
            world.Simulate(4.0f);
            Assert.IsTrue(player.IsDead);
        }

        [Test]
        public void Pattern_StaggerCheck_SuccessGroggies()
        {
            var player = world.Character(ECharacterClass.Infighter, Vector3.forward * 20.0f, false);
            var boss = world.Boss("Gate2", Vector3.zero, Vector3.forward);
            Assert.IsTrue(boss.TryActivatePattern("G2-19", player));
            world.Simulate(1.0f);
            Assert.IsTrue(boss.HasTag((int)ETag.Boss_StaggerCheck));
            boss.Stats.Set(EAttr.StaggerGauge, 0.0f);
            world.Simulate(0.2f);
            Assert.IsFalse(boss.HasTag((int)ETag.Boss_StaggerCheck));
            Assert.IsTrue(boss.HasTag((int)ETag.State_Groggy));
        }

        [Test]
        public void Pattern_StaggerCheck_FromSkills()
        {
            var player = world.Character(ECharacterClass.Infighter, Vector3.forward * 2.5f, true);
            var boss = world.Boss("Gate2", Vector3.zero, Vector3.forward);
            Assert.IsTrue(boss.TryActivatePattern("G2-19", player));
            world.Simulate(8.5f, 0.05f, InTime =>
            {
                player.Stats.Set(EAttr.Mana, player.Stats.Get(EAttr.MaxMana));
                for (int i = 0; i < player.Skills.Count && player.IsCasting == false; ++i)
                {
                    player.TryUseSlot(i, boss.Position);
                }
            });

            Debug.Log($"[Stagger] 남은 무력화 게이지 {boss.Stats.Get(EAttr.StaggerGauge):0} / {boss.Stats.Get(EAttr.MaxStaggerGauge):0}");
            Assert.Less(boss.Stats.Get(EAttr.StaggerGauge), boss.Stats.Get(EAttr.MaxStaggerGauge), "스킬 무력화 수치가 게이지를 깎아야 한다");
        }

        [Test]
        public void Pattern_LethalDouble_WithDeathWard()
        {
            var player = world.Character(ECharacterClass.Artist, Vector3.forward * 4.0f, false);
            var boss = world.Boss("Gate2", Vector3.zero, Vector3.forward);
            player.ApplyEffect(DemoLA.Effects.GameEffects.DeathWard, boss.Asc);
            Assert.IsTrue(boss.TryActivatePattern("G2-03", player));
            world.Simulate(3.5f);
            Assert.IsFalse(player.IsDead, "첫 장판은 방어 구슬이 막는다");
            world.Simulate(5.0f);
            Assert.IsTrue(player.IsDead, "두 번째 장판에 사망");
        }

        [Test]
        public void HpLines_TrackHealth()
        {
            var boss = world.Boss("Gate1", Vector3.zero, Vector3.forward);
            Assert.AreEqual(boss.Definition.TotalHpLines, boss.CurrentHpLine);
            boss.Stats.Set(EAttr.Health, boss.HpPerLine * 29.5f);
            Assert.AreEqual(30, boss.CurrentHpLine);
            Assert.AreEqual(29.5f, boss.HpLinesExact, 0.001f);
        }

        [Test]
        public void ContentFromXlsx_AllPatternsLoaded()
        {
            var db = TestWorld.Database;
            var gate1 = db.GetBoss("Gate1");
            var gate2 = db.GetBoss("Gate2");
            // xlsx 패턴목록 43개 + 프로젝트 자체 추가 3개 (G2-21 휠윈드 · G2-22 3연 베기 · G2-23 도약 강타 → 추적 휠윈드).
            Assert.AreEqual(43 + 3, gate1.Patterns.Count + gate2.Patterns.Count, "xlsx 패턴목록 43개 + 자체 추가 3개");
            Assert.AreEqual(5, gate1.Phases.Count);
            Assert.AreEqual(4, gate2.Phases.Count);
            Assert.AreEqual(EPatternCategory.PartyGimmick, gate1.GetPattern("G1-01").Category);
            Assert.AreEqual("G2-18", gate2.GetPattern("G2-04").LinkedPatternId);
            foreach (var pattern in gate1.Patterns)
            {
                Assert.IsFalse(pattern.TimingsMeasured, "xlsx 에 시간 측정값이 없으므로 전부 미측정");
            }
        }

        [Test]
        public void DefaultTrees_Validate()
        {
            foreach (var tree in new[] { DefaultTrees.CreateGate1Boss(), DefaultTrees.CreateGate2Boss(), DefaultTrees.CreatePartyAI() })
            {
                var errors = tree.Validate();
                Assert.IsEmpty(errors, string.Join("\n", errors));
                Object.DestroyImmediate(tree);
            }
        }
    }
}
