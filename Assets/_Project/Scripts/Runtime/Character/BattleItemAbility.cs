using DemoLA.Attributes;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Data;
using DemoLA.Effects;
using DemoLA.Skills;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Character
{
    /// <summary>
    /// 배틀 아이템 (BattleItemTable 한 행). 소지 개수(Remaining)와 쿨다운이 있고, 레이드를 다시 시작하면 채워진다.
    /// 종류별 동작은 EBattleItemType. 수치는 CSV 에서 바꾼다.
    /// </summary>
    public class BattleItemAbility : GameplayAbility
    {
        public const int AbilityIdBase = 9200;

        private static GameplayEffectDef cleanse;

        public readonly BattleItemRow Row;
        public int Remaining { get; private set; }

        public BattleItemAbility(BattleItemRow InRow)
        {
            Row = InRow;
            AbilityId = AbilityIdBase + InRow.Index;
            ActivationBlockedTags.Add((int)ETag.State_Dead);
            ActivationBlockedTags.Add((int)ETag.State_Stunned);
            ActivationBlockedTags.Add((int)ETag.State_Grabbed);
            ActivationBlockedTags.Add((int)ETag.State_Falling);
            CooldownEffect = GameEffects.Cooldown;
            CooldownDuration = InRow.Cooldown;
            Refill();
        }

        public void Refill()
        {
            Remaining = Row.MaxCount;
        }

        public override bool CanActivate(out EActivateFailReason OutReason)
        {
            if (Remaining <= 0)
            {
                OutReason = EActivateFailReason.Cost;
                return false;
            }

            return base.CanActivate(out OutReason);
        }

        protected override void OnActivate(AbilityActivationInfo InInfo)
        {
            var actor = Owner.OwnerActor as CombatActor;
            if (actor == null || CommitAbility() == false)
            {
                EndAbility(true);
                return;
            }

            Remaining -= 1;
            switch (Row.Type)
            {
                case EBattleItemType.HealPotion:
                    var heal = Owner.MakeOutgoingSpec(GameEffects.Heal, AbilityId);
                    heal.SetByCaller((int)ESetByCaller.FlatDamage, actor.Stats.Get(EAttr.MaxHealth) * Row.Value * 0.01f);
                    Owner.ApplyGameplayEffectSpecToSelf(heal);
                    break;
                case EBattleItemType.TimeStop:
                    // 시간 정지: 무적 + 행동 불가. 걸려 있던 공중/공포 상태는 걷어 낸다.
                    actor.Asc.RemoveEffectsWithGrantedTags(GameTags.Container(ETag.State_Airborne, ETag.State_Feared, ETag.State_Grabbed));
                    actor.ApplyStatus(ETag.State_Invulnerable, Row.Duration, Owner);
                    actor.ApplyStatus(ETag.State_Frozen, Row.Duration, Owner);
                    if (actor.Motor != null)
                    {
                        actor.Motor.Stop();
                    }

                    break;
                case EBattleItemType.Panacea:
                    if (cleanse == null)
                    {
                        cleanse = EffectBuilder.Instant().RemoveWith(ETag.Debuff).Build();
                    }

                    Owner.ApplyGameplayEffectSpecToSelf(Owner.MakeOutgoingSpec(cleanse, AbilityId));
                    break;
                case EBattleItemType.DestructionBomb:
                    var bomb = new HitParams();
                    bomb.SkillId = AbilityId;
                    bomb.Destruction = Row.Value;
                    bomb.Stagger = 30.0f;
                    bomb.FlatDamage = 1.0f;
                    bomb.Flags = ESkillFlags.BattleItem;
                    SkillHitResolver.ApplyArea(actor, HitArea.Circle(Row.Radius), InInfo.AimPoint, Vector3.forward, bomb);
                    break;
                case EBattleItemType.Flashbang:
                    var targets = new System.Collections.Generic.List<CombatActor>();
                    CombatRegistry.FindHostiles(actor, HitArea.Circle(Row.Radius), InInfo.AimPoint, Vector3.forward, targets);
                    for (int i = 0; i < targets.Count; ++i)
                    {
                        targets[i].ApplyStatus(ETag.State_Stunned, Row.Duration, Owner);
                    }

                    break;
            }

            EndAbility(false);
        }
    }
}
