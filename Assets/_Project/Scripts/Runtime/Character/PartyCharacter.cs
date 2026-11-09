using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Build;
using DemoLA.Character.Identity;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Data;
using DemoLA.Effects;
using DemoLA.Skills;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Character
{
    /// <summary>
    /// 파티원 (플레이어 조작 / 아군 AI 공용). 직업 정의 + 빌드 데이터로 초기화한다.
    /// 조작 주체는 PlayerController(입력) 또는 CombatBrain(StateTree) 이다.
    /// </summary>
    public class PartyCharacter : CombatActor
    {
        public const int SkillSlotCount = 8;
        public const int BattleItemSlotCount = 4;

        private readonly List<SkillAbility> skills = new List<SkillAbility>();
        private readonly List<BattleItemAbility> battleItems = new List<BattleItemAbility>();
        private readonly BuildApplier applier = new BuildApplier();
        private int identityVariant = -1;

        public CharacterClassDefinition ClassDefinition { get; private set; }
        public CharacterBuildData Build { get; private set; }
        public GameDatabase Database { get; private set; }
        public DodgeAbility Dodge { get; private set; }
        public SkillAbility Awakening { get; private set; }
        public IdentityAbility Identity { get; private set; }
        /// <summary>X 아이덴티티 (Z 와 자원 공유).</summary>
        public IdentitySecondaryAbility IdentityX { get; private set; }
        public bool IsPlayerControlled { get; set; }

        /// <summary>스킬 8개 + 선택한 각성기 (마지막).</summary>
        public IReadOnlyList<SkillAbility> Skills
        {
            get { return skills; }
        }

        /// <summary>단축키 1~4 와 같은 순서. 빈 칸은 null.</summary>
        public IReadOnlyList<BattleItemAbility> BattleItems
        {
            get { return battleItems; }
        }

        private readonly List<SkillAbility> equippedSkills = new List<SkillAbility>();

        /// <summary>단축키에 놓인 스킬 + 각성기 (AI 도 이것만 쓴다).</summary>
        public IReadOnlyList<SkillAbility> EquippedSkills
        {
            get
            {
                equippedSkills.Clear();
                for (int i = 0; i < SkillSlotCount; ++i)
                {
                    var skill = GetSkillInSlot(i);
                    if (skill != null)
                    {
                        equippedSkills.Add(skill);
                    }
                }

                if (Awakening != null)
                {
                    equippedSkills.Add(Awakening);
                }

                return equippedSkills;
            }
        }

        public BuildResult LastBuildResult
        {
            get { return applier.LastResult; }
        }

        public void Initialize(CharacterClassDefinition InClass, CharacterBuildData InBuild, GameDatabase InDatabase)
        {
            ClassDefinition = InClass;
            Database = InDatabase;
            Team = ETeam.Player;
            var stats = new CombatAttributeSet();
            InitializeCombat(stats);
            stats.InitializeDefaults(InClass.BaseMaxHealth, InClass.BaseMaxMana, 0.0f, InClass.BaseDefense);
            stats.Set(EAttr.MainStat, InClass.BaseMainStat);
            stats.Set(EAttr.WeaponPower, InClass.BaseWeaponPower);
            stats.Set(EAttr.CritDamage, InClass.BaseCritDamage);

            // 직업 스킬은 전부 배운다. 쓸 수 있는 것은 단축키(SkillSlots)에 놓은 8개.
            for (int i = 0; i < InClass.Skills.Count; ++i)
            {
                if (InClass.Skills[i] != null)
                {
                    skills.Add(GrantAbility(new SkillAbility(InClass.Skills[i])));
                }
            }

            Dodge = GrantAbility(new DodgeAbility(InClass.DodgeCooldown));
            Dodge.Distance = InClass.DodgeDistance;
            Dodge.InvulnerableTime = InClass.DodgeInvulnerableTime;
            ApplyEffectToSelf(GameEffects.ManaRegen, 0);
            ApplyBuild(InBuild);
        }

        public void ApplyBuild(CharacterBuildData InBuild)
        {
            Build = InBuild;
            ensureSkillSlots();
            selectAwakening(InBuild != null ? InBuild.SelectedAwakening : 0);
            setupBattleItems(InBuild);
            var result = BuildCalculator.Compute(InBuild, Database);
            applier.Apply(this, skills, result);
            selectIdentity(result.IdentityVariant);
        }

        // ------------------------------------------------------------------ actions

        /// <summary>단축키 칸(0~7 = Q W E R A S D F)에 놓인 스킬. 빈 칸이면 null.</summary>
        public SkillAbility GetSkillInSlot(int InSlot)
        {
            if (Build == null || Build.SkillSlots == null || InSlot < 0 || InSlot >= Build.SkillSlots.Length)
            {
                return null;
            }

            return FindSkill(Build.SkillSlots[InSlot]);
        }

        public SkillAbility FindSkill(int InSkillId)
        {
            if (InSkillId == 0)
            {
                return null;
            }

            for (int i = 0; i < skills.Count; ++i)
            {
                if (skills[i].Definition.SkillId == InSkillId && skills[i].Definition.IsAwakening == false)
                {
                    return skills[i];
                }
            }

            return null;
        }

        private void ensureSkillSlots()
        {
            if (Build == null || ClassDefinition == null)
            {
                return;
            }

            var ids = new List<int>();
            for (int i = 0; i < ClassDefinition.Skills.Count; ++i)
            {
                if (ClassDefinition.Skills[i] != null)
                {
                    ids.Add(ClassDefinition.Skills[i].SkillId);
                }
            }

            Build.EnsureSkillSlots(ids);
        }

        /// <summary>배틀 아이템 칸만 다시 만든다 (전투 중 단축키 교체 — 빌드 전체를 다시 적용하지 않는다).</summary>
        public void RefreshBattleItems()
        {
            setupBattleItems(Build);
        }

        public bool TryUseSkill(SkillAbility InSkill, Vector3 InAimPoint, out EActivateFailReason OutReason)
        {
            return tryActivate(InSkill, InAimPoint, out OutReason);
        }

        public bool TryUseSlot(int InSlot, Vector3 InAimPoint)
        {
            EActivateFailReason reason;
            return tryActivate(GetSkillInSlot(InSlot), InAimPoint, out reason);
        }

        public bool TryDodge(Vector3 InAimPoint)
        {
            EActivateFailReason reason;
            return tryActivate(Dodge, InAimPoint, out reason);
        }

        public bool TryUseIdentity(Vector3 InAimPoint)
        {
            EActivateFailReason reason;
            return tryActivate(Identity, InAimPoint, out reason);
        }

        public bool TryUseIdentityX(Vector3 InAimPoint)
        {
            EActivateFailReason reason;
            return tryActivate(IdentityX, InAimPoint, out reason);
        }

        public bool TryUseBattleItem(int InSlot, Vector3 InAimPoint)
        {
            EActivateFailReason reason;
            return InSlot >= 0 && InSlot < battleItems.Count && tryActivate(battleItems[InSlot], InAimPoint, out reason);
        }

        /// <summary>에스더 (파티 공용 게이지가 가득일 때). InIndex = 현재 관문에서 쓸 수 있는 에스더 순서.</summary>
        public bool TryUseEsther(int InIndex)
        {
            return IsDead == false && HasTag((int)ETag.State_Stunned) == false && EstherSystem.TryUse(this, InIndex);
        }

        public bool IsSkillReady(SkillAbility InSkill)
        {
            EActivateFailReason reason;
            return InSkill != null && InSkill.CanActivate(out reason) == true;
        }

        public float GetCooldownRemaining(GameplayAbility InAbility)
        {
            return InAbility == null ? 0.0f : Asc.GetCooldownRemaining(InAbility.AbilityId);
        }

        public bool IsCasting
        {
            get { return HasTag((int)ETag.State_Casting) == true || HasTag((int)ETag.State_Dashing) == true; }
        }

        /// <summary>레이드 재시작 시 배틀 아이템 개수를 채운다.</summary>
        public void RefillBattleItems()
        {
            for (int i = 0; i < battleItems.Count; ++i)
            {
                if (battleItems[i] != null)
                {
                    battleItems[i].Refill();
                }
            }
        }

        private void Update()
        {
            if (Identity != null)
            {
                Identity.TickResource();
            }
        }

        protected override void OnDeath()
        {
            DemoLA.World.Raid.Message($"{GetName()} 사망", 2.0f, true);
        }

        private bool tryActivate(GameplayAbility InAbility, Vector3 InAimPoint, out EActivateFailReason OutReason)
        {
            OutReason = EActivateFailReason.Custom;
            if (InAbility == null || IsDead == true)
            {
                return false;
            }

            var info = new AbilityActivationInfo();
            info.AimPoint = InAimPoint;
            info.AimDirection = HitGeometry.Flat(InAimPoint - Position).normalized;
            return Asc.TryActivateAbility(InAbility, info, out OutReason);
        }

        // ------------------------------------------------------------------ build-driven abilities

        /// <summary>각성기는 2종 중 선택한 것만 부여한다 (스킬 목록 마지막).</summary>
        private void selectAwakening(int InIndex)
        {
            var definition = ClassDefinition.GetAwakening(InIndex);
            if (Awakening != null && definition != null && Awakening.Definition == definition)
            {
                return;
            }

            if (Awakening != null)
            {
                skills.Remove(Awakening);
                RemoveAbility(Awakening);
                Awakening = null;
            }

            if (definition != null)
            {
                Awakening = GrantAbility(new SkillAbility(definition));
                skills.Add(Awakening);
            }
        }

        /// <summary>직업 각인에 따라 아이덴티티 어빌리티 클래스를 바꾼다 (같은 변형이면 유지).</summary>
        private void selectIdentity(int InVariant)
        {
            if (Identity != null && identityVariant == InVariant)
            {
                return;
            }

            if (Identity != null)
            {
                RemoveAbility(Identity);
            }

            identityVariant = InVariant;
            Identity = IdentityAbility.Create(ClassDefinition.Class, InVariant);
            if (Identity != null)
            {
                GrantAbility(Identity);
            }

            if (IdentityX == null)
            {
                IdentityX = IdentitySecondaryAbility.Create(ClassDefinition.Class);
                if (IdentityX != null)
                {
                    GrantAbility(IdentityX);
                }
            }
        }

        private void setupBattleItems(CharacterBuildData InBuild)
        {
            for (int i = 0; i < battleItems.Count; ++i)
            {
                if (battleItems[i] != null)
                {
                    RemoveAbility(battleItems[i]);
                }
            }

            battleItems.Clear();
            var table = Database != null && Database.Tables != null ? Database.Tables.BattleItem : null;
            if (table == null)
            {
                return;
            }

            var slots = InBuild != null && InBuild.BattleItemSlots != null && InBuild.BattleItemSlots.Length > 0 ? InBuild.BattleItemSlots : new[] { 1, 2, 3, 4 };
            for (int i = 0; i < slots.Length && i < BattleItemSlotCount; ++i)
            {
                // 빈 칸은 null 로 자리를 지킨다 → 단축키 번호가 칸 번호와 항상 같다.
                BattleItemRow row;
                battleItems.Add(table.TryGet(slots[i], out row) == true ? GrantAbility(new BattleItemAbility(row)) : null);
            }
        }
    }
}
