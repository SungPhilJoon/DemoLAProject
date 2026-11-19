using System;
using System.Collections;
using Framework.Ability;
using DemoLA.Skills;
using Framework.Testing;
using UnityEngine;

namespace DemoLA.Testing
{
    /// <summary>
    /// 파티원 스킬의 GameplayCue 슬롯(시전 전 · 중 · 후)에 큐를 끼운다.
    ///  - Cue 에셋(프레임워크 GameplayCueNotify)을 넣으면 그것을 쓰고, 비우면 ShaderName 셰이더로 실행 중에만 쓰는 ShaderGameplayCueNotify 를 만든다 (에셋을 만들지 않는다).
    ///  - 시나리오가 끝나면 원래 슬롯으로 되돌린다 (호스트 정리 훅).
    /// 기록: 호스트가 GameplayCueManager.OnCueInvoked 를 받아 Cue.{구간}.{Active|Removed|Executed} 로 남긴다 (이 단계가 큐 id 에 구간 이름표를 붙인다).
    /// </summary>
    [Serializable]
    public class AssignSkillCueStep : DemoLAStep
    {
        public int Member;
        [Tooltip("단축키 칸 (0~7 = Q W E R A S D F)")]
        public int SkillSlot;
        public ESkillCuePhase Phase = ESkillCuePhase.PreCast;
        [Tooltip("넣으면 이 큐 에셋을 쓴다")]
        public GameplayCueNotify Cue;
        [Tooltip("Cue 가 비었을 때 쓸 셰이더 (예: DemoLA/SkillCue/PreCast/AuraRuneCircle)")]
        public string ShaderName = "DemoLA/SkillCue/PreCast/AuraRuneCircle";
        public float StartOffset;
        [Tooltip("0 = 자동")]
        public float Duration;

        private static int nextTestCueId = 90000;

        public override IEnumerator Execute(ScenarioContext InContext)
        {
            var host = Host(InContext);
            var member = host.Member(Member);
            var skill = member != null ? member.GetSkillInSlot(SkillSlot) : null;
            if (skill == null)
            {
                Fail(InContext, $"파티원 {Member} 의 스킬 칸 {SkillSlot} 이 비어 있음");
                yield break;
            }

            var cue = Cue;
            if (cue == null)
            {
                var shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    Fail(InContext, $"셰이더 '{ShaderName}' 없음");
                    yield break;
                }

                var shaderCue = ScriptableObject.CreateInstance<ShaderGameplayCueNotify>();
                shaderCue.name = $"TestCue_{Phase}_{shader.name.Substring(shader.name.LastIndexOf('/') + 1)}";
                shaderCue.hideFlags = HideFlags.DontSave;
                shaderCue.Shape = SkillCueTiming.ShapeOf(Phase);
                shaderCue.CueId = nextTestCueId++;
                shaderCue.ApplyShapeDefaults();
                shaderCue.Material = new Material(shader) { hideFlags = HideFlags.DontSave };
                host.AddCleanup(() =>
                {
                    GameplayCueManager.Unregister(shaderCue.RuntimeCueId);
                    UnityEngine.Object.Destroy(shaderCue.Material);
                    UnityEngine.Object.Destroy(shaderCue);
                });
                cue = shaderCue;
            }

            host.LabelCue(cue.RuntimeCueId, Phase.ToString());
            var definition = skill.Definition;
            var previous = SkillCueTiming.Slot(definition, Phase);
            SkillCueTiming.SetSlot(definition, Phase, new SkillCueSlot { Cue = cue, StartOffset = StartOffset, Duration = Duration });
            host.AddCleanup(() => SkillCueTiming.SetSlot(definition, Phase, previous));

            float start;
            float end;
            SkillCueTiming.Window(definition, Phase, out start, out end);
            var asShader = cue as ShaderGameplayCueNotify;
            InContext.Log($"[{definition.DisplayName}] {Phase} ← {cue.name} ({(asShader != null && asShader.Shader != null ? asShader.Shader.name : cue.GetType().Name)}, id {cue.RuntimeCueId}) 구간 {start:0.00}~{end:0.00}s, 타격 {definition.Hits.Count}회, 시전 {definition.CastDuration:0.00}s");
            yield return null;
        }

        public override string Describe()
        {
            return string.IsNullOrEmpty(Label) ? $"파티원 {Member} 스킬 칸 {SkillSlot} {Phase} 큐 ← {(Cue != null ? Cue.name : ShaderName)}" : Label;
        }
    }

    /// <summary>
    /// 파티원이 스킬 칸의 스킬을 쓴다 (실제 SkillAbility 경로: 비용 · 쿨다운 · 애니메이션 · GameplayCue).
    /// 기록: Skill.Cast (값 = 시전 시간 1배) · Skill.Ended. WaitForEnd 면 스킬이 끝날 때까지 기다린다.
    /// </summary>
    [Serializable]
    public class CastSkillStep : DemoLAStep
    {
        public int Member;
        [Tooltip("단축키 칸 (0~7 = Q W E R A S D F)")]
        public int SkillSlot;
        [Tooltip("조준 지점 (파티원 기준 상대 위치)")]
        public Vector3 AimOffset = new Vector3(0.0f, 0.0f, 5.0f);
        public bool ClearCooldown = true;
        public bool WaitForEnd = true;
        public float Timeout = 10.0f;

        public override IEnumerator Execute(ScenarioContext InContext)
        {
            var member = Host(InContext).Member(Member);
            var skill = member != null ? member.GetSkillInSlot(SkillSlot) : null;
            if (skill == null)
            {
                Fail(InContext, $"파티원 {Member} 의 스킬 칸 {SkillSlot} 이 비어 있음");
                yield break;
            }

            if (ClearCooldown == true)
            {
                member.Asc.ClearCooldowns();
            }

            Framework.Ability.EActivateFailReason reason;
            if (member.TryUseSkill(skill, member.Position + AimOffset, out reason) == false)
            {
                Fail(InContext, $"스킬 '{skill.Definition.DisplayName}' 시전 실패 ({reason})");
                yield break;
            }

            InContext.Recorder.Record("Skill.Cast", skill.Definition.CastDuration, skill.Definition.DisplayName);
            if (WaitForEnd == false)
            {
                yield break;
            }

            float end = InContext.Elapsed + Timeout;
            while (skill.IsActive == true)
            {
                if (InContext.Elapsed >= end)
                {
                    Fail(InContext, $"스킬 '{skill.Definition.DisplayName}' 이 {Timeout:0.#}초 안에 끝나지 않음");
                    yield break;
                }

                yield return null;
            }

            InContext.Recorder.Record("Skill.Ended", 1.0f, skill.Definition.DisplayName);
        }

        public override string Describe()
        {
            return string.IsNullOrEmpty(Label) ? $"파티원 {Member} 스킬 칸 {SkillSlot} 시전" : Label;
        }
    }
}
