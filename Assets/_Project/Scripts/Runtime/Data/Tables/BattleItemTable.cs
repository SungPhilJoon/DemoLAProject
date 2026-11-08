using System;
using DemoLA.Combat;
using Framework;

namespace DemoLA.Data
{
    /// <summary>배틀 아이템 한 행 (쿨다운 · 소지 개수 · 효과 수치).</summary>
    [Serializable]
    public class BattleItemRow : ITableRow
    {
        public int Index;
        public string Name;
        public EBattleItemType Type;
        public float Cooldown = 30.0f;
        public int MaxCount = 3;
        public float Value;
        public float Duration;
        public float Radius;
        public string Description;

        public int GetKey()
        {
            return Index;
        }
    }

    public class BattleItemTable : TableAsset<BattleItemRow>
    {
    }
}
