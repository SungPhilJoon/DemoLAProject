using DemoLA.Boss;
using DemoLA.Core;
using NUnit.Framework;
using UnityEngine;

namespace DemoLA.Tests
{
    /// <summary>카운터 가능 구간 보스 빛 (CounterGlowView · DemoLA/CounterGlow 셰이더).</summary>
    public class CounterGlowTests
    {
        private TestWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld(5);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void Glow_FollowsCounterWindowTag()
        {
            var boss = world.Boss("Gate2", Vector3.zero, Vector3.forward);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(boss.transform, false);
            var view = boss.gameObject.AddComponent<CounterGlowView>();
            view.Bind(new[] { body.GetComponent<Renderer>() });

            Assert.IsNotNull(CounterGlowView.SharedMaterial, "M_CounterGlow 재질 / DemoLA/CounterGlow 셰이더");
            Assert.AreEqual(2, body.GetComponent<Renderer>().sharedMaterials.Length, "원래 재질 + 빛 재질");
            Assert.AreEqual(CounterGlowView.ShaderName, body.GetComponent<Renderer>().sharedMaterials[1].shader.name);

            view.Tick(0.016f, 10.0f);
            Assert.AreEqual(0.0f, view.CurrentIntensity, "카운터 구간이 아니면 꺼짐");

            boss.Asc.Tags.AddTag((int)ETag.Boss_CounterWindow);
            view.Tick(0.016f, 10.0f);
            Assert.AreEqual(view.FlashIntensity, view.CurrentIntensity, 0.01f, "열리는 순간 번쩍");
            view.Tick(0.016f, 11.0f);
            Assert.AreEqual(view.HoldIntensity, view.CurrentIntensity, 0.01f, "이후 유지 밝기");

            boss.Asc.Tags.RemoveTag((int)ETag.Boss_CounterWindow);
            for (int i = 0; i < 30; ++i)
            {
                view.Tick(0.016f, 12.0f + i * 0.016f);
            }

            Assert.AreEqual(0.0f, view.CurrentIntensity, "닫히면 사라짐");
        }

        [Test]
        public void CounterSettings_TimingAndStepCounter()
        {
            var pattern = ScriptableObject.CreateInstance<BossPatternDefinition>();
            pattern.Steps.Add(new PatternStep { StartDelay = 0.5f, TelegraphTime = 1.0f });
            pattern.Steps.Add(new PatternStep { TelegraphTime = 0.8f, LeapDuration = 0.4f });
            Assert.AreEqual(0.5f + 1.0f + 0.8f + 0.4f, pattern.RepeatDuration(), 0.001f, "StartDelay + 전조 + 도약");

            float cursor = 0.0f;
            float hit;
            float end;
            Assert.AreEqual(0.5f, pattern.StepTiming(0, ref cursor, out hit, out end), 0.001f, "전조 시작");
            Assert.AreEqual(1.5f, hit, 0.001f, "첫 판정");

            Assert.IsFalse(pattern.IsCounterable);
            pattern.Steps[1].Counterable = true;
            Assert.IsTrue(pattern.HasStepCounter, "다단 공격 중 두 번째 타격만 카운터");
            Assert.IsTrue(pattern.IsCounterable);
            Object.DestroyImmediate(pattern);
        }

        [Test]
        public void AxeCombo_OnlyLastSlamOpensCounterWindow()
        {
            var boss = world.Boss("Gate2", Vector3.zero, Vector3.forward);
            world.Combatant(DemoLA.Core.ETeam.Player, Vector3.forward * 4.0f, 10000000.0f);
            var pattern = boss.Definition.GetPattern("G2-24");
            Assert.IsNotNull(pattern, "G2-24 도끼 앞뒤 찍기 → 카운터 강타");
            Assert.AreEqual(3, pattern.Steps.Count);
            Assert.IsFalse(pattern.Steps[0].Counterable, "앞 찍기 카운터 불가");
            Assert.IsFalse(pattern.Steps[1].Counterable, "뒤 찍기 카운터 불가");
            Assert.IsTrue(pattern.Steps[2].Counterable, "마지막 앞 강타만 카운터");
            Assert.Greater(pattern.Steps[0].ForwardOffset, 0.0f, "앞에 찍음");
            Assert.Less(pattern.Steps[1].ForwardOffset, 0.0f, "뒤에 찍음");

            float cursor = 0.0f;
            float hit;
            float end;
            pattern.StepTiming(0, ref cursor, out hit, out end);
            pattern.StepTiming(1, ref cursor, out hit, out end);
            float secondHit = hit;
            float lastTelegraph = pattern.StepTiming(2, ref cursor, out hit, out end);

            Assert.IsTrue(boss.TryActivatePattern("G2-24", null));
            float firstOpen = -1.0f;
            world.Simulate(hit + 0.5f, 0.02f, t =>
            {
                if (firstOpen < 0.0f && boss.HasTag((int)ETag.Boss_CounterWindow) == true)
                {
                    firstOpen = t;
                }
            });

            Assert.Greater(firstOpen, secondHit, "앞 · 뒤 찍기 동안에는 구간이 열리지 않는다");
            Assert.AreEqual(lastTelegraph + pattern.Steps[2].CounterWindowStart, firstOpen, 0.1f, "마지막 강타 전조 기준으로 열림");
        }
    }
}
