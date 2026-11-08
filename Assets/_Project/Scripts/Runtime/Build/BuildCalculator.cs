using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Combat;
using DemoLA.Data;
using DemoLA.Effects;
using DemoLA.Skills;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Build
{
    public struct BuildEffect
    {
        public string Label;
        public GameplayEffectDef Def;
    }

    public struct SkillBuildEffect
    {
        public int SkillId;
        public string Label;
        public GameplayEffectDef Def;
        public ETripodTiming Timing;
        public float Scale;
    }

    public class SkillBuildOverride
    {
        public int SkillId;
        public int Level = 1;
        public List<SkillHit> Hits;
        public bool SuperArmor;
        public bool Counter;
    }

    /// <summary>빌드 계산 결과. 순수 데이터라 씬 없이 테스트할 수 있다.</summary>
    public class BuildResult
    {
        public readonly List<BuildEffect> CharacterEffects = new List<BuildEffect>();
        public readonly List<SkillBuildEffect> SkillEffects = new List<SkillBuildEffect>();
        public readonly Dictionary<int, SkillBuildOverride> SkillOverrides = new Dictionary<int, SkillBuildOverride>();
        public readonly Dictionary<int, int> EngravingLevels = new Dictionary<int, int>();
        /// <summary>조건부 피해 모디파이어 (조건부 각인).</summary>
        public readonly List<DamageModifierEntry> DamageModifiers = new List<DamageModifierEntry>();
        public readonly List<string> Summary = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        /// <summary>발동형 효과 (효과, 출처 이름) — BuildProcSystem 이 실행한다.</summary>
        public readonly List<KeyValuePair<ProcDefinition, string>> Procs = new List<KeyValuePair<ProcDefinition, string>>();
        public float MainStat;
        public float WeaponPower;
        public float Vitality;
        public int UsedSkillPoints;
        /// <summary>카드 속성 변환 (None = 스킬 고유 속성).</summary>
        public EElement AttackElement;
        /// <summary>직업 각인이 고른 아이덴티티 변형 (0 = 기본).</summary>
        public int IdentityVariant;
    }

    /// <summary>
    /// CharacterBuildData + GameDatabase → 적용할 GameplayEffect / 조건부 모디파이어 / 어빌리티 선택.
    /// 장비 · 품질 · 상급 재련 · 세트 · 악세 특성 · 각인 · 카드 → 캐릭터, 보석 · 트라이포드 → 스킬.
    /// </summary>
    public static class BuildCalculator
    {
        public static BuildResult Compute(CharacterBuildData InBuild, GameDatabase InDatabase)
        {
            var result = new BuildResult();
            if (InBuild == null || InDatabase == null)
            {
                return result;
            }

            computeEquipment(InBuild, InDatabase, result);
            computeAccessories(InBuild, result);
            computeEngravings(InBuild, InDatabase, result);
            computeCards(InBuild, InDatabase, result);
            computeGems(InBuild, InDatabase, result);
            computeSkills(InBuild, InDatabase, result);
            return result;
        }

        private static void computeEquipment(CharacterBuildData InBuild, GameDatabase InDatabase, BuildResult OutResult)
        {
            var setCounts = new Dictionary<int, int>();
            float weaponDamage = 0.0f;
            var quality = InDatabase.Quality;
            for (int i = 0; i < InBuild.Equipment.Count; ++i)
            {
                var state = InBuild.Equipment[i];
                var set = InDatabase.GetEquipmentSet(state.SetId);
                var piece = set != null ? set.GetPiece(state.Slot) : null;
                if (piece == null)
                {
                    continue;
                }

                float advanced = 1.0f + HoningService.AdvancedStatPercent(state, InDatabase.AdvancedHoning) * 0.01f;
                float q = Mathf.Clamp(state.Quality, 0, 100) * 0.01f;
                OutResult.MainStat += (piece.BaseMainStat + piece.MainStatPerHoning * state.HoningLevel) * advanced;
                OutResult.WeaponPower += (piece.BaseWeaponPower + piece.WeaponPowerPerHoning * state.HoningLevel) * advanced;
                float vitality = (piece.BaseVitality + piece.VitalityPerHoning * state.HoningLevel) * advanced;
                if (state.Slot == EEquipSlot.Weapon)
                {
                    weaponDamage = Mathf.Lerp(quality.WeaponDamageAt0, quality.WeaponDamageAt100, q);
                }
                else
                {
                    vitality *= 1.0f + quality.ArmorVitalityPercentAt100 * q * 0.01f;
                }

                OutResult.Vitality += vitality;
                int count;
                setCounts.TryGetValue(state.SetId, out count);
                setCounts[state.SetId] = count + 1;
            }

            float healthPerVitality = InDatabase.EquipmentSets.Count > 0 && InDatabase.EquipmentSets[0] != null ? InDatabase.EquipmentSets[0].HealthPerVitality : 6.0f;
            var builder = EffectBuilder.Infinite()
                .Add(EAttr.MainStat, OutResult.MainStat)
                .Add(EAttr.WeaponPower, OutResult.WeaponPower)
                .Add(EAttr.MaxHealth, OutResult.Vitality * healthPerVitality);
            if (weaponDamage > 0.0f)
            {
                builder.Add(EAttr.IndependentDamage, weaponDamage);
            }

            OutResult.CharacterEffects.Add(make("장비 기본 능력치", builder.Build()));
            OutResult.Summary.Add($"장비: 주스탯 {OutResult.MainStat:0} / 무기 공격력 {OutResult.WeaponPower:0} / 체력 {OutResult.Vitality:0} / 무기 품질 추가 피해 {weaponDamage:0.#}%");

            foreach (var pair in setCounts)
            {
                var set = InDatabase.GetEquipmentSet(pair.Key);
                if (set == null)
                {
                    continue;
                }

                for (int b = 0; b < set.Bonuses.Count; ++b)
                {
                    var bonus = set.Bonuses[b];
                    if (pair.Value < bonus.RequiredPieces)
                    {
                        continue;
                    }

                    if (bonus.Modifiers.Count > 0)
                    {
                        OutResult.CharacterEffects.Add(make($"{set.DisplayName} {bonus.RequiredPieces}세트", fromEntries(bonus.Modifiers)));
                    }

                    for (int p = 0; p < bonus.Procs.Count; ++p)
                    {
                        OutResult.Procs.Add(new KeyValuePair<ProcDefinition, string>(bonus.Procs[p], $"{set.DisplayName} {bonus.RequiredPieces}세트"));
                    }

                    OutResult.Summary.Add($"{set.DisplayName} {bonus.RequiredPieces}세트: {bonus.Description}");
                }
            }
        }

        private static void computeAccessories(CharacterBuildData InBuild, BuildResult OutResult)
        {
            var builder = EffectBuilder.Infinite();
            bool any = false;
            for (int i = 0; i < InBuild.Accessories.Count; ++i)
            {
                var accessory = InBuild.Accessories[i];
                any |= addStat(builder, accessory.StatA, accessory.StatAValue);
                any |= addStat(builder, accessory.StatB, accessory.StatBValue);
            }

            if (any == true)
            {
                OutResult.CharacterEffects.Add(make("악세사리 특성", builder.Build()));
            }
        }

        private static bool addStat(EffectBuilder InBuilder, ECombatStat InStat, float InValue)
        {
            var attr = CombatStatUtil.ToAttr(InStat);
            if (attr == EAttr.Count || InValue == 0.0f)
            {
                return false;
            }

            InBuilder.Add(attr, InValue);
            return true;
        }

        private static void computeEngravings(CharacterBuildData InBuild, GameDatabase InDatabase, BuildResult OutResult)
        {
            var rules = InDatabase.EngravingRules;
            var levels = EngravingCalculator.ComputeLevels(InBuild, rules);
            int active = 0;
            foreach (var pair in levels)
            {
                var row = InDatabase.GetEngraving(pair.Key);
                if (row == null)
                {
                    continue;
                }

                if (row.IsClass == true && row.ClassRestriction != InBuild.Class)
                {
                    OutResult.Warnings.Add($"{row.Name}: 다른 직업의 직업 각인이라 무시");
                    continue;
                }

                if (row.IsNegative == false)
                {
                    if (rules.MaxActiveEngravings > 0 && active >= rules.MaxActiveEngravings)
                    {
                        OutResult.Warnings.Add($"{row.Name}: 활성 각인 수 상한({rules.MaxActiveEngravings}) 초과");
                        continue;
                    }

                    active += 1;
                }

                int level = pair.Value;
                OutResult.EngravingLevels[row.Index] = level;
                var builder = EffectBuilder.Infinite();
                bool hasStat = false;
                hasStat |= addModifier(builder, row.Mod1Attr, row.Mod1Op, EngravingRow.At(row.Mod1Values, level));
                hasStat |= addModifier(builder, row.Mod2Attr, row.Mod2Op, EngravingRow.At(row.Mod2Values, level));
                if (hasStat == true)
                {
                    OutResult.CharacterEffects.Add(make($"각인 {row.Name} Lv{level}", builder.Build()));
                }

                if (row.HasCondition == true)
                {
                    var modifier = new DamageModifierEntry();
                    modifier.Condition = row.Condition;
                    modifier.Param = row.ConditionParam;
                    modifier.Group = row.ConditionGroup;
                    modifier.Value = EngravingRow.At(row.ConditionValues, level);
                    modifier.Source = row.Name;
                    OutResult.DamageModifiers.Add(modifier);
                }

                if (row.IsClass == true && row.IdentityVariant > 0)
                {
                    OutResult.IdentityVariant = row.IdentityVariant;
                }

                var procs = EngravingProcs.Get(row.Index, level);
                for (int p = 0; p < procs.Count; ++p)
                {
                    OutResult.Procs.Add(new KeyValuePair<ProcDefinition, string>(procs[p], $"각인 {row.Name} Lv{level}"));
                }

                OutResult.Summary.Add($"각인 {row.Name} Lv{level}: {row.Description}");
            }
        }

        private static bool addModifier(EffectBuilder InBuilder, EAttr InAttr, EModifierOp InOp, float InValue)
        {
            if (InValue == 0.0f)
            {
                return false;
            }

            var modifier = new ModifierDef();
            modifier.Attribute = (int)InAttr;
            modifier.Op = InOp;
            modifier.MagnitudeType = EMagnitudeType.Scalar;
            modifier.Value = InValue;
            InBuilder.Modifier(modifier);
            return true;
        }

        private static void computeCards(CharacterBuildData InBuild, GameDatabase InDatabase, BuildResult OutResult)
        {
            var counts = new Dictionary<CardSetRow, int>();
            var awakening = new Dictionary<CardSetRow, int>();
            var seen = new HashSet<int>();
            for (int i = 0; i < InBuild.Cards.Count; ++i)
            {
                var card = InBuild.Cards[i];
                // 같은 카드를 두 장 장착해도 세트 수에는 한 번만 센다.
                if (card.CardId == 0 || seen.Add(card.CardId) == false)
                {
                    continue;
                }

                var set = InDatabase.GetCardSetOf(card.CardId);
                if (set == null)
                {
                    continue;
                }

                int count;
                counts.TryGetValue(set, out count);
                counts[set] = count + 1;
                int sum;
                awakening.TryGetValue(set, out sum);
                awakening[set] = sum + Mathf.Clamp(card.Awakening, 0, set.MaxAwakening);
            }

            foreach (var pair in counts)
            {
                var set = pair.Key;
                int totalAwakening = awakening[set];
                for (int b = 0; b < set.BonusCount; ++b)
                {
                    int needCards = set.BonusCards[b];
                    int needAwakening = set.BonusAwakening != null && b < set.BonusAwakening.Length ? set.BonusAwakening[b] : 0;
                    if (pair.Value < needCards || totalAwakening < needAwakening)
                    {
                        continue;
                    }

                    var attr = set.BonusAttr != null && b < set.BonusAttr.Length ? set.BonusAttr[b] : EAttr.Count;
                    float value = set.BonusValue != null && b < set.BonusValue.Length ? set.BonusValue[b] : 0.0f;
                    string label = needAwakening > 0 ? $"{set.Name} {needCards}세트 ({needAwakening}각성)" : $"{set.Name} {needCards}세트";
                    if (attr != EAttr.Count && value != 0.0f)
                    {
                        OutResult.CharacterEffects.Add(make(label, EffectBuilder.Infinite().Add(attr, value).Build()));
                    }

                    if (set.ConvertElement != EElement.None && b == set.ConvertAtBonus)
                    {
                        OutResult.AttackElement = set.ConvertElement;
                    }

                    OutResult.Summary.Add($"{label}: {set.BonusLabel(b)}");
                }
            }
        }

        private static void computeGems(CharacterBuildData InBuild, GameDatabase InDatabase, BuildResult OutResult)
        {
            if (InDatabase.Gems == null)
            {
                return;
            }

            // 규칙: 11칸 · 같은 스킬에 같은 종류 보석 중복 불가(먼저 장착한 것만) · 각성기에는 적용 불가.
            var used = new HashSet<long>();
            int slots = Mathf.Min(InBuild.Gems.Count, GemTable.SlotCount);
            for (int i = 0; i < slots; ++i)
            {
                var gem = InBuild.Gems[i];
                // 레벨 0 = 빈 칸, 스킬 0 = 아직 스킬을 고르지 않음.
                if (gem.Level <= 0 || gem.SkillId == 0)
                {
                    continue;
                }

                var skill = InDatabase.GetSkill(gem.SkillId);
                if (skill == null || skill.IsAwakening == true)
                {
                    OutResult.Warnings.Add($"보석 {i + 1}: 각성기/없는 스킬에는 장착할 수 없음");
                    continue;
                }

                long key = ((long)gem.SkillId << 8) | (long)gem.Type;
                if (used.Add(key) == false)
                {
                    OutResult.Warnings.Add($"보석 {i + 1}: {skill.DisplayName} 에 같은 종류 보석이 이미 있음");
                    continue;
                }

                float value = InDatabase.Gems.GetValue(gem.Type, gem.Level);
                var attr = gem.Type == EGemType.Damage ? ESkillAttr.GemDamagePercent : ESkillAttr.GemCooldownPercent;
                var effect = new SkillBuildEffect();
                effect.SkillId = gem.SkillId;
                effect.Label = gem.Type == EGemType.Damage ? $"피해 보석 +{value}%" : $"쿨감 보석 -{value}%";
                effect.Def = EffectBuilder.Infinite().SkillAdd(attr, value).Build();
                effect.Timing = ETripodTiming.SkillParameter;
                effect.Scale = 1.0f;
                OutResult.SkillEffects.Add(effect);
            }
        }

        private static void computeSkills(CharacterBuildData InBuild, GameDatabase InDatabase, BuildResult OutResult)
        {
            // 문서: 트라이포드 레벨은 모두 최대 (기본 5).
            int tripodLevel = Mathf.Max(1, InDatabase.TripodLevel);
            for (int i = 0; i < InBuild.Skills.Count; ++i)
            {
                var progress = InBuild.Skills[i];
                var skill = InDatabase.GetSkill(progress.SkillId);
                if (skill == null)
                {
                    continue;
                }

                var skillOverride = new SkillBuildOverride();
                skillOverride.SkillId = skill.SkillId;
                skillOverride.Level = Mathf.Clamp(progress.Level, 1, skill.MaxLevel);
                OutResult.UsedSkillPoints += SkillPointCost(skillOverride.Level);

                for (int tier = 0; tier < skill.Tiers.Count; ++tier)
                {
                    var tripodTier = skill.Tiers[tier];
                    int choice = progress.GetChoice(tier);
                    if (choice < 0 || choice >= tripodTier.Options.Count || skillOverride.Level < tripodTier.UnlockSkillLevel)
                    {
                        continue;
                    }

                    var option = tripodTier.Options[choice];
                    int level = Mathf.Clamp(tripodLevel, 1, option.MaxLevel);
                    for (int e = 0; e < option.Effects.Count; ++e)
                    {
                        var tripodEffect = option.Effects[e];
                        var def = tripodEffect.GetDef();
                        if (def == null)
                        {
                            continue;
                        }

                        var effect = new SkillBuildEffect();
                        effect.SkillId = skill.SkillId;
                        effect.Label = $"{skill.DisplayName} - {option.Name} Lv{level}";
                        effect.Def = def;
                        effect.Timing = tripodEffect.Timing;
                        effect.Scale = tripodEffect.ScaleAt(level);
                        OutResult.SkillEffects.Add(effect);
                    }

                    if (option.ReplaceHits != null && option.ReplaceHits.Count > 0)
                    {
                        skillOverride.Hits = option.ReplaceHits;
                    }

                    skillOverride.SuperArmor |= option.GrantsSuperArmor;
                    skillOverride.Counter |= option.GrantsCounter;
                }

                OutResult.SkillOverrides[skill.SkillId] = skillOverride;
            }

            if (OutResult.UsedSkillPoints > InDatabase.MaxSkillPoints)
            {
                OutResult.Warnings.Add($"스킬 포인트 초과: {OutResult.UsedSkillPoints} / {InDatabase.MaxSkillPoints}");
            }
        }

        /// <summary>스킬 레벨 → 누적 스킬 포인트 (자리 표시 규칙: 2레벨부터 레벨당 4, 10레벨부터 레벨당 8).</summary>
        public static int SkillPointCost(int InLevel)
        {
            int cost = 0;
            for (int level = 2; level <= InLevel; ++level)
            {
                cost += level >= 10 ? 8 : 4;
            }

            return cost;
        }

        private static GameplayEffectDef fromEntries(List<EffectModifierEntry> InEntries)
        {
            var builder = EffectBuilder.Infinite();
            for (int i = 0; i < InEntries.Count; ++i)
            {
                builder.Modifier(InEntries[i].ToDef());
            }

            return builder.Build();
        }

        private static BuildEffect make(string InLabel, GameplayEffectDef InDef)
        {
            var effect = new BuildEffect();
            effect.Label = InLabel;
            effect.Def = InDef;
            return effect;
        }
    }
}
