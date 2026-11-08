using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Framework.Testing.Editor
{
    /// <summary>
    /// [SubclassSelector] 가 붙은 [SerializeReference] 필드 · 목록 원소의 형식 선택 드롭다운.
    ///  - 첫 줄: 형식 드롭다운 (없음 + 기반 형식을 상속한 모든 비추상 [Serializable] 클래스 — 다른 어셈블리의 프로젝트 단계도 자동으로 나온다)
    ///  - 아래: 고른 형식의 필드를 펼쳐 편집
    ///  - 목록에서 "+" 로 원소를 늘리면 Unity 가 앞 원소의 참조를 공유하는 경우가 있어, 바로 앞 원소와 같은 참조면 복사본으로 바꾼다.
    /// </summary>
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, Type[]> Cache = new Dictionary<Type, Type[]>();
        private static readonly Regex ElementIndex = new Regex(@"\.Array\.data\[(\d+)\]$");

        public override float GetPropertyHeight(SerializedProperty InProperty, GUIContent InLabel)
        {
            if (InProperty.propertyType != SerializedPropertyType.ManagedReference)
            {
                return EditorGUI.GetPropertyHeight(InProperty, InLabel, true);
            }

            float height = EditorGUIUtility.singleLineHeight;
            if (InProperty.managedReferenceValue != null && InProperty.isExpanded == true)
            {
                height += EditorGUI.GetPropertyHeight(InProperty, InLabel, true) - EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            return height;
        }

        public override void OnGUI(Rect InPosition, SerializedProperty InProperty, GUIContent InLabel)
        {
            if (InProperty.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(InPosition, InProperty, InLabel, true);
                return;
            }

            fixSharedReference(InProperty);

            var baseType = elementType(fieldInfo.FieldType);
            var types = derivedTypes(baseType);
            var current = InProperty.managedReferenceValue != null ? InProperty.managedReferenceValue.GetType() : null;
            int index = current != null ? Array.IndexOf(types, current) + 1 : 0;
            var names = new[] { "(없음)" }.Concat(types.Select(niceName)).ToArray();

            var line = new Rect(InPosition.x, InPosition.y, InPosition.width, EditorGUIUtility.singleLineHeight);
            var foldoutRect = new Rect(line.x, line.y, EditorGUIUtility.labelWidth, line.height);
            var popupRect = new Rect(line.x + EditorGUIUtility.labelWidth, line.y, line.width - EditorGUIUtility.labelWidth, line.height);
            string title = InLabel.text;
            if (InProperty.managedReferenceValue is ScenarioStep step)
            {
                title = $"{InLabel.text}: {step.Describe()}";
            }

            if (current != null)
            {
                InProperty.isExpanded = EditorGUI.Foldout(foldoutRect, InProperty.isExpanded, title, true);
            }
            else
            {
                EditorGUI.LabelField(foldoutRect, title);
            }

            int chosen = EditorGUI.Popup(popupRect, index, names);
            if (chosen != index)
            {
                InProperty.managedReferenceValue = chosen == 0 ? null : Activator.CreateInstance(types[chosen - 1]);
                InProperty.isExpanded = true;
                InProperty.serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }

            if (InProperty.managedReferenceValue == null || InProperty.isExpanded == false)
            {
                return;
            }

            // 자식 필드를 한 단계 들여 그린다.
            EditorGUI.indentLevel += 1;
            float y = line.yMax + EditorGUIUtility.standardVerticalSpacing;
            var end = InProperty.GetEndProperty();
            var child = InProperty.Copy();
            bool enter = true;
            while (child.NextVisible(enter) && SerializedProperty.EqualContents(child, end) == false)
            {
                enter = false;
                float h = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(new Rect(InPosition.x, y, InPosition.width, h), child, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
            }

            EditorGUI.indentLevel -= 1;
        }

        /// <summary>목록 원소가 바로 앞 원소와 같은 참조를 공유하면 복사본으로 바꾼다.</summary>
        private static void fixSharedReference(SerializedProperty InProperty)
        {
            var match = ElementIndex.Match(InProperty.propertyPath);
            if (match.Success == false || InProperty.managedReferenceValue == null)
            {
                return;
            }

            int index = int.Parse(match.Groups[1].Value);
            if (index == 0)
            {
                return;
            }

            string previousPath = InProperty.propertyPath.Substring(0, match.Index) + $".Array.data[{index - 1}]";
            var previous = InProperty.serializedObject.FindProperty(previousPath);
            if (previous == null || previous.propertyType != SerializedPropertyType.ManagedReference)
            {
                return;
            }

            if (previous.managedReferenceId == InProperty.managedReferenceId)
            {
                var value = InProperty.managedReferenceValue;
                InProperty.managedReferenceValue = JsonUtility.FromJson(JsonUtility.ToJson(value), value.GetType());
                InProperty.serializedObject.ApplyModifiedProperties();
            }
        }

        private static Type elementType(Type InFieldType)
        {
            if (InFieldType.IsArray == true)
            {
                return InFieldType.GetElementType();
            }

            if (InFieldType.IsGenericType == true && InFieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return InFieldType.GetGenericArguments()[0];
            }

            return InFieldType;
        }

        private static Type[] derivedTypes(Type InBase)
        {
            Type[] types;
            if (Cache.TryGetValue(InBase, out types) == true)
            {
                return types;
            }

            types = TypeCache.GetTypesDerivedFrom(InBase)
                .Where(t => t.IsAbstract == false && t.IsGenericType == false && t.IsDefined(typeof(SerializableAttribute), false) && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Namespace).ThenBy(t => t.Name)
                .ToArray();
            Cache[InBase] = types;
            return types;
        }

        private static string niceName(Type InType)
        {
            string ns = InType.Namespace != null && InType.Namespace.StartsWith("Framework") ? "공통" : (InType.Namespace ?? string.Empty);
            return $"{ns}/{ObjectNames.NicifyVariableName(InType.Name)}";
        }
    }
}
