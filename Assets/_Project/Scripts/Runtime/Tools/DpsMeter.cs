using System.Collections.Generic;
using System.Linq;
using System.Text;
using DemoLA.Character;
using DemoLA.Combat;
using DemoLA.Core;
using Framework;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Tools
{
    /// <summary>
    /// 런타임 딜 미터. DamageAppliedEvent 를 출처(액터) · 스킬 단위로 모은다.
    /// 화면(F8 · DpsMeterView), Pipeline CLI(unity command demola dps), 밸런스 시뮬레이터가 같은 데이터를 본다.
    /// 시간 기준: 첫 피해 ~ 마지막 피해 (전투 시간). Reset 으로 구간을 다시 잰다.
    /// </summary>
    public static class DpsMeter
    {
        public class SkillStats
        {
            public int SkillId;
            public string Name;
            public float Total;
            public int Hits;
            public int Crits;
            public int BackAttacks;
            public int HeadAttacks;
            public float MaxHit;
        }

        public class ActorStats
        {
            public AbilitySystemComponent Source;
            public string Name;
            public float Total;
            public int Hits;
            public int Crits;
            public float FirstHit = -1.0f;
            public float LastHit;
            public readonly Dictionary<int, SkillStats> Skills = new Dictionary<int, SkillStats>();

            public float Duration
            {
                get { return FirstHit < 0.0f ? 0.0f : Mathf.Max(1.0f, LastHit - FirstHit); }
            }

            public float Dps
            {
                get { return Duration > 0.0f ? Total / Duration : 0.0f; }
            }
        }

        private static readonly Dictionary<AbilitySystemComponent, ActorStats> Actors = new Dictionary<AbilitySystemComponent, ActorStats>();
        private static bool subscribed;
        private static float startTime;

        /// <summary>시간 공급자 (시뮬레이터가 가상 시간을 넣는다). 기본 = Time.time.</summary>
        public static System.Func<float> Clock = () => Time.time;

        public static IEnumerable<ActorStats> All
        {
            get { return Actors.Values; }
        }

        public static float DamageTakenByEnemies
        {
            get { return Actors.Values.Sum(a => a.Total); }
        }

        public static void Enable()
        {
            if (subscribed == true)
            {
                return;
            }

            subscribed = true;
            EventBus<DamageAppliedEvent>.Subscribe(handle);
            startTime = Clock();
        }

        public static void Disable()
        {
            subscribed = false;
            EventBus<DamageAppliedEvent>.Unsubscribe(handle);
        }

        public static void Reset()
        {
            Actors.Clear();
            startTime = Clock();
        }

        public static ActorStats Get(AbilitySystemComponent InSource)
        {
            ActorStats stats;
            return InSource != null && Actors.TryGetValue(InSource, out stats) == true ? stats : null;
        }

        private static void handle(DamageAppliedEvent InEvent)
        {
            if (InEvent.Source == null || InEvent.Target == null)
            {
                return;
            }

            // 플레이어 팀이 적에게 준 피해만 센다.
            var source = InEvent.Source.OwnerActor as CombatActor;
            var target = InEvent.Target.OwnerActor as CombatActor;
            if (source == null || target == null || CombatRegistry.IsHostile(source.Team, target.Team) == false || source.Team != ETeam.Player)
            {
                return;
            }

            ActorStats actor;
            if (Actors.TryGetValue(InEvent.Source, out actor) == false)
            {
                actor = new ActorStats();
                actor.Source = InEvent.Source;
                actor.Name = source.GetName();
                Actors.Add(InEvent.Source, actor);
            }

            float amount = InEvent.DeathWardConsumed ? 0.0f : InEvent.RawAmount;
            float now = Clock();
            if (actor.FirstHit < 0.0f)
            {
                actor.FirstHit = now;
            }

            actor.LastHit = now;
            actor.Total += amount;
            actor.Hits += 1;
            actor.Crits += InEvent.IsCrit == true ? 1 : 0;

            SkillStats skill;
            if (actor.Skills.TryGetValue(InEvent.SkillId, out skill) == false)
            {
                skill = new SkillStats();
                skill.SkillId = InEvent.SkillId;
                skill.Name = skillName(source, InEvent.SkillId);
                actor.Skills.Add(InEvent.SkillId, skill);
            }

            skill.Total += amount;
            skill.Hits += 1;
            skill.Crits += InEvent.IsCrit == true ? 1 : 0;
            skill.BackAttacks += InEvent.Positional == EPositional.BackAttack ? 1 : 0;
            skill.HeadAttacks += InEvent.Positional == EPositional.HeadAttack ? 1 : 0;
            skill.MaxHit = Mathf.Max(skill.MaxHit, amount);
        }

        private static string skillName(CombatActor InSource, int InSkillId)
        {
            var party = InSource as PartyCharacter;
            if (party != null)
            {
                for (int i = 0; i < party.Skills.Count; ++i)
                {
                    if (party.Skills[i].AbilityId == InSkillId)
                    {
                        return party.Skills[i].Definition.DisplayName;
                    }
                }

                if (party.Identity != null && party.Identity.AbilityId == InSkillId)
                {
                    return $"[Z] {party.Identity.DisplayName}";
                }

                if (party.IdentityX != null && party.IdentityX.AbilityId == InSkillId)
                {
                    return $"[X] {party.IdentityX.DisplayName}";
                }

                if (InSkillId == DemoLA.Build.BuildProcSystem.ProcSkillId)
                {
                    return "각인 · 세트 발동 효과";
                }

                for (int i = 0; i < party.BattleItems.Count; ++i)
                {
                    if (party.BattleItems[i] != null && party.BattleItems[i].AbilityId == InSkillId)
                    {
                        return party.BattleItems[i].Row.Name;
                    }
                }
            }

            return $"#{InSkillId}";
        }

        /// <summary>텍스트 리포트 (CLI · 로그용).</summary>
        public static string Report()
        {
            var builder = new StringBuilder();
            float total = 0.0f;
            foreach (var actor in Actors.Values.OrderByDescending(a => a.Total))
            {
                total += actor.Total;
                builder.AppendLine($"{actor.Name}: 총 {actor.Total:N0} · 초당 {actor.Dps:N0} · 타격 {actor.Hits} · 치명 {percent(actor.Crits, actor.Hits)}");
                foreach (var skill in actor.Skills.Values.OrderByDescending(s => s.Total))
                {
                    builder.AppendLine($"    {skill.Name,-12} {skill.Total,14:N0} ({percent(skill.Total, actor.Total)}) 타격 {skill.Hits} 치명 {percent(skill.Crits, skill.Hits)} 백 {percent(skill.BackAttacks, skill.Hits)} 최대 {skill.MaxHit:N0}");
                }
            }

            builder.Insert(0, $"[딜 미터] 파티 총 피해 {total:N0} · 측정 {Clock() - startTime:0.0}s\n");
            return builder.ToString();
        }

        private static string percent(float InPart, float InWhole)
        {
            return InWhole <= 0.0f ? "0%" : $"{InPart / InWhole * 100.0f:0.#}%";
        }
    }
}
