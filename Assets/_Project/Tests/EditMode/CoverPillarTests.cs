using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.World;
using NUnit.Framework;
using UnityEngine;

namespace DemoLA.Tests
{
    /// <summary>Pattern.G2.PillarCoverDrop 기둥 엄폐 낙하 · 진입 시네마틱 정지.</summary>
    public class CoverPillarTests
    {
        private TestWorld world;
        private BossActor boss;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld(23);
            boss = world.Boss("Gate2", Vector3.zero, Vector3.forward);
        }

        [TearDown]
        public void TearDown()
        {
            CoverPillars.Clear();
            CinematicHold.Release();
            world.Dispose();
        }

        [Test]
        public void Pillars_At12_3_6_9_AndCoverIsBehindPillar()
        {
            var positions = CoverPillars.Positions(Vector3.zero, 10.0f);
            Assert.AreEqual(new Vector3(0, 0, 10), positions[0], "12시");
            Assert.AreEqual(new Vector3(10, 0, 0), positions[1], "3시");
            Assert.AreEqual(new Vector3(0, 0, -10), positions[2], "6시");
            Assert.AreEqual(new Vector3(-10, 0, 0), positions[3], "9시");
            CoverPillars.Spawn(positions);
            Assert.AreEqual(4, CoverPillars.Pillars.Count);
            Assert.IsTrue(CoverPillars.IsCovered(Vector3.zero, new Vector3(0, 0, 13)), "12시 기둥 뒤");
            Assert.IsFalse(CoverPillars.IsCovered(Vector3.zero, new Vector3(0, 0, 8)), "기둥 앞");
            Assert.IsFalse(CoverPillars.IsCovered(Vector3.zero, new Vector3(5, 0, 13)), "기둥 옆");
        }

        [Test]
        public void G2_27_Definition()
        {
            var pattern = boss.Definition.GetPattern(ETag.Pattern_G2_PillarCoverDrop);
            Assert.IsNotNull(pattern);
            Assert.AreEqual(7.0f, pattern.Steps[0].StartLeapDuration, 0.01f, "7초 도약");
            Assert.IsTrue(pattern.Steps[0].SpawnCoverPillars);
            Assert.AreEqual(7.0f, pattern.Steps[0].TelegraphTime + pattern.Steps[1].TelegraphTime, 0.01f, "착지 순간 충격파");
            Assert.IsTrue(pattern.Steps[1].BlockedByCover);
            Assert.AreEqual(EDamageTier.Lethal, pattern.Steps[1].DamageTier);
            var fan = pattern.Steps[2];
            Assert.AreEqual(EHitShape.Fan, fan.Area.Shape);
            Assert.AreEqual(25.0f, fan.Area.Angle, 0.01f, "부채꼴 약 25도");
            Assert.AreEqual(5.5f, fan.TelegraphTime, 0.01f, "5초 따라가기 + 0.5초 정지");
            Assert.IsTrue(fan.TrackDuringTelegraph);
            Assert.AreEqual(0.5f, fan.TrackLockBefore, 0.01f);
            Assert.IsTrue(fan.ExplodesCover);
            Assert.IsInstanceOf<RandomSelector>(pattern.TargetSelector, "무작위 대상");
        }

        [Test]
        public void Blast_KillsExposed_SparesHidden()
        {
            var hidden = world.Combatant(ETeam.Player, new Vector3(0, 0, 12.5f), 1000000.0f);
            var exposed = world.Combatant(ETeam.Player, new Vector3(6, 0, 6), 1000000.0f);
            Assert.IsTrue(boss.TryActivatePattern(ETag.Pattern_G2_PillarCoverDrop, null));
            world.Simulate(7.2f, 0.02f);
            Assert.AreEqual(4, CoverPillars.Pillars.Count, "기둥 4개");
            Assert.IsFalse(hidden.IsDead, "기둥 뒤는 무사");
            Assert.IsTrue(exposed.IsDead, "기둥 뒤가 아니면 즉사");
        }

        [Test]
        public void IntroHold_StopsAiAndDelaysFirstPattern()
        {
            CinematicHold.Hold(5.0f);
            Assert.IsTrue(CinematicHold.Active);
            boss.DelayFirstPattern(5.0f);
            Assert.IsFalse(boss.IsPatternGapElapsed, "연출 동안 첫 패턴 대기");
            CinematicHold.Release();
            Assert.IsFalse(CinematicHold.Active);
            Assert.IsNotEmpty(boss.Definition.IntroLines, "진입 대사");
            Assert.IsTrue(boss.Definition.PlayIntro);
        }
    }
}
