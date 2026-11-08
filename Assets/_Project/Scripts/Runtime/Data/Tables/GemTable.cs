using System;
using DemoLA.Build;
using Framework;

namespace DemoLA.Data
{
    /// <summary>보석 레벨별 수치 (Index = 레벨).</summary>
    [Serializable]
    public class GemRow : ITableRow
    {
        public int Index;
        public float DamagePercent;
        public float CooldownPercent;

        public int GetKey()
        {
            return Index;
        }
    }

    public class GemTable : TableAsset<GemRow>
    {
        public const int SlotCount = 11;

        public int MaxLevel
        {
            get { return Count; }
        }

        public float GetValue(EGemType InType, int InLevel)
        {
            GemRow row;
            if (TryGet(InLevel, out row) == false)
            {
                return 0.0f;
            }

            return InType == EGemType.Damage ? row.DamagePercent : row.CooldownPercent;
        }
    }
}
