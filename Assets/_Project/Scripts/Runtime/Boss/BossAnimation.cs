using System;
using DemoLA.Character;
using DemoLA.Core;
using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// 보스 모델 애니메이션: 대기 · 이동은 모델 컨트롤러(Speed), 패턴 단계 · 사망 동작은 CharacterAnimatorDriver 로 위에 섞는다.
    ///  - 서버(또는 오프라인)가 재생하면 이벤트를 내고, NetworkBossProxy 가 클라이언트에 같은 재생을 보낸다.
    ///  - 클라이언트는 같은 함수를 InBroadcast = false 로 부른다.
    /// </summary>
    public static class BossAnimation
    {
        /// <summary>(보스, 패턴 태그, 단계 번호) — 단계 동작을 재생했다.</summary>
        public static event Action<BossActor, ETag, int> StepPlayed;
        /// <summary>(보스, 블렌드 아웃 초) — 패턴 동작을 끝내고 대기/이동으로 돌아간다.</summary>
        public static event Action<BossActor, float> Stopped;
        /// <summary>사망 동작을 재생했다.</summary>
        public static event Action<BossActor> DeathPlayed;

        private static CharacterAnimatorDriver driver(BossActor InBoss)
        {
            return InBoss != null ? InBoss.GetComponent<CharacterAnimatorDriver>() : null;
        }

        /// <summary>패턴 InPattern 의 InStepIndex 단계 클립을 재생한다. 클립이 없으면 아무 일도 하지 않는다 (앞 동작 유지).</summary>
        public static void PlayStep(BossActor InBoss, BossPatternDefinition InPattern, int InStepIndex, bool InBroadcast)
        {
            var anim = driver(InBoss);
            if (anim == null || InPattern == null || InStepIndex < 0 || InStepIndex >= InPattern.Steps.Count || InBoss.IsDead == true)
            {
                return;
            }

            var step = InPattern.Steps[InStepIndex];
            if (step == null || step.Animation == null)
            {
                return;
            }

            // 단계 클립이 끝나도 패턴이 끝날 때까지는 마지막 자세를 유지한다 (Stop 에서 블렌드 아웃).
            anim.PlayAction(step.Animation, step.AnimationSpeed, step.AnimationStartTime, InPattern.AnimationBlendIn, InPattern.AnimationBlendOut, true);
            if (InBroadcast == true)
            {
                StepPlayed?.Invoke(InBoss, InPattern.PatternTag, InStepIndex);
            }
        }

        public static void Stop(BossActor InBoss, float InBlendOut, bool InBroadcast)
        {
            var anim = driver(InBoss);
            if (anim == null || anim.IsPlayingAction == false || InBoss.IsDead == true)
            {
                return;
            }

            anim.StopAction(InBlendOut);
            if (InBroadcast == true)
            {
                Stopped?.Invoke(InBoss, InBlendOut);
            }
        }

        public static void PlayDeath(BossActor InBoss, bool InBroadcast)
        {
            var anim = driver(InBoss);
            var clip = InBoss != null && InBoss.Definition != null ? InBoss.Definition.DeathAnimation : null;
            if (anim == null || clip == null || anim.CurrentAction == clip)
            {
                return;
            }

            anim.PlayAction(clip, 1.0f, 0.0f, 0.15f, 0.0f, true);
            if (InBroadcast == true)
            {
                DeathPlayed?.Invoke(InBoss);
            }
        }
    }
}
