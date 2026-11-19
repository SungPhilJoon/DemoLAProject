using System.Collections.Generic;
using System.Linq;
using DemoLA.Skills;
using Framework.Ability;
using Framework.Editor;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor.SkillTool
{
    /// <summary>
    /// 스킬 GameplayCue 구간 ↔ 프레임워크 셰이더 GameplayCue 연결 (Skill Ability Tool · GAS Tool 의 GameplayCue 탭 공용).
    ///  - 구간(시전 전 · 중 · 후) = 셰이더 큐 모양(Aura · Trail · Burst)
    ///  - 셰이더 폴더: Art/Shaders/SkillCue/{PreCast | Casting | PostCast} → 프레임워크 GameplayCueEditorUtil.RegisterShaderFolder 로 등록
    ///  - 큐 에셋 폴더: Data/GameplayCues/Skill/{구간} (머티리얼은 큐 에셋의 하위 에셋, Data/Generated 밖이라 전체 셋업에도 남는다)
    ///  - 큐 태그: GameplayCue.Skill.{구간}.{큐 이름} (프레임워크 GameplayTagRegistry.RequestTag)
    /// </summary>
    [InitializeOnLoad]
    public static class SkillCueLibrary
    {
        public const string ShaderRoot = "Assets/_Project/Art/Shaders/SkillCue";
        public const string CueRoot = "Assets/_Project/Data/GameplayCues/Skill";
        public const string CueTagRoot = "GameplayCue.Skill";

        public static readonly ESkillCuePhase[] Phases = SkillCueTiming.Phases;

        static SkillCueLibrary()
        {
            foreach (var phase in Phases)
            {
                var shape = SkillCueTiming.ShapeOf(phase);
                GameplayCueEditorUtil.RegisterShaderFolder(shape, ShaderFolder(phase));
                GameplayCueEditorUtil.RegisterCueFolder(shape, CueFolder(phase));
            }
        }

        public static string PhaseLabel(ESkillCuePhase InPhase)
        {
            switch (InPhase)
            {
                case ESkillCuePhase.PreCast:
                    return "시전 전 · 아우라";
                case ESkillCuePhase.Casting:
                    return "시전 중 · 무기 궤적";
                default:
                    return "시전 후 · 무기 분출";
            }
        }

        public static string PhaseDescription(ESkillCuePhase InPhase)
        {
            switch (InPhase)
            {
                case ESkillCuePhase.PreCast:
                    return "캐릭터를 중심으로 아우라가 펼쳐진다 (시전 시작 → 첫 타격) — 셰이더 큐 Aura";
                case ESkillCuePhase.Casting:
                    return "무기 궤적을 따라가는 잔상 (첫 타격 0.3초 전 → 마지막 타격) — 셰이더 큐 Trail";
                default:
                    return "마지막 타격 때 무기에서 빛이 뿜어져 나온다 (1회) — 셰이더 큐 Burst";
            }
        }

        public static Color PhaseColor(ESkillCuePhase InPhase)
        {
            switch (InPhase)
            {
                case ESkillCuePhase.PreCast:
                    return new Color(0.45f, 0.75f, 1.0f, 0.85f);
                case ESkillCuePhase.Casting:
                    return new Color(1.0f, 0.8f, 0.3f, 0.85f);
                default:
                    return new Color(1.0f, 0.45f, 0.85f, 0.85f);
            }
        }

        public static string ShaderFolder(ESkillCuePhase InPhase)
        {
            return $"{ShaderRoot}/{InPhase}";
        }

        public static string CueFolder(ESkillCuePhase InPhase)
        {
            return $"{CueRoot}/{InPhase}";
        }

        public static List<Shader> Shaders(ESkillCuePhase InPhase)
        {
            return GameplayCueEditorUtil.Shaders(SkillCueTiming.ShapeOf(InPhase));
        }

        /// <summary>이 구간에 끼울 수 있는 큐: 같은 모양의 셰이더 큐 + 셰이더 큐가 아닌 Notify (이름순).</summary>
        public static List<GameplayCueNotify> Cues(ESkillCuePhase InPhase)
        {
            var shape = SkillCueTiming.ShapeOf(InPhase);
            return GameplayCueEditorUtil.AllNotifies().Where(n => n is ShaderGameplayCueNotify shader ? shader.Shape == shape : true).ToList();
        }

        /// <summary>
        /// InShader 로 그리는 이 구간의 새 셰이더 큐. 태그 = GameplayCue.Skill.{구간}.{이름표} — 이름표는 InName 을 주면 그것, 아니면 셰이더 이름.
        /// </summary>
        public static ShaderGameplayCueNotify CreateCue(ESkillCuePhase InPhase, Shader InShader, string InName = null)
        {
            string shaderName = GameplayCueEditorUtil.ShortName(InShader);
            string name = string.IsNullOrEmpty(InName) == true ? $"GC_Skill_{InPhase}_{shaderName}" : InName;
            string tagLeaf = string.IsNullOrEmpty(InName) == true ? shaderName : InName;
            return GameplayCueEditorUtil.CreateShaderCue(SkillCueTiming.ShapeOf(InPhase), InShader, name, CueFolder(InPhase), $"{CueTagRoot}.{InPhase}.{tagLeaf}");
        }

        /// <summary>구간 폴더의 셰이더마다 큐 에셋을 하나씩 만든다 (이미 그 셰이더를 쓰는 같은 모양의 큐가 있으면 건너뛴다).</summary>
        public static int CreateDefaultCues()
        {
            int created = 0;
            foreach (var phase in Phases)
            {
                var shape = SkillCueTiming.ShapeOf(phase);
                foreach (var shader in Shaders(phase))
                {
                    if (GameplayCueEditorUtil.ShaderNotifies(shape).Any(c => c.Shader == shader) == true)
                    {
                        continue;
                    }

                    if (CreateCue(phase, shader) != null)
                    {
                        created += 1;
                    }
                }
            }

            Debug.Log($"[Skill Cue] 기본 큐 {created}개를 만들었습니다 → {CueRoot}");
            return created;
        }

        [MenuItem("DemoLA/Tools/Skill Cue/셰이더마다 기본 큐 만들기", priority = 210)]
        private static void CreateDefaultCuesMenu()
        {
            CreateDefaultCues();
        }
    }
}
