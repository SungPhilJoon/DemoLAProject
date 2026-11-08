using System;
using Framework;
using UnityEngine;

namespace DemoLA.Data
{
    /// <summary>
    /// 강화 확률표 한 행 (Data/CSV/HoningTable.csv). Index = 목표 단계 (+N → +N+1 의 N+1).
    /// 재료는 무한(문서) 이라 비용은 없다. 보조 재료(숨결)는 개수 비례로 확률을 더한다.
    /// </summary>
    [Serializable]
    public class HoningRow : ITableRow
    {
        public int Index;
        /// <summary>기본 성공 확률 %.</summary>
        public float BaseRate = 100.0f;
        /// <summary>실패 시 다음 시도 확률 증가 %p.</summary>
        public float FailBonus;
        /// <summary>실패 누적 포함 확률 상한 %.</summary>
        public float MaxRate = 100.0f;
        /// <summary>보조 재료 최대 사용 시 추가 확률 %p.</summary>
        public float AuxMaxBonus;
        /// <summary>보조 재료 최대 개수.</summary>
        public int AuxMaxCount;
        /// <summary>실패 시 장인의 기운 = 이번 시도 확률 × 이 값.</summary>
        public float ArtisanFactor = 0.465f;

        public int GetKey()
        {
            return Index;
        }
    }

    public class HoningTable : TableAsset<HoningRow>
    {
        public int MaxLevel
        {
            get
            {
                int max = 0;
                for (int i = 0; i < All.Count; ++i)
                {
                    max = Mathf.Max(max, All[i].Index);
                }

                return max;
            }
        }

        public HoningRow GetStep(int InTargetLevel)
        {
            HoningRow row;
            return TryGet(InTargetLevel, out row) == true ? row : null;
        }
    }
}
