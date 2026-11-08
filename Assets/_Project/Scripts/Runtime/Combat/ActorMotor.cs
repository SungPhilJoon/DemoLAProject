using DemoLA.Attributes;
using DemoLA.Core;
using DemoLA.World;
using Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DemoLA.Combat
{
    /// <summary>
    /// 이동 담당. NavMeshAgent 가 NavMesh 위에 있으면 경로 이동, 아니면 직접 이동으로 대체한다
    /// (NavMesh 를 굽기 전 테스트 씬이나 EditMode 테스트에서도 동작하도록).
    /// 이동 속도 = StatFormula.BaseMoveSpeedMeters × MoveSpeed%.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ActorMotor : BaseBehaviour
    {
        [SerializeField] private float turnSpeed = 1080.0f;

        private NavMeshAgent agent;
        private CombatActor owner;
        private Vector3 directDestination;
        private bool hasDirectDestination;
        private Vector3 dashVelocity;
        private float dashRemaining;

        private static readonly List<CombatActor> SeparationBuffer = new List<CombatActor>();

        // 도약 / 넉백 (몸이 포물선으로 뜨는 연출은 "Body" · "Facing" 자식만 올린다 — 발밑 표시는 그대로)
        private bool leaping;
        private bool knockedBack;
        private float knockbackEndTime = -100.0f;
        private bool arcActive;
        private float arcHeight;
        private float arcDuration;
        private float arcElapsed;
        private float visualOffset;

        /// <summary>넉백이 실제로 시작될 때 (액터, 날아가는 거리). 테스트 · 연출이 듣는다.</summary>
        public static event System.Action<CombatActor, float> Knockbacked;

        /// <summary>몬스터 공격에 날아가는 중 (NavMesh 를 벗어나 맵 밖으로도 날아갈 수 있다).</summary>
        public bool IsKnockedBack
        {
            get { return knockedBack; }
        }

        /// <summary>마지막 넉백이 끝난 시각. 낙사 판정은 넉백 중 / 직후에만 한다 (ArenaFloor).</summary>
        public float LastKnockbackEndTime
        {
            get { return knockbackEndTime; }
        }

        /// <summary>돌진 중 구조물(기둥)에 부딪혀 멈췄을 때. 기둥은 이미 부서진 상태로 넘어온다.</summary>
        public event System.Action<ArenaStructure> OnDashCrashed;

        public NavMeshAgent Agent
        {
            get { return agent; }
        }

        public bool IsDashing
        {
            get { return dashRemaining > 0.0f; }
        }

        public bool UsesNavMesh
        {
            get { return agent != null && agent.enabled == true && agent.isOnNavMesh == true; }
        }

        public bool IsMoving
        {
            get
            {
                if (IsDashing == true)
                {
                    return true;
                }

                if (UsesNavMesh == true)
                {
                    return agent.hasPath == true && agent.remainingDistance > agent.stoppingDistance + 0.05f;
                }

                return hasDirectDestination;
            }
        }

        protected override void OnUnityAwake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.angularSpeed = turnSpeed;
            agent.acceleration = 60.0f;
            agent.autoBraking = true;
        }

        public void Bind(CombatActor InOwner)
        {
            owner = InOwner;
        }

        public bool CanMove
        {
            get
            {
                if (owner == null || owner.IsDead == true || owner.IsIncapacitated == true)
                {
                    return false;
                }

                return owner.HasTag((int)ETag.State_Casting) == false;
            }
        }

        public void MoveTo(Vector3 InDestination)
        {
            if (CanMove == false)
            {
                return;
            }

            if (UsesNavMesh == true)
            {
                agent.isStopped = false;
                agent.SetDestination(InDestination);
                return;
            }

            directDestination = InDestination;
            hasDirectDestination = true;
        }

        public void Stop()
        {
            hasDirectDestination = false;
            if (UsesNavMesh == true)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        /// <summary>InDirection 으로 InDistance 만큼 InDuration 동안 이동 (회피기/돌진 스킬). 상태 제약을 무시한다.</summary>
        public void Dash(Vector3 InDirection, float InDistance, float InDuration)
        {
            var direction = HitGeometry.Flat(InDirection);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = transform.forward;
            }

            Stop();
            direction.Normalize();
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            dashRemaining = Mathf.Max(0.01f, InDuration);
            dashVelocity = direction * (InDistance / dashRemaining);
        }

        /// <summary>진행 중인 돌진을 그 자리에서 끝낸다 (패턴 취소 등).</summary>
        public void CancelDash()
        {
            dashRemaining = 0.0f;
            dashVelocity = Vector3.zero;
        }

        /// <summary>이번 프레임에 InDelta 만큼 옮긴다 (보스 추적 휠윈드 등 패턴 이동).</summary>
        public void Nudge(Vector3 InDelta)
        {
            moveBy(HitGeometry.Flat(InDelta));
        }

        /// <summary>InTarget 지점으로 InDuration 동안 InHeight 높이 포물선으로 뛰어오른다 (보스 도약). 구조물 충돌은 무시.</summary>
        public void Leap(Vector3 InTarget, float InDuration, float InHeight)
        {
            var delta = HitGeometry.Flat(InTarget - transform.position);
            Dash(delta.sqrMagnitude > 0.0001f ? delta : transform.forward, delta.magnitude, InDuration);
            leaping = true;
            startArc(InDuration, InHeight);
        }

        /// <summary>
        /// 몬스터 공격에 맞아 InDirection 으로 날아간다. NavMesh 를 잠시 끄고 직접 움직이므로 맵 가장자리 밖으로도 날아갈 수 있다.
        /// 서 있는 벽 · 기둥에는 부딪혀 멈춘다. 착지 지점에 바닥이 없으면 ArenaFloor 가 낙사 처리한다.
        /// </summary>
        public void Knockback(Vector3 InDirection, float InDistance, float InDuration, float InHeight)
        {
            if (owner == null || owner.IsDead == true || InDistance <= 0.0f)
            {
                return;
            }

            // 넉백 면역 (경직 면역) · 피격 이상 면역 · 무적이면 밀려나지 않는다.
            if (owner.HasTag((int)ETag.State_PushImmune) == true || owner.HasTag((int)ETag.State_SuperArmor) == true || owner.HasTag((int)ETag.State_Invulnerable) == true)
            {
                return;
            }

            var direction = HitGeometry.Flat(InDirection);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = -transform.forward;
            }

            direction.Normalize();
            float distance = clipAgainstStructures(transform.position, direction, InDistance);
            hasDirectDestination = false;
            if (agent != null && agent.enabled == true)
            {
                if (agent.isOnNavMesh == true)
                {
                    agent.ResetPath();
                }

                agent.enabled = false;
            }

            knockedBack = true;
            leaping = false;
            Knockbacked?.Invoke(owner, distance);
            dashRemaining = Mathf.Max(0.05f, InDuration);
            dashVelocity = direction * (distance / dashRemaining);
            startArc(dashRemaining, InHeight);
        }

        /// <summary>날아가는 경로가 서 있는 구조물에 막히면 그 앞까지로 줄인다.</summary>
        private float clipAgainstStructures(Vector3 InFrom, Vector3 InDirection, float InDistance)
        {
            var structures = ArenaStructure.Structures;
            float radius = owner != null ? owner.Radius : 0.5f;
            const float step = 0.25f;
            for (float travelled = step; travelled <= InDistance + 0.001f; travelled += step)
            {
                var to = InFrom + InDirection * Mathf.Min(travelled, InDistance);
                for (int i = 0; i < structures.Count; ++i)
                {
                    if (structures[i].Intersects(to, to, radius) == true)
                    {
                        return Mathf.Max(0.0f, travelled - step);
                    }
                }
            }

            return InDistance;
        }

        private void startArc(float InDuration, float InHeight)
        {
            arcActive = InHeight > 0.0f;
            arcHeight = InHeight;
            arcDuration = Mathf.Max(0.05f, InDuration);
            arcElapsed = 0.0f;
        }

        private void tickArc(float InDeltaTime)
        {
            if (arcActive == false && visualOffset == 0.0f)
            {
                return;
            }

            float y = 0.0f;
            if (arcActive == true)
            {
                arcElapsed += InDeltaTime;
                float t = Mathf.Clamp01(arcElapsed / arcDuration);
                y = 4.0f * arcHeight * t * (1.0f - t);
                if (t >= 1.0f)
                {
                    arcActive = false;
                    y = 0.0f;
                }
            }

            float delta = y - visualOffset;
            visualOffset = y;
            for (int i = 0; i < transform.childCount; ++i)
            {
                var child = transform.GetChild(i);
                if (child.name == "Body" || child.name == "Facing")
                {
                    child.localPosition += Vector3.up * delta;
                }
            }
        }

        private void endKnockback()
        {
            knockedBack = false;
            knockbackEndTime = Time.time;
            // 바닥 위에 떨어졌으면 NavMesh 로 돌아온다. 바닥이 없으면 그대로 두고 ArenaFloor 가 낙사시킨다.
            bool solid = ArenaFloor.Current == null || ArenaFloor.Current.IsSolid(transform.position) == true;
            if (solid == true && agent != null && owner.IsDead == false)
            {
                agent.enabled = true;
                agent.Warp(transform.position);
            }
        }

        public void Teleport(Vector3 InPosition)
        {
            Stop();
            if (agent != null && agent.enabled == false && owner != null && owner.IsDead == false)
            {
                agent.enabled = true;
            }

            knockedBack = false;
            if (UsesNavMesh == true)
            {
                agent.Warp(InPosition);
            }
            else
            {
                transform.position = InPosition;
            }
        }

        private void Update()
        {
            if (owner == null || owner.IsInitialized == false)
            {
                return;
            }

            float speed = StatFormula.Settings.BaseMoveSpeedMeters * owner.Stats.Get(EAttr.MoveSpeed) * 0.01f;
            if (agent != null)
            {
                agent.speed = speed;
            }

            float deltaTime = Time.deltaTime;
            tickArc(deltaTime);
            if (knockedBack == true)
            {
                // 넉백: NavMesh 없이 직접 이동 (맵 밖으로도).
                float step = Mathf.Min(deltaTime, dashRemaining);
                dashRemaining -= step;
                transform.position += dashVelocity * step;
                if (dashRemaining <= 0.0f)
                {
                    endKnockback();
                }

                return;
            }

            if (dashRemaining > 0.0f)
            {
                float step = Mathf.Min(deltaTime, dashRemaining);
                dashRemaining -= step;
                var dashDelta = dashVelocity * step;
                if (dashRemaining <= 0.0f)
                {
                    leaping = false;
                }

                // 도약은 구조물 위로 뛰어넘는다.
                if (owner.Team == ETeam.Enemy && leaping == false && checkStructureCollision(dashDelta) == true)
                {
                    return;
                }

                // 플레이어 회피 · 돌진: 몬스터 몸에 닿으면 미끄러져 비껴가지 않고 닿은 지점에서 멈춘다.
                if (owner.Team == ETeam.Player && clipDashAgainstEnemies(ref dashDelta) == true)
                {
                    moveBy(dashDelta);
                    dashRemaining = 0.0f;
                    dashVelocity = Vector3.zero;
                    return;
                }

                moveBy(dashDelta);
                separateFromEnemies();
                return;
            }

            if (UsesNavMesh == true)
            {
                if (CanMove == false && agent.hasPath == true)
                {
                    Stop();
                }

                separateFromEnemies();
                return;
            }

            if (hasDirectDestination == false || CanMove == false)
            {
                return;
            }

            var delta = HitGeometry.Flat(directDestination - transform.position);
            float distance = delta.magnitude;
            if (distance < 0.1f)
            {
                hasDirectDestination = false;
                return;
            }

            var direction = delta / distance;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction, Vector3.up), turnSpeed * deltaTime);
            moveBy(direction * Mathf.Min(distance, speed * deltaTime));
            separateFromEnemies();
        }

        /// <summary>
        /// 적(보스/몬스터) 돌진이 이번 프레임 이동 구간에서 구조물(기둥)에 닿으면 기둥을 부수고 돌진을 멈춘다.
        /// 기둥 바로 앞(몸 반지름만큼 떨어진 지점)까지는 이동한다.
        /// </summary>
        private bool checkStructureCollision(Vector3 InDelta)
        {
            var from = transform.position;
            var to = from + InDelta;
            var structures = ArenaStructure.Structures;
            for (int i = 0; i < structures.Count; ++i)
            {
                var structure = structures[i];
                if (structure.Intersects(from, to, owner.Radius) == false)
                {
                    continue;
                }

                if (structure.Kind == EStructureKind.Wall)
                {
                    // 이미 벽에 붙어 있는 상태에서 떨어지는 방향이면 막지 않는다.
                    if (structure.Intersects(from, from, owner.Radius) == true)
                    {
                        continue;
                    }

                    // 벽: 부서지지 않고 그 자리에서 멈춘다 (그로기 없음).
                    dashRemaining = 0.0f;
                    dashVelocity = Vector3.zero;
                    OnDashCrashed?.Invoke(structure);
                    return true;
                }

                // 기둥에 몸이 닿는 지점까지만 전진한 뒤 멈춘다.
                var p = HitGeometry.Flat(structure.transform.position);
                var a = HitGeometry.Flat(from);
                var dir = HitGeometry.Flat(InDelta).normalized;
                float along = Vector3.Dot(p - a, dir);
                float side = (a + dir * along - p).magnitude;
                float reach = structure.Radius + owner.Radius;
                float stopAt = along - Mathf.Sqrt(Mathf.Max(0.0f, reach * reach - side * side));
                if (stopAt > 0.0f)
                {
                    moveBy(dir * Mathf.Min(stopAt, InDelta.magnitude));
                }

                dashRemaining = 0.0f;
                dashVelocity = Vector3.zero;
                structure.Break(owner);
                OnDashCrashed?.Invoke(structure);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 이동 InOutDelta 가 적 몸(원)에 닿으면 닿기 직전까지로 줄이고 true. 이미 겹쳐 있으면 적 쪽으로 가는 성분만 막는다.
        /// </summary>
        private bool clipDashAgainstEnemies(ref Vector3 InOutDelta)
        {
            var delta = HitGeometry.Flat(InOutDelta);
            float length = delta.magnitude;
            if (length < 0.0001f)
            {
                return false;
            }

            CombatRegistry.FindTeam(ETeam.Enemy, SeparationBuffer);
            var a = HitGeometry.Flat(transform.position);
            float best = 1.0f;
            bool hit = false;
            for (int i = 0; i < SeparationBuffer.Count; ++i)
            {
                var enemy = SeparationBuffer[i];
                if (enemy == null || enemy.IsDead == true)
                {
                    continue;
                }

                var p = HitGeometry.Flat(enemy.Position);
                float reach = owner.Radius + enemy.Radius;
                var toEnemy = p - a;
                if (toEnemy.sqrMagnitude <= reach * reach)
                {
                    // 이미 붙어 있다: 적 쪽으로 파고드는 방향이면 멈춘다.
                    if (Vector3.Dot(delta, toEnemy) > 0.0f)
                    {
                        best = 0.0f;
                        hit = true;
                    }

                    continue;
                }

                // |a + delta·t - p| = reach 의 가장 작은 t (0~1).
                float qa = Vector3.Dot(delta, delta);
                float qb = -2.0f * Vector3.Dot(delta, toEnemy);
                float qc = toEnemy.sqrMagnitude - reach * reach;
                float disc = qb * qb - 4.0f * qa * qc;
                if (disc < 0.0f)
                {
                    continue;
                }

                float t = (-qb - Mathf.Sqrt(disc)) / (2.0f * qa);
                if (t >= 0.0f && t < best)
                {
                    best = t;
                    hit = true;
                }
            }

            if (hit == false)
            {
                return false;
            }

            // 접촉면에서 1cm 앞에 선다 (떨림 방지).
            float travel = Mathf.Max(0.0f, best * length - 0.01f);
            InOutDelta = delta / length * travel;
            return true;
        }

        /// <summary>
        /// 플레이어는 적 몸통을 통과/밀지 못한다. 겹치면 플레이어 쪽만 바깥으로 밀어낸다 (적은 움직이지 않음).
        /// </summary>
        private void separateFromEnemies()
        {
            if (owner.Team != ETeam.Player || owner.IsDead == true || knockedBack == true)
            {
                return;
            }

            CombatRegistry.FindTeam(ETeam.Enemy, SeparationBuffer);
            var position = HitGeometry.Flat(transform.position);
            for (int i = 0; i < SeparationBuffer.Count; ++i)
            {
                var enemy = SeparationBuffer[i];
                if (enemy == null || enemy.IsDead == true)
                {
                    continue;
                }

                var offset = position - HitGeometry.Flat(enemy.Position);
                float minDistance = owner.Radius + enemy.Radius;
                float distance = offset.magnitude;
                if (distance >= minDistance)
                {
                    continue;
                }

                var push = distance < 0.0001f ? -transform.forward : offset / distance;
                var correction = HitGeometry.Flat(push) * (minDistance - distance);
                moveBy(correction);
                position += correction;
            }
        }

        private void moveBy(Vector3 InDelta)
        {
            if (UsesNavMesh == true)
            {
                agent.Move(InDelta);
            }
            else
            {
                var next = transform.position + InDelta;
                // 맵 밖(무너진 지형)으로는 직접 이동하지 않는다. 낙사 판정은 ArenaFloor 가 따로 한다.
                if (ArenaFloor.Current == null || ArenaFloor.Current.IsSolid(next) == true)
                {
                    transform.position = next;
                }
            }
        }
    }
}
