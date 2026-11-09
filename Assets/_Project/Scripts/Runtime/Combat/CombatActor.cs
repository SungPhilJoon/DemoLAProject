using System;
using DemoLA.Attributes;
using DemoLA.Core;
using DemoLA.Effects;
using Framework;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Combat
{
    /// <summary>
    /// 플레이어 · 아군 AI · 보스의 공통 부모. ASC(속성/태그/이펙트/어빌리티)가 상태의 단일 원본이다.
    /// </summary>
    public abstract class CombatActor : ActorBehaviour
    {
        private static long NextActorId = 1;

        [SerializeField] private ETeam team = ETeam.Player;
        [SerializeField] private float radius = 0.5f;

        public CombatAttributeSet Stats { get; private set; }

        /// <summary>조건부 피해 모디파이어 (각인 · 카드 · 버프). SkillDamageExecution 이 판정한다.</summary>
        public readonly System.Collections.Generic.List<DamageModifierEntry> DamageModifiers = new System.Collections.Generic.List<DamageModifierEntry>();

        /// <summary>None 이 아니면 모든 공격 속성을 이 속성으로 바꾼다 (카드 속성 변환).</summary>
        public EElement AttackElement { get; set; }
        public ActorMotor Motor { get; private set; }

        public event Action<CombatActor> OnActorDied;

        public ETeam Team
        {
            get { return team; }
            set { team = value; }
        }

        public float Radius
        {
            get { return radius; }
            set { radius = value; }
        }

        public bool IsInitialized
        {
            get { return Stats != null; }
        }

        public bool IsDead
        {
            get { return Stats == null || Stats.IsDead == true || HasTag((int)ETag.State_Dead) == true; }
        }

        public bool IsTargetable
        {
            get { return Stats != null && HasTag((int)ETag.Boss_Untargetable) == false; }
        }

        public bool IsIncapacitated
        {
            get
            {
                var tags = GameTags.IncapacitatedTags;
                for (int i = 0; i < tags.Length; ++i)
                {
                    if (HasTag((int)tags[i]) == true)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public Vector3 Position
        {
            get { return transform.position; }
        }

        public Vector3 Forward
        {
            get { return transform.forward; }
        }

        /// <summary>파생 클래스 초기화에서 가장 먼저 부른다.</summary>
        protected void InitializeCombat(CombatAttributeSet InStats)
        {
            GameEffects.Initialize();
            Stats = InStats;
            InitializeActor(NextActorId++, InStats);
            Motor = GetComponent<ActorMotor>();
            if (Motor != null)
            {
                Motor.Bind(this);
            }

            Asc.CooldownScaleProvider = () => Stats.CooldownScale;
            Stats.OnDied += handleDied;
            AddTag(team == ETeam.Enemy ? (int)ETag.Team_Enemy : (int)ETag.Team_Player);
            CombatRegistry.Register(this);
        }

        protected override void OnUnityDestroy()
        {
            CombatRegistry.Unregister(this);
            if (Stats != null)
            {
                Stats.OnDied -= handleDied;
            }

            base.OnUnityDestroy();
        }

        /// <summary>태그 상태이상을 InDuration 초 동안 건다.</summary>
        public int ApplyStatus(ETag InTag, float InDuration, AbilitySystemComponent InSource)
        {
            if (InTag == ETag.None || InDuration <= 0.0f || IsDead == true)
            {
                return 0;
            }

            // 슈퍼아머는 이동 불가 계열 상태이상을 막는다 (피해는 받는다).
            if (HasTag((int)ETag.State_SuperArmor) == true && isHardCrowdControl(InTag) == true)
            {
                return 0;
            }

            var source = InSource != null ? InSource : Asc;
            var spec = source.MakeOutgoingSpec(GameEffects.Status(InTag), 0);
            spec.DurationOverride = InDuration;
            return Asc.ApplyGameplayEffectSpecToSelf(spec);
        }

        public void ApplyEffect(GameplayEffectDef InDef, AbilitySystemComponent InSource, float InDurationOverride = -1.0f)
        {
            if (InDef == null)
            {
                return;
            }

            var source = InSource != null ? InSource : Asc;
            var spec = source.MakeOutgoingSpec(InDef, 0);
            spec.DurationOverride = InDurationOverride;
            Asc.ApplyGameplayEffectSpecToSelf(spec);
        }

        public void FaceTowards(Vector3 InWorldPoint)
        {
            var direction = HitGeometry.Flat(InWorldPoint - transform.position);
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        public void Kill()
        {
            if (Stats == null || IsDead == true)
            {
                return;
            }

            Stats.Set(EAttr.Shield, 0.0f);
            Stats.Set(EAttr.Health, 0.0f);
        }

        public void Revive(float InHealthRatio)
        {
            if (Stats == null)
            {
                return;
            }

            RemoveTag((int)ETag.State_Dead);
            RemoveTag((int)ETag.State_Falling);
            if (Motor != null && Motor.Agent != null)
            {
                Motor.Agent.enabled = true;
            }

            Stats.Set(EAttr.Health, Stats.Get(EAttr.MaxHealth) * Mathf.Clamp01(InHealthRatio));
            gameObject.SetActive(true);
        }

        protected virtual void OnDeath()
        {
        }

        private void handleDied()
        {
            // 네트워크 클라이언트: 사망은 서버가 판정하고 State_Dead 태그로 복제한다.
            // (복제 전 초기 체력 0 · 일시적인 값 때문에 클라이언트가 혼자 죽는 것을 막는다.)
            if (HasTag((int)ETag.State_Dead) == true || NetAuthority.IsReplica == true)
            {
                return;
            }

            AddTag((int)ETag.State_Dead);
            Asc.CancelAllAbilities();
            if (Motor != null)
            {
                Motor.Stop();
            }

            OnDeath();
            OnActorDied?.Invoke(this);
            var evt = new ActorDiedEvent();
            evt.Actor = this;
            EventBus<ActorDiedEvent>.Publish(evt);
        }

        private static bool isHardCrowdControl(ETag InTag)
        {
            return InTag == ETag.State_Stunned || InTag == ETag.State_Airborne || InTag == ETag.State_Frozen
                || InTag == ETag.State_Feared || InTag == ETag.State_Grabbed;
        }
    }
}
