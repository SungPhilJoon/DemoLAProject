using System.Reflection;
using DemoLA.AI;
using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Combat;
using DemoLA.Core;
using Framework.StateTree;
using NUnit.Framework;
using UnityEngine;

namespace DemoLA.Tests
{
    /// <summary>패턴별 대상 선택 규칙 (PatternTargetSelector · PatternTargeting.Resolve).</summary>
    public class PatternTargetingTests
    {
        private TestWorld world;
        private BossActor boss;
        private TestCombatant near;
        private TestCombatant far;
        private TestCombatant side;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld(33);
            boss = world.Boss("Gate1", Vector3.zero, Vector3.forward);
            near = world.Combatant(ETeam.Player, Vector3.forward * 2.0f);
            far = world.Combatant(ETeam.Player, Vector3.back * 20.0f);
            side = world.Combatant(ETeam.Player, Vector3.right * 8.0f);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        private static BossPatternDefinition pattern(PatternTargetSelector InSelector)
        {
            var definition = ScriptableObject.CreateInstance<BossPatternDefinition>();
            definition.TargetSelector = InSelector;
            return definition;
        }

        [Test]
        public void Nearest_And_Farthest()
        {
            Assert.AreEqual(near, PatternTargeting.Resolve(boss, pattern(new NearestSelector()), null));
            Assert.AreEqual(far, PatternTargeting.Resolve(boss, pattern(new FarthestSelector()), null));
        }

        [Test]
        public void FrontCone_PicksInsideAngle()
        {
            Assert.AreEqual(near, PatternTargeting.Resolve(boss, pattern(new FrontConeSelector { Angle = 60.0f }), null));
            boss.FaceTowards(Vector3.right * 10.0f);
            Assert.AreEqual(side, PatternTargeting.Resolve(boss, pattern(new FrontConeSelector { Angle = 60.0f }), null));
        }

        [Test]
        public void Threat_Rank()
        {
            boss.AddThreat(side, 500.0f);
            boss.AddThreat(far, 300.0f);
            boss.AddThreat(near, 100.0f);
            Assert.AreEqual(side, PatternTargeting.Resolve(boss, pattern(new HighestThreatSelector { Rank = 1 }), null));
            Assert.AreEqual(far, PatternTargeting.Resolve(boss, pattern(new HighestThreatSelector { Rank = 2 }), null));
            Assert.AreEqual(near, PatternTargeting.Resolve(boss, pattern(new HighestThreatSelector { Rank = 9 }), null), "인원보다 큰 순위 = 꼴찌");
        }

        [Test]
        public void LowestHealth()
        {
            far.Stats.Set(EAttr.Health, far.Stats.Get(EAttr.MaxHealth) * 0.2f);
            Assert.AreEqual(far, PatternTargeting.Resolve(boss, pattern(new LowestHealthSelector()), null));
        }

        [Test]
        public void Random_ExcludeAggro_NeverPicksAggro()
        {
            var aggro = boss.AggroTarget;
            var selector = new RandomSelector { ExcludeAggro = true };
            for (int i = 0; i < 100; ++i)
            {
                Assert.AreNotEqual(aggro, PatternTargeting.Resolve(boss, pattern(selector), null));
            }
        }

        [Test]
        public void Taunt_Overrides_Selector()
        {
            boss.Taunt(side, 5.0f);
            Assert.AreEqual(side, PatternTargeting.Resolve(boss, pattern(new FarthestSelector()), null));
        }

        [Test]
        public void ExplicitRequest_BeatsPatternSelector()
        {
            Assert.AreEqual(far, PatternTargeting.Resolve(boss, pattern(new NearestSelector()), far), "StateTree 가 지정한 대상 > 패턴 규칙");
        }

        // ------------------------------------------------------------------ StateTree 노드

        private StateTreeContext context()
        {
            var brain = boss.gameObject.AddComponent<CombatBrain>();
            typeof(CombatBrain).GetField("actor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(brain, boss);
            return new StateTreeContext { Owner = brain, Blackboard = new StateTreeBlackboard() };
        }

        [Test]
        public void SelectTargetTask_NextPattern_WritesPatternTarget()
        {
            var ctx = context();
            var task = new SelectTargetTask { Selector = new FarthestSelector(), ApplyTo = ETargetApply.NextPattern, CompleteImmediately = true };
            Assert.AreEqual(ETaskStatus.Succeeded, task.EnterState(ctx));
            Assert.AreEqual(far, AIContext.PatternTarget(ctx));
            AIContext.ClearPatternTarget(ctx);
            Assert.IsNull(AIContext.PatternTarget(ctx));
        }

        [Test]
        public void SelectTargetTask_Aggro_ChangesBossAggro()
        {
            var ctx = context();
            var task = new SelectTargetTask { Selector = new FarthestSelector(), ApplyTo = ETargetApply.Aggro, HoldSeconds = 10.0f, CompleteImmediately = true };
            Assert.AreEqual(ETaskStatus.Succeeded, task.EnterState(ctx));
            Assert.AreEqual(far, boss.AggroTarget);
            boss.Taunt(side, 5.0f);
            Assert.AreEqual(side, boss.AggroTarget, "도발은 여전히 우선");
        }

        [Test]
        public void SelectTargetTask_NoTarget_FailsOrPasses()
        {
            var ctx = context();
            Assert.AreEqual(ETaskStatus.Failed, new SelectTargetTask { Selector = null }.EnterState(ctx));
            Assert.AreEqual(ETaskStatus.Succeeded, new SelectTargetTask { Selector = null, FailIfNone = false, CompleteImmediately = true }.EnterState(ctx));
            Assert.AreEqual(ETaskStatus.Running, new SelectTargetTask { Selector = new FarthestSelector() }.EnterState(ctx), "기본은 Running (같은 상태의 패턴 태스크를 기다린다)");
            AIContext.ClearPatternTarget(ctx);
            Assert.IsNull(AIContext.PatternTarget(ctx), "못 고르면 아무것도 바꾸지 않는다");
        }

        [Test]
        public void TargetRuleCondition_ComparesDistance()
        {
            var ctx = context();
            Assert.IsTrue(new TargetRuleCondition { Selector = new FarthestSelector(), Op = ECompareOp.GreaterOrEqual, Distance = 10.0f }.Evaluate(ctx));
            Assert.IsFalse(new TargetRuleCondition { Selector = new FarthestSelector(), Op = ECompareOp.GreaterOrEqual, Distance = 30.0f }.Evaluate(ctx));
            Assert.IsTrue(new TargetRuleCondition { Selector = new NearestSelector(), Op = ECompareOp.LessOrEqual, Distance = 3.0f }.Evaluate(ctx));
        }

        [Test]
        public void NoSelector_UsesRequested_ThenAggro()
        {
            Assert.AreEqual(far, PatternTargeting.Resolve(boss, pattern(null), far));
            Assert.AreEqual(boss.AggroTarget, PatternTargeting.Resolve(boss, pattern(null), null));
        }
    }
}
