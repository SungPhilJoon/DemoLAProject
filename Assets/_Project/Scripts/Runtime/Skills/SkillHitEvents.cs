using DemoLA.Combat;
using Framework;
using UnityEngine;

namespace DemoLA.Skills
{
    /// <summary>스킬 타격 1회 (적중 여부와 무관). 판정 범위 시각화와 AI 학습/디버그에 쓴다.</summary>
    public struct SkillHitEvent : IGameEvent
    {
        public SkillAbility Ability;
        public HitArea Area;
        public Vector3 Origin;
        public Vector3 Forward;
        public int TargetCount;
    }

    public static class SkillHitEvents
    {
        public static void RaiseHit(SkillAbility InAbility, HitArea InArea, Vector3 InOrigin, Vector3 InForward, int InTargetCount)
        {
            var evt = new SkillHitEvent();
            evt.Ability = InAbility;
            evt.Area = InArea;
            evt.Origin = InOrigin;
            evt.Forward = InForward;
            evt.TargetCount = InTargetCount;
            EventBus<SkillHitEvent>.Publish(evt);
        }
    }
}
