using DemoLA.Attributes;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Effects;
using DemoLA.Skills;
using Framework;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Character.Identity
{
    /// <summary>
    /// 직업 고유 시스템(아이덴티티) 공통 부모. 스킬 적중으로 IdentityGauge 가 차고(SkillAbility), 이 어빌리티가 소비한다.
    /// 종류 키 = 직업 × 10 + 변형. 변형 0 = 기본, 1/2 = 직업 각인 (EngravingRow.IdentityVariant).
    /// 직업 각인은 수치가 아니라 이 어빌리티 "클래스" 를 바꾼다 → 각인마다 GameplayAbility 하나.
    /// </summary>
    public abstract class IdentityAbility : GameplayAbility
    {
        public const int IdentityAbilityId = 9100;
        public const float GaugePerOrb = 100.0f;

        private int orbs;

        public static int Key(ECharacterClass InClass, int InVariant)
        {
            return (int)InClass * 10 + Mathf.Clamp(InVariant, 0, 9);
        }

        /// <summary>직업 · 변형으로 만든다. 변형이 없으면 기본(0).</summary>
        public static IdentityAbility Create(ECharacterClass InClass, int InVariant)
        {
            var ability = KindRegistry<IdentityAbility>.Create(Key(InClass, InVariant));
            return ability ?? KindRegistry<IdentityAbility>.Create(Key(InClass, 0));
        }

        protected IdentityAbility()
        {
            AbilityId = IdentityAbilityId;
            AbilityTags.Add((int)ETag.Ability_Identity);
            var blocked = GameTags.IncapacitatedTags;
            for (int i = 0; i < blocked.Length; ++i)
            {
                ActivationBlockedTags.Add((int)blocked[i]);
            }

            CooldownEffect = GameEffects.Cooldown;
            CooldownDuration = 1.0f;
        }

        public abstract string DisplayName { get; }

        /// <summary>툴팁 설명 (Z 키).</summary>
        public virtual string Description
        {
            get { return string.Empty; }
        }

        /// <summary>0 이면 게이지 직접 소비형, 1 이상이면 게이지 100 마다 구슬 1개 (최대 MaxOrbs).</summary>
        public virtual int MaxOrbs
        {
            get { return 0; }
        }

        /// <summary>게이지형 직업이 발동에 필요한 게이지.</summary>
        protected virtual float RequiredGauge
        {
            get { return GaugePerOrb; }
        }

        public int Orbs
        {
            get { return orbs; }
        }

        public float Gauge
        {
            get { return Owner != null ? Owner.Attributes.GetCurrent((int)EAttr.IdentityGauge) : 0.0f; }
        }

        public PartyCharacter Character
        {
            get { return Owner != null ? Owner.OwnerActor as PartyCharacter : null; }
        }

        public virtual string StatusText
        {
            get { return MaxOrbs > 0 ? $"{DisplayName} 구슬 {orbs}/{MaxOrbs}" : $"{DisplayName} {Gauge:0}%"; }
        }

        public bool HasResource
        {
            get { return MaxOrbs > 0 ? orbs > 0 : Gauge >= RequiredGauge; }
        }

        public override bool CanActivate(out EActivateFailReason OutReason)
        {
            if (base.CanActivate(out OutReason) == false)
            {
                return false;
            }

            if (HasResource == false)
            {
                OutReason = EActivateFailReason.Cost;
                return false;
            }

            return true;
        }

        /// <summary>구슬 InCount 개를 쓴다 (X 아이덴티티가 Z 와 같은 자원을 공유할 때).</summary>
        public bool TrySpendOrbs(int InCount)
        {
            if (orbs < InCount)
            {
                return false;
            }

            orbs -= InCount;
            return true;
        }

        /// <summary>남은 구슬을 모두 쓰고 그 개수를 돌려준다.</summary>
        public int SpendAllOrbs()
        {
            int count = orbs;
            orbs = 0;
            return count;
        }

        /// <summary>게이지 InAmount 를 쓴다 (게이지형 직업의 X).</summary>
        public bool TrySpendGauge(float InAmount)
        {
            if (Owner == null || Gauge < InAmount)
            {
                return false;
            }

            Owner.Attributes.ApplyToBase((int)EAttr.IdentityGauge, EModifierOp.Add, -InAmount);
            return true;
        }

        /// <summary>구슬형: 게이지가 100 이 될 때마다 구슬로 바꾼다. 어빌리티가 비활성이어도 ASC 가 매 틱 부른다.</summary>
        public void TickResource()
        {
            if (MaxOrbs <= 0 || Owner == null)
            {
                return;
            }

            while (orbs < MaxOrbs && Gauge >= GaugePerOrb)
            {
                orbs += 1;
                Owner.Attributes.ApplyToBase((int)EAttr.IdentityGauge, EModifierOp.Add, -GaugePerOrb);
            }
        }

        protected override void OnActivate(AbilityActivationInfo InInfo)
        {
            if (CommitAbility() == false || Character == null)
            {
                EndAbility(true);
                return;
            }

            if (MaxOrbs > 0)
            {
                orbs -= 1;
            }
            else
            {
                Owner.Attributes.ApplyToBase((int)EAttr.IdentityGauge, EModifierOp.Add, -RequiredGauge);
            }

            Perform(InInfo);
        }

        /// <summary>자원 소비 후 실제 동작. 즉발이면 마지막에 EndAbility(false) 를 부른다.</summary>
        protected abstract void Perform(AbilityActivationInfo InInfo);

        protected HitParams MakeHit(float InCoefficient, float InStagger, float InDestruction)
        {
            var hit = new HitParams();
            hit.SkillId = IdentityAbilityId;
            hit.Coefficient = InCoefficient;
            hit.Stagger = InStagger;
            hit.Destruction = InDestruction;
            hit.Flags = ESkillFlags.Identity;
            return hit;
        }

        protected void FaceAim(AbilityActivationInfo InInfo)
        {
            var actor = Character;
            if (actor != null && HitGeometry.Flat(InInfo.AimPoint - actor.Position).sqrMagnitude > 0.01f)
            {
                actor.FaceTowards(InInfo.AimPoint);
            }
        }

        /// <summary>시전자 주변 아군 전원에게 InAction.</summary>
        protected void ForEachAlly(float InRadius, System.Action<CombatActor> InAction)
        {
            var actor = Character;
            var allies = new System.Collections.Generic.List<CombatActor>();
            CombatRegistry.FindTeam(actor.Team, allies);
            for (int i = 0; i < allies.Count; ++i)
            {
                if ((allies[i].Position - actor.Position).sqrMagnitude <= InRadius * InRadius)
                {
                    InAction(allies[i]);
                }
            }
        }
    }
}
