using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Build;
using DemoLA.Character;
using DemoLA.Skills;
using UnityEngine;

namespace DemoLA.Data
{
    /// <summary>
    /// 게임 정의 데이터 묶음. Addressables 주소 "GameDatabase" (로컬 번들) 로 로드한다.
    ///  - CSV 정적 데이터: Tables (강화 · 보석 · 각인 · 카드 · 배틀 아이템 · 보스 패턴/페이즈)
    ///  - 에디터에서 다루는 에셋: 직업 · 스킬 · 장비 세트 · 보스
    /// </summary>
    [CreateAssetMenu(menuName = "DemoLA/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        public const string Address = "GameDatabase";

        public StatFormulaSettings StatFormula = new StatFormulaSettings();
        public EngravingRules EngravingRules = new EngravingRules();
        public EquipmentQualitySettings Quality = new EquipmentQualitySettings();
        [Tooltip("최대 스킬 포인트")]
        public int MaxSkillPoints = 460;
        [Tooltip("트라이포드 레벨 (전부 최대)")]
        public int TripodLevel = 5;

        public DemoLATables Tables;
        public List<CharacterClassDefinition> Classes = new List<CharacterClassDefinition>();
        public List<EquipmentSetDefinition> EquipmentSets = new List<EquipmentSetDefinition>();
        public List<BossDefinition> Bosses = new List<BossDefinition>();
        [Tooltip("UI 아이콘 글리프 (Assets/_Project/Art/Icons/IconLibrary.asset). 비어 있으면 절차적 아이콘")]
        public DemoLA.UI.IconLibrary Icons;
        [Tooltip("카운터 가능 구간 보스 빛 재질 (Art/Materials/M_CounterGlow.mat) — 셰이더를 빌드에 포함시키는 참조이기도 하다")]
        public Material CounterGlowMaterial;
        [Tooltip("에스더 (파티 공용 게이지가 차면 단축키 5 · 6 · 7). 비어 있으면 기본 3명")]
        public List<DemoLA.Character.EstherDefinition> Esthers = new List<DemoLA.Character.EstherDefinition>();
        [Tooltip("보스 체력 몇 줄을 깎으면 에스더 게이지가 가득 차는가")]
        public float EstherHpLinesPerGauge = 12.0f;

        public HoningTable Honing
        {
            get { return Tables != null ? Tables.Honing : null; }
        }

        public AdvancedHoningTable AdvancedHoning
        {
            get { return Tables != null ? Tables.AdvancedHoning : null; }
        }

        public GemTable Gems
        {
            get { return Tables != null ? Tables.Gem : null; }
        }

        public IReadOnlyList<EngravingRow> Engravings
        {
            get { return Tables != null && Tables.Engraving != null ? Tables.Engraving.All : (IReadOnlyList<EngravingRow>)new EngravingRow[0]; }
        }

        public IReadOnlyList<CardSetRow> CardSets
        {
            get { return Tables != null && Tables.CardSet != null ? Tables.CardSet.All : (IReadOnlyList<CardSetRow>)new CardSetRow[0]; }
        }

        public CharacterClassDefinition GetClass(ECharacterClass InClass)
        {
            for (int i = 0; i < Classes.Count; ++i)
            {
                if (Classes[i] != null && Classes[i].Class == InClass)
                {
                    return Classes[i];
                }
            }

            return null;
        }

        public SkillDefinition GetSkill(int InSkillId)
        {
            for (int c = 0; c < Classes.Count; ++c)
            {
                var cls = Classes[c];
                if (cls == null)
                {
                    continue;
                }

                for (int s = 0; s < cls.Skills.Count; ++s)
                {
                    if (cls.Skills[s] != null && cls.Skills[s].SkillId == InSkillId)
                    {
                        return cls.Skills[s];
                    }
                }

                for (int a = 0; a < cls.Awakenings.Count; ++a)
                {
                    if (cls.Awakenings[a] != null && cls.Awakenings[a].SkillId == InSkillId)
                    {
                        return cls.Awakenings[a];
                    }
                }
            }

            return null;
        }

        public EquipmentSetDefinition GetEquipmentSet(int InSetId)
        {
            for (int i = 0; i < EquipmentSets.Count; ++i)
            {
                if (EquipmentSets[i] != null && EquipmentSets[i].SetId == InSetId)
                {
                    return EquipmentSets[i];
                }
            }

            return null;
        }

        public EngravingRow GetEngraving(int InId)
        {
            EngravingRow row;
            return Tables != null && Tables.Engraving != null && Tables.Engraving.TryGet(InId, out row) == true ? row : null;
        }

        public CardSetRow GetCardSetOf(int InCardId)
        {
            return Tables != null && Tables.CardSet != null ? Tables.CardSet.FindByCard(InCardId) : null;
        }

        public string GetCardName(int InCardId)
        {
            var set = GetCardSetOf(InCardId);
            return set != null ? set.CardNames[InCardId - set.Index * 100] : "(없음)";
        }

        public BossDefinition GetBoss(string InBossId)
        {
            for (int i = 0; i < Bosses.Count; ++i)
            {
                if (Bosses[i] != null && Bosses[i].BossId == InBossId)
                {
                    return Bosses[i];
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 장비 품질 (0~100). 방어구는 체력 %, 무기는 추가 피해 % 로 반영한다 (어림 공식 — 값은 에디터에서 조정).
    /// </summary>
    [System.Serializable]
    public class EquipmentQualitySettings
    {
        [Tooltip("방어구 품질 100 일 때 체력 증가 %")]
        public float ArmorVitalityPercentAt100 = 10.0f;
        [Tooltip("무기 품질 0 일 때 추가 피해 %")]
        public float WeaponDamageAt0 = 10.0f;
        [Tooltip("무기 품질 100 일 때 추가 피해 %")]
        public float WeaponDamageAt100 = 30.0f;
    }
}
