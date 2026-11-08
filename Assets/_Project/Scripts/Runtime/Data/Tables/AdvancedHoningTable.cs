using System;
using Framework;

namespace DemoLA.Data
{
    /// <summary>
    /// 상급 재련 (경험치 방식) 한 행. 시도마다 기본 경험치 × (일반 1 / 대성공 2 / 초대성공 4)을 얻고 RequiredExp 를 넘으면 단계 상승.
    /// </summary>
    [Serializable]
    public class AdvancedHoningRow : ITableRow
    {
        /// <summary>목표 단계 (1 ~ 20).</summary>
        public int Index;
        public float RequiredExp = 1000.0f;
        public float ExpPerAttempt = 100.0f;
        /// <summary>대성공 / 초대성공 확률 %.</summary>
        public float GreatChance = 15.0f;
        public float SuperChance = 5.0f;
        /// <summary>이 단계까지의 누적 주스탯 · 체력 증가 %.</summary>
        public float StatPercent;

        public int GetKey()
        {
            return Index;
        }
    }

    public class AdvancedHoningTable : TableAsset<AdvancedHoningRow>
    {
    }
}
