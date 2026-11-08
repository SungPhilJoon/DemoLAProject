using DemoLA.Data;
using UnityEngine;

namespace DemoLA.Build
{
    public struct HoningResult
    {
        public bool Attempted;
        public bool Success;
        public bool Guaranteed;
        public float RateUsed;
        public int NewLevel;
        public float ArtisanEnergy;
    }

    public enum EAdvancedHoningGrade
    {
        Normal = 0,
        Great = 1,
        Super = 2,
    }

    public struct AdvancedHoningResult
    {
        public bool Attempted;
        public EAdvancedHoningGrade Grade;
        public float ExpGained;
        public bool LevelUp;
        public int NewLevel;
    }

    /// <summary>
    /// 장비 강화 (재료 무한 — 문서).
    ///  - 일반 재련: 확률 = min(기본 + 실패 누적, 상한) + 보조 재료 보너스. 실패 시 장인의 기운 += 시도 확률 × 계수, 100% 면 다음 시도 확정.
    ///  - 상급 재련: 시도마다 경험치(일반 1 · 대성공 2 · 초대성공 4 배)를 얻어 단계 상승.
    /// 수치는 HoningTable / AdvancedHoningTable CSV.
    /// </summary>
    public static class HoningService
    {
        public static float AuxBonus(HoningRow InStep, int InAuxCount)
        {
            if (InStep == null || InStep.AuxMaxCount <= 0)
            {
                return 0.0f;
            }

            return InStep.AuxMaxBonus * Mathf.Clamp01(InAuxCount / (float)InStep.AuxMaxCount);
        }

        public static float CurrentRate(EquipmentState InState, HoningTable InTable)
        {
            var step = InTable != null ? InTable.GetStep(InState.HoningLevel + 1) : null;
            if (step == null)
            {
                return 0.0f;
            }

            return Mathf.Min(100.0f, Mathf.Min(step.MaxRate, step.BaseRate + InState.FailBonusRate) + AuxBonus(step, InState.AuxMaterials));
        }

        public static bool CanHone(EquipmentState InState, HoningTable InTable)
        {
            return InState != null && InTable != null && InTable.GetStep(InState.HoningLevel + 1) != null;
        }

        /// <param name="InRoll">[0,1) 난수 (테스트에서 고정값 주입)</param>
        public static HoningResult TryHone(EquipmentState InState, HoningTable InTable, float InRoll)
        {
            var result = new HoningResult();
            if (CanHone(InState, InTable) == false)
            {
                result.NewLevel = InState != null ? InState.HoningLevel : 0;
                return result;
            }

            var step = InTable.GetStep(InState.HoningLevel + 1);
            result.Attempted = true;
            result.Guaranteed = InState.ArtisanEnergy >= 100.0f;
            result.RateUsed = result.Guaranteed == true ? 100.0f : CurrentRate(InState, InTable);
            result.Success = result.Guaranteed == true || InRoll < result.RateUsed * 0.01f;

            if (result.Success == true)
            {
                InState.HoningLevel += 1;
                InState.ArtisanEnergy = 0.0f;
                InState.FailBonusRate = 0.0f;
            }
            else
            {
                InState.ArtisanEnergy = Mathf.Min(100.0f, InState.ArtisanEnergy + result.RateUsed * step.ArtisanFactor);
                InState.FailBonusRate = Mathf.Min(Mathf.Max(0.0f, step.MaxRate - step.BaseRate), InState.FailBonusRate + step.FailBonus);
            }

            result.NewLevel = InState.HoningLevel;
            result.ArtisanEnergy = InState.ArtisanEnergy;
            return result;
        }

        public static bool CanAdvancedHone(EquipmentState InState, AdvancedHoningTable InTable)
        {
            AdvancedHoningRow row;
            return InState != null && InTable != null && InTable.TryGet(InState.AdvancedLevel + 1, out row) == true;
        }

        public static AdvancedHoningResult TryAdvancedHone(EquipmentState InState, AdvancedHoningTable InTable, float InRoll)
        {
            var result = new AdvancedHoningResult();
            AdvancedHoningRow row;
            if (InState == null || InTable == null || InTable.TryGet(InState.AdvancedLevel + 1, out row) == false)
            {
                result.NewLevel = InState != null ? InState.AdvancedLevel : 0;
                return result;
            }

            result.Attempted = true;
            float roll = InRoll * 100.0f;
            result.Grade = roll < row.SuperChance ? EAdvancedHoningGrade.Super : (roll < row.SuperChance + row.GreatChance ? EAdvancedHoningGrade.Great : EAdvancedHoningGrade.Normal);
            float multiplier = result.Grade == EAdvancedHoningGrade.Super ? 4.0f : (result.Grade == EAdvancedHoningGrade.Great ? 2.0f : 1.0f);
            result.ExpGained = row.ExpPerAttempt * multiplier;
            InState.AdvancedExp += result.ExpGained;
            if (InState.AdvancedExp >= row.RequiredExp)
            {
                InState.AdvancedExp = 0.0f;
                InState.AdvancedLevel += 1;
                result.LevelUp = true;
            }

            result.NewLevel = InState.AdvancedLevel;
            return result;
        }

        public static float AdvancedStatPercent(EquipmentState InState, AdvancedHoningTable InTable)
        {
            AdvancedHoningRow row;
            return InState != null && InTable != null && InState.AdvancedLevel > 0 && InTable.TryGet(InState.AdvancedLevel, out row) == true ? row.StatPercent : 0.0f;
        }
    }
}
