using System.Collections.Generic;
using DemoLA.Boss;
using DemoLA.Data;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor.Gas
{
    /// <summary>
    /// [PatternId] 문자열: 모든 보스 패턴 드롭다운(◆ = 카운터 가능한 타격 있음) + "GAS" 버튼.
    /// StateTree Editor 의 UsePatternTask · PatternReadyCondition 등에서 패턴을 고르고 바로 GAS Tool 로 넘어가는 연결 고리.
    /// </summary>
    [CustomPropertyDrawer(typeof(PatternIdAttribute))]
    public class PatternIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect InPosition, SerializedProperty InProperty, GUIContent InLabel)
        {
            if (InProperty.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(InPosition, InProperty, InLabel);
                return;
            }

            EditorGUI.BeginProperty(InPosition, InLabel, InProperty);
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DemoLAProjectSetup.DatabasePath);
            var ids = new List<string>();
            var labels = new List<GUIContent>();
            if (database != null)
            {
                foreach (var boss in database.Bosses)
                {
                    if (boss == null)
                    {
                        continue;
                    }

                    foreach (var pattern in boss.Patterns)
                    {
                        if (pattern == null)
                        {
                            continue;
                        }

                        ids.Add(pattern.PatternId);
                        labels.Add(new GUIContent($"{boss.BossId}/{pattern.PatternId} {pattern.DisplayName}{(pattern.IsCounterable ? " ◆" : string.Empty)}"));
                    }
                }
            }

            string current = InProperty.stringValue;
            int index = ids.IndexOf(current);
            if (index < 0)
            {
                ids.Add(current);
                labels.Add(new GUIContent($"{current} (없는 패턴)"));
                index = ids.Count - 1;
            }

            var popupRect = new Rect(InPosition.x, InPosition.y, InPosition.width - 46.0f, InPosition.height);
            var buttonRect = new Rect(InPosition.xMax - 44.0f, InPosition.y, 44.0f, InPosition.height);
            int next = EditorGUI.Popup(popupRect, InLabel, index, labels.ToArray());
            if (next != index)
            {
                InProperty.stringValue = ids[next];
            }

            if (GUI.Button(buttonRect, new GUIContent("GAS", "GAS Tool 에서 이 패턴 어빌리티 열기 (단계 · 카운터 · 대상)"), EditorStyles.miniButton) == true)
            {
                GasToolWindow.OpenPattern(InProperty.stringValue);
            }

            EditorGUI.EndProperty();
        }
    }
}
