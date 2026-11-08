using System;
using System.Collections.Generic;
using DemoLA.Character;

namespace DemoLA.Build
{
    [Serializable]
    public class EquipmentState
    {
        public EEquipSlot Slot;
        public int SetId;
        public int HoningLevel;
        /// <summary>장인의 기운 % (100 이면 다음 강화 확정).</summary>
        public float ArtisanEnergy;
        /// <summary>현재 단계에서 실패로 누적된 추가 확률 %p.</summary>
        public float FailBonusRate;
        public int Quality = 100;
        /// <summary>상급 재련 단계 / 현재 경험치.</summary>
        public int AdvancedLevel;
        public float AdvancedExp;
        /// <summary>다음 강화 시도에 쓸 보조 재료 개수.</summary>
        public int AuxMaterials;
    }

    [Serializable]
    public class AccessoryState
    {
        public EAccessorySlot Slot;
        public int EngravingA;
        public int PointsA = 3;
        public int EngravingB;
        public int PointsB = 5;
        public int NegativeEngraving;
        public int NegativePoints;
        public ECombatStat StatA = ECombatStat.Crit;
        public float StatAValue = 500.0f;
        public ECombatStat StatB = ECombatStat.None;
        public float StatBValue;
    }

    [Serializable]
    public class EngravingBookState
    {
        public int EngravingId;
        public int Points = 12;
    }

    [Serializable]
    public class AbilityStoneState
    {
        public int EngravingA;
        public int PointsA = 7;
        public int EngravingB;
        public int PointsB = 7;
        public int NegativeEngraving;
        public int NegativePoints;
    }

    [Serializable]
    public class CardSlotState
    {
        public int CardId;
        public int Awakening = 5;
    }

    [Serializable]
    public class GemSlotState
    {
        public EGemType Type;
        public int Level = 10;
        public int SkillId;
    }

    [Serializable]
    public class SkillProgress
    {
        public int SkillId;
        /// <summary>새로 배우는 스킬은 1레벨 (스킬 창에서 포인트를 써 올린다).</summary>
        public int Level = 1;
        /// <summary>단계별 선택한 옵션 인덱스 (-1 = 미선택).</summary>
        public int[] TripodChoices = { -1, -1, -1 };
        public int[] TripodLevels = { 5, 5, 5 };

        public int GetChoice(int InTier)
        {
            return TripodChoices != null && InTier < TripodChoices.Length ? TripodChoices[InTier] : -1;
        }

        public int GetTripodLevel(int InTier)
        {
            return TripodLevels != null && InTier < TripodLevels.Length ? Math.Max(1, TripodLevels[InTier]) : 1;
        }
    }

    /// <summary>
    /// 캐릭터 한 명의 설정 전체 (로컬 JSON 저장 단위). 정의 에셋은 id 로만 참조한다.
    /// </summary>
    [Serializable]
    public class CharacterBuildData
    {
        public string BuildName = "Character";
        public ECharacterClass Class;
        public List<EquipmentState> Equipment = new List<EquipmentState>();
        public List<AccessoryState> Accessories = new List<AccessoryState>();
        public List<EngravingBookState> Books = new List<EngravingBookState>();
        public AbilityStoneState Stone = new AbilityStoneState();
        public List<CardSlotState> Cards = new List<CardSlotState>();
        public List<GemSlotState> Gems = new List<GemSlotState>();
        public List<SkillProgress> Skills = new List<SkillProgress>();
        /// <summary>각성기 2종 중 선택 (0/1).</summary>
        public int SelectedAwakening;
        /// <summary>배틀 아이템 단축키 1~4 에 놓을 BattleItemTable Index. 0 = 빈 칸 (단축키는 칸 번호 그대로).</summary>
        public int[] BattleItemSlots = { 1, 2, 3, 4 };
        /// <summary>스킬 단축키 Q W E R A S D F 에 놓은 스킬 ID. 0 = 빈 칸. 비어 있으면(예전 저장) 직업 스킬 앞 8개.</summary>
        public int[] SkillSlots = new int[0];

        public const int SkillSlotCount = 8;

        /// <summary>SkillSlots 가 없거나 길이가 다르면 InDefault 로 채운다.</summary>
        public void EnsureSkillSlots(IReadOnlyList<int> InDefault)
        {
            if (SkillSlots != null && SkillSlots.Length == SkillSlotCount)
            {
                return;
            }

            SkillSlots = new int[SkillSlotCount];
            for (int i = 0; i < SkillSlotCount && i < InDefault.Count; ++i)
            {
                SkillSlots[i] = InDefault[i];
            }
        }

        /// <summary>InSkillId 를 InSlot 에 놓는다. 이미 다른 칸에 있으면 두 칸을 바꾼다.</summary>
        public void PlaceSkill(int InSlot, int InSkillId)
        {
            if (SkillSlots == null || InSlot < 0 || InSlot >= SkillSlots.Length)
            {
                return;
            }

            int previous = Array.IndexOf(SkillSlots, InSkillId);
            if (previous >= 0 && previous != InSlot)
            {
                SkillSlots[previous] = SkillSlots[InSlot];
            }

            SkillSlots[InSlot] = InSkillId;
        }

        public int SkillSlotOf(int InSkillId)
        {
            return SkillSlots != null ? Array.IndexOf(SkillSlots, InSkillId) : -1;
        }

        public EquipmentState GetEquipment(EEquipSlot InSlot)
        {
            for (int i = 0; i < Equipment.Count; ++i)
            {
                if (Equipment[i].Slot == InSlot)
                {
                    return Equipment[i];
                }
            }

            return null;
        }

        public SkillProgress GetSkill(int InSkillId)
        {
            for (int i = 0; i < Skills.Count; ++i)
            {
                if (Skills[i].SkillId == InSkillId)
                {
                    return Skills[i];
                }
            }

            return null;
        }

        public SkillProgress GetOrAddSkill(int InSkillId)
        {
            var progress = GetSkill(InSkillId);
            if (progress == null)
            {
                progress = new SkillProgress();
                progress.SkillId = InSkillId;
                Skills.Add(progress);
            }

            return progress;
        }

        public CharacterBuildData Clone()
        {
            return UnityEngine.JsonUtility.FromJson<CharacterBuildData>(UnityEngine.JsonUtility.ToJson(this));
        }
    }
}
