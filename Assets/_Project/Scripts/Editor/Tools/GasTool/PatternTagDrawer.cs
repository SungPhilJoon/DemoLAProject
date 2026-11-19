using System;
using System.Collections.Generic;
using DemoLA.Boss;
using DemoLA.Core;
using DemoLA.Data;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor.Gas
{
    /// <summary>
    /// [PatternTag] ETag: Pattern 하위 태그만 계층 드롭다운(G1/OrbStagger 이름 ◆ = 카운터 가능)으로 고르고 "GAS" 버튼으로 GAS Tool 을 연다.
    /// StateTree Editor 의 UsePatternTask · PatternReadyCondition, 패턴의 후속 패턴 목록 등에서 쓴다.
    /// </summary>
    [CustomPropertyDrawer(typeof(PatternTagAttribute))]
    public class PatternTagDrawer : PropertyDrawer
    {
        private static GameDatabase database;
        private static List<ETag> tags;
        private static GUIContent[] labels;
        private static double cacheTime = -10.0;

        public override void OnGUI(Rect InPosition, SerializedProperty InProperty, GUIContent InLabel)
        {
            if (InProperty.propertyType != SerializedPropertyType.Enum)
            {
                EditorGUI.PropertyField(InPosition, InProperty, InLabel);
                return;
            }

            EditorGUI.BeginProperty(InPosition, InLabel, InProperty);
            var current = (ETag)InProperty.intValue;
            int next = Popup(new Rect(InPosition.x, InPosition.y, InPosition.width - 46.0f, InPosition.height), InLabel, current);
            if (next != (int)current)
            {
                InProperty.intValue = next;
            }

            var buttonRect = new Rect(InPosition.xMax - 44.0f, InPosition.y, 44.0f, InPosition.height);
            using (new EditorGUI.DisabledScope(current == ETag.None))
            {
                if (GUI.Button(buttonRect, new GUIContent("GAS", "GAS Tool 에서 이 패턴 어빌리티 열기 (단계 · 카운터 · 대상)"), EditorStyles.miniButton) == true)
                {
                    DemoLAGasTool.OpenPattern(current);
                }
            }

            EditorGUI.EndProperty();
        }

        /// <summary>패턴 태그 드롭다운 (다른 툴에서도 쓴다). 반환 = 고른 태그 값.</summary>
        public static int Popup(Rect InRect, GUIContent InLabel, ETag InCurrent)
        {
            rebuild();
            int index = tags.IndexOf(InCurrent);
            var shown = labels;
            if (index < 0)
            {
                // Pattern 하위가 아닌 값이 들어 있으면 그대로 보여 준다.
                shown = new GUIContent[labels.Length + 1];
                Array.Copy(labels, shown, labels.Length);
                shown[labels.Length] = new GUIContent($"{GameTags.ToTagString(InCurrent)} (패턴 태그 아님)");
                index = labels.Length;
            }

            int picked = EditorGUI.Popup(InRect, InLabel, index, shown);
            return picked < tags.Count ? (int)tags[picked] : (int)InCurrent;
        }

        /// <summary>표시 이름: "Pattern.G1.OrbStagger 구슬 획득 후 무력화 ◆".</summary>
        public static string Describe(ETag InTag)
        {
            var pattern = FindPattern(InTag);
            string name = GameTags.ToTagString(InTag);
            return pattern != null ? $"{name}  {pattern.DisplayName}{(pattern.IsCounterable ? " ◆" : string.Empty)}" : name;
        }

        public static BossPatternDefinition FindPattern(ETag InTag)
        {
            loadDatabase();
            if (database == null)
            {
                return null;
            }

            foreach (var boss in database.Bosses)
            {
                var pattern = boss != null ? boss.GetPattern(InTag) : null;
                if (pattern != null)
                {
                    return pattern;
                }
            }

            return null;
        }

        private static void loadDatabase()
        {
            if (database == null)
            {
                database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DemoLAProjectSetup.DatabasePath);
            }
        }

        private static void rebuild()
        {
            // 패턴 이름 · 카운터 표시가 바뀔 수 있어 잠깐씩만 캐시한다.
            if (tags != null && EditorApplication.timeSinceStartup - cacheTime < 1.0)
            {
                return;
            }

            cacheTime = EditorApplication.timeSinceStartup;
            tags = new List<ETag> { ETag.None };
            var list = new List<GUIContent> { new GUIContent("None") };
            foreach (ETag tag in Enum.GetValues(typeof(ETag)))
            {
                if (GameTags.IsPattern(tag) == false)
                {
                    continue;
                }

                var pattern = FindPattern(tag);
                // 메뉴 경로: G1/OrbStagger  구슬 획득 후 무력화 ◆   (에셋이 없는 태그는 "(에셋 없음)")
                string path = GameTags.ToTagString(tag).Substring("Pattern.".Length).Replace('.', '/');
                string suffix = pattern != null ? $"  {pattern.DisplayName}{(pattern.IsCounterable ? " ◆" : string.Empty)}" : "  (에셋 없음)";
                tags.Add(tag);
                list.Add(new GUIContent(path + suffix));
            }

            labels = list.ToArray();
        }
    }
}
