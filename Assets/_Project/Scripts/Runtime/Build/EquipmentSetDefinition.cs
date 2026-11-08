using System;
using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Effects;
using UnityEngine;

namespace DemoLA.Build
{
    /// <summary>
    /// 장비 세트 (문서: "환각 세트"). 세트 효과 수치는 확인되지 않아 자리 표시 값이며 에디터에서 바꾼다.
    /// </summary>
    [CreateAssetMenu(menuName = "DemoLA/Build/Equipment Set", fileName = "SET_New")]
    public class EquipmentSetDefinition : ScriptableObject
    {
        public int SetId;
        public string DisplayName = "Set";
        [TextArea] public string Description;
        public List<EquipmentPieceDefinition> Pieces = new List<EquipmentPieceDefinition>();
        public List<EquipmentSetBonus> Bonuses = new List<EquipmentSetBonus>();
        [Tooltip("체력 1 당 최대 생명력")]
        public float HealthPerVitality = 6.0f;

        public EquipmentPieceDefinition GetPiece(EEquipSlot InSlot)
        {
            for (int i = 0; i < Pieces.Count; ++i)
            {
                if (Pieces[i].Slot == InSlot)
                {
                    return Pieces[i];
                }
            }

            return null;
        }
    }
}
