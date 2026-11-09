using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace DemoLA.Tests
{
    /// <summary>2관문 갑옷 부위파괴 · 천상 낙하 반격 · 돌진 순서 · 재도약.</summary>
    public class Gate2ArmorTests
    {
        private TestWorld world;
        private readonly List<GameObject> extra = new List<GameObject>();
        private BossActor boss;
        private ArmorStackGimmick armor;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld(17);
            // 기믹 컴포넌트는 Initialize 전에 붙어 있어야 묶인다 (RaidSpawner.AttachBoss 와 같은 순서).
            var go = new GameObject("Test_Gate2_Armor");
            extra.Add(go);
            go.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            go.AddComponent<NavMeshAgent>();
            go.AddComponent<ActorMotor>();
            armor = go.AddComponent<ArmorStackGimmick>();
            armor.GimmickId = "G2-01";
            armor.PatternId = "G2-01";
            boss = go.AddComponent<BossActor>();
            var definition = TestWorld.Database.GetBoss("Gate2");
            boss.Initialize(definition, definition.DesignPartySize);
            world.Combatant(ETeam.Player, Vector3.forward * 4.0f, 100000000.0f);
            Assert.IsTrue(boss.StartGimmick("G2-01"));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in extra)
            {
                Object.DestroyImmediate(go);
            }

            extra.Clear();
            world.Dispose();
        }

        private void addDestruction(float InAmount)
        {
            boss.Stats.Set(EAttr.DestructionAccum, boss.Stats.GetBaseValue(EAttr.DestructionAccum) + InAmount);
            armor.Tick(0.02f);
        }

        private ArenaStructure wall()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            extra.Add(go);
            var structure = go.AddComponent<ArenaStructure>();
            structure.Kind = EStructureKind.Wall;
            return structure;
        }

        [Test]
        public void WithoutCrash_PartBreakIsSlow()
        {
            addDestruction(armor.DestructionPerStack * 0.4f);
            Assert.AreEqual(2, armor.RemainingStacks);
            Assert.AreEqual(0.4f, armor.Progress, 0.01f, "평소 계수 ×1");
        }

        [Test]
        public void ChargeCrashIntoWall_Groggy_AndWeakensArmor()
        {
            Assert.IsTrue(boss.TryActivatePattern("G2-11", null));
            armor.NotifyChargeCrashed(wall());
            Assert.IsTrue(boss.HasTag((int)ETag.State_Groggy), "벽에 부딪혀도 그로기");
            Assert.IsTrue(armor.IsWeakened, "충돌 그로기 = 부위파괴 계수 감소");

            // 평소라면 40% 인 수치가 ×2.5 로 들어가 갑옷 1중첩이 깨진다.
            addDestruction(armor.DestructionPerStack * 0.4f);
            Assert.AreEqual(1, armor.RemainingStacks);
            Assert.IsFalse(boss.HasTag((int)ETag.State_Groggy), "부위파괴 → 그로기 해제");
            Assert.IsNotNull(boss.ActivePattern);
            Assert.AreEqual("G2-25", boss.ActivePattern.Definition.PatternId, "부위파괴 → 천상 낙하 반격");
        }

        [Test]
        public void Skyfall_TenSeconds_LethalExceptOuterBand_ThenChargeNext()
        {
            var skyfall = boss.Definition.GetPattern("G2-25");
            Assert.AreEqual(10.0f, skyfall.RepeatDuration(), 0.01f, "10초 동안 공중");
            Assert.AreEqual(10.0f, skyfall.Steps[0].StartLeapDuration, 0.01f);
            int bursts = skyfall.Steps.FindAll(s => s.Targeting == EPatternTargeting.EachPlayer).Count;
            Assert.AreEqual(8, bursts, "1초마다 플레이어 위치 폭발");
            var crash = skyfall.Steps[skyfall.Steps.Count - 1];
            Assert.AreEqual(EDamageTier.Lethal, crash.DamageTier);
            Assert.IsTrue(crash.BigImpact);
            float wallInner = ArenaBuilder.WallRingRadius - ArenaBuilder.WallThickness * 0.5f;
            Assert.AreEqual(wallInner - 1.5f, crash.Area.Radius, 0.01f, "다닐 수 있는 가장자리(벽 안쪽) − 캐릭터 크기 1.5배");
            Assert.IsTrue(HitGeometry.Contains(crash.Area, Vector3.zero, Vector3.forward, new Vector3(16.0f, 0.0f, 0.0f), 0.5f), "맵 안쪽은 즉사 범위");
            Assert.IsFalse(HitGeometry.Contains(crash.Area, Vector3.zero, Vector3.forward, new Vector3(wallInner - 0.6f, 0.0f, 0.0f), 0.5f), "벽 안쪽 가장자리 띠는 안전");

            Assert.IsTrue(boss.TryActivatePattern("G2-25", null));
            boss.NotifyPatternEnded(boss.ActivePattern, false);
            Assert.GreaterOrEqual(boss.PatternsSinceCharge, 3, "착지 후 다음 패턴은 돌진");
        }

        [Test]
        public void ChargeCadence_ThreeOtherPatterns_ReLeapNotCounted()
        {
            Assert.GreaterOrEqual(boss.PatternsSinceCharge, 3, "조우 직후 돌진");
            boss.NotifyPatternEnded(boss.GetPattern("G2-11"), false);
            Assert.AreEqual(0, boss.PatternsSinceCharge);
            boss.NotifyPatternEnded(boss.GetPattern("G2-13"), false);
            boss.NotifyPatternEnded(boss.GetPattern("G2-26"), false);
            boss.NotifyPatternEnded(boss.GetPattern("G2-21"), false);
            Assert.AreEqual(2, boss.PatternsSinceCharge, "재도약은 세지 않는다");
            boss.NotifyPatternEnded(boss.GetPattern("G2-22"), false);
            Assert.AreEqual(3, boss.PatternsSinceCharge, "다른 패턴 3개 → 돌진 차례");
            Assert.AreEqual("G2-22", boss.LastEndedPatternId);
            Assert.IsTrue(boss.Definition.GetPattern("G2-07").IsCharge, "135줄 이후 돌진(G2-07)도 돌진으로 센다");
            Assert.IsTrue(boss.Definition.GetPattern("G2-07").IsCounterable, "135줄 이후 돌진은 카운터 가능");
        }

        [Test]
        public void Below135Lines_NoMorePartBreak()
        {
            float perLine = boss.Stats.Get(EAttr.MaxHealth) / boss.Definition.TotalHpLines;
            boss.Stats.Set(EAttr.Health, perLine * 134.5f);
            armor.Tick(0.02f);
            Assert.AreNotEqual(EGimmickState.Running, armor.State, "135줄 이하 → 부위파괴 종료");
            addDestruction(armor.DestructionPerStack * 3.0f);
            Assert.AreEqual(2, armor.RemainingStacks, "더 이상 갑옷이 벗겨지지 않는다");
        }

        [Test]
        public void ReLeap_IsCinematic()
        {
            var leap = boss.Definition.GetPattern("G2-26");
            Assert.IsTrue(leap.CinematicLeap);
            Assert.Greater(leap.Steps[0].LeapDuration, 0.0f);
            Assert.IsTrue(leap.IgnoreInPatternCount);
            Assert.IsFalse(leap.RandomSelectable);
        }
    }
}
