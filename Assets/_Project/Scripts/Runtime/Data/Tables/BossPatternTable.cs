using System;
using Framework;

namespace DemoLA.Data
{
    /// <summary>
    /// xlsx 패턴목록 한 행 (Data/CSV/BossPatternTable.csv). Index = 관문 × 100 + 번호 (Pattern.G2.LethalFieldDouble → 203).
    /// TelegraphTime / Recovery / AreaScale 에 값을 적으면(0 이상) 패턴 프리셋보다 우선한다 → 영상 측정값 입력용.
    /// </summary>
    [Serializable]
    public class BossPatternRow : ITableRow
    {
        public int Index;
        /// <summary>패턴 태그 (CSV 표기 "Pattern.G1.OrbStagger"). 에셋을 만들 때 GameTags.TryParse 로 ETag 로 바꾼다.</summary>
        public string PatternTag;
        public string PhaseId;
        public string Name;
        public string Category;
        public string Shape;
        public string Response;
        /// <summary>끝나면 이어서 쓰는 패턴 태그 (비우면 없음).</summary>
        public string LinkedPatternTag;
        public string Confidence;
        public string DamageTier;
        public string DamageText;
        /// <summary>전조 시간(초). -1 = 프리셋 기본값.</summary>
        public float TelegraphTime = -1.0f;
        /// <summary>후딜(초). -1 = 프리셋 기본값.</summary>
        public float Recovery = -1.0f;
        /// <summary>판정 크기 배율. -1 = 1.</summary>
        public float AreaScale = -1.0f;
        /// <summary>영상 측정값을 넣었으면 1.</summary>
        public bool Measured;
        public string Notes;

        public int GetKey()
        {
            return Index;
        }
    }

    public class BossPatternTable : TableAsset<BossPatternRow>
    {
    }
}
