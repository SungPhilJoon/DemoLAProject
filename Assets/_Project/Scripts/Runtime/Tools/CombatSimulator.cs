using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Build;
using DemoLA.Character;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Data;
using DemoLA.Effects;
using Framework;
using UnityEngine;
using UnityEngine.AI;

namespace DemoLA.Tools
{
    [Serializable]
    public class SimulationConfig
    {
        public string Label = "A";
        public ECharacterClass Class = ECharacterClass.Slayer;
        public CharacterBuildData Build;
        public string BossId = "Gate1";
        public float Duration = 60.0f;
        public float Step = 0.05f;
        public int Seed = 1;
        public int Trials = 5;
        [Tooltip("보스 뒤에서 공격 (백어택)")]
        public bool FromBehind;
        public float Distance = 2.5f;
        public bool InfiniteMana = true;
        public bool UseIdentity = true;
        public bool UseAwakening;
        [Tooltip("보스가 그로기 상태 (무력화 응징 등 조건 확인용)")]
        public bool TargetGroggy;
    }

    [Serializable]
    public class SimulationResult
    {
        public string Label;
        public float MeanDps;
        public float StdDps;
        public float MinDps;
        public float MaxDps;
        public float MeanTotal;
        public float CritRate;
        public float BackRate;
        public readonly Dictionary<string, float> SkillShare = new Dictionary<string, float>();
        public readonly List<float> TrialDps = new List<float>();

        public string ToCsvRow()
        {
            return $"{Label},{MeanDps:0},{StdDps:0},{MinDps:0},{MaxDps:0},{MeanTotal:0},{CritRate:0.000},{BackRate:0.000}";
        }

        public const string CsvHeader = "Label,MeanDps,StdDps,MinDps,MaxDps,MeanTotal,CritRate,BackRate";
    }

    /// <summary>
    /// 결정적 전투 시뮬레이터 (밸런스 툴 · 테스트 · CLI 공용).
    /// 고정 간격으로 ASC 를 직접 진행하고, 시드를 바꿔 Trials 번 돌린 평균/표준편차를 낸다.
    /// 스킬 사용 순서: 사용 가능한 스킬을 슬롯 순서대로 (각성기는 옵션) — 실제 플레이 순서가 아니라 비교용 기준 회전이다.
    /// </summary>
    public static class CombatSimulator
    {
        public static SimulationResult Run(GameDatabase InDatabase, SimulationConfig InConfig)
        {
            var result = new SimulationResult();
            result.Label = InConfig.Label;
            var skillTotals = new Dictionary<string, float>();
            int hits = 0;
            int crits = 0;
            int backs = 0;
            for (int trial = 0; trial < Mathf.Max(1, InConfig.Trials); ++trial)
            {
                var single = runOnce(InDatabase, InConfig, InConfig.Seed + trial * 7919, skillTotals, ref hits, ref crits, ref backs);
                result.TrialDps.Add(single / InConfig.Duration);
                result.MeanTotal += single;
            }

            int count = result.TrialDps.Count;
            result.MeanTotal /= count;
            result.MeanDps = result.TrialDps.Average();
            result.MinDps = result.TrialDps.Min();
            result.MaxDps = result.TrialDps.Max();
            float mean = result.MeanDps;
            result.StdDps = Mathf.Sqrt(result.TrialDps.Sum(d => (d - mean) * (d - mean)) / count);
            result.CritRate = hits > 0 ? crits / (float)hits : 0.0f;
            result.BackRate = hits > 0 ? backs / (float)hits : 0.0f;
            float all = skillTotals.Values.Sum();
            foreach (var pair in skillTotals.OrderByDescending(p => p.Value))
            {
                result.SkillShare[pair.Key] = all > 0.0f ? pair.Value / all : 0.0f;
            }

            return result;
        }

        public static string Describe(SimulationResult InResult)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[{InResult.Label}] DPS {InResult.MeanDps:N0} ± {InResult.StdDps:N0} (최소 {InResult.MinDps:N0} / 최대 {InResult.MaxDps:N0}) · 치명 {InResult.CritRate * 100.0f:0.#}% · 백 {InResult.BackRate * 100.0f:0.#}%");
            foreach (var pair in InResult.SkillShare)
            {
                builder.AppendLine($"    {pair.Key,-12} {pair.Value * 100.0f:0.#}%");
            }

            return builder.ToString();
        }

        private static float runOnce(GameDatabase InDatabase, SimulationConfig InConfig, int InSeed, Dictionary<string, float> OutSkills, ref int OutHits, ref int OutCrits, ref int OutBacks)
        {
            GameTags.RegisterAll();
            GameEffects.Initialize();
            GameRandom.SetSeed(InSeed);
            var objects = new List<GameObject>();
            float total = 0.0f;
            int hits = 0;
            int crits = 0;
            int backs = 0;
            var names = new Dictionary<int, string>();
            PartyCharacter character = null;
            Action<DamageAppliedEvent> listener = InEvent =>
            {
                if (character == null || InEvent.Source != character.Asc)
                {
                    return;
                }

                // 계산된 피해(RawAmount) 기준: 허수아비처럼 체력이 매우 크면 float 정밀도 때문에 HealthLost 가 0 이 된다.
                float amount = InEvent.DeathWardConsumed ? 0.0f : InEvent.RawAmount;
                total += amount;
                hits += 1;
                crits += InEvent.IsCrit ? 1 : 0;
                backs += InEvent.Positional == EPositional.BackAttack ? 1 : 0;
                string name;
                if (names.TryGetValue(InEvent.SkillId, out name) == false)
                {
                    name = $"#{InEvent.SkillId}";
                }

                float current;
                OutSkills.TryGetValue(name, out current);
                OutSkills[name] = current + amount;
            };

            EventBus<DamageAppliedEvent>.Subscribe(listener);
            try
            {
                var bossDef = InDatabase.GetBoss(InConfig.BossId) ?? InDatabase.Bosses[0];
                var bossGo = new GameObject("Sim_Boss");
                objects.Add(bossGo);
                bossGo.SetActive(false);
                bossGo.AddComponent<NavMeshAgent>().enabled = false;
                bossGo.AddComponent<ActorMotor>();
                var boss = bossGo.AddComponent<BossActor>();
                bossGo.SetActive(true);
                boss.Initialize(bossDef);
                boss.Stats.Set(EAttr.MaxHealth, 1e15f);
                boss.Stats.Set(EAttr.Health, 1e15f);
                bossGo.transform.rotation = Quaternion.LookRotation(InConfig.FromBehind ? Vector3.back : Vector3.forward);
                if (InConfig.TargetGroggy == true)
                {
                    boss.ApplyStatus(ETag.State_Groggy, InConfig.Duration + 10.0f, boss.Asc);
                }

                var charGo = new GameObject("Sim_Character");
                objects.Add(charGo);
                charGo.SetActive(false);
                charGo.AddComponent<NavMeshAgent>().enabled = false;
                charGo.AddComponent<ActorMotor>();
                character = charGo.AddComponent<PartyCharacter>();
                charGo.SetActive(true);
                charGo.transform.position = Vector3.forward * (bossDef.Radius + InConfig.Distance);
                var build = InConfig.Build ?? DemoLA.Content.DefaultContent.CreateDefaultBuild(InDatabase, InConfig.Class);
                character.Initialize(InDatabase.GetClass(InConfig.Class), build.Clone(), InDatabase);
                character.FaceTowards(boss.Position);
                for (int i = 0; i < character.Skills.Count; ++i)
                {
                    names[character.Skills[i].AbilityId] = character.Skills[i].Definition.DisplayName;
                }

                if (character.Identity != null)
                {
                    names[character.Identity.AbilityId] = character.Identity.DisplayName;
                }

                // 실시간 Update 가 함께 돌면 두 번 진행되므로 ASC 를 끄고 직접 Tick 한다.
                character.Asc.enabled = false;
                boss.Asc.enabled = false;

                for (float time = 0.0f; time < InConfig.Duration; time += InConfig.Step)
                {
                    if (InConfig.InfiniteMana == true)
                    {
                        character.Stats.Set(EAttr.Mana, character.Stats.Get(EAttr.MaxMana));
                    }

                    if (character.IsCasting == false)
                    {
                        act(character, boss, InConfig);
                    }

                    character.Identity?.TickResource();
                    character.Asc.Tick(InConfig.Step);
                    boss.Asc.Tick(InConfig.Step);
                }
            }
            finally
            {
                EventBus<DamageAppliedEvent>.Unsubscribe(listener);
                for (int i = 0; i < objects.Count; ++i)
                {
                    ObjectUtil.SafeDestroy(objects[i]);
                }
            }

            OutHits += hits;
            OutCrits += crits;
            OutBacks += backs;
            return total;
        }

        private static void act(PartyCharacter InCharacter, BossActor InBoss, SimulationConfig InConfig)
        {
            var aim = InBoss.Position;
            if (InConfig.UseIdentity == true && InCharacter.TryUseIdentity(aim) == true)
            {
                return;
            }

            for (int i = 0; i < InCharacter.EquippedSkills.Count; ++i)
            {
                var skill = InCharacter.EquippedSkills[i];
                if (skill.Definition.IsAwakening == true && InConfig.UseAwakening == false)
                {
                    continue;
                }

                if (InCharacter.TryUseSlot(i, aim) == true)
                {
                    return;
                }
            }
        }
    }
}
