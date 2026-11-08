using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Build;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Data;
using NUnit.Framework;
using UnityEngine;

namespace DemoLA.Tests
{
    /// <summary>순수 공식/규칙 (씬 불필요).</summary>
    public class FormulaTests
    {
        [SetUp]
        public void SetUp()
        {
            // 공식 단위 테스트는 밸런스 배율 없이 계산값을 그대로 본다 (밸런스는 시나리오 TS_밸런스_* 가 본다).
            StatFormula.Settings = new StatFormulaSettings { PlayerDamageScale = 1.0f };
        }

        private static DamageInput baseInput()
        {
            var input = new DamageInput();
            input.AttackPower = 10000.0f;
            input.Coefficient = 2.0f;
            input.SkillDamagePercent = 100.0f;
            input.IndependentMultiplier = 1.0f;
            input.CritRate = 0.0f;
            input.CritDamage = 200.0f;
            input.CritRoll = 0.99f;
            return input;
        }

        [Test]
        public void Damage_Base_IsAttackTimesCoefficient()
        {
            var result = DamageCalculator.Calculate(baseInput());
            Assert.AreEqual(20000.0f, result.Damage, 0.01f);
            Assert.IsFalse(result.IsCrit);
        }

        [Test]
        public void Damage_Crit_UsesCritDamage()
        {
            var input = baseInput();
            input.CritRate = 100.0f;
            input.CritRoll = 0.5f;
            var result = DamageCalculator.Calculate(input);
            Assert.IsTrue(result.IsCrit);
            Assert.AreEqual(40000.0f, result.Damage, 0.01f);
        }

        [Test]
        public void Damage_MultipliersStack_InDocumentedOrder()
        {
            var input = baseInput();
            input.GemDamagePercent = 40.0f;      // ×1.4
            input.DamageIncrease = 10.0f;        // ×1.1
            input.IndependentMultiplier = 1.2f;  // ×1.2
            input.Positional = EPositional.BackAttack;
            input.PositionalBonus = 5.0f;        // ×1.05
            var result = DamageCalculator.Calculate(input);
            Assert.AreEqual(20000.0f * 1.4f * 1.1f * 1.2f * 1.05f, result.Damage, 0.5f);
        }

        [Test]
        public void Damage_Defense_ReducesByFormula()
        {
            var input = baseInput();
            input.TargetDefense = StatFormula.Settings.DefenseConstant; // 50% 감소
            Assert.AreEqual(10000.0f, DamageCalculator.Calculate(input).Damage, 0.5f);
            input.DefensePenetration = 1.0f;
            Assert.AreEqual(20000.0f, DamageCalculator.Calculate(input).Damage, 0.5f);
        }

        [Test]
        public void AttackPower_SqrtFormula()
        {
            float attack = StatFormula.AttackPower(60000.0f, 60000.0f, 0.0f);
            Assert.AreEqual(Mathf.Sqrt(60000.0f * 60000.0f / 6.0f), attack, 0.5f);
            Assert.AreEqual(attack * 1.1f, StatFormula.AttackPower(60000.0f, 60000.0f, 10.0f), 0.5f);
        }

        [TestCase(EHitShape.Circle, 0.0f, 2.9f, true)]
        [TestCase(EHitShape.Circle, 0.0f, 3.6f, false)]
        [TestCase(EHitShape.Fan, 0.0f, 2.0f, true)]
        [TestCase(EHitShape.Fan, 180.0f, 2.0f, false)]
        [TestCase(EHitShape.Rect, 0.0f, 9.0f, true)]
        [TestCase(EHitShape.Rect, 90.0f, 3.0f, false)]
        [TestCase(EHitShape.Donut, 0.0f, 1.0f, false)]
        [TestCase(EHitShape.Donut, 0.0f, 5.0f, true)]
        public void HitGeometry_Shapes(EHitShape InShape, float InAngle, float InDistance, bool InExpected)
        {
            HitArea area;
            switch (InShape)
            {
                case EHitShape.Fan: area = HitArea.Fan(5.0f, 90.0f); break;
                case EHitShape.Rect: area = HitArea.Rect(10.0f, 2.0f); break;
                case EHitShape.Donut: area = HitArea.Donut(3.0f, 6.0f); break;
                default: area = HitArea.Circle(3.0f); break;
            }

            var direction = Quaternion.Euler(0.0f, InAngle, 0.0f) * Vector3.forward;
            bool inside = HitGeometry.Contains(area, Vector3.zero, Vector3.forward, direction * InDistance, 0.5f);
            Assert.AreEqual(InExpected, inside);
        }

        [Test]
        public void Positional_BackAndHead()
        {
            Assert.AreEqual(EPositional.BackAttack, HitGeometry.ResolvePositional(Vector3.zero, Vector3.forward, Vector3.back * 3.0f));
            Assert.AreEqual(EPositional.HeadAttack, HitGeometry.ResolvePositional(Vector3.zero, Vector3.forward, Vector3.forward * 3.0f));
            Assert.AreEqual(EPositional.None, HitGeometry.ResolvePositional(Vector3.zero, Vector3.forward, Vector3.right * 3.0f));
        }

        [Test]
        public void Honing_FailAccumulatesArtisanEnergy_ThenGuaranteed()
        {
            var table = honingTable(new HoningRow { Index = 1, BaseRate = 10.0f, FailBonus = 1.0f, MaxRate = 20.0f, ArtisanFactor = 0.465f });
            var state = new EquipmentState();

            int tries = 0;
            HoningResult result;
            do
            {
                result = HoningService.TryHone(state, table, 0.999f); // 항상 실패 굴림
                tries += 1;
            }
            while (result.Success == false && tries < 100);

            Assert.IsTrue(result.Success, "장인의 기운 100% 에서 확정 성공해야 한다");
            Assert.IsTrue(result.Guaranteed);
            Assert.AreEqual(1, state.HoningLevel);
            Assert.AreEqual(0.0f, state.ArtisanEnergy);
            Assert.Greater(tries, 5);
            Object.DestroyImmediate(table);
        }

        [Test]
        public void Honing_RateCappedByMax()
        {
            var table = honingTable(new HoningRow { Index = 1, BaseRate = 10.0f, FailBonus = 5.0f, MaxRate = 18.0f });
            var state = new EquipmentState();
            HoningService.TryHone(state, table, 0.999f);
            HoningService.TryHone(state, table, 0.999f);
            Assert.AreEqual(18.0f, HoningService.CurrentRate(state, table), 0.001f);
            Object.DestroyImmediate(table);
        }

        private static HoningTable honingTable(params HoningRow[] InRows)
        {
            var table = ScriptableObject.CreateInstance<HoningTable>();
            table.SetRows(InRows);
            return table;
        }

        [Test]
        public void Honing_AuxMaterials_AddRate()
        {
            var table = honingTable(new HoningRow { Index = 1, BaseRate = 10.0f, MaxRate = 20.0f, AuxMaxBonus = 10.0f, AuxMaxCount = 20 });
            var state = new EquipmentState();
            Assert.AreEqual(10.0f, HoningService.CurrentRate(state, table), 0.001f);
            state.AuxMaterials = 10;
            Assert.AreEqual(15.0f, HoningService.CurrentRate(state, table), 0.001f, "보조 재료 절반 = 추가 확률 절반");
            state.AuxMaterials = 999;
            Assert.AreEqual(20.0f, HoningService.CurrentRate(state, table), 0.001f);
            Object.DestroyImmediate(table);
        }

        [Test]
        public void AdvancedHoning_ExpLevelsUp()
        {
            var table = ScriptableObject.CreateInstance<AdvancedHoningTable>();
            table.SetRows(new[] { new AdvancedHoningRow { Index = 1, RequiredExp = 300.0f, ExpPerAttempt = 100.0f, GreatChance = 0.0f, SuperChance = 100.0f, StatPercent = 2.0f } });
            var state = new EquipmentState();
            var result = HoningService.TryAdvancedHone(state, table, 0.0f);
            Assert.AreEqual(EAdvancedHoningGrade.Super, result.Grade);
            Assert.IsTrue(result.LevelUp, "초대성공 400 경험치 ≥ 300");
            Assert.AreEqual(2.0f, HoningService.AdvancedStatPercent(state, table), 0.001f);
            Object.DestroyImmediate(table);
        }

        [Test]
        public void Engraving_PointsToLevels()
        {
            var rules = new EngravingRules();
            var build = new CharacterBuildData();
            build.Books.Add(new EngravingBookState { EngravingId = 1, Points = 12 });
            build.Accessories.Add(new AccessoryState { EngravingA = 1, PointsA = 3, EngravingB = 2, PointsB = 5 });
            build.Stone = new AbilityStoneState { EngravingA = 2, PointsA = 7, EngravingB = 3, PointsB = 4 };

            var points = EngravingCalculator.ComputePoints(build, rules);
            Assert.AreEqual(15, points[1]);
            Assert.AreEqual(12, points[2]);
            var levels = EngravingCalculator.ComputeLevels(build, rules);
            Assert.AreEqual(3, levels[1]);
            Assert.AreEqual(2, levels[2]);
            Assert.IsFalse(levels.ContainsKey(3), "4포인트는 레벨 0");
        }

        [Test]
        public void Engraving_BookSlotsLimited()
        {
            var rules = new EngravingRules { BookSlots = 2 };
            var build = new CharacterBuildData();
            build.Books.Add(new EngravingBookState { EngravingId = 1, Points = 12 });
            build.Books.Add(new EngravingBookState { EngravingId = 2, Points = 12 });
            build.Books.Add(new EngravingBookState { EngravingId = 3, Points = 12 });
            Assert.IsFalse(EngravingCalculator.ComputePoints(build, rules).ContainsKey(3));
        }

        [Test]
        public void OrbRule_AlternateColors_Completes()
        {
            var rule = new OrbSequenceRule(4);
            Assert.AreEqual(EOrbPickResult.Accepted, rule.Pick(EOrbColor.Red));
            Assert.AreEqual(EOrbPickResult.Accepted, rule.Pick(EOrbColor.Blue));
            Assert.AreEqual(EOrbPickResult.Accepted, rule.Pick(EOrbColor.Red));
            Assert.AreEqual(EOrbPickResult.Accepted, rule.Pick(EOrbColor.Blue));
            Assert.IsTrue(rule.IsComplete);
        }

        [Test]
        public void OrbRule_SameColorTwice_Fails()
        {
            var rule = new OrbSequenceRule(8);
            rule.Pick(EOrbColor.Red);
            Assert.AreEqual(EOrbPickResult.SameColorTwice, rule.Pick(EOrbColor.Red));
            Assert.IsTrue(rule.IsFailed);
            Assert.AreEqual(EOrbPickResult.Ignored, rule.Pick(EOrbColor.Blue));
        }

        [Test]
        public void DamageTier_FromXlsxAssumptionTable()
        {
            Assert.AreEqual(1.0f, DamageTierUtil.Fraction(DamageTierUtil.FromKorean("즉사급")));
            Assert.AreEqual(0.6f, DamageTierUtil.Fraction(DamageTierUtil.FromKorean("매우 큰 피해")));
            Assert.AreEqual(0.35f, DamageTierUtil.Fraction(DamageTierUtil.FromKorean("큰 피해")));
            Assert.AreEqual(EDamageTier.None, DamageTierUtil.FromKorean(""));
        }

        [Test]
        public void SkillPointCost_IsMonotonic()
        {
            Assert.AreEqual(0, BuildCalculator.SkillPointCost(1));
            Assert.Less(BuildCalculator.SkillPointCost(10), BuildCalculator.SkillPointCost(12));
        }
    }
}
