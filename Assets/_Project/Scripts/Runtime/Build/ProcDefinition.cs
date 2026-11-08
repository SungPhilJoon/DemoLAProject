using System;
using System.Collections.Generic;
using System.Text;
using DemoLA.Attributes;
using DemoLA.Core;
using DemoLA.Effects;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Build
{
    /// <summary>발동 조건.</summary>
    public enum EProcTrigger
    {
        /// <summary>내 공격이 적중할 때.</summary>
        OnHit = 0,
        OnCrit = 1,
        OnBackAttack = 2,
        OnHeadAttack = 3,
        /// <summary>내가 피해를 받을 때.</summary>
        OnDamaged = 4,
        /// <summary>스킬(각성기 포함)을 사용할 때.</summary>
        OnSkillUse = 5,
        /// <summary>재사용 대기시간이 TriggerParam 초 이상인 스킬을 사용할 때.</summary>
        OnLongCooldownSkill = 6,
        OnIdentity = 7,
        OnAwakening = 8,
        OnDodge = 9,
        /// <summary>TriggerParam 초마다.</summary>
        Periodic = 10,
        /// <summary>생명력이 TriggerParam% 아래로 떨어지는 순간 (다시 올라가면 재장전).</summary>
        OnHealthBelow = 11,
    }

    /// <summary>발동 효과.</summary>
    public enum EProcAction
    {
        /// <summary>나에게 Duration 초 버프 (Modifiers × 중첩).</summary>
        SelfBuff = 0,
        /// <summary>Radius 안 아군 전원에게 버프.</summary>
        PartyBuff = 1,
        /// <summary>맞은 대상에게 약화 효과.</summary>
        TargetDebuff = 2,
        /// <summary>맞은 대상(없으면 가장 가까운 적) 주변에 공격력 × Value 피해.</summary>
        Damage = 3,
        /// <summary>최대 생명력의 Value% 회복.</summary>
        Heal = 4,
        /// <summary>Radius 안 아군 최대 생명력의 Value% 회복.</summary>
        PartyHeal = 5,
        /// <summary>최대 생명력의 Value% 보호막.</summary>
        Shield = 6,
        /// <summary>아이덴티티 게이지 + Value.</summary>
        IdentityGauge = 7,
        /// <summary>모든 스킬 재사용 대기시간 Value 초 감소.</summary>
        ReduceCooldowns = 8,
    }

    /// <summary>
    /// 각인 · 장비 세트의 "발동형" 효과 하나. BuildProcSystem 이 전투 이벤트(피해 적용 · 어빌리티 발동 · 생명력)를 듣고 실행한다.
    /// 버프 / 디버프는 GameplayEffect(지속시간 · 중첩 · 태그)로 붙고, 중첩이 BurstAtStacks 에 닿으면 중첩을 소모해 폭발 효과를 낸다.
    /// </summary>
    [Serializable]
    public class ProcDefinition
    {
        public string Name = "발동 효과";
        public EProcTrigger Trigger;
        public float TriggerParam;
        [Range(0.0f, 1.0f)] public float Chance = 1.0f;
        [Tooltip("발동 후 다시 발동하기까지 (초)")]
        public float InternalCooldown;
        public EProcAction Action;
        public float Value;
        public float Duration = 5.0f;
        public int MaxStacks = 1;
        public float Radius = 12.0f;
        [Tooltip("중첩 1개당 수치")]
        public List<EffectModifierEntry> Modifiers = new List<EffectModifierEntry>();
        public ETag GrantTag = ETag.None;
        [Tooltip("0 보다 크면 이 중첩에 닿을 때 중첩을 모두 소모하고 BurstAction 발동")]
        public int BurstAtStacks;
        public string BurstName;
        public EProcAction BurstAction = EProcAction.Damage;
        public float BurstValue;

        [NonSerialized] private GameplayEffectDef cachedEffect;

        public static ProcDefinition Buff(string InName, EProcTrigger InTrigger, float InDuration, int InMaxStacks, params EffectModifierEntry[] InModifiers)
        {
            var proc = new ProcDefinition { Name = InName, Trigger = InTrigger, Action = EProcAction.SelfBuff, Duration = InDuration, MaxStacks = Mathf.Max(1, InMaxStacks) };
            proc.Modifiers.AddRange(InModifiers);
            return proc;
        }

        public ProcDefinition WithChance(float InChance, float InCooldown = 0.0f)
        {
            Chance = InChance;
            InternalCooldown = InCooldown;
            return this;
        }

        public ProcDefinition WithParam(float InParam)
        {
            TriggerParam = InParam;
            return this;
        }

        public ProcDefinition WithBurst(int InStacks, string InName, EProcAction InAction, float InValue)
        {
            BurstAtStacks = InStacks;
            BurstName = InName;
            BurstAction = InAction;
            BurstValue = InValue;
            return this;
        }

        /// <summary>버프/디버프 행동이 쓰는 GameplayEffect (지속 · 중첩 · 태그). 처음 쓸 때 만든다.</summary>
        public GameplayEffectDef GetEffect()
        {
            if (cachedEffect != null)
            {
                return cachedEffect;
            }

            var builder = EffectBuilder.Timed(Mathf.Max(0.1f, Duration)).Stack(MaxStacks > 1 ? EStackPolicy.Stack : EStackPolicy.Refresh, Mathf.Max(1, MaxStacks));
            for (int i = 0; i < Modifiers.Count; ++i)
            {
                builder.Modifier(Modifiers[i].ToDef());
            }

            if (GrantTag != ETag.None)
            {
                builder.Grant(GrantTag);
            }

            cachedEffect = builder.Build();
            EffectNames.Register(cachedEffect, Name, Describe(), Action == EProcAction.TargetDebuff);
            return cachedEffect;
        }

        // ------------------------------------------------------------------ text

        public static string TriggerText(EProcTrigger InTrigger, float InParam)
        {
            switch (InTrigger)
            {
                case EProcTrigger.OnHit: return "공격 적중 시";
                case EProcTrigger.OnCrit: return "치명타 적중 시";
                case EProcTrigger.OnBackAttack: return "백어택 적중 시";
                case EProcTrigger.OnHeadAttack: return "헤드어택 적중 시";
                case EProcTrigger.OnDamaged: return "피해를 받을 때";
                case EProcTrigger.OnSkillUse: return "스킬 사용 시";
                case EProcTrigger.OnLongCooldownSkill: return $"재사용 대기시간 {InParam:0}초 이상 스킬 사용 시";
                case EProcTrigger.OnIdentity: return "아이덴티티 사용 시";
                case EProcTrigger.OnAwakening: return "각성기 사용 시";
                case EProcTrigger.OnDodge: return "회피 시";
                case EProcTrigger.Periodic: return $"{InParam:0.#}초마다";
                case EProcTrigger.OnHealthBelow: return $"생명력이 {InParam:0}% 아래로 떨어지면";
                default: return "특정 조건에서";
            }
        }

        public static string ActionText(EProcAction InAction, float InValue, float InRadius)
        {
            switch (InAction)
            {
                case EProcAction.Damage: return $"공격력의 {InValue * 100.0f:0}% 피해";
                case EProcAction.Heal: return $"최대 생명력의 {InValue:0.#}% 회복";
                case EProcAction.PartyHeal: return $"{InRadius:0}m 안 아군 최대 생명력의 {InValue:0.#}% 회복";
                case EProcAction.Shield: return $"최대 생명력의 {InValue:0.#}% 보호막";
                case EProcAction.IdentityGauge: return $"아이덴티티 게이지 +{InValue:0}";
                case EProcAction.ReduceCooldowns: return $"모든 스킬 재사용 대기시간 {InValue:0.#}초 감소";
                default: return string.Empty;
            }
        }

        /// <summary>툴팁용 한글 설명 (여러 줄).</summary>
        public string Describe()
        {
            var builder = new StringBuilder();
            builder.Append(TriggerText(Trigger, TriggerParam));
            var extras = new List<string>();
            if (Chance < 1.0f)
            {
                extras.Add($"확률 {Chance * 100.0f:0}%");
            }

            if (InternalCooldown > 0.0f)
            {
                extras.Add($"재발동 {InternalCooldown:0.#}초");
            }

            if (extras.Count > 0)
            {
                builder.Append(" (").Append(string.Join(", ", extras)).Append(')');
            }

            builder.Append(": ");
            switch (Action)
            {
                case EProcAction.SelfBuff:
                case EProcAction.PartyBuff:
                case EProcAction.TargetDebuff:
                    string who = Action == EProcAction.PartyBuff ? $"{Radius:0}m 안 아군" : (Action == EProcAction.TargetDebuff ? "대상" : "나");
                    builder.Append($"[{Name}] {who}에게 {Duration:0.#}초 동안 ");
                    var parts = new List<string>();
                    for (int i = 0; i < Modifiers.Count; ++i)
                    {
                        parts.Add(KoreanText.Modifier(Modifiers[i].Attribute, Modifiers[i].Op, Modifiers[i].Value));
                    }

                    builder.Append(parts.Count > 0 ? string.Join(", ", parts) : "효과");
                    if (MaxStacks > 1)
                    {
                        builder.Append($" (중첩당, 최대 {MaxStacks}중첩)");
                    }

                    break;
                default:
                    builder.Append($"[{Name}] ").Append(ActionText(Action, Value, Radius));
                    break;
            }

            if (BurstAtStacks > 0)
            {
                builder.Append($"\n  → {BurstAtStacks}중첩이 되면 중첩을 모두 소모하고 [{BurstName}] {ActionText(BurstAction, BurstValue, Radius)}");
            }

            return builder.ToString();
        }

        public ProcDefinition Clone()
        {
            var clone = (ProcDefinition)MemberwiseClone();
            clone.Modifiers = new List<EffectModifierEntry>(Modifiers);
            clone.cachedEffect = null;
            return clone;
        }
    }
}
