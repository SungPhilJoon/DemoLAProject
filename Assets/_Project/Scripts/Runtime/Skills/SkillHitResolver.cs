using System.Collections.Generic;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Effects;
using Framework.Ability;

namespace DemoLA.Skills
{
    [System.Flags]
    public enum ESkillFlags
    {
        None = 0,
        Awakening = 1,
        Identity = 2,
        BattleItem = 4,
    }

    /// <summary>타격 1회 수치 (스킬 · 아이덴티티 · 배틀 아이템 공용).</summary>
    public struct HitParams
    {
        public int SkillId;
        public float Coefficient;
        public float FlatDamage;
        public float Stagger;
        public float Destruction;
        public float CritRateBonus;
        public float CritDamageBonus;
        public float GemDamagePercent;
        public ESkillPositionalType PositionalType;
        public float BackAttackBonus;
        public float HeadAttackBonus;
        public bool IsCounter;
        public EElement Element;
        public ESkillFlags Flags;
        public float SkillCooldown;
    }

    /// <summary>
    /// 판정 범위 안의 대상에게 피해 스펙을 만들어 적용한다. 카운터 판정과 방향(백/헤드) 판정도 여기서 한다.
    /// </summary>
    public static class SkillHitResolver
    {
        private static readonly List<CombatActor> Buffer = new List<CombatActor>();

        public static int ApplyArea(CombatActor InActor, in HitArea InArea, UnityEngine.Vector3 InOrigin, UnityEngine.Vector3 InForward, in HitParams InParams, List<CombatActor> OutTargets = null)
        {
            var targets = OutTargets ?? Buffer;
            CombatRegistry.FindHostiles(InActor, InArea, InOrigin, InForward, targets);
            for (int i = 0; i < targets.Count; ++i)
            {
                ApplyHit(InActor, targets[i], InParams);
            }

            return targets.Count;
        }

        public static void ApplyHit(CombatActor InActor, CombatActor InTarget, in HitParams InParams)
        {
            var owner = InActor.Asc;
            var spec = owner.MakeOutgoingSpec(GameEffects.SkillDamage, InParams.SkillId);
            spec.SetByCaller((int)ESetByCaller.Coefficient, InParams.Coefficient);
            spec.SetByCaller((int)ESetByCaller.FlatDamage, InParams.FlatDamage);
            spec.SetByCaller((int)ESetByCaller.StaggerValue, InParams.Stagger);
            spec.SetByCaller((int)ESetByCaller.DestructionValue, InParams.Destruction);
            spec.SetByCaller((int)ESetByCaller.CritRateBonus, InParams.CritRateBonus);
            spec.SetByCaller((int)ESetByCaller.CritDamageBonus, InParams.CritDamageBonus);
            spec.SetByCaller((int)ESetByCaller.GemDamagePercent, InParams.GemDamagePercent);
            spec.SetByCaller((int)ESetByCaller.SkillId, InParams.SkillId);
            spec.SetByCaller((int)ESetByCaller.Element, (float)InParams.Element);
            spec.SetByCaller((int)ESetByCaller.SkillFlags, (float)InParams.Flags);
            spec.SetByCaller((int)ESetByCaller.SkillCooldown, InParams.SkillCooldown);

            var positional = resolvePositional(InParams.PositionalType, InActor, InTarget);
            spec.SetByCaller((int)ESetByCaller.PositionalType, (float)positional);
            if (positional == EPositional.BackAttack)
            {
                spec.SetByCaller((int)ESetByCaller.PositionalBonus, InParams.BackAttackBonus);
            }
            else if (positional == EPositional.HeadAttack)
            {
                spec.SetByCaller((int)ESetByCaller.PositionalBonus, InParams.HeadAttackBonus);
            }

            spec.HitPoint = InTarget.Position;
            spec.HitDirection = (InTarget.Position - InActor.Position).normalized;

            if (InParams.IsCounter == true && InTarget.HasTag((int)ETag.Boss_CounterWindow) == true)
            {
                var cue = new GameplayCueParams();
                cue.Source = owner;
                cue.Target = InTarget.Asc;
                cue.Location = InTarget.Position;
                InTarget.Asc.SendGameplayEvent((int)ETag.Event_Countered, cue);
                GameplayCueManager.Execute((int)ECue.Countered, cue);
            }

            owner.ApplyGameplayEffectSpecToTarget(spec, InTarget.Asc);
        }

        private static EPositional resolvePositional(ESkillPositionalType InType, CombatActor InActor, CombatActor InTarget)
        {
            if (InType == ESkillPositionalType.None)
            {
                return EPositional.None;
            }

            var detected = HitGeometry.ResolvePositional(InTarget.Position, InTarget.Forward, InActor.Position);
            if (InType == ESkillPositionalType.BackAttack && detected == EPositional.BackAttack)
            {
                return EPositional.BackAttack;
            }

            if (InType == ESkillPositionalType.HeadAttack && detected == EPositional.HeadAttack)
            {
                return EPositional.HeadAttack;
            }

            return EPositional.None;
        }
    }
}
