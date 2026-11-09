using System.Collections.Generic;
using DemoLA.Core;
using DemoLA.Effects;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor.Gas
{
    /// <summary>
    /// GAS Tool / StateTree Editor 가 함께 쓰는 IMGUI 조각: 태그 목록(칩 + 추가 메뉴), GE 한 줄 요약.
    /// (원본 GasEditorGUI 에서 CSV 테이블 팝업은 DemoLA 에 해당 테이블이 없어 뺐다.)
    /// </summary>
    public static class GasEditorGUI
    {
        /// <summary>
        /// 태그 목록: 현재 태그를 칩(✕로 제거)으로, "+" 로 ETag 를 골라 추가. 바뀐 배열은 InSet 으로 넘긴다
        /// (추가 메뉴는 OnGUI 밖에서 콜백되므로 반환값 대신 콜백을 쓴다).
        /// </summary>
        public static void TagList(GUIContent InLabel, ETag[] InTags, System.Action<ETag[]> InSet)
        {
            var tags = new List<ETag>(InTags != null ? InTags : new ETag[0]);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(InLabel);
            if (tags.Count == 0)
            {
                GUILayout.Label("(없음)", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
            }

            int remove = -1;
            for (int i = 0; i < tags.Count; ++i)
            {
                if (GUILayout.Button(tags[i] + "  ✕", EditorStyles.miniButton, GUILayout.ExpandWidth(false)) == true)
                {
                    remove = i;
                }
            }

            if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(22.0f)) == true)
            {
                var menu = new GenericMenu();
                foreach (ETag tag in System.Enum.GetValues(typeof(ETag)))
                {
                    if (tag == ETag.None || tags.Contains(tag) == true)
                    {
                        continue;
                    }

                    var captured = tag;
                    menu.AddItem(new GUIContent(tag.ToString().Replace('_', '/')), false, () =>
                    {
                        var added = new List<ETag>(tags);
                        added.Add(captured);
                        InSet(added.ToArray());
                    });
                }

                menu.ShowAsContext();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (remove >= 0)
            {
                tags.RemoveAt(remove);
                InSet(tags.ToArray());
            }
        }

        /// <summary>읽기 전용 태그 칩 (코드가 정하는 태그 표시).</summary>
        public static void TagChips(GUIContent InLabel, ETag[] InTags)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(InLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                if (InTags == null || InTags.Length == 0)
                {
                    GUILayout.Label("(없음)", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                }
                else
                {
                    foreach (var tag in InTags)
                    {
                        GUILayout.Button(tag.ToString(), EditorStyles.miniButton, GUILayout.ExpandWidth(false));
                    }
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>GE 한 줄 요약: 지속 방식, 부여 태그, 첫 모디파이어.</summary>
        public static string EffectSummary(GameplayEffectAsset InEffect)
        {
            string duration = InEffect.DurationPolicy == Framework.Ability.EDurationPolicy.HasDuration ? $"{InEffect.Duration:0.##}s" : InEffect.DurationPolicy.ToString();
            var parts = new List<string> { duration };
            if (InEffect.GrantedTags != null && InEffect.GrantedTags.Length > 0)
            {
                parts.Add(string.Join("|", InEffect.GrantedTags));
            }

            if (InEffect.Modifiers != null && InEffect.Modifiers.Count > 0)
            {
                var m = InEffect.Modifiers[0];
                parts.Add($"{(m.IsSkillAttribute ? m.SkillAttribute.ToString() : m.Attribute.ToString())} {m.Op} {m.Value:0.##}");
            }

            if (InEffect.Execution != EExecution.None)
            {
                parts.Add(InEffect.Execution.ToString());
            }

            return string.Join(", ", parts);
        }
    }
}
