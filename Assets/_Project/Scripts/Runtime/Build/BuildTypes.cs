using System;
using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Effects;
using UnityEngine;

namespace DemoLA.Build
{
    public enum EEquipSlot
    {
        Helmet = 0,
        Shoulder = 1,
        Chest = 2,
        Pants = 3,
        Gloves = 4,
        Weapon = 5,
    }

    public enum EAccessorySlot
    {
        Necklace = 0,
        Earring1 = 1,
        Earring2 = 2,
        Ring1 = 3,
        Ring2 = 4,
    }

    public enum ECombatStat
    {
        None = 0,
        Crit = 1,
        Specialization = 2,
        Swiftness = 3,
        Domination = 4,
        Endurance = 5,
        Expertise = 6,
    }

    public enum EGemType
    {
        /// <summary>피해 증가 보석 (멸화 대응).</summary>
        Damage = 0,
        /// <summary>재사용 대기시간 감소 보석 (홍염 대응).</summary>
        Cooldown = 1,
    }

    public static class CombatStatUtil
    {
        public static EAttr ToAttr(ECombatStat InStat)
        {
            switch (InStat)
            {
                case ECombatStat.Crit: return EAttr.StatCrit;
                case ECombatStat.Specialization: return EAttr.StatSpecialization;
                case ECombatStat.Swiftness: return EAttr.StatSwiftness;
                case ECombatStat.Domination: return EAttr.StatDomination;
                case ECombatStat.Endurance: return EAttr.StatEndurance;
                case ECombatStat.Expertise: return EAttr.StatExpertise;
            }

            return EAttr.Count;
        }
    }

    [Serializable]
    public class EquipmentPieceDefinition
    {
        public EEquipSlot Slot;
        public string Name;
        [Tooltip("0강 주스탯 (무기는 0)")]
        public float BaseMainStat = 10000.0f;
        public float MainStatPerHoning = 600.0f;
        public float BaseVitality = 2500.0f;
        public float VitalityPerHoning = 150.0f;
        [Tooltip("무기 공격력 (무기만)")]
        public float BaseWeaponPower;
        public float WeaponPowerPerHoning;
    }

    [Serializable]
    public class EquipmentSetBonus
    {
        public int RequiredPieces = 2;
        [TextArea] public string Description;
        public List<EffectModifierEntry> Modifiers = new List<EffectModifierEntry>();
        [Tooltip("발동형 효과 (조건 · 중첩 · 폭발)")]
        public List<ProcDefinition> Procs = new List<ProcDefinition>();
    }

    [Serializable]
    public class EngravingRules
    {
        [Tooltip("레벨 1 당 필요한 포인트")]
        public int PointsPerLevel = 5;
        public int MaxLevel = 3;
        [Tooltip("각인서 슬롯 수")]
        public int BookSlots = 2;
        [Tooltip("각인서 한 권 최대 포인트")]
        public int MaxBookPoints = 12;
        [Tooltip("어빌리티 스톤 각인 한 줄 최대 포인트")]
        public int MaxStonePoints = 10;
        [Tooltip("악세사리 각인 포인트 선택지 (문서: 3, 5)")]
        public int[] AccessoryPointOptions = { 3, 5 };
        [Tooltip("동시에 활성화되는 (감소 각인 제외) 각인 수 상한. 0 = 제한 없음")]
        public int MaxActiveEngravings;
    }

}
