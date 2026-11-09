using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Build;
using DemoLA.Character;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Data;
using DemoLA.Effects;
using DemoLA.Skills;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Content
{
    /// <summary>
    /// 기본 콘텐츠를 코드로 만든다. 에디터 셋업이 이 결과를 에셋으로 저장하고, 에셋이 없을 때 런타임/테스트가 그대로 쓴다.
    /// 이름은 모두 이 프로젝트용 이름이며, 수치는 전부 자리 표시 값이다 (구현 보류 사항.md 에 채울 항목 정리).
    /// </summary>
    public static class DefaultContent
    {
        /// <summary>생성된 하위 ScriptableObject (에디터가 서브 에셋으로 저장할 때 쓴다).</summary>
        public static readonly List<ScriptableObject> CreatedObjects = new List<ScriptableObject>();

        /// <summary>
        /// InTables(CSV 정적 데이터)로 기본 GameDatabase 를 만든다. 테이블은 에디터 임포터가 만든 에셋이거나,
        /// 에디터/테스트에서 DemoLATables.FromCsv 로 만든 메모리 테이블이다.
        /// </summary>
        public static GameDatabase CreateDatabase(DemoLATables InTables)
        {
            CreatedObjects.Clear();
            GameEffects.Initialize();
            var db = ScriptableObject.CreateInstance<GameDatabase>();
            db.name = "GameDatabase";
            db.Tables = InTables;
            db.Esthers = DemoLA.Character.EstherSystem.CreateDefaults();
            db.EquipmentSets.AddRange(createEquipmentSets());
            db.Classes.Add(createSummoner());
            db.Classes.Add(createInfighter());
            db.Classes.Add(createSlayer());
            db.Classes.Add(createArtist());
            for (int i = 0; i < db.Classes.Count; ++i)
            {
                // 스킬 창 · 기본 단축키 순서 = 스킬 번호 순.
                db.Classes[i].Skills.Sort((x, y) => x.SkillId.CompareTo(y.SkillId));
            }

            if (InTables != null && InTables.BossPattern != null && InTables.BossPhase != null)
            {
                db.Bosses.Add(createBoss("Gate1", "1관문 수문장", "G1", 60, InTables, DefaultTrees.CreateGate1Boss()));
                db.Bosses.Add(createBoss("Gate2", "2관문 수문장", "G2", 160, InTables, DefaultTrees.CreateGate2Boss()));
            }

            return db;
        }

        /// <summary>문서 조건: 모든 강화/각인/카드/보석/스킬 포인트를 갖춘 상태로 시작.</summary>
        public static CharacterBuildData CreateDefaultBuild(GameDatabase InDatabase, ECharacterClass InClass)
        {
            var build = new CharacterBuildData();
            build.Class = InClass;
            var cls = InDatabase.GetClass(InClass);
            build.BuildName = cls != null ? cls.DisplayName : InClass.ToString();
            // 딜러는 잿빛 맹세, 서포터는 성역 수호 6세트로 시작한다.
            bool supportRole = cls != null && cls.Role == ECombatRole.Support;
            var set = InDatabase.EquipmentSets.Count > 0 ? InDatabase.EquipmentSets[supportRole && InDatabase.EquipmentSets.Count > 2 ? 2 : 0] : null;
            foreach (EEquipSlot slot in System.Enum.GetValues(typeof(EEquipSlot)))
            {
                var equipment = new EquipmentState();
                equipment.Slot = slot;
                equipment.SetId = set != null ? set.SetId : 0;
                equipment.HoningLevel = 15;
                build.Equipment.Add(equipment);
            }

            // 악세사리: 각인 3 + 5 (문서). 각인/특성 배치는 직업 역할에 맞춘 예시.
            bool support = cls != null && cls.Role == ECombatRole.Support;
            // 직업 각인(변형 1)을 하나 넣는다: 서머너 101 · 인파이터 111 · 슬레이어 121 · 도화가 131.
            int classEngraving = 101 + (int)InClass * 10;
            int[] engravings = support ? new[] { classEngraving, 8, 7, 6, 13 } : new[] { classEngraving, 2, 3, 4, 1 };
            foreach (EAccessorySlot slot in System.Enum.GetValues(typeof(EAccessorySlot)))
            {
                var accessory = new AccessoryState();
                accessory.Slot = slot;
                int index = (int)slot;
                accessory.EngravingA = engravings[index % engravings.Length];
                accessory.PointsA = 3;
                accessory.EngravingB = engravings[(index + 1) % engravings.Length];
                accessory.PointsB = 5;
                accessory.StatA = support ? ECombatStat.Swiftness : (index % 2 == 0 ? ECombatStat.Crit : ECombatStat.Specialization);
                // 치명 특성 합계 약 700 (≈ 치명타 25%). 목걸이가 가장 높다.
                accessory.StatAValue = slot == EAccessorySlot.Necklace ? 400.0f : 150.0f;
                // 모든 악세사리가 특성 2개.
                accessory.StatB = support ? ECombatStat.Specialization : ECombatStat.Swiftness;
                accessory.StatBValue = slot == EAccessorySlot.Necklace ? 250.0f : 100.0f;

                build.Accessories.Add(accessory);
            }

            build.Books.Add(new EngravingBookState { EngravingId = engravings[0], Points = 12 });
            build.Books.Add(new EngravingBookState { EngravingId = engravings[1], Points = 12 });
            build.Stone = new AbilityStoneState { EngravingA = engravings[2], PointsA = 7, EngravingB = engravings[3], PointsB = 7 };

            var cardSet = InDatabase.CardSets.Count > 0 ? InDatabase.CardSets[0] : null;
            for (int i = 0; i < 6; ++i)
            {
                build.Cards.Add(new CardSlotState { CardId = cardSet != null && i < cardSet.CardNames.Length ? cardSet.CardId(i) : 0, Awakening = 5 });
            }

            if (cls != null)
            {
                // 단축키 8칸에는 직업 스킬 앞 8개 (스킬 창에서 끌어 바꾼다). 나머지는 1레벨로 시작.
                var slotIds = new List<int>();
                for (int i = 0; i < cls.Skills.Count; ++i)
                {
                    var skill = cls.Skills[i];
                    var progress = build.GetOrAddSkill(skill.SkillId);
                    bool slotted = slotIds.Count < CharacterBuildData.SkillSlotCount;
                    progress.Level = slotted ? skill.MaxLevel : 1;
                    progress.TripodChoices = slotted ? new[] { 0, 0, 0 } : new[] { -1, -1, -1 };
                    progress.TripodLevels = new[] { 5, 5, 5 };
                    if (slotted == true)
                    {
                        slotIds.Add(skill.SkillId);
                    }
                }

                build.EnsureSkillSlots(slotIds);

                int gemSlots = GemTable.SlotCount;
                for (int i = 0; i < gemSlots && cls.Skills.Count > 0; ++i)
                {
                    var gem = new GemSlotState();
                    gem.Type = i % 2 == 0 ? EGemType.Damage : EGemType.Cooldown;
                    gem.Level = 10;
                    // 같은 스킬에 피해/쿨감 한 개씩 (중복 장착 불가 규칙).
                    gem.SkillId = cls.Skills[(i / 2) % cls.Skills.Count].SkillId;
                    build.Gems.Add(gem);
                }
            }

            return build;
        }

        // ================================================================== build data

        /// <summary>
        /// 장비 세트 3종. 부위마다 다른 세트를 섞어 낄 수 있고, 같은 세트 개수(2 / 4 / 6)에 따라 효과가 켜진다.
        /// 2세트 = 상시 수치, 4세트 = 발동형 효과, 6세트 = 중첩 · 폭발 같은 복합 효과. 수치는 자리 표시 값.
        /// </summary>
        private static List<EquipmentSetDefinition> createEquipmentSets()
        {
            var sets = new List<EquipmentSetDefinition>();

            // 1. 잿빛 맹세 — 치명타 딜러
            var ash = createSet(1, "잿빛 맹세", "치명타가 터질수록 불씨가 쌓이고, 쌓인 불씨가 한 번에 폭발한다.");
            ash.Bonuses.Add(bonus(2, "치명타 적중률 +10%, 치명타 피해 +20%",
                EffectModifierEntry.Char(EAttr.CritRate, EModifierOp.Add, 10.0f), EffectModifierEntry.Char(EAttr.CritDamage, EModifierOp.Add, 20.0f)));
            var ember = ProcDefinition.Buff("잿불", EProcTrigger.OnCrit, 8.0f, 5, EffectModifierEntry.Char(EAttr.DamageIncrease, EModifierOp.Add, 2.0f));
            ash.Bonuses.Add(bonus(4, "치명타 적중 시 잿불 중첩 (주는 피해 +2%, 최대 5중첩)", ember));
            var hellfire = ProcDefinition.Buff("업화", EProcTrigger.OnCrit, 10.0f, 10, EffectModifierEntry.Char(EAttr.CritDamage, EModifierOp.Add, 3.0f))
                .WithChance(0.5f).WithBurst(10, "업화 폭발", EProcAction.Damage, 12.0f);
            var ashRefresh = new ProcDefinition { Name = "재점화", Trigger = EProcTrigger.OnAwakening, Action = EProcAction.ReduceCooldowns, Value = 5.0f };
            ash.Bonuses.Add(bonus(6, "치명타 50% 확률로 업화 중첩 (치명타 피해 +3%), 10중첩 시 큰 폭발 · 각성기 사용 시 모든 스킬 재사용 대기시간 5초 감소",
                hellfire, ashRefresh));
            sets.Add(ash);

            // 2. 폭풍 추적자 — 속도 · 백어택
            var storm = createSet(2, "폭풍 추적자", "빠르게 움직이며 등 뒤를 노린다. 회피가 곧 공격의 시작이다.");
            storm.Bonuses.Add(bonus(2, "공격 속도 +8%, 이동 속도 +10%",
                EffectModifierEntry.Char(EAttr.AttackSpeed, EModifierOp.Add, 8.0f), EffectModifierEntry.Char(EAttr.MoveSpeed, EModifierOp.Add, 10.0f)));
            var mark = ProcDefinition.Buff("폭풍 표식", EProcTrigger.OnBackAttack, 6.0f, 3, EffectModifierEntry.Char(EAttr.IncomingDamageAmplify, EModifierOp.Add, 2.0f));
            mark.Action = EProcAction.TargetDebuff;
            storm.Bonuses.Add(bonus(4, "백어택 적중 시 대상에게 폭풍 표식 (받는 피해 +2%, 최대 3중첩)", mark));
            var gale = ProcDefinition.Buff("돌풍", EProcTrigger.OnDodge, 4.0f, 1,
                EffectModifierEntry.Char(EAttr.DamageIncrease, EModifierOp.Add, 10.0f), EffectModifierEntry.Char(EAttr.MoveSpeed, EModifierOp.Add, 15.0f));
            var galeCooldown = new ProcDefinition { Name = "바람 타기", Trigger = EProcTrigger.OnDodge, Action = EProcAction.ReduceCooldowns, Value = 2.0f };
            var tempest = new ProcDefinition { Name = "뇌격", Trigger = EProcTrigger.OnBackAttack, Action = EProcAction.Damage, Value = 2.5f, Chance = 0.2f, InternalCooldown = 3.0f };
            storm.Bonuses.Add(bonus(6, "회피 시 4초 동안 주는 피해 +10% · 이동 속도 +15%, 모든 스킬 재사용 대기시간 2초 감소 · 백어택 20% 확률로 뇌격",
                gale, galeCooldown, tempest));
            sets.Add(storm);

            // 3. 성역 수호 — 서포터 · 생존
            var sanctum = createSet(3, "성역 수호", "맞을수록 단단해지고, 아군을 지키는 힘이 커진다.");
            sanctum.Bonuses.Add(bonus(2, "받는 피해 감소 +8%, 치유량 +10%",
                EffectModifierEntry.Char(EAttr.IncomingDamageReduction, EModifierOp.Add, 8.0f), EffectModifierEntry.Char(EAttr.OutgoingHealing, EModifierOp.Add, 10.0f)));
            var bulwark = new ProcDefinition { Name = "성역 방벽", Trigger = EProcTrigger.OnDamaged, Action = EProcAction.Shield, Value = 8.0f, InternalCooldown = 12.0f };
            var resolve = ProcDefinition.Buff("굳은 신념", EProcTrigger.OnDamaged, 6.0f, 4, EffectModifierEntry.Char(EAttr.IncomingDamageReduction, EModifierOp.Add, 2.0f))
                .WithChance(1.0f, 1.0f);
            sanctum.Bonuses.Add(bonus(4, "피격 시 보호막 (최대 생명력 8%, 12초마다) · 굳은 신념 중첩 (받는 피해 감소 +2%, 최대 4중첩)", bulwark, resolve));
            var hymn = new ProcDefinition { Name = "수호의 찬가", Trigger = EProcTrigger.Periodic, TriggerParam = 6.0f, Action = EProcAction.PartyHeal, Value = 2.0f };
            var oath = ProcDefinition.Buff("수호 맹약", EProcTrigger.OnIdentity, 8.0f, 1, EffectModifierEntry.Char(EAttr.DamageIncrease, EModifierOp.Add, 6.0f));
            oath.Action = EProcAction.PartyBuff;
            var lastStand = new ProcDefinition { Name = "최후의 성역", Trigger = EProcTrigger.OnHealthBelow, TriggerParam = 30.0f, Action = EProcAction.Shield, Value = 25.0f, InternalCooldown = 45.0f };
            sanctum.Bonuses.Add(bonus(6, "6초마다 주변 아군 2% 회복 · 아이덴티티 사용 시 아군 주는 피해 +6% (8초) · 생명력 30% 아래로 떨어지면 큰 보호막",
                hymn, oath, lastStand));
            sets.Add(sanctum);
            return sets;
        }

        private static EquipmentSetDefinition createSet(int InId, string InName, string InDescription)
        {
            var set = track(ScriptableObject.CreateInstance<EquipmentSetDefinition>(), $"SET_{InId}");
            set.SetId = InId;
            set.DisplayName = InName;
            set.Description = InDescription;
            string[] names = { "투구", "견갑", "갑옷", "각반", "장갑", "무기" };
            foreach (EEquipSlot slot in System.Enum.GetValues(typeof(EEquipSlot)))
            {
                var piece = new EquipmentPieceDefinition();
                piece.Slot = slot;
                piece.Name = $"{InName} {names[(int)slot]}";
                if (slot == EEquipSlot.Weapon)
                {
                    piece.BaseMainStat = 0.0f;
                    piece.MainStatPerHoning = 0.0f;
                    piece.BaseVitality = 0.0f;
                    piece.VitalityPerHoning = 0.0f;
                    piece.BaseWeaponPower = 40000.0f;
                    piece.WeaponPowerPerHoning = 1600.0f;
                }

                set.Pieces.Add(piece);
            }

            return set;
        }

        private static EquipmentSetBonus bonus(int InPieces, string InDescription, params EffectModifierEntry[] InModifiers)
        {
            var result = new EquipmentSetBonus { RequiredPieces = InPieces, Description = InDescription };
            result.Modifiers.AddRange(InModifiers);
            return result;
        }

        private static EquipmentSetBonus bonus(int InPieces, string InDescription, params ProcDefinition[] InProcs)
        {
            var result = new EquipmentSetBonus { RequiredPieces = InPieces, Description = InDescription };
            result.Procs.AddRange(InProcs);
            return result;
        }

        // ================================================================== classes

        private delegate SkillHit HitFactory();

        private static CharacterClassDefinition createClass(ECharacterClass InClass, string InName, ECombatRole InRole, Color InColor, float InRange, bool InBack)
        {
            var cls = track(ScriptableObject.CreateInstance<CharacterClassDefinition>(), $"CLS_{InClass}");
            cls.Class = InClass;
            cls.DisplayName = InName;
            cls.Role = InRole;
            cls.BodyColor = InColor;
            cls.AiPreferredRange = InRange;
            cls.AiPrefersBack = InBack;
            return cls;
        }

        private static SkillDefinition skill(CharacterClassDefinition InClass, int InIndex, string InName, float InCooldown, float InMana, params SkillHit[] InHits)
        {
            int id = ((int)InClass.Class + 1) * 100 + InIndex + 1;
            var skill = track(ScriptableObject.CreateInstance<SkillDefinition>(), $"SK_{id}");
            skill.SkillId = id;
            skill.DisplayName = InName;
            skill.OwnerClass = InClass.Class;
            skill.Cooldown = InCooldown;
            skill.ManaCost = InMana;
            skill.IconColor = Color.HSVToRGB((InIndex * 0.12f) % 1.0f, 0.55f, 0.95f);
            skill.Hits.AddRange(InHits);
            skill.Element = defaultElement(InClass.Class);
            addStandardTripods(skill);
            InClass.Skills.Add(skill);
            return skill;
        }

        private static EElement defaultElement(ECharacterClass InClass)
        {
            switch (InClass)
            {
                case ECharacterClass.Summoner: return EElement.Lightning;
                case ECharacterClass.Slayer: return EElement.Dark;
                case ECharacterClass.Artist: return EElement.Water;
            }

            return EElement.None;
        }

        /// <summary>스킬 번호(InIndex = skill() 의 번호)로 찾아 보호 효과를 단다.</summary>
        private static void protect(CharacterClassDefinition InClass, int InIndex, ESkillProtection InProtection)
        {
            int id = ((int)InClass.Class + 1) * 100 + InIndex + 1;
            var skill = InClass.Skills.Find(x => x.SkillId == id);
            if (skill != null)
            {
                skill.Protection = InProtection;
            }
        }

        private static SkillHit hit(HitArea InArea, float InCoefficient, float InDelay, float InStagger, float InDestruction = 0.0f)
        {
            var hit = new SkillHit();
            hit.Area = InArea;
            hit.Coefficient = InCoefficient;
            hit.Delay = InDelay;
            hit.Stagger = InStagger;
            hit.Destruction = InDestruction;
            return hit;
        }

        /// <summary>
        /// 모든 스킬에 붙는 예시 트라이포드 3단계. 4가지 적용 시점(SkillParameter / OwnerWhileSelected / OwnerOnActivate / TargetOnHit)을
        /// 모두 보여 주는 용도이며, 직업별 실제 트라이포드 목록은 구현 보류 사항.md.
        /// </summary>
        private static void addStandardTripods(SkillDefinition InSkill)
        {
            var tier1 = new TripodTier { UnlockSkillLevel = 4 };
            tier1.Options.Add(tripod("빠른 준비", "재사용 대기시간 -10% (레벨당 -2%)", ETripodTiming.SkillParameter,
                EffectBuilder.Infinite().SkillPercent(ESkillAttr.CooldownPercent, -10.0f).Build(), 0.2f));
            tier1.Options.Add(tripod("넓은 범위", "범위 +20% (레벨당 +4%)", ETripodTiming.SkillParameter,
                EffectBuilder.Infinite().SkillPercent(ESkillAttr.RangePercent, 20.0f).Build(), 0.2f));
            tier1.Options.Add(tripod("마나 절약", "마나 소모 -30%", ETripodTiming.SkillParameter,
                EffectBuilder.Infinite().SkillPercent(ESkillAttr.ManaCostPercent, -30.0f).Build(), 0.0f));

            var tier2 = new TripodTier { UnlockSkillLevel = 7 };
            tier2.Options.Add(tripod("약점 포착", "피해 +15% (레벨당 +3%)", ETripodTiming.SkillParameter,
                EffectBuilder.Infinite().SkillPercent(ESkillAttr.DamagePercent, 15.0f).Build(), 0.2f));
            tier2.Options.Add(tripod("정신 집중", "치명타 적중률 +10%", ETripodTiming.SkillParameter,
                EffectBuilder.Infinite().SkillAdd(ESkillAttr.CritRateAdd, 10.0f).Build(), 0.1f));
            var armor = tripod("강인함", "시전 중 슈퍼아머 · 무력화 +20%", ETripodTiming.SkillParameter,
                EffectBuilder.Infinite().SkillPercent(ESkillAttr.StaggerPercent, 20.0f).Build(), 0.0f);
            armor.GrantsSuperArmor = true;
            tier2.Options.Add(armor);

            var tier3 = new TripodTier { UnlockSkillLevel = 10 };
            tier3.Options.Add(tripod("연속 타격", "마지막 타격 1회 추가", ETripodTiming.SkillParameter,
                EffectBuilder.Infinite().SkillAdd(ESkillAttr.ExtraHits, 1.0f).Build(), 0.0f));
            tier3.Options.Add(tripod("약화 표식", "적중 대상 받는 피해 +6% (6초)", ETripodTiming.TargetOnHit,
                EffectBuilder.Timed(6.0f).Add(EAttr.IncomingDamageAmplify, 6.0f).Stack(EStackPolicy.Refresh, 1).Build(), 0.0f));
            tier3.Options.Add(tripod("투지", "시전 시 자신 피해 +8% (5초)", ETripodTiming.OwnerOnActivate,
                EffectBuilder.Timed(5.0f).Add(EAttr.DamageIncrease, 8.0f).Stack(EStackPolicy.Refresh, 1).Build(), 0.0f));
            tier3.Options.Add(tripod("숙련", "선택 시 상시 치명타 피해 +10%", ETripodTiming.OwnerWhileSelected,
                EffectBuilder.Infinite().Add(EAttr.CritDamage, 10.0f).Build(), 0.0f));

            InSkill.Tiers.Add(tier1);
            InSkill.Tiers.Add(tier2);
            InSkill.Tiers.Add(tier3);
        }

        private static TripodOption tripod(string InName, string InDescription, ETripodTiming InTiming, GameplayEffectDef InDef, float InPerLevel)
        {
            var option = new TripodOption();
            option.Name = InName;
            option.Description = InDescription;
            var effect = new TripodEffect();
            effect.Timing = InTiming;
            effect.RuntimeDef = InDef;
            effect.Effect = asAsset(InDef, InName, InTiming == ETripodTiming.SkillParameter);
            effect.BaseScale = 1.0f;
            effect.PerLevelScale = InPerLevel;
            option.Effects.Add(effect);
            return option;
        }

        private static CharacterClassDefinition createSummoner()
        {
            var cls = createClass(ECharacterClass.Summoner, "서머너", ECombatRole.Dealer, new Color(0.45f, 0.75f, 1.0f), 8.0f, false);
            cls.BaseMaxHealth = 140000.0f;
            skill(cls, 0, "정령 화살", 5.0f, 30.0f, hit(HitArea.Rect(12.0f, 1.5f), 2.5f, 0.25f, 6.0f));
            skill(cls, 1, "번개 구체", 9.0f, 60.0f, hit(HitArea.Circle(3.0f, 7.0f), 4.0f, 0.5f, 8.0f), hit(HitArea.Circle(3.5f, 7.0f), 3.0f, 0.3f, 6.0f));
            skill(cls, 2, "대지의 정령", 14.0f, 80.0f, hit(HitArea.Circle(4.0f, 6.0f), 6.5f, 0.6f, 15.0f));
            skill(cls, 3, "얼음 장막", 12.0f, 70.0f, hit(HitArea.Fan(8.0f, 70.0f), 5.0f, 0.4f, 10.0f));
            skill(cls, 4, "소환수 돌격", 18.0f, 100.0f, hit(HitArea.Rect(10.0f, 3.0f), 9.0f, 0.5f, 20.0f));
            skill(cls, 5, "별빛 낙하", 24.0f, 120.0f, hit(HitArea.Circle(5.0f, 8.0f), 12.0f, 0.9f, 25.0f, 1.0f));
            skill(cls, 6, "정령 폭발", 20.0f, 110.0f, hit(HitArea.Circle(4.5f, 0.0f), 10.0f, 0.4f, 30.0f));
            skill(cls, 8, "바람의 정령", 7.0f, 40.0f, hit(HitArea.Fan(6.0f, 90.0f), 4.5f, 0.3f, 10.0f));
            skill(cls, 9, "독안개", 12.0f, 70.0f, hit(HitArea.Circle(4.0f, 5.0f), 2.0f, 0.4f, 4.0f), hit(HitArea.Circle(4.0f, 5.0f), 2.0f, 0.6f, 4.0f), hit(HitArea.Circle(4.0f, 5.0f), 2.5f, 0.6f, 6.0f));
            skill(cls, 10, "정령의 사슬", 16.0f, 90.0f, hit(HitArea.Rect(9.0f, 2.0f), 8.0f, 0.4f, 25.0f));
            skill(cls, 11, "수호 정령", 22.0f, 110.0f, hit(HitArea.Circle(5.0f), 9.0f, 0.5f, 20.0f, 1.0f));
            skill(cls, 7, "고대 의식", 30.0f, 150.0f, hit(HitArea.Circle(6.0f, 6.0f), 16.0f, 1.0f, 20.0f));
            cls.Awakenings.Add(awakening(cls, 0, "정령왕 강림", HitArea.Circle(9.0f, 5.0f), 60.0f, EElement.Lightning));
            cls.Awakenings.Add(awakening(cls, 1, "대정령 폭풍", HitArea.Circle(6.0f, 4.0f), 72.0f, EElement.Lightning));
            // 스킬 사용 중 보호 효과 (넉백 면역 = 경직 면역 / 피격 이상 면역).
            protect(cls, 2, ESkillProtection.PushImmune);
            protect(cls, 7, ESkillProtection.SuperArmor);
            protect(cls, 11, ESkillProtection.SuperArmor);
            cls.DodgeCooldown = 9.0f;
            return cls;
        }

        private static CharacterClassDefinition createInfighter()
        {
            var cls = createClass(ECharacterClass.Infighter, "인파이터", ECombatRole.Dealer, new Color(1.0f, 0.65f, 0.3f), 2.2f, false);
            cls.BaseMaxHealth = 180000.0f;
            cls.BaseDefense = 26000.0f;
            skill(cls, 0, "연환 주먹", 4.0f, 20.0f, hit(HitArea.Fan(3.2f, 90.0f), 1.5f, 0.15f, 10.0f), hit(HitArea.Fan(3.2f, 90.0f), 1.5f, 0.15f, 10.0f), hit(HitArea.Fan(3.5f, 100.0f), 2.0f, 0.2f, 14.0f));
            var counter = skill(cls, 1, "반격의 일격", 8.0f, 40.0f, hit(HitArea.Fan(4.0f, 80.0f), 4.0f, 0.2f, 20.0f));
            counter.IsCounter = true;
            skill(cls, 2, "어깨 돌진", 10.0f, 50.0f, hit(HitArea.Rect(6.0f, 2.5f), 5.0f, 0.2f, 25.0f)).DashDistance = 4.0f;
            skill(cls, 3, "회오리 차기", 12.0f, 60.0f, hit(HitArea.Circle(3.8f), 6.0f, 0.4f, 30.0f));
            skill(cls, 4, "대지 강타", 16.0f, 90.0f, hit(HitArea.Circle(4.5f, 1.0f), 9.0f, 0.6f, 40.0f, 1.0f));
            skill(cls, 5, "충격파 장권", 20.0f, 100.0f, hit(HitArea.Rect(9.0f, 3.0f), 11.0f, 0.5f, 45.0f));
            skill(cls, 6, "백열권", 22.0f, 110.0f, hit(HitArea.Fan(3.5f, 60.0f), 2.5f, 0.12f, 12.0f), hit(HitArea.Fan(3.5f, 60.0f), 2.5f, 0.12f, 12.0f), hit(HitArea.Fan(3.5f, 60.0f), 2.5f, 0.12f, 12.0f), hit(HitArea.Fan(4.0f, 80.0f), 6.0f, 0.3f, 30.0f));
            skill(cls, 8, "기공 연타", 6.0f, 30.0f, hit(HitArea.Fan(4.0f, 100.0f), 2.5f, 0.2f, 12.0f), hit(HitArea.Fan(4.0f, 100.0f), 2.5f, 0.2f, 12.0f));
            skill(cls, 9, "하단 발차기", 9.0f, 45.0f, hit(HitArea.Circle(3.0f, 1.0f), 6.0f, 0.3f, 30.0f));
            skill(cls, 10, "용권 상승", 15.0f, 80.0f, hit(HitArea.Circle(3.5f), 8.0f, 0.35f, 28.0f, 1.0f));
            skill(cls, 11, "천둥 손바닥", 20.0f, 100.0f, hit(HitArea.Rect(6.0f, 3.0f), 11.0f, 0.45f, 35.0f));
            skill(cls, 7, "필살 정권", 30.0f, 150.0f, hit(HitArea.Rect(5.0f, 2.0f), 18.0f, 0.9f, 60.0f, 2.0f));
            cls.Awakenings.Add(awakening(cls, 0, "천지 붕괴권", HitArea.Circle(7.0f, 2.0f), 70.0f, EElement.None));
            cls.Awakenings.Add(awakening(cls, 1, "일점 관통권", HitArea.Rect(8.0f, 2.5f), 82.0f, EElement.None));
            // 스킬 사용 중 보호 효과 (넉백 면역 = 경직 면역 / 피격 이상 면역).
            protect(cls, 2, ESkillProtection.SuperArmor);
            protect(cls, 4, ESkillProtection.PushImmune);
            protect(cls, 7, ESkillProtection.SuperArmor);
            protect(cls, 10, ESkillProtection.PushImmune);
            cls.DodgeCooldown = 7.0f;
            return cls;
        }

        private static CharacterClassDefinition createSlayer()
        {
            var cls = createClass(ECharacterClass.Slayer, "슬레이어", ECombatRole.Dealer, new Color(0.85f, 0.25f, 0.3f), 2.8f, true);
            cls.BaseMaxHealth = 165000.0f;
            var positional = new List<SkillDefinition>();
            positional.Add(skill(cls, 0, "대검 베기", 5.0f, 25.0f, hit(HitArea.Fan(4.0f, 120.0f), 3.0f, 0.3f, 8.0f)));
            positional.Add(skill(cls, 1, "분쇄 일격", 9.0f, 50.0f, hit(HitArea.Circle(3.5f, 2.0f), 6.0f, 0.5f, 15.0f, 1.0f)));
            positional.Add(skill(cls, 2, "돌진 참격", 11.0f, 55.0f, hit(HitArea.Rect(7.0f, 2.5f), 5.5f, 0.25f, 12.0f)));
            positional[2].DashDistance = 5.0f;
            positional.Add(skill(cls, 3, "회전 베기", 14.0f, 70.0f, hit(HitArea.Circle(4.0f), 3.0f, 0.2f, 8.0f), hit(HitArea.Circle(4.0f), 3.0f, 0.2f, 8.0f), hit(HitArea.Circle(4.5f), 4.0f, 0.25f, 10.0f)));
            positional.Add(skill(cls, 4, "피의 폭풍", 18.0f, 90.0f, hit(HitArea.Fan(5.0f, 140.0f), 10.0f, 0.5f, 18.0f)));
            positional.Add(skill(cls, 5, "처형", 24.0f, 120.0f, hit(HitArea.Fan(4.0f, 60.0f), 15.0f, 0.8f, 20.0f)));
            positional.Add(skill(cls, 6, "검은 소용돌이", 20.0f, 100.0f, hit(HitArea.Circle(5.0f, 1.0f), 11.0f, 0.6f, 25.0f)));
            positional.Add(skill(cls, 8, "핏빛 일섬", 10.0f, 50.0f, hit(HitArea.Rect(7.0f, 2.0f), 8.0f, 0.25f, 12.0f)));
            positional[positional.Count - 1].DashDistance = 4.0f;
            positional.Add(skill(cls, 9, "분노의 내려찍기", 16.0f, 85.0f, hit(HitArea.Circle(4.0f, 2.0f), 12.0f, 0.5f, 30.0f, 1.0f)));
            positional.Add(skill(cls, 10, "칼날 폭풍", 15.0f, 75.0f, hit(HitArea.Circle(5.0f), 3.5f, 0.2f, 8.0f), hit(HitArea.Circle(5.0f), 3.5f, 0.2f, 8.0f), hit(HitArea.Circle(5.0f), 3.5f, 0.2f, 8.0f)));
            positional.Add(skill(cls, 11, "참수", 26.0f, 130.0f, hit(HitArea.Fan(4.0f, 70.0f), 17.0f, 0.7f, 20.0f)));
            positional.Add(skill(cls, 7, "광폭 질주", 28.0f, 140.0f, hit(HitArea.Rect(8.0f, 3.0f), 14.0f, 0.4f, 20.0f)));
            for (int i = 0; i < positional.Count; ++i)
            {
                positional[i].Positional = i % 2 == 0 ? ESkillPositionalType.BackAttack : ESkillPositionalType.HeadAttack;
            }

            cls.Awakenings.Add(awakening(cls, 0, "멸절의 대검", HitArea.Fan(8.0f, 160.0f), 80.0f, EElement.Dark));
            cls.Awakenings.Add(awakening(cls, 1, "파멸의 일섬", HitArea.Rect(10.0f, 3.0f), 92.0f, EElement.Dark));
            // 스킬 사용 중 보호 효과 (넉백 면역 = 경직 면역 / 피격 이상 면역).
            protect(cls, 2, ESkillProtection.SuperArmor);
            protect(cls, 4, ESkillProtection.PushImmune);
            protect(cls, 6, ESkillProtection.PushImmune);
            protect(cls, 9, ESkillProtection.SuperArmor);
            cls.DodgeCooldown = 8.0f;
            return cls;
        }

        private static CharacterClassDefinition createArtist()
        {
            var cls = createClass(ECharacterClass.Artist, "도화가", ECombatRole.Support, new Color(0.6f, 1.0f, 0.6f), 7.0f, false);
            cls.BaseMaxHealth = 150000.0f;
            var buff = asAsset(EffectBuilder.Timed(8.0f).Add(EAttr.DamageIncrease, 10.0f).Stack(EStackPolicy.Refresh, 1).Build(), "아군 공격력 강화");
            var brand = asAsset(EffectBuilder.Timed(10.0f).Add(EAttr.IncomingDamageAmplify, 10.0f).Stack(EStackPolicy.Refresh, 1).Build(), "낙인");

            skill(cls, 0, "붓 휘두르기", 4.0f, 20.0f, hit(HitArea.Fan(5.0f, 90.0f), 1.2f, 0.25f, 6.0f));
            var mark = skill(cls, 1, "먹물 낙인", 10.0f, 60.0f, hit(HitArea.Circle(4.0f, 6.0f), 1.5f, 0.4f, 10.0f));
            mark.Hits[0].TargetEffects.Add(brand);
            var heal = skill(cls, 2, "치유의 화폭", 14.0f, 90.0f, hit(HitArea.Circle(3.0f, 4.0f), 0.5f, 0.3f, 4.0f));
            heal.SelfPartyHealCoefficient = 1.2f;
            var shield = skill(cls, 3, "수호의 문양", 18.0f, 100.0f, hit(HitArea.Circle(3.0f), 0.3f, 0.3f, 2.0f));
            shield.PartyShieldAmount = 30000.0f;
            var empower = skill(cls, 4, "영감의 붓질", 16.0f, 80.0f, hit(HitArea.Fan(6.0f, 60.0f), 1.0f, 0.3f, 6.0f));
            empower.PartyBuffs.Add(buff);
            skill(cls, 5, "산수화 펼치기", 20.0f, 100.0f, hit(HitArea.Circle(6.0f, 5.0f), 3.0f, 0.6f, 25.0f));
            skill(cls, 6, "학 날리기", 12.0f, 60.0f, hit(HitArea.Rect(12.0f, 2.5f), 2.0f, 0.3f, 12.0f));
            skill(cls, 8, "먹물 튀기기", 5.0f, 25.0f, hit(HitArea.Fan(6.0f, 90.0f), 3.0f, 0.25f, 8.0f));
            skill(cls, 9, "달빛 붓질", 11.0f, 60.0f, hit(HitArea.Circle(4.0f, 4.0f), 5.0f, 0.4f, 12.0f));
            skill(cls, 10, "나비 떼", 14.0f, 70.0f, hit(HitArea.Circle(5.0f, 5.0f), 2.0f, 0.3f, 5.0f), hit(HitArea.Circle(5.0f, 5.0f), 2.0f, 0.3f, 5.0f));
            skill(cls, 11, "묵향의 장막", 24.0f, 110.0f, hit(HitArea.Circle(5.0f), 4.0f, 0.5f, 15.0f));
            var wide = skill(cls, 7, "천지 수묵", 30.0f, 140.0f, hit(HitArea.Circle(8.0f), 2.0f, 0.8f, 20.0f));
            wide.SelfPartyHealCoefficient = 2.0f;
            var revive = awakening(cls, 0, "만물 소생도", HitArea.Circle(10.0f), 10.0f, EElement.Water);
            revive.SelfPartyHealCoefficient = 6.0f;
            revive.PartyShieldAmount = 60000.0f;
            cls.Awakenings.Add(revive);
            var guard = awakening(cls, 1, "천지 수호진", HitArea.Circle(10.0f), 6.0f, EElement.Water);
            guard.PartyShieldAmount = 120000.0f;
            cls.Awakenings.Add(guard);
            // 스킬 사용 중 보호 효과 (넉백 면역 = 경직 면역 / 피격 이상 면역).
            protect(cls, 3, ESkillProtection.PushImmune);
            protect(cls, 7, ESkillProtection.SuperArmor);
            protect(cls, 11, ESkillProtection.SuperArmor);
            cls.DodgeCooldown = 9.0f;
            return cls;
        }

        private static SkillDefinition awakening(CharacterClassDefinition InClass, int InIndex, string InName, HitArea InArea, float InCoefficient, EElement InElement)
        {
            int id = ((int)InClass.Class + 1) * 100 + 50 + InIndex;
            var skill = track(ScriptableObject.CreateInstance<SkillDefinition>(), $"SK_{id}");
            skill.SkillId = id;
            skill.DisplayName = InName;
            skill.OwnerClass = InClass.Class;
            skill.Cooldown = 300.0f;
            skill.ManaCost = 0.0f;
            skill.IsAwakening = true;
            skill.SuperArmor = true;
            skill.MaxLevel = 1;
            skill.IconColor = new Color(1.0f, 0.85f, 0.3f);
            skill.Element = InElement;
            skill.Hits.Add(hit(InArea, InCoefficient, 1.0f, 50.0f, 2.0f));
            return skill;
        }

        // ================================================================== bosses

        private static BossDefinition createBoss(string InId, string InName, string InGatePrefix, int InLines, DemoLATables InTables, Framework.StateTree.StateTreeAsset InTree)
        {
            var boss = track(ScriptableObject.CreateInstance<BossDefinition>(), $"BOSS_{InId}");
            boss.BossId = InId;
            boss.DisplayName = InName;
            boss.TotalHpLines = InLines;
            boss.HpPerLine = 1500000.0f;
            boss.Brain = InTree;
            // 군단장 레이드: 성속성에 약하다 (카드 속성 변환과 연결).
            boss.Weaknesses.Add(new ElementWeakness { Element = EElement.Holy, DamagePercent = 15.0f });
            InTree.name = $"ST_{InId}";
            CreatedObjects.Add(InTree);

            var phases = InTables.BossPhase.All;
            for (int i = 0; i < phases.Count; ++i)
            {
                if (phases[i].PhaseId.StartsWith(InGatePrefix) == false)
                {
                    continue;
                }

                var phase = BossPatternCsv.CreatePhase(phases[i]);
                if (phase.PhaseId == "G2-P3")
                {
                    phase.ResetHpLines = 40;
                }

                boss.Phases.Add(phase);
            }

            var rows = InTables.BossPattern.All;
            for (int i = 0; i < rows.Count; ++i)
            {
                if (rows[i].PatternId.StartsWith(InGatePrefix) == false)
                {
                    continue;
                }

                var pattern = BossPatternCsv.CreatePattern(rows[i]);
                PatternPresets.Apply(pattern);
                BossPatternCsv.ApplyMeasured(pattern, rows[i]);
                CreatedObjects.Add(pattern);
                boss.Patterns.Add(pattern);
            }

            if (InGatePrefix == "G1")
            {
                boss.Gimmicks.Add(new GimmickSpec { GimmickId = "G1-01", Kind = EGimmickKind.OrbAlternation });
            }
            else
            {
                boss.BodyColor = new Color(0.35f, 0.15f, 0.08f);
                boss.Radius = 2.0f;
                boss.Gimmicks.Add(new GimmickSpec { GimmickId = "G2-01", Kind = EGimmickKind.ArmorStack });
                boss.Gimmicks.Add(new GimmickSpec { GimmickId = "G2-02", Kind = EGimmickKind.DeathWardOrbs });
                boss.Gimmicks.Add(new GimmickSpec { GimmickId = "G2-05", Kind = EGimmickKind.TerrainCollapseHalf });
                boss.Gimmicks.Add(new GimmickSpec { GimmickId = "G2-06", Kind = EGimmickKind.TerrainCollapseCenter });
            }

            return boss;
        }

        // ================================================================== util

        private static GameplayEffectAsset asAsset(GameplayEffectDef InDef, string InName, bool InIsSkillAttribute = false)
        {
            var asset = track(ScriptableObject.CreateInstance<GameplayEffectAsset>(), "GE_" + InName);
            // 0 = 런타임 할당. 런타임 id(1000000~)를 에셋에 저장하면 다음 실행의 id 와 겹칠 수 있다.
            asset.Id = 0;
            asset.DurationPolicy = InDef.DurationPolicy;
            asset.Duration = InDef.Duration;
            asset.Period = InDef.Period;
            asset.ExecutePeriodicOnApply = InDef.ExecutePeriodicOnApply;
            asset.StackPolicy = InDef.StackPolicy;
            asset.MaxStack = InDef.MaxStack;
            asset.Execution = (EExecution)InDef.ExecutionId;
            asset.Cue = (ECue)InDef.CueId;
            for (int i = 0; i < InDef.Modifiers.Length; ++i)
            {
                var modifier = InDef.Modifiers[i];
                var entry = new EffectModifierEntry();
                entry.Attribute = (EAttr)modifier.Attribute;
                entry.SkillAttribute = (ESkillAttr)modifier.Attribute;
                entry.IsSkillAttribute = InIsSkillAttribute;
                entry.Op = modifier.Op;
                entry.MagnitudeType = modifier.MagnitudeType;
                entry.Key = modifier.Key;
                entry.Value = modifier.Value;
                asset.Modifiers.Add(entry);
            }

            var granted = new List<ETag>();
            for (int i = 0; i < InDef.GrantedTags.Count; ++i)
            {
                granted.Add((ETag)InDef.GrantedTags.GetTag(i));
            }

            asset.GrantedTags = granted.ToArray();
            return asset;
        }

        private static T track<T>(T InObject, string InName) where T : ScriptableObject
        {
            InObject.name = InName;
            CreatedObjects.Add(InObject);
            return InObject;
        }
    }
}
