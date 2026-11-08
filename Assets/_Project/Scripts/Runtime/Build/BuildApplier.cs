using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Combat;
using DemoLA.Skills;

namespace DemoLA.Build
{
    /// <summary>
    /// BuildResult 를 액터와 스킬에 실제로 붙인다. 다시 적용하면 이전 것을 모두 떼고 새로 붙인다.
    /// </summary>
    public class BuildApplier
    {
        private readonly List<int> characterHandles = new List<int>();

        public BuildResult LastResult { get; private set; }

        public void Apply(CombatActor InActor, IReadOnlyList<SkillAbility> InSkills, BuildResult InResult)
        {
            Clear(InActor, InSkills);
            LastResult = InResult;
            if (InActor == null || InResult == null)
            {
                return;
            }

            InActor.DamageModifiers.AddRange(InResult.DamageModifiers);
            InActor.AttackElement = InResult.AttackElement;
            for (int i = 0; i < InResult.CharacterEffects.Count; ++i)
            {
                int handle = InActor.ApplyEffectToSelf(InResult.CharacterEffects[i].Def, 0);
                if (handle != 0)
                {
                    characterHandles.Add(handle);
                }
            }

            for (int i = 0; i < InSkills.Count; ++i)
            {
                var skill = InSkills[i];
                SkillBuildOverride skillOverride;
                if (InResult.SkillOverrides.TryGetValue(skill.AbilityId, out skillOverride) == true)
                {
                    skill.Level = skillOverride.Level;
                    skill.SetHitsOverride(skillOverride.Hits);
                    skill.SetGrantedFlags(skillOverride.SuperArmor, skillOverride.Counter);
                }

                for (int e = 0; e < InResult.SkillEffects.Count; ++e)
                {
                    var effect = InResult.SkillEffects[e];
                    if (effect.SkillId == skill.AbilityId)
                    {
                        skill.AddRuntimeEffect(effect.Def, effect.Timing, effect.Scale);
                    }
                }
            }

            // 발동형 효과 (각인 · 장비 세트).
            var procSystem = InActor.GetComponent<BuildProcSystem>();
            if (procSystem == null)
            {
                procSystem = InActor.gameObject.AddComponent<BuildProcSystem>();
            }

            procSystem.Set(InActor, InResult.Procs);

            // 빌드 변경은 전투 밖에서 하므로 체력/마나를 채운다.
            InActor.Stats.Set(EAttr.Health, InActor.Stats.Get(EAttr.MaxHealth));
            InActor.Stats.Set(EAttr.Mana, InActor.Stats.Get(EAttr.MaxMana));
        }

        public void Clear(CombatActor InActor, IReadOnlyList<SkillAbility> InSkills)
        {
            if (InActor != null)
            {
                InActor.DamageModifiers.Clear();
                InActor.AttackElement = DemoLA.Combat.EElement.None;
                for (int i = 0; i < characterHandles.Count; ++i)
                {
                    InActor.RemoveEffect(characterHandles[i]);
                }
            }

            characterHandles.Clear();
            if (InSkills == null)
            {
                return;
            }

            for (int i = 0; i < InSkills.Count; ++i)
            {
                InSkills[i].ClearRuntimeEffects();
                InSkills[i].Level = 1;
            }
        }
    }
}
