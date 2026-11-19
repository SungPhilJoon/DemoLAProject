using System;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Skills
{
    /// <summary>스킬 GameplayCue 가 재생되는 구간.</summary>
    public enum ESkillCuePhase
    {
        /// <summary>시전 전: 캐릭터를 중심으로 아우라가 펼쳐진다 (시전 시작 → 첫 타격). 셰이더 큐 모양 = Aura.</summary>
        PreCast = 0,
        /// <summary>시전 중: 무기 궤적을 따라가는 잔상 (첫 타격 직전 → 마지막 타격). 셰이더 큐 모양 = Trail.</summary>
        Casting = 1,
        /// <summary>시전 후: 마지막 타격 때 무기에서 빛이 뿜어져 나온다 (1회). 셰이더 큐 모양 = Burst.</summary>
        PostCast = 2,
    }

    /// <summary>
    /// 스킬 한 구간의 GameplayCue 슬롯 (SkillDefinition). 큐는 프레임워크 GameplayCueNotify (보통 ShaderGameplayCueNotify).
    /// </summary>
    [Serializable]
    public class SkillCueSlot
    {
        public GameplayCueNotify Cue;
        [Tooltip("자동 시작 시각에 더하는 보정 (초, 시전 속도 1배)")]
        public float StartOffset;
        [Tooltip("지속 (초, 시전 속도 1배). 0 = 자동 (시전 전: 첫 타격까지 / 시전 중: 마지막 타격까지 / 시전 후: 큐 기본 길이)")]
        public float Duration;

        public bool IsSet
        {
            get { return Cue != null && Cue.IsValid == true; }
        }

        public SkillCueSlot Clone()
        {
            return new SkillCueSlot { Cue = Cue, StartOffset = StartOffset, Duration = Duration };
        }
    }

    /// <summary>
    /// 스킬 큐 구간 시각 (시전 시작 기준 초, 시전 속도 1배). 게임(SkillCuePlayer)과 Skill Ability Tool 타임라인이 같이 쓴다.
    ///  - 시전 전: 0 + 보정 → 첫 타격 (지속 0 일 때)
    ///  - 시전 중: 첫 타격 − CastingLead + 보정 → 마지막 타격 + CastingTail
    ///  - 시전 후: 마지막 타격 + 보정 → + 큐 기본 길이
    /// </summary>
    public static class SkillCueTiming
    {
        /// <summary>시전 중 궤적이 첫 타격보다 먼저 켜지는 시간 (휘두르기 시작).</summary>
        public const float CastingLead = 0.3f;
        /// <summary>시전 중 궤적이 마지막 타격 뒤에 남는 시간.</summary>
        public const float CastingTail = 0.15f;
        private const float MinWindow = 0.1f;

        public static readonly ESkillCuePhase[] Phases = { ESkillCuePhase.PreCast, ESkillCuePhase.Casting, ESkillCuePhase.PostCast };

        /// <summary>구간 → 셰이더 큐 모양.</summary>
        public static EGameplayCueShape ShapeOf(ESkillCuePhase InPhase)
        {
            switch (InPhase)
            {
                case ESkillCuePhase.PreCast:
                    return EGameplayCueShape.Aura;
                case ESkillCuePhase.Casting:
                    return EGameplayCueShape.Trail;
                default:
                    return EGameplayCueShape.Burst;
            }
        }

        public static SkillCueSlot Slot(SkillDefinition InSkill, ESkillCuePhase InPhase)
        {
            if (InSkill == null)
            {
                return null;
            }

            switch (InPhase)
            {
                case ESkillCuePhase.PreCast:
                    return InSkill.PreCastCue;
                case ESkillCuePhase.Casting:
                    return InSkill.CastingCue;
                default:
                    return InSkill.PostCastCue;
            }
        }

        public static void SetSlot(SkillDefinition InSkill, ESkillCuePhase InPhase, SkillCueSlot InSlot)
        {
            if (InSkill == null)
            {
                return;
            }

            switch (InPhase)
            {
                case ESkillCuePhase.PreCast:
                    InSkill.PreCastCue = InSlot;
                    break;
                case ESkillCuePhase.Casting:
                    InSkill.CastingCue = InSlot;
                    break;
                default:
                    InSkill.PostCastCue = InSlot;
                    break;
            }
        }

        public static void Window(SkillDefinition InSkill, ESkillCuePhase InPhase, out float OutStart, out float OutEnd)
        {
            var slot = Slot(InSkill, InPhase);
            float offset = slot != null ? slot.StartOffset : 0.0f;
            float duration = slot != null ? slot.Duration : 0.0f;
            int count = InSkill != null ? InSkill.Hits.Count : 0;
            float firstHit = count > 0 ? InSkill.HitTime(0) : 0.0f;
            float lastHit = count > 0 ? InSkill.HitTime(count - 1) : 0.0f;

            switch (InPhase)
            {
                case ESkillCuePhase.PreCast:
                    OutStart = Mathf.Max(0.0f, offset);
                    OutEnd = duration > 0.0f ? OutStart + duration : Mathf.Max(OutStart + MinWindow, firstHit);
                    break;
                case ESkillCuePhase.Casting:
                    OutStart = Mathf.Max(0.0f, firstHit - CastingLead + offset);
                    OutEnd = duration > 0.0f ? OutStart + duration : Mathf.Max(OutStart + MinWindow, lastHit + CastingTail);
                    break;
                default:
                    OutStart = Mathf.Max(0.0f, lastHit + offset);
                    float length = duration > 0.0f ? duration : (slot != null && slot.Cue != null ? slot.Cue.DefaultDuration : 0.8f);
                    OutEnd = OutStart + Mathf.Max(MinWindow, length);
                    break;
            }
        }
    }
}
