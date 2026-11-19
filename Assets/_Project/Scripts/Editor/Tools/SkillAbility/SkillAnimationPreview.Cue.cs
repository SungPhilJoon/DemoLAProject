using DemoLA.Skills;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Editor.SkillTool
{
    /// <summary>
    /// 미리보기에 스킬 GameplayCue(시전 전 · 중 · 후 셰이더 큐)를 게임과 같은 메시 · 시간으로 그린다 (프레임워크 GameplayCueGeometry).
    /// 셰이더 시간은 _CueTime(큐 시작 후 초)이라 타임라인을 스크럽해도 같은 그림이 나온다.
    /// 시전 중 궤적은 지난 TrailTime 동안의 자세를 다시 샘플링해 무기 손잡이 · 칼끝을 모은다.
    /// </summary>
    public partial class SkillAnimationPreview
    {
        private const int TrailPreviewSamples = 14;

        public bool ShowCues = true;

        private readonly Mesh[] cueMeshes = new Mesh[3];
        private MaterialPropertyBlock cueBlock;
        private readonly GameplayCueTrail cueTrail = new GameplayCueTrail();
        private GameplayCueSocket cueWeapon;
        private GameObject cueWeaponOwner;

        private void drawCues(SkillDefinition InSkill, float InSkillTime)
        {
            if (ShowCues == false || InSkill == null || instance == null || preview == null)
            {
                return;
            }

            if (cueBlock == null)
            {
                cueBlock = new MaterialPropertyBlock();
            }

            if (cueWeaponOwner != instance)
            {
                cueWeaponOwner = instance;
                cueWeapon = GameplayCueSocket.Resolve(instance.transform);
            }

            foreach (var phase in SkillCueTiming.Phases)
            {
                var slot = SkillCueTiming.Slot(InSkill, phase);
                if (slot == null || slot.IsSet == false)
                {
                    continue;
                }

                var cue = slot.Cue as ShaderGameplayCueNotify;
                if (cue == null)
                {
                    continue;
                }

                float start;
                float end;
                SkillCueTiming.Window(InSkill, phase, out start, out end);
                float duration = Mathf.Max(0.05f, end - start);
                float elapsed = InSkillTime - start;
                bool oneShot = phase == ESkillCuePhase.PostCast;
                if (elapsed < 0.0f || (oneShot == true && elapsed >= duration) || (oneShot == false && elapsed >= duration + cue.FadeOut))
                {
                    continue;
                }

                float stop = oneShot == false && elapsed >= duration ? duration : -1.0f;
                float fade = cue.Envelope(elapsed, oneShot == true ? duration : 0.0f, stop);
                var mesh = cueMesh((int)phase);
                var matrix = Matrix4x4.identity;
                // 게임과 같이 큐 모양(Shape)으로 메시를 고른다.
                switch (cue.Shape)
                {
                    case EGameplayCueShape.Aura:
                        GameplayCueGeometry.BuildAura(mesh, cue.Size, cue.Height);
                        matrix = Matrix4x4.TRS(instance.transform.position, Quaternion.Euler(0.0f, instance.transform.eulerAngles.y, 0.0f), Vector3.one);
                        break;
                    case EGameplayCueShape.Trail:
                        buildPreviewTrail(InSkill, InSkillTime, start, cue, mesh);
                        break;
                    default:
                        if (cueWeapon == null || cueWeapon.IsValid == false)
                        {
                            continue;
                        }

                        GameplayCueGeometry.BuildBurst(mesh, cueWeapon.Base, cueWeapon.Tip, cue.EmitAlong, cue.Size, preview.camera.transform.position);
                        break;
                }

                GameplayCueShaderIds.Apply(cueBlock, elapsed / duration, fade, elapsed, 3.7f + (int)phase * 17.3f);
                preview.DrawMesh(mesh, matrix, cue.Material, 0, cueBlock);
            }
        }

        /// <summary>지난 TrailTime 의 자세를 다시 샘플링해 궤적을 만들고, 지금 자세로 되돌린다.</summary>
        private void buildPreviewTrail(SkillDefinition InSkill, float InSkillTime, float InStart, ShaderGameplayCueNotify InCue, Mesh InMesh)
        {
            cueTrail.Clear();
            if (cueWeapon != null && cueWeapon.IsValid == true)
            {
                float trailTime = Mathf.Max(0.02f, InCue.TrailTime);
                float from = Mathf.Max(InStart, InSkillTime - trailTime * 1.1f);
                for (int i = 0; i <= TrailPreviewSamples; ++i)
                {
                    float t = Mathf.Lerp(from, InSkillTime, (float)i / TrailPreviewSamples);
                    Sample(InSkill, t);
                    cueTrail.Push(cueWeapon.Base, cueWeapon.Tip, t, trailTime);
                }

                Sample(InSkill, InSkillTime);
            }

            GameplayCueGeometry.BuildTrail(InMesh, cueTrail.Bases, cueTrail.Tips, cueTrail.Ages, cueTrail.Count, InCue.TrailTime, InCue.Size);
        }

        private Mesh cueMesh(int InIndex)
        {
            if (cueMeshes[InIndex] == null)
            {
                cueMeshes[InIndex] = GameplayCueGeometry.CreateMesh("GameplayCuePreview");
                cueMeshes[InIndex].hideFlags = HideFlags.HideAndDontSave;
            }

            return cueMeshes[InIndex];
        }

        private void disposeCues()
        {
            for (int i = 0; i < cueMeshes.Length; ++i)
            {
                if (cueMeshes[i] != null)
                {
                    Object.DestroyImmediate(cueMeshes[i]);
                    cueMeshes[i] = null;
                }
            }

            cueWeapon = null;
            cueWeaponOwner = null;
        }
    }
}
