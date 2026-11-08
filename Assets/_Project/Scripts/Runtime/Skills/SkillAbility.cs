using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Effects;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Skills
{
    /// <summary>
    /// SkillDefinition 하나를 실행하는 GameplayAbility.
    ///  - 스킬 파라미터는 자기 SkillAttributeSet(Params) 에 있고, 트라이포드/보석은 GameplayEffectDef 를
    ///    AddRuntimeEffect 로 "동적으로" 붙여 스킬을 강화한다 (문서 요구 사항).
    ///  - 타격은 WaitDelayTask 를 이어 붙여 순서대로 실행한다.
    /// </summary>
    public class SkillAbility : GameplayAbility
    {
        private struct RuntimeEffect
        {
            public ETripodTiming Timing;
            public GameplayEffectDef Def;
            /// <summary>OwnerWhileSelected 일 때 시전자 ASC 의 활성 이펙트 핸들.</summary>
            public int OwnerEffectHandle;
        }

        public readonly SkillDefinition Definition;
        public readonly SkillAttributeSet Params = new SkillAttributeSet();
        public int Level = 1;

        private readonly Dictionary<int, RuntimeEffect> runtimeEffects = new Dictionary<int, RuntimeEffect>();
        private readonly List<GameplayEffectDef> activateEffects = new List<GameplayEffectDef>();
        private readonly List<GameplayEffectDef> hitEffects = new List<GameplayEffectDef>();
        private readonly List<CombatActor> targetBuffer = new List<CombatActor>();
        private List<SkillHit> hitsOverride;
        private int nextRuntimeHandle = 1;
        private int hitIndex;
        private int extraHitsRemaining;
        private bool superArmorGranted;
        private bool counterGranted;

        public SkillAbility(SkillDefinition InDefinition)
        {
            Definition = InDefinition;
            AbilityId = InDefinition.SkillId;
            AbilityTags.Add((int)ETag.Ability_Skill);
            if (InDefinition.IsAwakening == true)
            {
                AbilityTags.Add((int)ETag.Ability_Awakening);
            }

            var blocked = GameTags.IncapacitatedTags;
            for (int i = 0; i < blocked.Length; ++i)
            {
                ActivationBlockedTags.Add((int)blocked[i]);
            }

            ActivationBlockedTags.Add((int)ETag.State_Silenced);
            ActivationBlockedTags.Add((int)ETag.State_Dashing);
            if (InDefinition.LocksMovement == true)
            {
                ActivationOwnedTags.Add((int)ETag.State_Casting);
            }

            // 시전 중에는 다른 스킬을 막는다 (회피기는 Ability_Dodge 라 막히지 않는다).
            BlockAbilitiesWithTags.Add((int)ETag.Ability_Skill);

            CooldownEffect = GameEffects.Cooldown;
            CostEffect = GameEffects.ManaCost;
            CostAttribute = (int)EAttr.Mana;
            SetByCallerCostKey = (int)ESetByCaller.Cost;
            SetByCallerCooldownKey = (int)ESetByCaller.Cooldown;
            refreshCostAndCooldown();
        }

        public CombatActor Actor
        {
            get { return Owner != null ? Owner.OwnerActor as CombatActor : null; }
        }

        public IReadOnlyList<SkillHit> Hits
        {
            get { return hitsOverride != null && hitsOverride.Count > 0 ? hitsOverride : Definition.Hits; }
        }

        public bool IsCounterSkill
        {
            get { return Definition.IsCounter == true || counterGranted == true || Params.GetFlag(ESkillAttr.Counter) == true; }
        }

        /// <summary>최종 쿨다운 (초, 캐릭터 쿨감 전).</summary>
        public float EffectiveCooldown
        {
            get
            {
                float tripod = Params.Get(ESkillAttr.CooldownPercent) * 0.01f;
                float gem = 1.0f - Mathf.Clamp(Params.Get(ESkillAttr.GemCooldownPercent), 0.0f, 90.0f) * 0.01f;
                return Mathf.Max(0.0f, Definition.Cooldown * tripod * gem);
            }
        }

        public float EffectiveManaCost
        {
            get { return Mathf.Max(0.0f, Definition.ManaCost * Params.Get(ESkillAttr.ManaCostPercent) * 0.01f); }
        }

        // ------------------------------------------------------------------ dynamic effects (tripod / gem)

        /// <summary>
        /// 이펙트 정의를 이 스킬에 동적으로 붙인다. 돌려준 핸들로 뗀다.
        /// SkillParameter 는 Params 모디파이어로, OwnerWhileSelected 는 시전자 ASC 의 Infinite 이펙트로,
        /// OwnerOnActivate / TargetOnHit 는 시전/적중 때 적용할 목록에 들어간다.
        /// </summary>
        public int AddRuntimeEffect(GameplayEffectDef InDef, ETripodTiming InTiming, float InScale)
        {
            if (InDef == null)
            {
                return 0;
            }

            int handle = nextRuntimeHandle++;
            var runtime = new RuntimeEffect();
            runtime.Timing = InTiming;
            runtime.Def = InTiming == ETripodTiming.SkillParameter ? InDef : EffectScaling.Scaled(InDef, InScale);

            switch (InTiming)
            {
                case ETripodTiming.SkillParameter:
                    Params.AddEffectModifiers(InDef, handle, InScale);
                    break;
                case ETripodTiming.OwnerWhileSelected:
                    if (Owner != null)
                    {
                        runtime.OwnerEffectHandle = Owner.ApplyGameplayEffectSpecToSelf(Owner.MakeOutgoingSpec(runtime.Def, AbilityId));
                    }

                    break;
                case ETripodTiming.OwnerOnActivate:
                    activateEffects.Add(runtime.Def);
                    break;
                case ETripodTiming.TargetOnHit:
                    hitEffects.Add(runtime.Def);
                    break;
            }

            runtimeEffects.Add(handle, runtime);
            refreshCostAndCooldown();
            return handle;
        }

        public void RemoveRuntimeEffect(int InHandle)
        {
            RuntimeEffect runtime;
            if (runtimeEffects.TryGetValue(InHandle, out runtime) == false)
            {
                return;
            }

            runtimeEffects.Remove(InHandle);
            switch (runtime.Timing)
            {
                case ETripodTiming.SkillParameter:
                    Params.RemoveModifiers(InHandle);
                    break;
                case ETripodTiming.OwnerWhileSelected:
                    if (Owner != null && runtime.OwnerEffectHandle != 0)
                    {
                        Owner.RemoveActiveEffect(runtime.OwnerEffectHandle);
                    }

                    break;
                case ETripodTiming.OwnerOnActivate:
                    activateEffects.Remove(runtime.Def);
                    break;
                case ETripodTiming.TargetOnHit:
                    hitEffects.Remove(runtime.Def);
                    break;
            }

            refreshCostAndCooldown();
        }

        public void ClearRuntimeEffects()
        {
            var handles = new List<int>(runtimeEffects.Keys);
            for (int i = 0; i < handles.Count; ++i)
            {
                RemoveRuntimeEffect(handles[i]);
            }

            hitsOverride = null;
            setSuperArmor(false);
            counterGranted = false;
        }

        public int RuntimeEffectCount
        {
            get { return runtimeEffects.Count; }
        }

        public void SetHitsOverride(List<SkillHit> InHits)
        {
            hitsOverride = InHits;
        }

        public void SetGrantedFlags(bool InSuperArmor, bool InCounter)
        {
            setSuperArmor(InSuperArmor);
            counterGranted = InCounter;
        }

        // ------------------------------------------------------------------ activation

        public override bool CanActivate(out EActivateFailReason OutReason)
        {
            refreshCostAndCooldown();
            return base.CanActivate(out OutReason);
        }

        protected override void OnGiven()
        {
            setSuperArmor(definitionSuperArmor);
            if (Definition.Protection == ESkillProtection.PushImmune)
            {
                ActivationOwnedTags.Add((int)ETag.State_PushImmune);
            }
        }

        /// <summary>정의상 피격 이상 면역인가 (SuperArmor 체크 또는 Protection = SuperArmor).</summary>
        private bool definitionSuperArmor
        {
            get { return Definition.SuperArmor == true || Definition.Protection == ESkillProtection.SuperArmor; }
        }

        /// <summary>지금 적용되는 보호 효과 (트라이포드로 피격 이상 면역을 얻으면 그쪽).</summary>
        public ESkillProtection EffectiveProtection
        {
            get { return superArmorGranted == true || definitionSuperArmor == true ? ESkillProtection.SuperArmor : Definition.Protection; }
        }

        protected override void OnActivate(AbilityActivationInfo InInfo)
        {
            var actor = Actor;
            if (actor == null || CommitAbility() == false)
            {
                EndAbility(true);
                return;
            }

            var aim = HitGeometry.Flat(InInfo.AimPoint - actor.Position);
            if (aim.sqrMagnitude > 0.01f)
            {
                actor.FaceTowards(InInfo.AimPoint);
            }

            if (actor.Motor != null)
            {
                actor.Motor.Stop();
                if (Definition.DashDistance > 0.0f)
                {
                    actor.Motor.Dash(actor.Forward, Definition.DashDistance, Definition.DashDuration);
                }
            }

            for (int i = 0; i < activateEffects.Count; ++i)
            {
                Owner.ApplyGameplayEffectSpecToSelf(Owner.MakeOutgoingSpec(activateEffects[i], AbilityId));
            }

            applySupport(actor);

            hitIndex = 0;
            extraHitsRemaining = Mathf.Max(0, Mathf.RoundToInt(Params.Get(ESkillAttr.ExtraHits)));
            scheduleNext();
        }

        private void scheduleNext()
        {
            var hits = Hits;
            float timeScale = Params.TimeScale(Owner.Attributes.GetCurrent((int)EAttr.AttackSpeed));
            if (hitIndex >= hits.Count)
            {
                if (extraHitsRemaining > 0 && hits.Count > 0)
                {
                    extraHitsRemaining -= 1;
                    var last = hits[hits.Count - 1];
                    AddTask(new WaitDelayTask(last.Delay * timeScale, () =>
                    {
                        executeHit(last);
                        if (IsActive == true)
                        {
                            scheduleNext();
                        }
                    }));
                    return;
                }

                AddTask(new WaitDelayTask(Definition.Recovery * timeScale, () => EndAbility(false)));
                return;
            }

            var hit = hits[hitIndex];
            AddTask(new WaitDelayTask(hit.Delay * timeScale, () =>
            {
                hitIndex += 1;
                executeHit(hit);
                if (IsActive == true)
                {
                    scheduleNext();
                }
            }));
        }

        private void executeHit(SkillHit InHit)
        {
            var actor = Actor;
            if (actor == null || actor.IsDead == true)
            {
                EndAbility(true);
                return;
            }

            if (InHit.MoveForward > 0.0f && actor.Motor != null)
            {
                actor.Motor.Dash(actor.Forward, InHit.MoveForward, 0.08f);
            }

            float rangeScale = Params.Get(ESkillAttr.RangePercent) * 0.01f;
            var area = InHit.Area.Scaled(rangeScale);
            CombatRegistry.FindHostiles(actor, area, actor.Position, actor.Forward, targetBuffer);
            SkillHitEvents.RaiseHit(this, area, actor.Position, actor.Forward, targetBuffer.Count);

            float coefficient = InHit.Coefficient * Params.Get(ESkillAttr.DamagePercent) * 0.01f * Definition.LevelDamagePercent(Level) * 0.01f;
            float stagger = InHit.Stagger * Params.Get(ESkillAttr.StaggerPercent) * 0.01f;
            float destruction = InHit.Destruction > 0.0f ? InHit.Destruction + Params.Get(ESkillAttr.DestructionAdd) : 0.0f;

            var hitParams = new HitParams();
            hitParams.SkillId = AbilityId;
            hitParams.Coefficient = coefficient;
            hitParams.FlatDamage = InHit.FlatDamage;
            hitParams.Stagger = stagger;
            hitParams.Destruction = destruction;
            hitParams.CritRateBonus = Params.Get(ESkillAttr.CritRateAdd);
            hitParams.CritDamageBonus = Params.Get(ESkillAttr.CritDamageAdd);
            hitParams.GemDamagePercent = Params.Get(ESkillAttr.GemDamagePercent);
            hitParams.PositionalType = Definition.Positional;
            hitParams.BackAttackBonus = Params.Get(ESkillAttr.BackAttackAdd);
            hitParams.HeadAttackBonus = Params.Get(ESkillAttr.HeadAttackAdd);
            hitParams.IsCounter = IsCounterSkill;
            hitParams.Element = Definition.Element;
            hitParams.Flags = Definition.IsAwakening == true ? ESkillFlags.Awakening : ESkillFlags.None;
            hitParams.SkillCooldown = Definition.Cooldown;

            for (int i = 0; i < targetBuffer.Count; ++i)
            {
                var target = targetBuffer[i];
                SkillHitResolver.ApplyHit(actor, target, hitParams);

                if (InHit.ApplyStatus != ETag.None)
                {
                    target.ApplyStatus(InHit.ApplyStatus, InHit.StatusDuration, Owner);
                }

                for (int e = 0; e < InHit.TargetEffects.Count; ++e)
                {
                    if (InHit.TargetEffects[e] != null)
                    {
                        target.ApplyEffect(InHit.TargetEffects[e].GetDef(), Owner);
                    }
                }

                for (int e = 0; e < hitEffects.Count; ++e)
                {
                    target.ApplyEffect(hitEffects[e], Owner);
                }
            }

            if (targetBuffer.Count > 0)
            {
                // 아이덴티티 게이지 (직업별 규칙은 미정 — 구현 보류 사항.md). 적중 1회당 계수 비례 임시 획득.
                float gain = 2.0f * InHit.Coefficient * (1.0f + Owner.Attributes.GetCurrent((int)EAttr.StatSpecialization) * StatFormula.Settings.IdentityGainPerSpecialization * 0.01f);
                Owner.Attributes.ApplyToBase((int)EAttr.IdentityGauge, EModifierOp.Add, gain);
            }
        }

        private void applySupport(CombatActor InActor)
        {
            if (Definition.SelfPartyHealCoefficient <= 0.0f && Definition.PartyShieldAmount <= 0.0f && Definition.PartyBuffs.Count == 0)
            {
                return;
            }

            CombatRegistry.FindTeam(InActor.Team, targetBuffer);
            float radiusSqr = Definition.PartyBuffRadius * Definition.PartyBuffRadius;
            for (int i = 0; i < targetBuffer.Count; ++i)
            {
                var ally = targetBuffer[i];
                if ((ally.Position - InActor.Position).sqrMagnitude > radiusSqr)
                {
                    continue;
                }

                if (Definition.SelfPartyHealCoefficient > 0.0f)
                {
                    var heal = Owner.MakeOutgoingSpec(GameEffects.Heal, AbilityId);
                    heal.SetByCaller((int)ESetByCaller.Coefficient, Definition.SelfPartyHealCoefficient);
                    Owner.ApplyGameplayEffectSpecToTarget(heal, ally.Asc);
                }

                if (Definition.PartyShieldAmount > 0.0f)
                {
                    var shield = Owner.MakeOutgoingSpec(GameEffects.Shield, AbilityId);
                    shield.SetByCaller((int)ESetByCaller.Magnitude, Definition.PartyShieldAmount);
                    Owner.ApplyGameplayEffectSpecToTarget(shield, ally.Asc);
                }

                for (int b = 0; b < Definition.PartyBuffs.Count; ++b)
                {
                    if (Definition.PartyBuffs[b] != null)
                    {
                        ally.ApplyEffect(Definition.PartyBuffs[b].GetDef(), Owner);
                    }
                }
            }
        }

        private void refreshCostAndCooldown()
        {
            CostAmount = EffectiveManaCost;
            CooldownDuration = EffectiveCooldown;
        }

        private void setSuperArmor(bool InEnabled)
        {
            if (InEnabled == superArmorGranted)
            {
                return;
            }

            superArmorGranted = InEnabled;
            if (InEnabled == true)
            {
                ActivationOwnedTags.Add((int)ETag.State_SuperArmor);
            }
            else if (definitionSuperArmor == false)
            {
                ActivationOwnedTags.Remove((int)ETag.State_SuperArmor);
            }
        }
    }
}
