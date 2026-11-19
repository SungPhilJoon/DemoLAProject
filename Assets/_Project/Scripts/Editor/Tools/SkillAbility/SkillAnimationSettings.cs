using System.Collections.Generic;
using DemoLA.Skills;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor.SkillTool
{
    /// <summary>
    /// 스킬 시전 애니메이션 설정 보존. 스킬 에셋(Data/Generated/Skills)은 전체 셋업 때 코드(DefaultContent)로 다시 만들어지지만,
    /// Skill Ability Tool 에서 넣은 애니메이션(클립 · 속도 · 시작 · 블렌드)과 GameplayCue 슬롯(시전 전 · 중 · 후)은 코드에 없으므로 SkillId 별로 옮겨 붙인다.
    /// </summary>
    public static class SkillAnimationSettings
    {
        public struct Entry
        {
            public AnimationClip Clip;
            public float Speed;
            public float StartTime;
            public float BlendIn;
            public float BlendOut;
            public SkillCueSlot PreCastCue;
            public SkillCueSlot CastingCue;
            public SkillCueSlot PostCastCue;
        }

        /// <summary>(SkillId, 각성기 여부) 키.</summary>
        public static long Key(SkillDefinition InSkill)
        {
            return ((long)InSkill.SkillId << 1) | (InSkill.IsAwakening ? 1L : 0L);
        }

        public static Dictionary<long, Entry> Collect(string InFolder)
        {
            var kept = new Dictionary<long, Entry>();
            if (AssetDatabase.IsValidFolder(InFolder) == false)
            {
                return kept;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:SkillDefinition", new[] { InFolder }))
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (skill == null || (skill.CastAnimation == null && skill.HasCastCues == false))
                {
                    continue;
                }

                kept[Key(skill)] = new Entry
                {
                    Clip = skill.CastAnimation,
                    Speed = skill.AnimationSpeed,
                    StartTime = skill.AnimationStartTime,
                    BlendIn = skill.AnimationBlendIn,
                    BlendOut = skill.AnimationBlendOut,
                    PreCastCue = skill.PreCastCue != null ? skill.PreCastCue.Clone() : null,
                    CastingCue = skill.CastingCue != null ? skill.CastingCue.Clone() : null,
                    PostCastCue = skill.PostCastCue != null ? skill.PostCastCue.Clone() : null,
                };
            }

            return kept;
        }

        public static int Restore(IEnumerable<ScriptableObject> InCreated, Dictionary<long, Entry> InKept)
        {
            int restored = 0;
            foreach (var created in InCreated)
            {
                Entry entry;
                if (created is SkillDefinition skill && InKept.TryGetValue(Key(skill), out entry) == true)
                {
                    Apply(skill, entry);
                    restored += 1;
                }
            }

            if (restored > 0)
            {
                Debug.Log($"[DemoLA] 스킬 시전 애니메이션 · 큐 {restored}개를 새 스킬 에셋에 다시 넣었습니다");
            }

            return restored;
        }

        public static void Apply(SkillDefinition InSkill, Entry InEntry)
        {
            if (InEntry.Clip != null)
            {
                InSkill.CastAnimation = InEntry.Clip;
                InSkill.AnimationSpeed = InEntry.Speed;
                InSkill.AnimationStartTime = InEntry.StartTime;
                InSkill.AnimationBlendIn = InEntry.BlendIn;
                InSkill.AnimationBlendOut = InEntry.BlendOut;
            }

            if (InEntry.PreCastCue != null)
            {
                InSkill.PreCastCue = InEntry.PreCastCue.Clone();
            }

            if (InEntry.CastingCue != null)
            {
                InSkill.CastingCue = InEntry.CastingCue.Clone();
            }

            if (InEntry.PostCastCue != null)
            {
                InSkill.PostCastCue = InEntry.PostCastCue.Clone();
            }
        }
    }
}
