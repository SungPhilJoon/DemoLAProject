using System;
using DemoLA.Attributes;
using DemoLA.Combat;
using Framework;

namespace DemoLA.Data
{
    /// <summary>
    /// 카드 세트 한 행. 카드 id = Index × 100 + 카드 인덱스.
    /// 세트 효과 i 는 BonusCards[i] · BonusAwakening[i] · BonusAttr[i] · BonusValue[i] · BonusText[i] 가 한 묶음.
    /// ConvertElement 가 None 이 아니면 ConvertAtBonus 번째 효과가 켜질 때 시전자의 공격 속성을 그 속성으로 바꾼다.
    /// </summary>
    [Serializable]
    public class CardSetRow : ITableRow
    {
        public int Index;
        public string Name;
        public string[] CardNames;
        public int MaxAwakening = 5;
        public int[] BonusCards;
        public int[] BonusAwakening;
        public EAttr[] BonusAttr;
        public float[] BonusValue;
        public string[] BonusText;
        public EElement ConvertElement;
        public int ConvertAtBonus = -1;

        public int GetKey()
        {
            return Index;
        }

        public int CardId(int InIndex)
        {
            return Index * 100 + InIndex;
        }

        public bool Contains(int InCardId)
        {
            int local = InCardId - Index * 100;
            return InCardId / 100 == Index && CardNames != null && local >= 0 && local < CardNames.Length;
        }

        public int BonusCount
        {
            get { return BonusCards != null ? BonusCards.Length : 0; }
        }

        public string BonusLabel(int InIndex)
        {
            return BonusText != null && InIndex < BonusText.Length ? BonusText[InIndex] : string.Empty;
        }
    }

    public class CardSetTable : TableAsset<CardSetRow>
    {
        public CardSetRow FindByCard(int InCardId)
        {
            for (int i = 0; i < All.Count; ++i)
            {
                if (All[i].Contains(InCardId) == true)
                {
                    return All[i];
                }
            }

            return null;
        }
    }
}
