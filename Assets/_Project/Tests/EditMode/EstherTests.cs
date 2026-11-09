using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Character;
using DemoLA.Core;
using NUnit.Framework;
using UnityEngine;

namespace DemoLA.Tests
{
    /// <summary>에스더: 게이지 · 관문별 사용 가능 · 세 종류 효과.</summary>
    public class EstherTests
    {
        private TestWorld world;
        private BossActor boss;
        private TestCombatant player;
        private TestCombatant ally;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld(9);
            EstherSystem.IsReplica = false;
            EstherSystem.Configure(null, 12.0f);
            boss = world.Boss("Gate2", Vector3.zero, Vector3.forward);
            EstherSystem.Reset("Gate2");
            player = world.Combatant(ETeam.Player, Vector3.forward * 3.0f, 1000000.0f, 100000.0f);
            ally = world.Combatant(ETeam.Player, Vector3.back * 3.0f, 1000000.0f);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
            EstherSystem.Reset(null);
        }

        [Test]
        public void ThreeEsthers_AvailableInBothGates()
        {
            EstherSystem.Reset("Gate1");
            Assert.AreEqual(3, EstherSystem.Available.Count, "1관문");
            EstherSystem.Reset("Gate2");
            Assert.AreEqual(3, EstherSystem.Available.Count, "2관문");
        }

        [Test]
        public void Gauge_FillsFromBossDamage_AndCannotUseUntilFull()
        {
            Assert.IsFalse(player.Asc != null && EstherSystem.TryUse(player, 0), "게이지 0 이면 못 씀");
            EstherSystem.AddBossDamage(boss, boss.Definition.HpPerLine * 6.0f);
            Assert.AreEqual(50.0f, EstherSystem.Gauge, 0.01f, "12줄 중 6줄 = 50%");
            EstherSystem.AddBossDamage(boss, boss.Definition.HpPerLine * 100.0f);
            Assert.IsTrue(EstherSystem.IsFull);
        }

        [Test]
        public void Strike_DamagesBoss_BuffsParty_AndEmptiesGauge()
        {
            EstherSystem.SetGauge(EstherSystem.Max);
            float before = boss.Stats.Get(EAttr.Health);
            float allyIncrease = ally.Stats.Get(EAttr.DamageIncrease);
            Assert.IsTrue(EstherSystem.TryUse(player, 0));
            float lines = (before - boss.Stats.Get(EAttr.Health)) / boss.Definition.HpPerLine;
            Assert.AreEqual(EstherSystem.Available[0].DamageHpLines, lines, 0.05f, "고정 피해 = 설정한 체력 줄 수 (방어 · 배율 무시)");
            Assert.Greater(ally.Stats.Get(EAttr.DamageIncrease), allyIncrease, "파티 피해 증가 버프");
            Assert.AreEqual(0.0f, EstherSystem.Gauge, "쓰면 게이지 0");
        }

        [Test]
        public void Break_CountersDuringCounterWindow()
        {
            Assert.IsTrue(boss.TryActivatePattern("G2-24", player));
            float elapsed = 0.0f;
            while (boss.HasTag((int)ETag.Boss_CounterWindow) == false && elapsed < 6.0f)
            {
                world.Simulate(0.02f, 0.02f);
                elapsed += 0.02f;
            }

            Assert.IsTrue(boss.HasTag((int)ETag.Boss_CounterWindow), "마지막 강타의 카운터 구간이 열린다");
            EstherSystem.SetGauge(EstherSystem.Max);
            Assert.IsTrue(EstherSystem.TryUse(player, 1));
            Assert.IsTrue(boss.HasTag((int)ETag.State_Groggy), "카운터 성공 → 그로기");
        }

        [Test]
        public void Guard_MakesPartyInvulnerable()
        {
            EstherSystem.SetGauge(EstherSystem.Max);
            Assert.IsTrue(EstherSystem.TryUse(player, 2));
            Assert.IsTrue(player.HasTag((int)ETag.State_Invulnerable));
            Assert.IsTrue(ally.HasTag((int)ETag.State_Invulnerable));
        }
    }
}
