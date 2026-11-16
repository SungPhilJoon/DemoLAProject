using System;
using Framework;

namespace DemoLA.Data
{
    /// <summary>xlsx 페이즈 시트 한 행. PatternTags 는 '|' 구분.</summary>
    [Serializable]
    public class BossPhaseRow : ITableRow
    {
        public int Index;
        public string PhaseId;
        public string Name;
        public string EnterCondition;
        public string[] PatternTags;
        public string EndCondition;
        public string Notes;

        public int GetKey()
        {
            return Index;
        }
    }

    public class BossPhaseTable : TableAsset<BossPhaseRow>
    {
    }
}
