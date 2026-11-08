using System.Collections.Generic;
using DemoLA.Core;
using UnityEngine;

namespace DemoLA.Combat
{
    /// <summary>
    /// 살아 있는 전투 액터 목록. 판정은 Physics 대신 이 목록을 선형 탐색한다 (최대 4인 + 보스 몇 마리라 충분).
    /// 액터 수가 늘면 공간 분할로 바꿔야 한다 (Documentation/최적화 고려 사항.md).
    /// </summary>
    public static class CombatRegistry
    {
        private static readonly List<CombatActor> Actors = new List<CombatActor>();

        public static IReadOnlyList<CombatActor> All
        {
            get { return Actors; }
        }

        public static void Register(CombatActor InActor)
        {
            if (Actors.Contains(InActor) == false)
            {
                Actors.Add(InActor);
            }
        }

        public static void Unregister(CombatActor InActor)
        {
            Actors.Remove(InActor);
        }

        public static void Clear()
        {
            Actors.Clear();
        }

        public static bool IsHostile(ETeam InA, ETeam InB)
        {
            return InA != ETeam.Neutral && InB != ETeam.Neutral && InA != InB;
        }

        /// <summary>InArea 안의 적대 액터를 OutTargets 에 채운다.</summary>
        public static void FindHostiles(CombatActor InSource, in HitArea InArea, Vector3 InOrigin, Vector3 InForward, List<CombatActor> OutTargets)
        {
            OutTargets.Clear();
            for (int i = 0; i < Actors.Count; ++i)
            {
                var actor = Actors[i];
                if (actor == null || actor == InSource || actor.IsDead == true || actor.IsTargetable == false)
                {
                    continue;
                }

                if (IsHostile(InSource.Team, actor.Team) == false)
                {
                    continue;
                }

                if (HitGeometry.Contains(InArea, InOrigin, InForward, actor.transform.position, actor.Radius) == true)
                {
                    OutTargets.Add(actor);
                }
            }
        }

        /// <summary>InTeam 소속의 살아 있는 액터.</summary>
        public static void FindTeam(ETeam InTeam, List<CombatActor> OutActors)
        {
            OutActors.Clear();
            for (int i = 0; i < Actors.Count; ++i)
            {
                var actor = Actors[i];
                if (actor != null && actor.Team == InTeam && actor.IsDead == false)
                {
                    OutActors.Add(actor);
                }
            }
        }

        public static CombatActor FindNearestHostile(CombatActor InSource, float InMaxDistance)
        {
            CombatActor best = null;
            float bestDistance = InMaxDistance * InMaxDistance;
            var origin = InSource.transform.position;
            for (int i = 0; i < Actors.Count; ++i)
            {
                var actor = Actors[i];
                if (actor == null || actor.IsDead == true || actor.IsTargetable == false || IsHostile(InSource.Team, actor.Team) == false)
                {
                    continue;
                }

                float distance = (actor.transform.position - origin).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = actor;
                }
            }

            return best;
        }

        public static int CountAlive(ETeam InTeam)
        {
            int count = 0;
            for (int i = 0; i < Actors.Count; ++i)
            {
                if (Actors[i] != null && Actors[i].Team == InTeam && Actors[i].IsDead == false)
                {
                    count += 1;
                }
            }

            return count;
        }
    }
}
