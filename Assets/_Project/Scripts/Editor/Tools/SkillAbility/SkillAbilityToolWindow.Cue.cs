using System;
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
    /// Skill Ability Tool · GameplayCue 구간: 스킬의 시전 전 · 중 · 후 큐 슬롯을 설정한다.
    /// 큐는 프레임워크 GameplayCueNotify (셰이더 큐 = ShaderGameplayCueNotify), 편집 GUI 는 프레임워크 GameplayCueEditorGUI.
    ///  - 큐 고르기 / 새 큐 / 복제 (큐 에셋 값은 그 큐를 쓰는 모든 스킬에 적용된다) / GAS Tool 의 GameplayCue 탭에서 열기
    ///  - 셰이더 갈아끼우기: 구간 폴더(Art/Shaders/SkillCue/{구간})의 셰이더 목록에서 고르면 큐 머티리얼의 셰이더만 바뀐다
    ///  - 큐 태그 · 모양 · 시간 값과 셰이더 값(머티리얼 프로퍼티)을 여기서 바로 고친다
    ///  - 타임라인 아래 3줄 = 구간별 큐 재생 구간, 미리보기에서 셰이더가 실제로 그려진다
    /// </summary>
    public partial class SkillAbilityToolWindow
    {
        [SerializeField] private bool[] CueFoldouts = { true, true, true };

        private readonly GameplayCueEditorState[] cueStates = { new GameplayCueEditorState(), new GameplayCueEditorState(), new GameplayCueEditorState() };
        private readonly UnityEditor.Editor[] cueGenericEditors = new UnityEditor.Editor[3];

        private static string slotPropertyName(ESkillCuePhase InPhase)
        {
            switch (InPhase)
            {
                case ESkillCuePhase.PreCast:
                    return nameof(SkillDefinition.PreCastCue);
                case ESkillCuePhase.Casting:
                    return nameof(SkillDefinition.CastingCue);
                default:
                    return nameof(SkillDefinition.PostCastCue);
            }
        }

        // ------------------------------------------------------------------ details

        private void drawCueSection(SerializedObject InSo)
        {
            section("GameplayCue · 시전 연출 (셰이더)");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("셰이더마다 기본 큐 만들기", $"구간 폴더의 셰이더마다 큐 에셋을 하나씩 만든다 → {SkillCueLibrary.CueRoot}"), EditorStyles.miniButtonLeft) == true)
            {
                SkillCueLibrary.CreateDefaultCues();
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("셰이더 폴더", EditorStyles.miniButtonMid) == true)
            {
                pingFolder(SkillCueLibrary.ShaderRoot);
            }

            if (GUILayout.Button("큐 폴더", EditorStyles.miniButtonMid) == true)
            {
                pingFolder(SkillCueLibrary.CueRoot);
            }

            if (GUILayout.Button("GAS Tool · GameplayCue", EditorStyles.miniButtonRight) == true)
            {
                GasToolWindow.OpenTab<GameplayCueTab>();
            }

            EditorGUILayout.EndHorizontal();

            foreach (var phase in SkillCueLibrary.Phases)
            {
                drawCueSlot(InSo, phase);
            }
        }

        private void drawCueSlot(SerializedObject InSo, ESkillCuePhase InPhase)
        {
            int index = (int)InPhase;
            var slotProp = InSo.FindProperty(slotPropertyName(InPhase));
            if (slotProp == null)
            {
                return;
            }

            var cueProp = slotProp.FindPropertyRelative(nameof(SkillCueSlot.Cue));
            var current = cueProp.objectReferenceValue as GameplayCueNotify;

            float start;
            float end;
            SkillCueTiming.Window(Selected, InPhase, out start, out end);

            EditorGUILayout.Space(2.0f);
            var header = EditorGUILayout.GetControlRect();
            EditorGUI.DrawRect(new Rect(header.x, header.y + 2.0f, 4.0f, header.height - 4.0f), SkillCueLibrary.PhaseColor(InPhase));
            string window = current != null ? $"{start:0.00} → {end:0.00}s" : "없음";
            string title = $"{SkillCueLibrary.PhaseLabel(InPhase)}   ·   {(current != null ? current.name : "-")}   ·   {window}";
            CueFoldouts[index] = EditorGUI.Foldout(new Rect(header.x + 8.0f, header.y, header.width - 8.0f, header.height), CueFoldouts[index], title, true, EditorStyles.foldoutHeader);
            if (CueFoldouts[index] == false)
            {
                return;
            }

            EditorGUI.indentLevel += 1;
            EditorGUILayout.LabelField(SkillCueLibrary.PhaseDescription(InPhase), EditorStyles.miniLabel);

            // 큐 고르기
            var cues = SkillCueLibrary.Cues(InPhase);
            var options = new List<string> { "(없음)" };
            options.AddRange(cues.Select(c => c is ShaderGameplayCueNotify s ? $"{c.name}   [{GameplayCueEditorUtil.ShortName(s.Shader)}]" : $"{c.name}   ({c.GetType().Name})"));
            int selected = current == null ? 0 : cues.IndexOf(current) + 1;
            if (current != null && selected == 0)
            {
                options.Add($"{current.name} (다른 모양 큐)");
                selected = options.Count - 1;
            }

            options.Add("+ 새 큐 만들기");
            EditorGUILayout.BeginHorizontal();
            int next = EditorGUILayout.Popup(new GUIContent("큐", "GameplayCueNotify (셰이더 큐 = 셰이더 머티리얼 + 재생 설정)"), selected, options.ToArray());
            using (new EditorGUI.DisabledScope(current == null))
            {
                if (GUILayout.Button(new GUIContent("복제", "이 스킬만 따로 고칠 수 있게 큐 에셋을 복사해 끼운다"), EditorStyles.miniButtonLeft, GUILayout.Width(40.0f)) == true)
                {
                    var copy = GameplayCueEditorUtil.Duplicate(current, $"{current.name}_{Selected.name}");
                    if (copy != null)
                    {
                        if (string.IsNullOrEmpty(copy.CueTag) == true)
                        {
                            copy.CueTag = $"{SkillCueLibrary.CueTagRoot}.{InPhase}.{copy.name}";
                            EditorUtility.SetDirty(copy);
                        }

                        cueProp.objectReferenceValue = copy;
                    }
                }

                if (GUILayout.Button(new GUIContent("GAS", "GAS Tool 의 GameplayCue 탭에서 열기"), EditorStyles.miniButtonMid, GUILayout.Width(36.0f)) == true)
                {
                    GameplayCueTab.Open(current);
                }

                if (GUILayout.Button("◎", EditorStyles.miniButtonRight, GUILayout.Width(24.0f)) == true)
                {
                    EditorGUIUtility.PingObject(current);
                }
            }

            EditorGUILayout.EndHorizontal();

            if (next != selected)
            {
                if (next == 0)
                {
                    cueProp.objectReferenceValue = null;
                }
                else if (next == options.Count - 1)
                {
                    var shader = SkillCueLibrary.Shaders(InPhase).FirstOrDefault();
                    var created = SkillCueLibrary.CreateCue(InPhase, shader, $"GC_{Selected.name}_{InPhase}");
                    if (created != null)
                    {
                        cueProp.objectReferenceValue = created;
                    }
                }
                else if (next - 1 < cues.Count)
                {
                    cueProp.objectReferenceValue = cues[next - 1];
                }

                current = cueProp.objectReferenceValue as GameplayCueNotify;
                disposeCueEditor(index);
            }

            EditorGUILayout.PropertyField(slotProp.FindPropertyRelative(nameof(SkillCueSlot.StartOffset)), new GUIContent("시작 보정 (초)", "자동 시작 시각에 더한다 (시전 속도 1배)"));
            var durationProp = slotProp.FindPropertyRelative(nameof(SkillCueSlot.Duration));
            EditorGUILayout.PropertyField(durationProp, new GUIContent("지속 (초, 0 = 자동)", durationProp.tooltip));

            if (current is ShaderGameplayCueNotify shaderCue)
            {
                if (shaderCue.Shape != SkillCueTiming.ShapeOf(InPhase))
                {
                    EditorGUILayout.HelpBox($"이 큐의 모양은 {shaderCue.Shape} 입니다 — 이 구간({SkillCueTiming.ShapeOf(InPhase)})과 다른 모양으로 그려집니다.", MessageType.Warning);
                }

                if (GameplayCueEditorGUI.DrawShaderCue(shaderCue, cueStates[index], SkillCueLibrary.Shaders(InPhase), false) == true)
                {
                    Repaint();
                }
            }
            else if (current != null)
            {
                UnityEditor.Editor.CreateCachedEditor(current, null, ref cueGenericEditors[index]);
                if (cueGenericEditors[index] != null)
                {
                    cueGenericEditors[index].OnInspectorGUI();
                }
            }

            EditorGUI.indentLevel -= 1;
        }

        private static void pingFolder(string InPath)
        {
            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(InPath);
            if (folder != null)
            {
                EditorGUIUtility.PingObject(folder);
                Selection.activeObject = folder;
            }
            else
            {
                Debug.Log($"[Skill Ability Tool] 폴더가 아직 없습니다: {InPath}");
            }
        }

        private void disposeCueEditor(int InIndex)
        {
            cueStates[InIndex].Dispose();
            if (cueGenericEditors[InIndex] != null)
            {
                DestroyImmediate(cueGenericEditors[InIndex]);
                cueGenericEditors[InIndex] = null;
            }
        }

        private void disposeCueEditors()
        {
            for (int i = 0; i < cueStates.Length; ++i)
            {
                disposeCueEditor(i);
            }
        }

        // ------------------------------------------------------------------ timeline

        /// <summary>큐가 끝나는 가장 늦은 시각 (타임라인 길이).</summary>
        private static float cueTimelineEnd(SkillDefinition InSkill)
        {
            float last = 0.0f;
            foreach (var phase in SkillCueLibrary.Phases)
            {
                var slot = SkillCueTiming.Slot(InSkill, phase);
                if (slot == null || slot.IsSet == false)
                {
                    continue;
                }

                float start;
                float end;
                SkillCueTiming.Window(InSkill, phase, out start, out end);
                last = Mathf.Max(last, phase == ESkillCuePhase.PostCast ? end : end + slot.Cue.TailTime);
            }

            return last;
        }

        /// <summary>타임라인 아래 3줄: 시전 전 · 중 · 후 큐 구간.</summary>
        private static void drawCueLanes(Rect InRect, SkillDefinition InSkill, Func<float, float> InX)
        {
            float laneHeight = InRect.height / 3.0f;
            foreach (var phase in SkillCueLibrary.Phases)
            {
                var lane = new Rect(InRect.x, InRect.y + laneHeight * (int)phase, InRect.width, laneHeight - 1.0f);
                EditorGUI.DrawRect(lane, new Color(1.0f, 1.0f, 1.0f, 0.04f));
                var slot = SkillCueTiming.Slot(InSkill, phase);
                if (slot == null || slot.IsSet == false)
                {
                    continue;
                }

                float start;
                float end;
                SkillCueTiming.Window(InSkill, phase, out start, out end);
                var color = SkillCueLibrary.PhaseColor(phase);
                EditorGUI.DrawRect(new Rect(InX(start), lane.y, Mathf.Max(2.0f, InX(end) - InX(start)), lane.height), color);
                float tail = slot.Cue.TailTime;
                if (phase != ESkillCuePhase.PostCast && tail > 0.0f)
                {
                    color.a *= 0.35f;
                    EditorGUI.DrawRect(new Rect(InX(end), lane.y, Mathf.Max(1.0f, InX(end + tail) - InX(end)), lane.height), color);
                }
            }
        }
    }
}
