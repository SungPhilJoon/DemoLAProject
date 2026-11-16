using System;
using System.Collections.Generic;
using DemoLA.Character;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Effects;
using UnityEngine;

namespace DemoLA.Skills
{
    [Serializable]
    public class SkillHit
    {
        [Tooltip("이전 타격(첫 타격은 시전 시작)으로부터의 지연 (초, 시전 속도로 나뉜다)")]
        public float Delay = 0.25f;
        public HitArea Area = HitArea.Circle(3.0f, 1.5f);
        [Tooltip("공격력 × 계수")]
        public float Coefficient = 1.0f;
        public float FlatDamage;
        [Tooltip("무력화 수치")]
        public float Stagger = 10.0f;
        [Tooltip("부위 파괴 수치")]
        public float Destruction;
        [Tooltip("이 타격 직전에 시전자가 정면으로 이동하는 거리")]
        public float MoveForward;
        [Tooltip("대상에게 거는 상태이상 (None = 없음)")]
        public ETag ApplyStatus = ETag.None;
        public float StatusDuration;
        public List<GameplayEffectAsset> TargetEffects = new List<GameplayEffectAsset>();
    }

    /// <summary>스킬 사용 중 보호 효과.</summary>
    public enum ESkillProtection
    {
        None = 0,
        /// <summary>경직 면역 (넉백 면역): 밀려나지 않는다. 띄우기 · 기절 같은 이동 불가 효과는 받는다.</summary>
        PushImmune = 1,
        /// <summary>피격 이상 면역: 넉백 · 띄우기 · 기절 · 공포 등 모든 이동 불가 효과를 무시한다 (피해는 받는다).</summary>
        SuperArmor = 2,
    }

    public enum ESkillPositionalType
    {
        None = 0,
        BackAttack = 1,
        HeadAttack = 2,
    }

    /// <summary>
    /// 스킬 정의 (에디터에서 설정). 런타임 동작은 SkillAbility 가 한다.
    /// 트라이포드는 Tiers[단계].Options[선택] 이며, 고르면 그 옵션의 TripodEffect 들이 Ability 에 동적으로 붙는다.
    /// </summary>
    [CreateAssetMenu(menuName = "DemoLA/Skill/Skill Definition", fileName = "SK_New")]
    public class SkillDefinition : ScriptableObject
    {
        public int SkillId;
        public string DisplayName = "Skill";
        [TextArea] public string Description;
        public ECharacterClass OwnerClass;
        public Color IconColor = Color.white;

        [Header("Timing")]
        public float Cooldown = 6.0f;
        public float ManaCost = 40.0f;
        [Tooltip("마지막 타격 후 후딜 (초)")]
        public float Recovery = 0.25f;
        [Tooltip("시전 시작 시 이동 (돌진형)")]
        public float DashDistance;
        public float DashDuration = 0.2f;

        [Header("Animation")]
        [Tooltip("시전 애니메이션 (Humanoid 클립 권장 — 모델이 달라도 리타깃된다). 비우면 이동 애니메이션 그대로.\n툴: DemoLA/Tools/Skill Ability Tool (미리보기에서 타격 시점과 맞춘다)")]
        public AnimationClip CastAnimation;
        [Tooltip("클립 재생 배율. 실제 재생 속도 = 이 값 × 시전 속도 (공격 속도 · 트라이포드)")]
        public float AnimationSpeed = 1.0f;
        [Tooltip("클립 시작 위치 (초) — 앞쪽 준비 동작을 잘라낼 때")]
        public float AnimationStartTime;
        [Tooltip("이동 애니메이션 → 시전 클립 블렌드 (초)")]
        public float AnimationBlendIn = 0.08f;
        [Tooltip("시전 클립 → 이동 애니메이션 블렌드 (초). 클립이 끝나기 이만큼 전, 또는 스킬이 끝날 때 시작")]
        public float AnimationBlendOut = 0.15f;

        [Header("Combat")]
        public List<SkillHit> Hits = new List<SkillHit>();
        public ESkillPositionalType Positional;
        public EElement Element;
        public bool SuperArmor;
        [Tooltip("스킬 사용 중 보호 효과 (SuperArmor 를 켜면 피격 이상 면역으로 취급)")]
        public ESkillProtection Protection;
        public bool IsCounter;
        public bool IsAwakening;
        [Tooltip("시전 중 이동 입력 무시 / 시전 중 상태")]
        public bool LocksMovement = true;
        [Tooltip("AI/조준 보정용 사거리. 0 이면 Hits 로 계산")]
        public float AiRange;
        [Tooltip("시전자에게 회복을 거는 지원 스킬 (계수 = 회복 계수)")]
        public float SelfPartyHealCoefficient;
        public float PartyShieldAmount;
        public float PartyBuffRadius = 12.0f;
        public List<GameplayEffectAsset> PartyBuffs = new List<GameplayEffectAsset>();

        [Header("Level")]
        public int MaxLevel = 12;
        [Tooltip("스킬 레벨 1 당 피해 증가 %")]
        public float DamagePerLevel = 8.0f;

        [Header("Tripod")]
        public List<TripodTier> Tiers = new List<TripodTier>();

        public float EstimatedRange
        {
            get
            {
                if (AiRange > 0.0f)
                {
                    return AiRange;
                }

                float range = 2.0f;
                for (int i = 0; i < Hits.Count; ++i)
                {
                    range = Mathf.Max(range, Hits[i].Area.Reach);
                }

                return range + DashDistance;
            }
        }

        /// <summary>InHitIndex 번째 타격 시각 (시전 시작 기준 초, 시전 속도 1배). 지연은 누적이다.</summary>
        public float HitTime(int InHitIndex)
        {
            float t = 0.0f;
            for (int i = 0; i <= InHitIndex && i < Hits.Count; ++i)
            {
                t += Mathf.Max(0.0f, Hits[i].Delay);
            }

            return t;
        }

        /// <summary>시전 시간 (마지막 타격 + 후딜, 시전 속도 1배). 추가 타격 트라이포드는 뺀다.</summary>
        public float CastDuration
        {
            get { return HitTime(Hits.Count - 1) + Mathf.Max(0.0f, Recovery); }
        }

        /// <summary>시전 시간 동안 재생되는 클립 길이 (초, 클립 기준).</summary>
        public float AnimationCoveredTime
        {
            get { return CastDuration * Mathf.Max(0.01f, AnimationSpeed); }
        }

        /// <summary>레벨 피해 배율 % (1레벨 = 100).</summary>
        public float LevelDamagePercent(int InLevel)
        {
            return 100.0f + DamagePerLevel * Mathf.Max(0, Mathf.Min(InLevel, MaxLevel) - 1);
        }
    }

    [Serializable]
    public class TripodTier
    {
        [Tooltip("이 단계가 열리는 스킬 레벨")]
        public int UnlockSkillLevel = 4;
        public List<TripodOption> Options = new List<TripodOption>();
    }

    [Serializable]
    public class TripodOption
    {
        public string Name = "Tripod";
        [TextArea] public string Description;
        [Tooltip("트라이포드 최대 레벨")]
        public int MaxLevel = 5;
        public List<TripodEffect> Effects = new List<TripodEffect>();
        [Tooltip("비어 있지 않으면 스킬의 타격 목록을 이것으로 교체 (판정 모양/횟수 변경형)")]
        public List<SkillHit> ReplaceHits = new List<SkillHit>();
        [Tooltip("카운터 판정 부여")]
        public bool GrantsCounter;
        public bool GrantsSuperArmor;
    }

    /// <summary>트라이포드 효과가 언제 어디에 붙는가.</summary>
    public enum ETripodTiming
    {
        /// <summary>스킬 파라미터(SkillAttributeSet)에 상시 적용 — 피해%, 쿨다운%, 범위%...</summary>
        SkillParameter = 0,
        /// <summary>트라이포드를 고른 동안 시전자에게 상시 적용 (Infinite).</summary>
        OwnerWhileSelected = 1,
        /// <summary>스킬 시전 시 시전자에게 적용 (자기 버프).</summary>
        OwnerOnActivate = 2,
        /// <summary>스킬 적중 시 대상에게 적용 (디버프).</summary>
        TargetOnHit = 3,
    }

    [Serializable]
    public class TripodEffect
    {
        public ETripodTiming Timing;
        public GameplayEffectAsset Effect;
        [Tooltip("트라이포드 레벨 1 에서의 배율. 레벨당 PerLevelScale 만큼 증가")]
        public float BaseScale = 1.0f;
        public float PerLevelScale = 0.0f;

        [NonSerialized] public Framework.Ability.GameplayEffectDef RuntimeDef;

        public Framework.Ability.GameplayEffectDef GetDef()
        {
            if (RuntimeDef != null)
            {
                return RuntimeDef;
            }

            return Effect != null ? Effect.GetDef() : null;
        }

        public float ScaleAt(int InTripodLevel)
        {
            return BaseScale + PerLevelScale * Mathf.Max(0, InTripodLevel - 1);
        }
    }
}
