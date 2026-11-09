using System;
using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Effects;
using Framework;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.World
{
    public enum EStructureKind
    {
        /// <summary>생명 구슬 기둥 (원기둥). 보스 돌진에 부서지고 생명 구슬을 떨어뜨린다.</summary>
        LifePillar = 0,
        /// <summary>외곽 벽 (직육면체). 보스의 휠윈드 · 3연 베기 범위에 닿으면 부서진다. 구슬 없음.</summary>
        Wall = 1,
    }

    /// <summary>
    /// 맵 구조물. 생명 구슬 기둥: 보스 돌진이 닿으면 파괴되고 생명 구슬을 떨어뜨린다.
    /// 벽: 보스의 BreaksWalls 패턴 범위에 닿으면 무너진다. 돌진은 벽에 막혀 멈춘다(벽은 부서지지 않음).
    /// 한 번 부서진 구조물은 레이드를 다시 시작할 때까지 판정 · 구슬 · 연출 모두 없다.
    /// </summary>
    public class ArenaStructure : BaseBehaviour
    {
        private static readonly List<ArenaStructure> All = new List<ArenaStructure>();

        /// <summary>(구조물, 부순 액터) — 보스 돌진으로 부서졌을 때. 기믹(갑옷 그로기 등)이 구독한다.</summary>
        public static event Action<ArenaStructure, CombatActor> BrokenByActor;
        /// <summary>어떤 이유로든 부서졌을 때 (부순 액터가 없으면 null). 테스트 · 연출용.</summary>
        public static event Action<ArenaStructure, CombatActor> Broken;

        public EStructureKind Kind = EStructureKind.LifePillar;
        [Tooltip("기둥: 원 반지름. 벽: 두께의 절반")]
        public float Radius = 1.5f;
        [Tooltip("벽: 길이의 절반 (로컬 X 축 방향)")]
        public float HalfLength;
        [Tooltip("파괴되면 비활성")]
        public bool Destructible = true;

        [Header("생명 구슬 (보스 돌진으로 파괴 시)")]
        public int LifeOrbCount = 2;
        [Tooltip("획득 시 최대 생명력 대비 회복 %")]
        public float LifeOrbHealPercent = 30.0f;
        [Tooltip("기둥 중심에서 구슬이 떨어지는 거리 (m)")]
        public float LifeOrbScatter = 2.8f;
        [Tooltip("구슬이 튀어 오르는 높이 / 비행 시간")]
        public float LifeOrbLaunchHeight = 2.5f;
        public float LifeOrbLaunchTime = 0.55f;
        [Tooltip("바닥에 떨어진 뒤 사라지기까지 (초). 0 이면 계속 남는다")]
        public float LifeOrbLifetime = 15.0f;
        public float LifeOrbPickRadius = 0.8f;
        public Color LifeOrbColor = new Color(0.35f, 1.0f, 0.45f);

        [Header("붕괴 연출")]
        public bool PlayCollapseFx = true;
        public float CrashShakeAmplitude = 0.5f;
        public float CrashShakeTime = 0.45f;

        private readonly List<RaidPickup> lifeOrbs = new List<RaidPickup>();

        public bool IsDestroyed { get; private set; }

        public static IReadOnlyList<ArenaStructure> Structures
        {
            get { return All; }
        }

        protected override void OnUnityAwake()
        {
            All.Add(this);
        }

        protected override void OnUnityDestroy()
        {
            All.Remove(this);
            clearLifeOrbs();
        }

        /// <summary>기믹 연출용 파괴 (생명 구슬 없음).</summary>
        public void Break()
        {
            Break(null);
        }

        /// <summary>InBreaker 가 있으면(보스 돌진) 생명 구슬을 떨어뜨리고 BrokenByActor 를 알린다.</summary>
        public void Break(CombatActor InBreaker)
        {
            if (Destructible == false || IsDestroyed == true)
            {
                return;
            }

            IsDestroyed = true;
            var position = transform.position;
            var push = InBreaker != null ? InBreaker.Forward : Vector3.zero;
            if (PlayCollapseFx == true)
            {
                PillarCollapseFx.Play(gameObject, push);
            }

            gameObject.SetActive(false);
            Broken?.Invoke(this, InBreaker);

            if (InBreaker == null)
            {
                return;
            }

            DemoLA.Player.QuarterViewCamera.Shake(CrashShakeAmplitude, CrashShakeTime);
            // 생명 구슬은 서버만 만든다 (클라이언트는 NetworkRaidDirector 가 복제한 구슬을 본다).
            if (Kind == EStructureKind.LifePillar && NetAuthority.IsReplica == false)
            {
                spawnLifeOrbs(position, push);
            }

            BrokenByActor?.Invoke(this, InBreaker);
        }

        public void Restore()
        {
            IsDestroyed = false;
            clearLifeOrbs();
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 기둥 자리에서 구슬이 튀어나와 주변에 떨어진다. InPush(돌진 방향) 의 양옆으로 퍼진다 —
        /// 보스 몸 앞(진행 방향)이나 보스 등 뒤에 떨어져 줍기 어려워지는 것을 피한다.
        /// </summary>
        private void spawnLifeOrbs(Vector3 InPosition, Vector3 InPush)
        {
            var ground = new Vector3(InPosition.x, 0.0f, InPosition.z);
            var forward = new Vector3(InPush.x, 0.0f, InPush.z);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            for (int i = 0; i < LifeOrbCount; ++i)
            {
                // 보스는 기둥의 -forward 쪽에 서 있으므로 그쪽은 피한다.
                // 1개: 기둥 너머(forward), 2개: 좌/우, 3개 이상: forward 기준 ±110° 부채꼴에 고르게.
                float angle;
                if (LifeOrbCount == 1)
                {
                    angle = 0.0f;
                }
                else if (LifeOrbCount == 2)
                {
                    angle = i == 0 ? 90.0f : -90.0f;
                }
                else
                {
                    angle = Mathf.Lerp(-110.0f, 110.0f, (float)i / (LifeOrbCount - 1));
                }

                angle += UnityEngine.Random.Range(-15.0f, 15.0f);
                var direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;

                var landing = ground + direction.normalized * LifeOrbScatter * UnityEngine.Random.Range(0.85f, 1.15f);
                if (ArenaFloor.Current != null && ArenaFloor.Current.IsSolid(landing) == false)
                {
                    landing = ground + direction.normalized * 1.2f;
                }

                var orb = RaidPickup.Create("LifeOrb", landing, LifeOrbColor, 0.6f);
                orb.Launch(new Vector3(ground.x, 2.0f, ground.z), landing, LifeOrbLaunchHeight, LifeOrbLaunchTime);
                orb.Lifetime = LifeOrbLifetime;
                orb.BobAmplitude = 0.15f;
                orb.SpinSpeed = 90.0f;
                orb.PickRadius = LifeOrbPickRadius;
                // 체력이 가득 찬 캐릭터는 줍지 않는다 (필요한 사람이 먹도록).
                orb.CanPick = InActor => InActor.Stats.Get(EAttr.Health) < InActor.Stats.Get(EAttr.MaxHealth);
                orb.OnPicked += handleLifeOrbPicked;
                lifeOrbs.Add(orb);
            }
        }

        private void handleLifeOrbPicked(RaidPickup InOrb, CombatActor InActor)
        {
            lifeOrbs.Remove(InOrb);
            InOrb.Consume();

            var heal = InActor.Asc.MakeOutgoingSpec(GameEffects.Heal, 0);
            heal.SetByCaller((int)ESetByCaller.FlatDamage, InActor.Stats.Get(EAttr.MaxHealth) * LifeOrbHealPercent * 0.01f);
            InActor.Asc.ApplyGameplayEffectSpecToSelf(heal);
        }

        private void clearLifeOrbs()
        {
            for (int i = 0; i < lifeOrbs.Count; ++i)
            {
                if (lifeOrbs[i] != null)
                {
                    lifeOrbs[i].Consume();
                }
            }

            lifeOrbs.Clear();
        }

        /// <summary>보스 패턴 범위(장판)가 이 벽에 닿는가. 벽 중심선 위 여러 점을 두께만큼 반지름으로 검사한다.</summary>
        public bool Overlaps(in HitArea InArea, Vector3 InOrigin, Vector3 InForward)
        {
            if (IsDestroyed == true)
            {
                return false;
            }

            if (Kind != EStructureKind.Wall || HalfLength <= 0.0f)
            {
                return HitGeometry.Contains(InArea, InOrigin, InForward, transform.position, Radius);
            }

            var axis = HitGeometry.Flat(transform.right).normalized;
            int samples = Mathf.Max(2, Mathf.CeilToInt(HalfLength * 2.0f / Mathf.Max(0.5f, Radius * 2.0f))) + 1;
            for (int i = 0; i < samples; ++i)
            {
                float t = Mathf.Lerp(-HalfLength, HalfLength, (float)i / (samples - 1));
                if (HitGeometry.Contains(InArea, InOrigin, InForward, transform.position + axis * t, Radius) == true)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>InFrom → InTo 선분이 이 구조물에 닿는가 (돌진 충돌 · 엄폐 판정).</summary>
        public bool Intersects(Vector3 InFrom, Vector3 InTo, float InRadius)
        {
            if (IsDestroyed == true)
            {
                return false;
            }

            if (Kind == EStructureKind.Wall && HalfLength > 0.0f)
            {
                return intersectsWall(InFrom, InTo, InRadius);
            }

            var a = new Vector2(InFrom.x, InFrom.z);
            var b = new Vector2(InTo.x, InTo.z);
            var p = new Vector2(transform.position.x, transform.position.z);
            var ab = b - a;
            float t = ab.sqrMagnitude < 0.0001f ? 0.0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (a + ab * t - p).magnitude <= Radius + InRadius;
        }

        /// <summary>선분(이동 경로)을 벽 로컬 좌표로 옮겨, InRadius 만큼 넓힌 직사각형과 겹치는지 표본 검사.</summary>
        private bool intersectsWall(Vector3 InFrom, Vector3 InTo, float InRadius)
        {
            var center = HitGeometry.Flat(transform.position);
            var axis = HitGeometry.Flat(transform.right).normalized;
            var normal = new Vector3(-axis.z, 0.0f, axis.x);
            float halfLength = HalfLength + InRadius;
            float halfThickness = Radius + InRadius;
            var a = HitGeometry.Flat(InFrom) - center;
            var b = HitGeometry.Flat(InTo) - center;
            int samples = Mathf.Max(2, Mathf.CeilToInt((b - a).magnitude / 0.25f) + 1);
            for (int i = 0; i < samples; ++i)
            {
                var p = Vector3.Lerp(a, b, (float)i / (samples - 1));
                if (Mathf.Abs(Vector3.Dot(p, axis)) <= halfLength && Mathf.Abs(Vector3.Dot(p, normal)) <= halfThickness)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
