using System.Collections.Generic;
using DemoLA.Build;
using DemoLA.Character;
using DemoLA.Data;
using DemoLA.Skills;
using UnityEngine;

namespace DemoLA.UI
{
    /// <summary>각 아이콘(장비 · 악세사리 · 각인 · 카드 · 보석 · 스킬 · 배틀 아이템)의 아이콘 스프라이트와 툴팁 내용.</summary>
    public static class ItemTooltips
    {
        // ------------------------------------------------------------------ names

        public static string SlotName(EEquipSlot InSlot)
        {
            switch (InSlot)
            {
                case EEquipSlot.Helmet: return "머리 방어구";
                case EEquipSlot.Shoulder: return "어깨 방어구";
                case EEquipSlot.Chest: return "상의";
                case EEquipSlot.Pants: return "하의";
                case EEquipSlot.Gloves: return "장갑";
                default: return "무기";
            }
        }

        public static string SlotName(EAccessorySlot InSlot)
        {
            switch (InSlot)
            {
                case EAccessorySlot.Necklace: return "목걸이";
                case EAccessorySlot.Earring1: return "귀걸이 1";
                case EAccessorySlot.Earring2: return "귀걸이 2";
                case EAccessorySlot.Ring1: return "반지 1";
                default: return "반지 2";
            }
        }

        public static string StatName(ECombatStat InStat)
        {
            switch (InStat)
            {
                case ECombatStat.Crit: return "치명";
                case ECombatStat.Specialization: return "특화";
                case ECombatStat.Swiftness: return "신속";
                case ECombatStat.Domination: return "제압";
                case ECombatStat.Endurance: return "인내";
                case ECombatStat.Expertise: return "숙련";
                default: return "없음";
            }
        }

        public static EIconKind AccessoryKind(EAccessorySlot InSlot)
        {
            switch (InSlot)
            {
                case EAccessorySlot.Necklace: return EIconKind.Necklace;
                case EAccessorySlot.Earring1:
                case EAccessorySlot.Earring2: return EIconKind.Earring;
                default: return EIconKind.Ring;
            }
        }

        /// <summary>강화 단계 → 테두리 등급 (0~4).</summary>
        public static int HoningGrade(int InLevel)
        {
            return Mathf.Clamp(InLevel / 5, 0, 4);
        }

        // ------------------------------------------------------------------ equipment

        public static Sprite EquipmentIcon(GameDatabase InDatabase, EquipmentState InState)
        {
            var piece = findPiece(InDatabase, InState);
            var kind = InState.Slot == EEquipSlot.Weapon ? EIconKind.Weapon : EIconKind.Armor;
            return IconFactory.Get(kind, piece != null ? piece.Name : InState.Slot.ToString(), HoningGrade(InState.HoningLevel));
        }

        public static TooltipContent Equipment(GameDatabase InDatabase, EquipmentState InState)
        {
            return Equipment(InDatabase, InState, null);
        }

        /// <summary>InBuild 가 있으면 세트 효과 중 지금 켜진 단계를 초록색으로 표시한다.</summary>
        public static TooltipContent Equipment(GameDatabase InDatabase, EquipmentState InState, CharacterBuildData InBuild)
        {
            var set = InDatabase.GetEquipmentSet(InState.SetId);
            var piece = findPiece(InDatabase, InState);
            int grade = HoningGrade(InState.HoningLevel);
            var content = new TooltipContent
            {
                Icon = EquipmentIcon(InDatabase, InState),
                Title = $"+{InState.HoningLevel} {(piece != null ? piece.Name : SlotName(InState.Slot))}",
                TitleColor = IconFactory.GradeColor(grade),
                Subtitle = $"{SlotName(InState.Slot)} · {(set != null ? set.DisplayName : "세트 없음")}",
            };

            content.Line($"품질 {InState.Quality}", qualityColor(InState.Quality));
            if (piece != null)
            {
                float advanced = 1.0f + HoningService.AdvancedStatPercent(InState, InDatabase.AdvancedHoning) * 0.01f;
                content.Header("기본 효과");
                content.Line($"주 능력치 +{(piece.BaseMainStat + piece.MainStatPerHoning * InState.HoningLevel) * advanced:N0}");
                content.Line($"체력 +{(piece.BaseVitality + piece.VitalityPerHoning * InState.HoningLevel) * advanced:N0}");
                if (piece.BaseWeaponPower > 0.0f || piece.WeaponPowerPerHoning > 0.0f)
                {
                    content.Line($"무기 공격력 +{(piece.BaseWeaponPower + piece.WeaponPowerPerHoning * InState.HoningLevel) * advanced:N0}");
                }
            }

            content.Header("재련");
            content.Line($"상급 재련 {InState.AdvancedLevel}단계 ({InState.AdvancedExp:0} 경험치)");
            content.Line($"장인의 기운 {InState.ArtisanEnergy:0.0}%", TooltipContent.Dim);

            AppendSetBonuses(content, set, InBuild);
            return content;
        }

        /// <summary>도감의 장비 조각 (아직 끼지 않은 것 포함).</summary>
        public static TooltipContent EquipmentPiece(EquipmentSetDefinition InSet, EEquipSlot InSlot, CharacterBuildData InBuild)
        {
            var piece = InSet.GetPiece(InSlot);
            var content = new TooltipContent
            {
                Icon = IconFactory.Get(InSlot == EEquipSlot.Weapon ? EIconKind.Weapon : EIconKind.Armor, piece != null ? piece.Name : InSlot.ToString(), 3),
                Title = piece != null ? piece.Name : SlotName(InSlot),
                TitleColor = IconFactory.GradeColor(3),
                Subtitle = $"{SlotName(InSlot)} · {InSet.DisplayName}",
            };

            if (string.IsNullOrEmpty(InSet.Description) == false)
            {
                content.Line(InSet.Description, TooltipContent.Dim);
            }

            if (piece != null)
            {
                content.Header("기본 효과 (재련 0 기준, 재련 단계마다 증가)");
                if (piece.BaseMainStat > 0.0f)
                {
                    content.Line($"주 능력치 +{piece.BaseMainStat:N0} (단계당 +{piece.MainStatPerHoning:N0})");
                    content.Line($"체력 +{piece.BaseVitality:N0} (단계당 +{piece.VitalityPerHoning:N0})");
                }

                if (piece.BaseWeaponPower > 0.0f)
                {
                    content.Line($"무기 공격력 +{piece.BaseWeaponPower:N0} (단계당 +{piece.WeaponPowerPerHoning:N0})");
                }
            }

            AppendSetBonuses(content, InSet, InBuild);
            content.Line(string.Empty);
            content.Line("끌어서 같은 부위 칸에 놓거나 우클릭하면 장착 (재련 단계는 유지)", TooltipContent.Dim);
            return content;
        }

        /// <summary>세트 효과: 단계별 설명 + 수치 + 발동형 효과. 켜진 단계는 초록, 아니면 회색.</summary>
        public static void AppendSetBonuses(TooltipContent OutContent, EquipmentSetDefinition InSet, CharacterBuildData InBuild)
        {
            if (InSet == null || InSet.Bonuses.Count == 0)
            {
                return;
            }

            int count = 0;
            if (InBuild != null)
            {
                for (int i = 0; i < InBuild.Equipment.Count; ++i)
                {
                    count += InBuild.Equipment[i].SetId == InSet.SetId ? 1 : 0;
                }
            }

            OutContent.Header(InBuild != null ? $"{InSet.DisplayName} 세트 효과 ({count}/6)" : $"{InSet.DisplayName} 세트 효과");
            for (int i = 0; i < InSet.Bonuses.Count; ++i)
            {
                var bonus = InSet.Bonuses[i];
                bool active = InBuild != null && count >= bonus.RequiredPieces;
                var color = active ? TooltipContent.Good : TooltipContent.Dim;
                OutContent.Line($"[{bonus.RequiredPieces}세트] {bonus.Description}", active ? TooltipContent.Section : TooltipContent.Body);
                for (int m = 0; m < bonus.Modifiers.Count; ++m)
                {
                    OutContent.Line("  · " + KoreanText.Modifier(bonus.Modifiers[m].Attribute, bonus.Modifiers[m].Op, bonus.Modifiers[m].Value), color);
                }

                for (int p = 0; p < bonus.Procs.Count; ++p)
                {
                    OutContent.Line("  · " + bonus.Procs[p].Describe(), color);
                }
            }
        }

        private static EquipmentPieceDefinition findPiece(GameDatabase InDatabase, EquipmentState InState)
        {
            var set = InDatabase.GetEquipmentSet(InState.SetId);
            return set != null ? set.GetPiece(InState.Slot) : null;
        }

        private static Color qualityColor(int InQuality)
        {
            if (InQuality >= 100)
            {
                return IconFactory.GradeColor(4);
            }

            if (InQuality >= 90)
            {
                return IconFactory.GradeColor(3);
            }

            return InQuality >= 70 ? IconFactory.GradeColor(2) : TooltipContent.Body;
        }

        // ------------------------------------------------------------------ accessory / stone

        public static Sprite AccessoryIcon(AccessoryState InState)
        {
            int grade = Mathf.Clamp((InState.PointsA + InState.PointsB) / 3, 0, 4);
            return IconFactory.Get(AccessoryKind(InState.Slot), InState.Slot.ToString(), grade);
        }

        public static TooltipContent Accessory(GameDatabase InDatabase, AccessoryState InState)
        {
            var content = new TooltipContent
            {
                Icon = AccessoryIcon(InState),
                Title = SlotName(InState.Slot),
                TitleColor = IconFactory.GradeColor(Mathf.Clamp((InState.PointsA + InState.PointsB) / 3, 0, 4)),
                Subtitle = "악세사리",
            };

            content.Header("특성 (2종)");
            content.Line(InState.StatA != ECombatStat.None ? $"{StatName(InState.StatA)} +{InState.StatAValue:0}" : "특성 1: 없음", InState.StatA != ECombatStat.None ? TooltipContent.Body : TooltipContent.Dim);
            content.Line(InState.StatB != ECombatStat.None ? $"{StatName(InState.StatB)} +{InState.StatBValue:0}" : "특성 2: 없음", InState.StatB != ECombatStat.None ? TooltipContent.Body : TooltipContent.Dim);

            content.Header("각인 효과");
            engravingLine(content, InDatabase, InState.EngravingA, InState.PointsA, false);
            engravingLine(content, InDatabase, InState.EngravingB, InState.PointsB, false);
            engravingLine(content, InDatabase, InState.NegativeEngraving, InState.NegativePoints, true);
            return content;
        }

        public static Sprite StoneIcon(AbilityStoneState InState)
        {
            return IconFactory.Get(EIconKind.Stone, "stone", Mathf.Clamp((InState.PointsA + InState.PointsB) / 4, 0, 4));
        }

        public static TooltipContent Stone(GameDatabase InDatabase, AbilityStoneState InState)
        {
            var content = new TooltipContent
            {
                Icon = StoneIcon(InState),
                Title = "어빌리티 스톤",
                TitleColor = IconFactory.GradeColor(Mathf.Clamp((InState.PointsA + InState.PointsB) / 4, 0, 4)),
                Subtitle = $"세공 {InState.PointsA} / {InState.PointsB} / 감소 {InState.NegativePoints}",
            };

            content.Header("각인 효과");
            engravingLine(content, InDatabase, InState.EngravingA, InState.PointsA, false);
            engravingLine(content, InDatabase, InState.EngravingB, InState.PointsB, false);
            engravingLine(content, InDatabase, InState.NegativeEngraving, InState.NegativePoints, true);
            return content;
        }

        private static void engravingLine(TooltipContent OutContent, GameDatabase InDatabase, int InId, int InPoints, bool InNegative)
        {
            var row = InDatabase.GetEngraving(InId);
            if (row == null || InPoints <= 0)
            {
                return;
            }

            OutContent.Line($"[{row.Name}] 활성도 +{InPoints}", InNegative == true || row.IsNegative == true ? TooltipContent.Bad : TooltipContent.Body);
        }

        // ------------------------------------------------------------------ engraving

        public static Sprite EngravingIcon(GameDatabase InDatabase, int InId, int InLevel)
        {
            var row = InDatabase.GetEngraving(InId);
            if (row == null)
            {
                return IconFactory.Get(EIconKind.Empty, "none", 0);
            }

            return IconFactory.Get(EIconKind.Engraving, row.Name, row.IsNegative == true ? 0 : Mathf.Clamp(InLevel + 1, 1, 4));
        }

        public static TooltipContent Engraving(GameDatabase InDatabase, int InId, int InPoints)
        {
            var row = InDatabase.GetEngraving(InId);
            if (row == null)
            {
                return new TooltipContent { Title = "(각인 없음)", TitleColor = TooltipContent.Dim };
            }

            var rules = InDatabase.EngravingRules;
            int level = EngravingCalculator.LevelOf(InPoints, rules);
            var content = new TooltipContent
            {
                Icon = EngravingIcon(InDatabase, InId, level),
                Title = row.Name,
                TitleColor = row.IsNegative == true ? TooltipContent.Bad : (row.IsClass == true ? IconFactory.GradeColor(4) : UIKit.AccentColor),
                Subtitle = $"{(row.IsNegative ? "감소 각인" : (row.IsClass ? "직업 각인" : "전투 각인"))} · 활성도 {InPoints} → Lv{level}",
            };

            if (string.IsNullOrEmpty(row.Description) == false)
            {
                content.Line(row.Description);
            }

            for (int lv = 1; lv <= rules.MaxLevel; ++lv)
            {
                bool current = lv == level;
                var color = current ? TooltipContent.Good : TooltipContent.Dim;
                content.Header($"Lv{lv} (활성도 {lv * rules.PointsPerLevel}){(current ? "  ◀ 현재" : string.Empty)}");
                float v1 = EngravingRow.At(row.Mod1Values, lv);
                float v2 = EngravingRow.At(row.Mod2Values, lv);
                if (v1 != 0.0f)
                {
                    content.Line("· " + KoreanText.Modifier(row.Mod1Attr, row.Mod1Op, v1), color);
                }

                if (v2 != 0.0f)
                {
                    content.Line("· " + KoreanText.Modifier(row.Mod2Attr, row.Mod2Op, v2), color);
                }

                if (row.HasCondition == true)
                {
                    float value = EngravingRow.At(row.ConditionValues, lv);
                    if (value != 0.0f)
                    {
                        string unit = row.Condition == DemoLA.Combat.EDamageCondition.MoveSpeedScaled ? "배" : "%";
                        content.Line($"· {KoreanText.Condition(row.Condition, row.ConditionParam)} {KoreanText.Group(row.ConditionGroup)} +{value:0.##}{unit}", color);
                    }
                }

                var procs = EngravingProcs.Get(row.Index, lv);
                for (int p = 0; p < procs.Count; ++p)
                {
                    content.Line("· " + procs[p].Describe(), color);
                }
            }

            return content;
        }

        // ------------------------------------------------------------------ card

        public static Sprite CardIcon(GameDatabase InDatabase, CardSlotState InSlot)
        {
            if (InSlot.CardId == 0)
            {
                return IconFactory.Get(EIconKind.Empty, "card", 0);
            }

            return IconFactory.Get(EIconKind.Card, InDatabase.GetCardName(InSlot.CardId), Mathf.Clamp(InSlot.Awakening - 1, 0, 4));
        }

        public static TooltipContent Card(GameDatabase InDatabase, CharacterBuildData InBuild, CardSlotState InSlot)
        {
            var set = InDatabase.GetCardSetOf(InSlot.CardId);
            if (InSlot.CardId == 0 || set == null)
            {
                return new TooltipContent { Title = "(빈 카드 슬롯)", TitleColor = TooltipContent.Dim };
            }

            int count = 0;
            int awakening = 0;
            for (int i = 0; i < InBuild.Cards.Count; ++i)
            {
                if (InDatabase.GetCardSetOf(InBuild.Cards[i].CardId) == set)
                {
                    count += 1;
                    awakening += Mathf.Clamp(InBuild.Cards[i].Awakening, 0, set.MaxAwakening);
                }
            }

            var content = new TooltipContent
            {
                Icon = CardIcon(InDatabase, InSlot),
                Title = InDatabase.GetCardName(InSlot.CardId),
                TitleColor = IconFactory.GradeColor(Mathf.Clamp(InSlot.Awakening - 1, 0, 4)),
                Subtitle = $"각성 {InSlot.Awakening} / {set.MaxAwakening}",
            };

            content.Header($"{set.Name} ({count}장 · 각성 합계 {awakening})");
            for (int b = 0; b < set.BonusCount; ++b)
            {
                int needCards = set.BonusCards[b];
                int needAwakening = set.BonusAwakening != null && b < set.BonusAwakening.Length ? set.BonusAwakening[b] : 0;
                bool active = count >= needCards && awakening >= needAwakening;
                string label = needAwakening > 0 ? $"{needCards}세트 ({needAwakening}각성)" : $"{needCards}세트";
                content.Line($"{label}: {set.BonusLabel(b)}", active == true ? TooltipContent.Good : TooltipContent.Dim);
            }

            return content;
        }

        // ------------------------------------------------------------------ gem

        public static Sprite GemIcon(GemSlotState InGem)
        {
            var kind = InGem.Type == EGemType.Damage ? EIconKind.GemDamage : EIconKind.GemCooldown;
            return IconFactory.Get(kind, "gem", Mathf.Clamp((InGem.Level - 1) / 2, 0, 4));
        }

        public static TooltipContent Gem(GameDatabase InDatabase, GemSlotState InGem)
        {
            var table = InDatabase.Gems;
            var skill = InDatabase.GetSkill(InGem.SkillId);
            bool damage = InGem.Type == EGemType.Damage;
            var content = new TooltipContent
            {
                Icon = GemIcon(InGem),
                Title = $"{InGem.Level}레벨 {(damage ? "피해" : "재사용 대기시간")} 보석",
                TitleColor = IconFactory.GradeColor(Mathf.Clamp((InGem.Level - 1) / 2, 0, 4)),
                Subtitle = "보석",
            };

            content.Header("효과");
            string value = table != null ? $"{table.GetValue(InGem.Type, InGem.Level)}%" : "?";
            string target = skill != null ? skill.DisplayName : "(스킬 없음)";
            content.Line(damage ? $"[{target}] 피해 {value} 증가" : $"[{target}] 재사용 대기시간 {value} 감소", TooltipContent.Good);
            content.Line("같은 스킬 · 같은 종류 보석은 가장 높은 것 하나만 적용", TooltipContent.Dim);
            return content;
        }

        // ------------------------------------------------------------------ skill

        public static Sprite SkillIcon(SkillDefinition InSkill)
        {
            return IconFactory.Get(EIconKind.Skill, InSkill != null ? InSkill.DisplayName : "none", InSkill != null && InSkill.IsAwakening ? 4 : 2);
        }

        public static TooltipContent Skill(SkillDefinition InSkill, SkillProgress InProgress)
        {
            return Skill(InSkill, InProgress, null);
        }

        /// <summary>
        /// 스킬 상세 툴팁: 분류 · 재사용 대기시간 · 마나 · 피해(계수와 내 공격력 기준 예상치) · 타격별 범위 · 무력화/부위 파괴 등급 ·
        /// 공격 방향 · 보호 효과(넉백 면역 / 피격 이상 면역) · 카운터 · 이동 · 상태이상 · 속성 · 트라이포드.
        /// </summary>
        public static TooltipContent Skill(SkillDefinition InSkill, SkillProgress InProgress, DemoLA.Combat.CombatActor InActor)
        {
            if (InSkill == null)
            {
                return new TooltipContent { Title = "(스킬 없음)", TitleColor = TooltipContent.Dim };
            }

            int level = InProgress != null ? InProgress.Level : 1;
            var tags = new List<string>();
            tags.Add(InSkill.IsAwakening ? "각성기" : "일반 스킬");
            if (InSkill.Positional == ESkillPositionalType.BackAttack)
            {
                tags.Add("백어택");
            }
            else if (InSkill.Positional == ESkillPositionalType.HeadAttack)
            {
                tags.Add("헤드어택");
            }

            if (InSkill.IsCounter == true)
            {
                tags.Add("카운터");
            }

            var content = new TooltipContent
            {
                Icon = SkillIcon(InSkill),
                Title = InSkill.DisplayName,
                TitleColor = InSkill.IsAwakening ? IconFactory.GradeColor(4) : UIKit.AccentColor,
                Subtitle = $"스킬 레벨 {level} / {InSkill.MaxLevel} · {string.Join(" · ", tags)}",
            };

            content.Line($"재사용 대기시간 {InSkill.Cooldown:0.#}초 · 마나 {InSkill.ManaCost:0} · 사거리 약 {InSkill.EstimatedRange:0.#}m");
            if (string.IsNullOrEmpty(InSkill.Description) == false)
            {
                content.Line(InSkill.Description, TooltipContent.Dim);
            }

            // 피해
            float coefficient = 0.0f;
            float stagger = 0.0f;
            float destruction = 0.0f;
            for (int i = 0; i < InSkill.Hits.Count; ++i)
            {
                coefficient += InSkill.Hits[i].Coefficient;
                stagger += InSkill.Hits[i].Stagger;
                destruction += InSkill.Hits[i].Destruction;
            }

            float levelBonus = InSkill.LevelDamagePercent(level);
            content.Header("피해");
            content.Line($"공격력의 {coefficient * 100.0f:0}% 피해 (스킬 레벨 보정 +{levelBonus:0}%)");
            if (InActor != null && InActor.Stats != null)
            {
                float attack = InActor.Stats.Get(DemoLA.Attributes.EAttr.AttackPower);
                float estimate = attack * coefficient * (1.0f + levelBonus * 0.01f) * DemoLA.Attributes.StatFormula.Settings.PlayerDamageScale;
                content.Line($"내 공격력 {attack:N0} 기준 약 {estimate:N0} (치명타 · 방어 · 각인 제외)", TooltipContent.Good);
            }

            for (int i = 0; i < InSkill.Hits.Count; ++i)
            {
                var hit = InSkill.Hits[i];
                var extras = new List<string>();
                if (hit.MoveForward > 0.0f)
                {
                    extras.Add($"앞으로 {hit.MoveForward:0.#}m 이동");
                }

                if (hit.ApplyStatus != DemoLA.Core.ETag.None)
                {
                    extras.Add($"{StatusName(hit.ApplyStatus)} {hit.StatusDuration:0.#}초");
                }

                content.Line($"  {i + 1}타 · {AreaText(hit.Area)} · 계수 {hit.Coefficient * 100.0f:0}%{(extras.Count > 0 ? " · " + string.Join(" · ", extras) : string.Empty)}", TooltipContent.Dim);
            }

            content.Header("특성");
            content.Line($"무력화 {StaggerGrade(stagger)} · 부위 파괴 {(destruction > 0.0f ? $"{Mathf.CeilToInt(destruction)}레벨" : "없음")} · {KoreanText.Element(InSkill.Element)}");
            if (InSkill.DashDistance > 0.0f)
            {
                content.Line($"시전 시 {InSkill.DashDistance:0.#}m 돌진");
            }

            var protection = InSkill.SuperArmor == true ? ESkillProtection.SuperArmor : InSkill.Protection;
            switch (protection)
            {
                case ESkillProtection.PushImmune:
                    content.Line("경직 면역 (넉백 면역): 사용 중 밀려나지 않습니다. 띄우기 · 기절은 받습니다.", TooltipContent.Section);
                    break;
                case ESkillProtection.SuperArmor:
                    content.Line("피격 이상 면역: 사용 중 넉백 · 띄우기 · 기절 · 공포 등에 걸리지 않습니다. 피해는 받습니다.", TooltipContent.Section);
                    break;
            }

            if (InSkill.IsCounter == true)
            {
                content.Line("카운터 가능: 보스의 카운터 구간(푸른 빛)에 맞히면 보스가 무력화됩니다.", TooltipContent.Section);
            }

            if (InSkill.Tiers.Count > 0)
            {
                content.Header("트라이포드");
                for (int t = 0; t < InSkill.Tiers.Count && t < 3; ++t)
                {
                    var tier = InSkill.Tiers[t];
                    int choice = InProgress != null ? InProgress.GetChoice(t) : -1;
                    bool unlocked = level >= tier.UnlockSkillLevel;
                    if (choice < 0 || choice >= tier.Options.Count)
                    {
                        content.Line($"{t + 1}단계 (스킬 레벨 {tier.UnlockSkillLevel}): 선택 안 함", TooltipContent.Dim);
                        continue;
                    }

                    var option = tier.Options[choice];
                    content.Line($"{t + 1}단계 {option.Name}{(unlocked ? string.Empty : $" (스킬 레벨 {tier.UnlockSkillLevel} 필요)")}", unlocked ? TooltipContent.Good : TooltipContent.Dim);
                    if (string.IsNullOrEmpty(option.Description) == false)
                    {
                        content.Line("  " + option.Description, TooltipContent.Dim);
                    }
                }
            }

            return content;
        }

        /// <summary>무력화 수치 합 → 등급.</summary>
        public static string StaggerGrade(float InStagger)
        {
            if (InStagger <= 0.0f)
            {
                return "없음";
            }

            if (InStagger < 15.0f)
            {
                return "하";
            }

            if (InStagger < 25.0f)
            {
                return "중";
            }

            if (InStagger < 35.0f)
            {
                return "중상";
            }

            return InStagger < 60.0f ? "상" : "최상";
        }

        public static string AreaText(DemoLA.Combat.HitArea InArea)
        {
            switch (InArea.Shape)
            {
                case DemoLA.Combat.EHitShape.Circle: return InArea.ForwardOffset > 0.0f ? $"전방 {InArea.ForwardOffset:0.#}m 원형 {InArea.Radius:0.#}m" : $"주변 원형 {InArea.Radius:0.#}m";
                case DemoLA.Combat.EHitShape.Fan: return $"전방 부채꼴 {InArea.Angle:0}° {InArea.Radius:0.#}m";
                case DemoLA.Combat.EHitShape.Rect: return $"전방 직선 {InArea.Length:0.#}×{InArea.Width:0.#}m";
                case DemoLA.Combat.EHitShape.Donut: return $"도넛 {InArea.InnerRadius:0.#}~{InArea.Radius:0.#}m";
                default: return "전체 범위";
            }
        }

        public static string StatusName(DemoLA.Core.ETag InTag)
        {
            switch (InTag)
            {
                case DemoLA.Core.ETag.State_Stunned: return "기절";
                case DemoLA.Core.ETag.State_Airborne: return "띄우기";
                case DemoLA.Core.ETag.State_Frozen: return "빙결";
                case DemoLA.Core.ETag.State_Feared: return "공포";
                case DemoLA.Core.ETag.State_Silenced: return "침묵";
                default: return "상태 이상";
            }
        }

        public static TooltipContent Tripod(SkillDefinition InSkill, int InTier, int InChoice)
        {
            if (InSkill == null || InTier >= InSkill.Tiers.Count)
            {
                return null;
            }

            var tier = InSkill.Tiers[InTier];
            var content = new TooltipContent
            {
                Title = $"{InSkill.DisplayName} — {InTier + 1}단계 트라이포드",
                Subtitle = $"스킬 Lv{tier.UnlockSkillLevel} 에 열림",
            };

            for (int o = 0; o < tier.Options.Count; ++o)
            {
                var option = tier.Options[o];
                content.Line($"{option.Name}", o == InChoice ? TooltipContent.Good : TooltipContent.Section);
                if (string.IsNullOrEmpty(option.Description) == false)
                {
                    content.Line("  " + option.Description, o == InChoice ? TooltipContent.Body : TooltipContent.Dim);
                }
            }

            return content;
        }

        // ------------------------------------------------------------------ battle item

        public static Sprite BattleItemIcon(BattleItemRow InRow)
        {
            return IconFactory.Get(EIconKind.BattleItem, InRow != null ? InRow.Name : "none", InRow != null ? 2 : 0);
        }

        public static TooltipContent BattleItem(BattleItemRow InRow)
        {
            if (InRow == null)
            {
                return new TooltipContent { Title = "(비어 있음)", TitleColor = TooltipContent.Dim };
            }

            var content = new TooltipContent
            {
                Icon = BattleItemIcon(InRow),
                Title = InRow.Name,
                Subtitle = "배틀 아이템",
            };

            if (string.IsNullOrEmpty(InRow.Description) == false)
            {
                content.Line(InRow.Description);
            }

            content.Line($"재사용 대기시간 {InRow.Cooldown:0.#}초 · 최대 {InRow.MaxCount}개", TooltipContent.Dim);
            return content;
        }
    }
}
