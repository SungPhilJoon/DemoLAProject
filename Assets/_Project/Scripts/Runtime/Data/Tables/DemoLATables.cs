using System;
using System.Collections.Generic;
using Framework;
using UnityEngine;

namespace DemoLA.Data
{
    /// <summary>
    /// CSV 로 만든 정적 데이터 테이블 묶음 (ActionRPGProject 의 GameTables 방식).
    /// CSV(Assets/_Project/Data/CSV) → TableAsset(Assets/_Project/Data/Tables) 변환은 에디터 임포터가, 로드는 Addressables(로컬 번들)가 한다.
    /// Resources 는 쓰지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "DemoLA/Data/Tables Root", fileName = "DemoLATables")]
    public class DemoLATables : ScriptableObject
    {
        public BossPatternTable BossPattern;
        public BossPhaseTable BossPhase;
        public HoningTable Honing;
        public AdvancedHoningTable AdvancedHoning;
        public GemTable Gem;
        public EngravingTable Engraving;
        public CardSetTable CardSet;
        public BattleItemTable BattleItem;

        public TableAssetBase[] GetAll()
        {
            return new TableAssetBase[] { BossPattern, BossPhase, Honing, AdvancedHoning, Gem, Engraving, CardSet, BattleItem };
        }

        /// <summary>테이블 타입 목록 (임포터 · 테스트가 CSV 파일 이름 = 타입 이름 규칙으로 쓴다).</summary>
        public static readonly Type[] TableTypes =
        {
            typeof(BossPatternTable), typeof(BossPhaseTable), typeof(HoningTable), typeof(AdvancedHoningTable),
            typeof(GemTable), typeof(EngravingTable), typeof(CardSetTable), typeof(BattleItemTable),
        };

        /// <summary>
        /// CSV 텍스트(타입 이름 → 내용)로 메모리 테이블을 만든다. 에디터 테스트와 데이터 에셋이 없을 때의 에디터 대체 경로에서만 쓴다.
        /// </summary>
        public static DemoLATables FromCsv(Func<string, string> InReadCsvByTypeName, out string OutErrors)
        {
            OutErrors = string.Empty;
            var root = CreateInstance<DemoLATables>();
            root.name = "DemoLATables (memory)";
            var created = new Dictionary<Type, TableAssetBase>();
            foreach (var type in TableTypes)
            {
                var table = (TableAssetBase)CreateInstance(type);
                table.name = type.Name;
                string csv = InReadCsvByTypeName(type.Name);
                if (string.IsNullOrEmpty(csv) == false)
                {
                    string error;
                    table.SetRows(CsvConvert.ToRows(table.GetRowType(), csv, out error));
                    if (string.IsNullOrEmpty(error) == false)
                    {
                        OutErrors += $"{type.Name}: {error}\n";
                    }
                }

                created[type] = table;
            }

            root.Assign(created);
            return root;
        }

        public void Assign(IDictionary<Type, TableAssetBase> InTables)
        {
            TableAssetBase table;
            if (InTables.TryGetValue(typeof(BossPatternTable), out table) == true) BossPattern = (BossPatternTable)table;
            if (InTables.TryGetValue(typeof(BossPhaseTable), out table) == true) BossPhase = (BossPhaseTable)table;
            if (InTables.TryGetValue(typeof(HoningTable), out table) == true) Honing = (HoningTable)table;
            if (InTables.TryGetValue(typeof(AdvancedHoningTable), out table) == true) AdvancedHoning = (AdvancedHoningTable)table;
            if (InTables.TryGetValue(typeof(GemTable), out table) == true) Gem = (GemTable)table;
            if (InTables.TryGetValue(typeof(EngravingTable), out table) == true) Engraving = (EngravingTable)table;
            if (InTables.TryGetValue(typeof(CardSetTable), out table) == true) CardSet = (CardSetTable)table;
            if (InTables.TryGetValue(typeof(BattleItemTable), out table) == true) BattleItem = (BattleItemTable)table;
        }
    }
}
