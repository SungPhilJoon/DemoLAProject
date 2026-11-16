using System;
using System.Collections.Generic;
using DemoLA.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace DemoLA.World
{
    /// <summary>
    /// 엄폐 기둥 (Pattern.G2.PillarCoverDrop 기둥 엄폐 낙하): 경기장 중심 기준 12 · 3 · 6 · 9시 방향에 솟아나는 임시 기둥 4개.
    ///  - 충격파(PatternStep.BlockedByCover)는 원점 → 대상 선분이 기둥을 지나면 막힌다 = 기둥 "뒤" 에 있으면 안전.
    ///  - 부채꼴(PatternStep.ExplodesCover)에 맞은 기둥은 크게 폭발한다 (주변 즉사).
    ///  - 패턴이 끝나면 땅속으로 들어가 사라진다.
    /// ArenaStructure(Kind = Cover)이므로 돌진 충돌 · 벽 파괴 공격에도 반응한다. 네트워크: 서버가 Spawned / Cleared 를 클라이언트에 복제.
    /// </summary>
    public static class CoverPillars
    {
        public const float PillarRadius = 1.4f;
        public const float PillarHeight = 5.0f;

        private static readonly List<ArenaStructure> Active = new List<ArenaStructure>();
        private static Material material;

        /// <summary>(기둥 위치들) 솟아날 때.</summary>
        public static event Action<Vector3[]> Spawned;
        public static event Action Cleared;

        /// <summary>충격파가 막 터지려는 원점 (AI 가 기둥 뒤로 숨는다). 없으면 null.</summary>
        public static Vector3? PendingBlastOrigin { get; set; }

        public static IReadOnlyList<ArenaStructure> Pillars
        {
            get { return Active; }
        }

        /// <summary>InCenter 기준 12시(+Z) · 3시(+X) · 6시 · 9시 방향 InDistance 에 기둥 4개.</summary>
        public static Vector3[] Positions(Vector3 InCenter, float InDistance)
        {
            var center = HitGeometry.Flat(InCenter);
            return new[]
            {
                center + Vector3.forward * InDistance,
                center + Vector3.right * InDistance,
                center + Vector3.back * InDistance,
                center + Vector3.left * InDistance,
            };
        }

        public static void Spawn(Vector3[] InPositions)
        {
            Clear();
            for (int i = 0; i < InPositions.Length; ++i)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = $"CoverPillar_{i}";
                DemoLA.Core.ObjectUtil.SafeDestroy(go.GetComponent<Collider>());
                go.transform.localScale = new Vector3(PillarRadius * 2.0f, PillarHeight * 0.5f, PillarRadius * 2.0f);
                go.transform.position = new Vector3(InPositions[i].x, -PillarHeight * 0.5f, InPositions[i].z);
                var renderer = go.GetComponent<Renderer>();
                if (material == null)
                {
                    material = new Material(renderer.sharedMaterial) { name = "M_CoverPillar", color = new Color(0.78f, 0.66f, 0.42f) };
                }

                renderer.sharedMaterial = material;
                var structure = go.AddComponent<ArenaStructure>();
                structure.Kind = EStructureKind.Cover;
                structure.Radius = PillarRadius;
                structure.LifeOrbCount = 0;
                var obstacle = go.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.carving = true;
                go.AddComponent<CoverPillarMotion>().Rise(PillarHeight * 0.5f);
                Active.Add(structure);
            }

            DemoLA.Player.QuarterViewCamera.Shake(0.35f, 0.8f);
            Spawned?.Invoke(InPositions);
        }

        /// <summary>남은 기둥을 땅속으로 내리고 없앤다.</summary>
        public static void Clear()
        {
            PendingBlastOrigin = null;
            bool any = Active.Count > 0;
            for (int i = 0; i < Active.Count; ++i)
            {
                var pillar = Active[i];
                if (pillar == null)
                {
                    continue;
                }

                var motion = pillar.GetComponent<CoverPillarMotion>();
                if (pillar.IsDestroyed == false && motion != null && pillar.gameObject.activeSelf == true)
                {
                    motion.SinkAndDestroy();
                }
                else
                {
                    DemoLA.Core.ObjectUtil.SafeDestroy(pillar.gameObject);
                }
            }

            Active.Clear();
            if (any == true)
            {
                Cleared?.Invoke();
            }
        }

        /// <summary>InOrigin(충격파 중심)에서 InTarget 까지 선분이 서 있는 엄폐 기둥을 지나는가 = 기둥 뒤.</summary>
        public static bool IsCovered(Vector3 InOrigin, Vector3 InTarget)
        {
            var a = HitGeometry.Flat(InOrigin);
            var b = HitGeometry.Flat(InTarget);
            var ab = b - a;
            float length = ab.magnitude;
            if (length < 0.01f)
            {
                return false;
            }

            var dir = ab / length;
            for (int i = 0; i < Active.Count; ++i)
            {
                var pillar = Active[i];
                if (pillar == null || pillar.IsDestroyed == true)
                {
                    continue;
                }

                var p = HitGeometry.Flat(pillar.transform.position);
                float along = Vector3.Dot(p - a, dir);
                if (along <= 0.0f || along >= length)
                {
                    continue;
                }

                float side = (a + dir * along - p).magnitude;
                if (side <= pillar.Radius)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>InFrom 에서 가장 가까운 기둥 뒤(충격파 반대편) 지점. 기둥이 없으면 null.</summary>
        public static Vector3? NearestShelter(Vector3 InOrigin, Vector3 InFrom, float InRadius)
        {
            Vector3? best = null;
            float bestDistance = float.MaxValue;
            var origin = HitGeometry.Flat(InOrigin);
            for (int i = 0; i < Active.Count; ++i)
            {
                var pillar = Active[i];
                if (pillar == null || pillar.IsDestroyed == true)
                {
                    continue;
                }

                var p = HitGeometry.Flat(pillar.transform.position);
                var away = (p - origin).normalized;
                var spot = p + away * (pillar.Radius + InRadius + 0.8f);
                float distance = (spot - HitGeometry.Flat(InFrom)).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = spot;
                }
            }

            return best;
        }
    }

    /// <summary>엄폐 기둥이 솟아나고 / 가라앉는 움직임.</summary>
    public class CoverPillarMotion : MonoBehaviour
    {
        private const float Duration = 0.8f;

        private float fromY;
        private float toY;
        private float elapsed = Duration;
        private bool destroyAtEnd;

        public void Rise(float InTargetY)
        {
            fromY = transform.position.y;
            toY = InTargetY;
            elapsed = 0.0f;
        }

        public void SinkAndDestroy()
        {
            fromY = transform.position.y;
            toY = -CoverPillars.PillarHeight * 0.5f;
            elapsed = 0.0f;
            destroyAtEnd = true;
        }

        private void Update()
        {
            if (elapsed >= Duration)
            {
                return;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            var p = transform.position;
            p.y = Mathf.Lerp(fromY, toY, t * t * (3.0f - 2.0f * t));
            transform.position = p;
            if (t >= 1.0f && destroyAtEnd == true)
            {
                Destroy(gameObject);
            }
        }
    }
}
