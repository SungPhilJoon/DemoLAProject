using System;
using DemoLA.Combat;
using DemoLA.Core;
using Framework;
using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// 바닥 구슬 (기믹 픽업). 플레이어 팀이 닿으면 OnPicked. 목표 지점으로 천천히 움직일 수 있다.
    /// </summary>
    public class RaidPickup : BaseBehaviour
    {
        public float PickRadius = 1.0f;
        public float MoveSpeed;
        public Vector3 MoveTarget;
        public bool HasMoveTarget;
        public int Payload;

        [Header("연출 (선택)")]
        [Tooltip("0 보다 크면 이 시간 뒤 사라진다")]
        public float Lifetime;
        [Tooltip("사라지기 몇 초 전부터 깜빡이는가")]
        public float BlinkBefore = 3.0f;
        [Tooltip("위아래로 둥실거리는 폭 (m)")]
        public float BobAmplitude;
        public float SpinSpeed;

        private bool launching;
        private Vector3 launchFrom;
        private Vector3 launchTo;
        private float launchHeight;
        private float launchDuration;
        private float launchElapsed;
        private float age;
        private float baseHeight = 0.8f;
        private Renderer cachedRenderer;

        public event Action<RaidPickup, CombatActor> OnPicked;
        public event Action<RaidPickup> OnReachedTarget;
        /// <summary>true 를 돌려주면 그 액터는 줍지 못한다 (예: 이미 버프 보유).</summary>
        public Func<CombatActor, bool> CanPick;

        private static readonly System.Collections.Generic.List<CombatActor> Buffer = new System.Collections.Generic.List<CombatActor>();

        public static RaidPickup Create(string InName, Vector3 InPosition, Color InColor, float InScale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = InName;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                ObjectUtil.SafeDestroy(collider);
            }

            go.transform.position = InPosition + Vector3.up * 0.8f;
            go.transform.localScale = Vector3.one * InScale;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = InColor;
            }

            return go.AddComponent<RaidPickup>();
        }

        /// <summary>InFrom 에서 InTo 로 포물선을 그리며 튀어나간다. 착지 전에는 주울 수 없다.</summary>
        public void Launch(Vector3 InFrom, Vector3 InTo, float InHeight, float InDuration)
        {
            launching = true;
            launchFrom = InFrom;
            launchTo = new Vector3(InTo.x, baseHeight, InTo.z);
            launchHeight = InHeight;
            launchDuration = Mathf.Max(0.05f, InDuration);
            launchElapsed = 0.0f;
            transform.position = InFrom;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            age += deltaTime;
            if (launching == true)
            {
                launchElapsed += deltaTime;
                float t = Mathf.Clamp01(launchElapsed / launchDuration);
                var flat = Vector3.Lerp(launchFrom, launchTo, t);
                // 포물선: 4h·t(1-t) + 시작/끝 높이 보간.
                flat.y = Mathf.Lerp(launchFrom.y, launchTo.y, t) + 4.0f * launchHeight * t * (1.0f - t);
                transform.position = flat;
                if (t < 1.0f)
                {
                    return;
                }

                launching = false;
                age = 0.0f;
            }

            if (Lifetime > 0.0f)
            {
                float left = Lifetime - age;
                if (left <= 0.0f)
                {
                    Consume();
                    return;
                }

                if (cachedRenderer == null)
                {
                    cachedRenderer = GetComponent<Renderer>();
                }

                if (cachedRenderer != null)
                {
                    // 남은 시간이 줄수록 빨리 깜빡인다.
                    cachedRenderer.enabled = left > BlinkBefore || Mathf.Repeat(age * Mathf.Lerp(10.0f, 4.0f, left / Mathf.Max(0.01f, BlinkBefore)), 1.0f) > 0.35f;
                }
            }

            if (BobAmplitude > 0.0f && HasMoveTarget == false)
            {
                var p = transform.position;
                p.y = baseHeight + Mathf.Sin(age * 3.0f) * BobAmplitude;
                transform.position = p;
            }

            if (SpinSpeed != 0.0f)
            {
                transform.Rotate(0.0f, SpinSpeed * deltaTime, 0.0f, Space.World);
            }
            if (HasMoveTarget == true && MoveSpeed > 0.0f)
            {
                var flatTarget = new Vector3(MoveTarget.x, transform.position.y, MoveTarget.z);
                transform.position = Vector3.MoveTowards(transform.position, flatTarget, MoveSpeed * deltaTime);
                if ((transform.position - flatTarget).sqrMagnitude < 0.04f)
                {
                    HasMoveTarget = false;
                    OnReachedTarget?.Invoke(this);
                    return;
                }
            }

            CombatRegistry.FindTeam(ETeam.Player, Buffer);
            var position = HitGeometry.Flat(transform.position);
            for (int i = 0; i < Buffer.Count; ++i)
            {
                var actor = Buffer[i];
                if ((HitGeometry.Flat(actor.Position) - position).magnitude > PickRadius + actor.Radius)
                {
                    continue;
                }

                if (CanPick != null && CanPick(actor) == false)
                {
                    continue;
                }

                OnPicked?.Invoke(this, actor);
                return;
            }
        }

        public void Consume()
        {
            OnPicked = null;
            OnReachedTarget = null;
            ObjectUtil.SafeDestroy(gameObject);
        }
    }
}
