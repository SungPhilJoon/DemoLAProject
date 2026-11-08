using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Effects;
using DemoLA.Skills;
using DemoLA.World;
using Framework;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Build
{
    /// <summary>
    /// 각인 · 장비 세트의 발동형 효과(ProcDefinition)를 실행한다. 캐릭터마다 하나.
    ///  - 듣는 것: DamageAppliedEvent (내가 때림 / 내가 맞음), ASC.OnAbilityActivated (스킬 · 각성기 · 아이덴티티 · 회피), 매 프레임(주기 · 생명력 문턱)
    ///  - 하는 것: GameplayEffect 버프/디버프(중첩 · 지속 · 태그), 피해, 회복, 보호막, 게이지, 쿨다운 감소
    ///  - 발동형 피해는 ProcSkillId 로 표시해 다시 발동 조건이 되지 않게 막는다 (무한 연쇄 방지).
    /// </summary>
    public class BuildProcSystem : BaseBehaviour
    {
        public const int ProcSkillId = 9300;

        private class Entry
        {
            public ProcDefinition Def;
            public string Source;
            public float ReadyAt;
            public float PeriodTimer;
            public bool HealthArmed = true;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<CombatActor> buffer = new List<CombatActor>();
        private CombatActor actor;

        public void Set(CombatActor InActor, IReadOnlyList<KeyValuePair<ProcDefinition, string>> InProcs)
        {
            unbind();
            actor = InActor;
            entries.Clear();
            for (int i = 0; i < InProcs.Count; ++i)
            {
                entries.Add(new Entry { Def = InProcs[i].Key, Source = InProcs[i].Value });
            }

            if (actor != null && actor.Asc != null)
            {
                actor.Asc.OnAbilityActivated += handleAbility;
            }
        }

        private void OnEnable()
        {
            EventBus<DamageAppliedEvent>.Subscribe(handleDamage);
        }

        private void OnDisable()
        {
            EventBus<DamageAppliedEvent>.Unsubscribe(handleDamage);
        }

        protected override void OnUnityDestroy()
        {
            unbind();
        }

        private void unbind()
        {
            if (actor != null && actor.Asc != null)
            {
                actor.Asc.OnAbilityActivated -= handleAbility;
            }
        }

        // ------------------------------------------------------------------ triggers

        private void handleDamage(DamageAppliedEvent InEvent)
        {
            if (actor == null || actor.IsDead == true || InEvent.SkillId == ProcSkillId)
            {
                return;
            }

            if (InEvent.Source == actor.Asc && InEvent.Target != actor.Asc)
            {
                var target = InEvent.Target != null ? InEvent.Target.OwnerActor as CombatActor : null;
                fire(EProcTrigger.OnHit, target);
                if (InEvent.IsCrit == true)
                {
                    fire(EProcTrigger.OnCrit, target);
                }

                if (InEvent.Positional == EPositional.BackAttack)
                {
                    fire(EProcTrigger.OnBackAttack, target);
                }
                else if (InEvent.Positional == EPositional.HeadAttack)
                {
                    fire(EProcTrigger.OnHeadAttack, target);
                }
            }
            else if (InEvent.Target == actor.Asc && InEvent.RawAmount > 0.0f)
            {
                fire(EProcTrigger.OnDamaged, InEvent.Source != null ? InEvent.Source.OwnerActor as CombatActor : null);
            }
        }

        private void handleAbility(GameplayAbility InAbility)
        {
            var skill = InAbility as SkillAbility;
            if (skill != null)
            {
                fire(EProcTrigger.OnSkillUse, null, skill.Definition.Cooldown);
                fire(EProcTrigger.OnLongCooldownSkill, null, skill.Definition.Cooldown);
                if (skill.Definition.IsAwakening == true)
                {
                    fire(EProcTrigger.OnAwakening, null);
                }

                return;
            }

            if (InAbility.AbilityTags.HasTag((int)ETag.Ability_Identity) == true)
            {
                fire(EProcTrigger.OnIdentity, null);
            }
            else if (InAbility.AbilityTags.HasTag((int)ETag.Ability_Dodge) == true)
            {
                fire(EProcTrigger.OnDodge, null);
            }
        }

        private void Update()
        {
            if (actor == null || actor.IsDead == true || actor.Stats == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            float healthPercent = actor.Stats.HealthRatio * 100.0f;
            for (int i = 0; i < entries.Count; ++i)
            {
                var entry = entries[i];
                if (entry.Def.Trigger == EProcTrigger.Periodic)
                {
                    entry.PeriodTimer += deltaTime;
                    if (entry.PeriodTimer >= Mathf.Max(0.5f, entry.Def.TriggerParam))
                    {
                        entry.PeriodTimer = 0.0f;
                        tryRun(entry, null);
                    }
                }
                else if (entry.Def.Trigger == EProcTrigger.OnHealthBelow)
                {
                    if (healthPercent > entry.Def.TriggerParam + 5.0f)
                    {
                        entry.HealthArmed = true;
                    }
                    else if (entry.HealthArmed == true && healthPercent <= entry.Def.TriggerParam)
                    {
                        if (tryRun(entry, null) == true)
                        {
                            entry.HealthArmed = false;
                        }
                    }
                }
            }
        }

        private void fire(EProcTrigger InTrigger, CombatActor InTarget, float InSkillCooldown = 0.0f)
        {
            for (int i = 0; i < entries.Count; ++i)
            {
                var entry = entries[i];
                if (entry.Def.Trigger != InTrigger)
                {
                    continue;
                }

                if (InTrigger == EProcTrigger.OnLongCooldownSkill && InSkillCooldown < entry.Def.TriggerParam)
                {
                    continue;
                }

                tryRun(entry, InTarget);
            }
        }

        private bool tryRun(Entry InEntry, CombatActor InTarget)
        {
            if (Time.time < InEntry.ReadyAt || (InEntry.Def.Chance < 1.0f && GameRandom.Value > InEntry.Def.Chance))
            {
                return false;
            }

            InEntry.ReadyAt = Time.time + InEntry.Def.InternalCooldown;
            run(InEntry.Def, InEntry.Def.Action, InEntry.Def.Value, InTarget);
            return true;
        }

        // ------------------------------------------------------------------ actions

        private void run(ProcDefinition InDef, EProcAction InAction, float InValue, CombatActor InTarget)
        {
            switch (InAction)
            {
                case EProcAction.SelfBuff:
                    applyStacking(InDef, actor, InTarget);
                    break;
                case EProcAction.PartyBuff:
                    CombatRegistry.FindTeam(actor.Team, buffer);
                    for (int i = 0; i < buffer.Count; ++i)
                    {
                        if (HitGeometry.Flat(buffer[i].Position - actor.Position).magnitude <= InDef.Radius)
                        {
                            buffer[i].ApplyEffect(InDef.GetEffect(), actor.Asc);
                        }
                    }

                    break;
                case EProcAction.TargetDebuff:
                    if (InTarget != null && InTarget.IsDead == false && CombatRegistry.IsHostile(actor.Team, InTarget.Team) == true)
                    {
                        applyStacking(InDef, InTarget, InTarget);
                    }

                    break;
                case EProcAction.Damage:
                    var victim = InTarget != null && InTarget.IsDead == false ? InTarget : CombatRegistry.FindNearestHostile(actor, 15.0f);
                    if (victim != null)
                    {
                        var hit = new HitParams();
                        hit.SkillId = ProcSkillId;
                        hit.Coefficient = InValue;
                        hit.Stagger = 10.0f * InValue;
                        SkillHitResolver.ApplyArea(actor, HitArea.Circle(2.0f), victim.Position, actor.Forward, hit);
                    }

                    break;
                case EProcAction.Heal:
                    heal(actor, InValue);
                    break;
                case EProcAction.PartyHeal:
                    CombatRegistry.FindTeam(actor.Team, buffer);
                    for (int i = 0; i < buffer.Count; ++i)
                    {
                        if (HitGeometry.Flat(buffer[i].Position - actor.Position).magnitude <= InDef.Radius)
                        {
                            heal(buffer[i], InValue);
                        }
                    }

                    break;
                case EProcAction.Shield:
                    var shield = actor.Asc.MakeOutgoingSpec(GameEffects.Shield, ProcSkillId);
                    shield.SetByCaller((int)ESetByCaller.Magnitude, actor.Stats.Get(EAttr.MaxHealth) * InValue * 0.01f);
                    actor.Asc.ApplyGameplayEffectSpecToSelf(shield);
                    break;
                case EProcAction.IdentityGauge:
                    float max = Mathf.Max(1.0f, actor.Stats.Get(EAttr.MaxIdentityGauge));
                    float gauge = actor.Asc.Attributes.GetCurrent((int)EAttr.IdentityGauge);
                    actor.Asc.Attributes.ApplyToBase((int)EAttr.IdentityGauge, EModifierOp.Add, Mathf.Min(InValue, max - gauge));
                    break;
                case EProcAction.ReduceCooldowns:
                    var effects = actor.Asc.Effects;
                    for (int i = 0; i < effects.Count; ++i)
                    {
                        if (effects[i].Spec != null && effects[i].Spec.Def.IsCooldown == true && effects[i].Spec.AbilityId < 9000)
                        {
                            effects[i].Remaining = Mathf.Max(0.0f, effects[i].Remaining - InValue);
                        }
                    }

                    break;
            }
        }

        /// <summary>버프/디버프를 붙이고, 폭발 중첩에 닿으면 중첩을 지우고 폭발 효과.</summary>
        private void applyStacking(ProcDefinition InDef, CombatActor InCarrier, CombatActor InTarget)
        {
            var effect = InDef.GetEffect();
            InCarrier.ApplyEffect(effect, actor.Asc);
            if (InDef.BurstAtStacks <= 0)
            {
                return;
            }

            var effects = InCarrier.Asc.Effects;
            for (int i = 0; i < effects.Count; ++i)
            {
                var active = effects[i];
                if (active.Spec == null || active.Spec.Def != effect || active.StackCount < InDef.BurstAtStacks)
                {
                    continue;
                }

                InCarrier.Asc.RemoveActiveEffect(active.Handle);
                Raid.Message($"{InDef.BurstName}!", 1.0f, false);
                run(InDef, InDef.BurstAction, InDef.BurstValue, InTarget);
                return;
            }
        }

        private void heal(CombatActor InTarget, float InPercent)
        {
            if (InTarget == null || InTarget.IsDead == true)
            {
                return;
            }

            var spec = actor.Asc.MakeOutgoingSpec(GameEffects.Heal, ProcSkillId);
            spec.SetByCaller((int)ESetByCaller.FlatDamage, InTarget.Stats.Get(EAttr.MaxHealth) * InPercent * 0.01f);
            actor.Asc.ApplyGameplayEffectSpecToTarget(spec, InTarget.Asc);
        }
    }
}
