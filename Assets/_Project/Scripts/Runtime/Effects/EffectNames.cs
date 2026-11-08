using System.Collections.Generic;
using Framework.Ability;

namespace DemoLA.Effects
{
    /// <summary>
    /// GameplayEffectDef → 표시 이름 · 설명 · 버프/디버프 구분. 버프/디버프 아이콘 줄과 툴팁이 쓴다.
    /// (Def 에는 이름 필드가 없어서 만든 쪽이 등록한다.)
    /// </summary>
    public static class EffectNames
    {
        public struct Info
        {
            public string Name;
            public string Description;
            public bool IsDebuff;
        }

        private static readonly Dictionary<GameplayEffectDef, Info> Table = new Dictionary<GameplayEffectDef, Info>();

        public static void Register(GameplayEffectDef InDef, string InName, string InDescription, bool InIsDebuff)
        {
            if (InDef != null)
            {
                Table[InDef] = new Info { Name = InName, Description = InDescription, IsDebuff = InIsDebuff };
            }
        }

        public static bool TryGet(GameplayEffectDef InDef, out Info OutInfo)
        {
            if (InDef == null)
            {
                OutInfo = default(Info);
                return false;
            }

            return Table.TryGetValue(InDef, out OutInfo);
        }
    }
}
