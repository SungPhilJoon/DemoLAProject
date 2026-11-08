using DemoLA.Core;
using Framework;
using Framework.Ability;

namespace DemoLA.Combat
{
    /// <summary>피해 1건이 실제로 적용된 뒤 발행 (UI 데미지 숫자, 딜 미터, 테스트 집계).</summary>
    public struct DamageAppliedEvent : IGameEvent
    {
        public AbilitySystemComponent Source;
        public AbilitySystemComponent Target;
        /// <summary>보호막 흡수 전 피해.</summary>
        public float RawAmount;
        /// <summary>체력에서 실제로 빠진 양.</summary>
        public float HealthLost;
        public float ShieldAbsorbed;
        public bool IsCrit;
        public EPositional Positional;
        public int SkillId;
        public bool WasLethal;
        public bool DeathWardConsumed;
    }

    public struct ActorDiedEvent : IGameEvent
    {
        public CombatActor Actor;
    }

    public struct HealAppliedEvent : IGameEvent
    {
        public AbilitySystemComponent Source;
        public AbilitySystemComponent Target;
        public float Amount;
    }

    /// <summary>보스 기믹/페이즈 안내 문구 (HUD 가 표시).</summary>
    public struct RaidMessageEvent : IGameEvent
    {
        public string Message;
        public float Duration;
        public bool IsWarning;
    }

    /// <summary>파티 전멸 / 레이드 종료.</summary>
    public struct RaidResultEvent : IGameEvent
    {
        public bool Cleared;
        public string Reason;
    }
}
