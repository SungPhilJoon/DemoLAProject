using System;
using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Combat;
using DemoLA.Core;
using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// 패턴(스킬) 하나가 "누구를 노리는가" 를 정하는 규칙. 규칙마다 클래스 하나이며,
    /// BossPatternDefinition.TargetSelector 에 [SerializeReference] 로 하나를 고른다 (비어 있으면 현재 어그로 대상).
    /// 결정 순서는 PatternTargeting.Resolve. 사용법: Documentation/md파일/사용법/패턴 대상 선택 (어그로 규칙).md
    ///
    /// 새 규칙 추가: 이 클래스를 상속해 Select 를 구현하고 [Serializable] 을 붙이면
    /// StateTree 노드 · 인스펙터 · GAS Tool 드롭다운에 자동으로 나타난다.
    ///
    /// 도발(Taunt) 중에는 규칙을 무시하고 도발한 대상을 노린다 (PatternTargeting.Resolve).
    /// </summary>
    [Serializable]
    public abstract class PatternTargetSelector
    {
        /// <summary>
        /// InCandidates(살아 있고 노릴 수 있는 플레이어) 중 대상을 고른다. 고를 수 없으면 null → 현재 어그로 대상으로 대체된다.
        /// </summary>
        public abstract CombatActor Select(BossActor InBoss, IReadOnlyList<CombatActor> InCandidates);

        /// <summary>툴 · 인스펙터에 보여 줄 한 줄 설명.</summary>
        public abstract string Describe();

        protected static bool IsValid(CombatActor InActor)
        {
            return InActor != null && InActor.IsDead == false && InActor.IsTargetable == true;
        }

        /// <summary>InScore 가 가장 큰 후보 (동점이면 목록 앞쪽).</summary>
        protected static CombatActor Best(IReadOnlyList<CombatActor> InCandidates, Func<CombatActor, float> InScore)
        {
            CombatActor best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < InCandidates.Count; ++i)
            {
                var candidate = InCandidates[i];
                if (IsValid(candidate) == false)
                {
                    continue;
                }

                float score = InScore(candidate);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        protected static float FlatDistance(BossActor InBoss, CombatActor InActor)
        {
            var delta = InActor.Position - InBoss.Position;
            delta.y = 0.0f;
            return delta.magnitude;
        }
    }

    /// <summary>
    /// 패턴 시작 시 첫 대상(primaryTarget)을 정하는 순서:
    ///  1. 도발 중이면 도발한 대상
    ///  2. 호출자가 일부러 지정한 대상 — StateTree SelectTargetTask(다음 패턴), 시나리오 · 디버그 명령. 살아 있을 때만
    ///  3. 패턴의 TargetSelector 가 있으면 그 규칙 (null 을 돌려주면 다음 단계로)
    ///  4. 보스의 현재 어그로 대상 (SelectTargetTask 의 Aggro 모드로 바꿀 수 있다)
    /// </summary>
    public static class PatternTargeting
    {
        public static CombatActor Resolve(BossActor InBoss, BossPatternDefinition InPattern, CombatActor InRequested)
        {
            if (InBoss == null)
            {
                return InRequested;
            }

            var taunt = InBoss.TauntTarget;
            if (taunt != null)
            {
                return taunt;
            }

            if (InRequested != null && InRequested.IsDead == false)
            {
                return InRequested;
            }

            var selector = InPattern != null ? InPattern.TargetSelector : null;
            if (selector != null)
            {
                var chosen = selector.Select(InBoss, InBoss.AlivePlayers());
                if (chosen != null && chosen.IsDead == false)
                {
                    return chosen;
                }
            }

            return InBoss.AggroTarget;
        }

        /// <summary>툴 표시용: 비어 있으면 기본 동작 설명.</summary>
        public static string Describe(BossPatternDefinition InPattern)
        {
            return InPattern != null && InPattern.TargetSelector != null ? InPattern.TargetSelector.Describe() : "현재 어그로 대상 (기본)";
        }
    }

    /// <summary>보스의 현재 어그로 대상 (기본 동작과 같다).</summary>
    [Serializable]
    public class CurrentAggroSelector : PatternTargetSelector
    {
        public override CombatActor Select(BossActor InBoss, IReadOnlyList<CombatActor> InCandidates)
        {
            return InBoss.AggroTarget;
        }

        public override string Describe()
        {
            return "현재 어그로 대상";
        }
    }

    /// <summary>보스와 가장 가까운 플레이어.</summary>
    [Serializable]
    public class NearestSelector : PatternTargetSelector
    {
        public override CombatActor Select(BossActor InBoss, IReadOnlyList<CombatActor> InCandidates)
        {
            return Best(InCandidates, c => -FlatDistance(InBoss, c));
        }

        public override string Describe()
        {
            return "가장 가까운 플레이어";
        }
    }

    /// <summary>보스와 가장 먼 플레이어 (원거리 견제 · 돌진 패턴).</summary>
    [Serializable]
    public class FarthestSelector : PatternTargetSelector
    {
        public override CombatActor Select(BossActor InBoss, IReadOnlyList<CombatActor> InCandidates)
        {
            return Best(InCandidates, c => FlatDistance(InBoss, c));
        }

        public override string Describe()
        {
            return "가장 먼 플레이어";
        }
    }

    /// <summary>위협 수치(누적 피해, 시간에 따라 감쇠)가 Rank 번째로 높은 플레이어. 인원이 모자라면 가장 낮은 사람.</summary>
    [Serializable]
    public class HighestThreatSelector : PatternTargetSelector
    {
        [Tooltip("1 = 위협 1위, 2 = 2위 ...")]
        [Min(1)] public int Rank = 1;

        [NonSerialized] private readonly List<CombatActor> buffer = new List<CombatActor>();

        public override CombatActor Select(BossActor InBoss, IReadOnlyList<CombatActor> InCandidates)
        {
            var threat = InBoss.Threat;
            buffer.Clear();
            for (int i = 0; i < InCandidates.Count; ++i)
            {
                if (IsValid(InCandidates[i]) == true)
                {
                    buffer.Add(InCandidates[i]);
                }
            }

            if (buffer.Count == 0)
            {
                return null;
            }

            // 안정 정렬 (동점이면 목록 앞쪽이 위).
            for (int i = 1; i < buffer.Count; ++i)
            {
                var item = buffer[i];
                float score = threatOf(threat, item);
                int j = i - 1;
                while (j >= 0 && threatOf(threat, buffer[j]) < score)
                {
                    buffer[j + 1] = buffer[j];
                    --j;
                }

                buffer[j + 1] = item;
            }

            return buffer[Mathf.Clamp(Rank - 1, 0, buffer.Count - 1)];
        }

        private static float threatOf(IReadOnlyDictionary<CombatActor, float> InThreat, CombatActor InActor)
        {
            float value;
            return InThreat.TryGetValue(InActor, out value) == true ? value : 0.0f;
        }

        public override string Describe()
        {
            return $"위협 수치 {Mathf.Max(1, Rank)}위";
        }
    }

    /// <summary>남은 체력 비율이 가장 낮은 플레이어 (마무리 패턴).</summary>
    [Serializable]
    public class LowestHealthSelector : PatternTargetSelector
    {
        public override CombatActor Select(BossActor InBoss, IReadOnlyList<CombatActor> InCandidates)
        {
            return Best(InCandidates, c => -healthRatio(c));
        }

        private static float healthRatio(CombatActor InActor)
        {
            float max = InActor.Stats.Get(EAttr.MaxHealth);
            return max > 0.0f ? InActor.Stats.Get(EAttr.Health) / max : 1.0f;
        }

        public override string Describe()
        {
            return "체력 비율 최저";
        }
    }

    /// <summary>무작위 플레이어 1명.</summary>
    [Serializable]
    public class RandomSelector : PatternTargetSelector
    {
        [Tooltip("켜면 현재 어그로 대상은 후보에서 뺀다 (다른 사람이 1명도 없으면 어그로 대상)")]
        public bool ExcludeAggro;

        [NonSerialized] private readonly List<CombatActor> buffer = new List<CombatActor>();

        public override CombatActor Select(BossActor InBoss, IReadOnlyList<CombatActor> InCandidates)
        {
            buffer.Clear();
            var aggro = ExcludeAggro == true ? InBoss.AggroTarget : null;
            for (int i = 0; i < InCandidates.Count; ++i)
            {
                if (IsValid(InCandidates[i]) == true && InCandidates[i] != aggro)
                {
                    buffer.Add(InCandidates[i]);
                }
            }

            if (buffer.Count == 0)
            {
                return aggro;
            }

            return buffer[GameRandom.Range(0, buffer.Count)];
        }

        public override string Describe()
        {
            return ExcludeAggro == true ? "무작위 (어그로 대상 제외)" : "무작위 플레이어";
        }
    }

    /// <summary>보스 정면 기준 각도 안에서 가장 가까운 플레이어. 없으면 가장 가까운 플레이어.</summary>
    [Serializable]
    public class FrontConeSelector : PatternTargetSelector
    {
        [Range(10.0f, 360.0f)] public float Angle = 90.0f;

        public override CombatActor Select(BossActor InBoss, IReadOnlyList<CombatActor> InCandidates)
        {
            float half = Angle * 0.5f;
            var forward = InBoss.Forward;
            forward.y = 0.0f;
            var inCone = Best(InCandidates, c =>
            {
                var to = c.Position - InBoss.Position;
                to.y = 0.0f;
                if (to.sqrMagnitude < 0.0001f || Vector3.Angle(forward, to) <= half)
                {
                    return -to.magnitude;
                }

                return float.NegativeInfinity;
            });

            // 각도 밖 후보는 점수가 -∞ 라 Best 가 고르지 않는다 → null 이면 각도 안에 아무도 없다.
            return inCone != null ? inCone : Best(InCandidates, c => -FlatDistance(InBoss, c));
        }

        public override string Describe()
        {
            return $"정면 {Angle:0}° 안 가장 가까운 대상";
        }
    }
}
