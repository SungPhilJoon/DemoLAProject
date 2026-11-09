using System;
using System.Collections.Generic;
using System.Linq;
using DemoLA.Core;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor
{
    /// <summary>
    /// [SerializeReference, SubclassPicker] 필드: 첫 줄에 구현 클래스 드롭다운, 그 아래에 고른 클래스의 필드를 그린다.
    /// 클래스를 바꾸면 이전 값은 버리고 새 인스턴스를 만든다 (Undo 가능).
    /// </summary>
    [CustomPropertyDrawer(typeof(SubclassPickerAttribute))]
    public class SubclassPickerDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty InProperty, GUIContent InLabel)
        {
            if (InProperty.propertyType != SerializedPropertyType.ManagedReference)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            return EditorGUI.GetPropertyHeight(InProperty, InLabel, true);
        }

        public override void OnGUI(Rect InPosition, SerializedProperty InProperty, GUIContent InLabel)
        {
            if (InProperty.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.LabelField(InPosition, InLabel.text, "[SubclassPicker] 는 [SerializeReference] 필드에만 쓴다");
                return;
            }

            EditorGUI.BeginProperty(InPosition, InLabel, InProperty);
            var line = new Rect(InPosition.x, InPosition.y, InPosition.width, EditorGUIUtility.singleLineHeight);
            var buttonRect = new Rect(line.x + EditorGUIUtility.labelWidth + 2.0f, line.y, line.width - EditorGUIUtility.labelWidth - 2.0f, line.height);
            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(CurrentLabel(InProperty)), FocusType.Keyboard) == true)
            {
                ShowMenu(InProperty, BaseTypeOf(InProperty));
            }

            // 기본 그리기: 접이식 라벨 + 자식 필드. 드롭다운을 먼저 그려야 같은 줄의 클릭을 드롭다운이 받는다.
            EditorGUI.PropertyField(InPosition, InProperty, new GUIContent(InLabel.text, InLabel.tooltip), true);
            EditorGUI.EndProperty();
        }

        /// <summary>현재 인스턴스의 표시 이름 ("(없음)" 포함).</summary>
        public static string CurrentLabel(SerializedProperty InProperty)
        {
            string full = InProperty.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(full) == true)
            {
                return "(없음 — 기본 동작)";
            }

            var type = TypeOf(full);
            return type != null ? DisplayName(type) : full;
        }

        public static Type BaseTypeOf(SerializedProperty InProperty)
        {
            return TypeOf(InProperty.managedReferenceFieldTypename);
        }

        public static IReadOnlyList<Type> ConcreteTypes(Type InBase)
        {
            if (InBase == null)
            {
                return Array.Empty<Type>();
            }

            return TypeCache.GetTypesDerivedFrom(InBase)
                .Where(t => t.IsAbstract == false && t.IsGenericType == false && t.IsDefined(typeof(SerializableAttribute), false) == true
                            && t.GetConstructor(Type.EmptyTypes) != null && typeof(UnityEngine.Object).IsAssignableFrom(t) == false)
                .OrderBy(t => t.Name)
                .ToList();
        }

        public static void ShowMenu(SerializedProperty InProperty, Type InBase)
        {
            var menu = new GenericMenu();
            var target = InProperty.serializedObject;
            string path = InProperty.propertyPath;
            string current = InProperty.managedReferenceFullTypename;
            menu.AddItem(new GUIContent("(없음 — 기본 동작)"), string.IsNullOrEmpty(current), () => Assign(target, path, null));
            menu.AddSeparator(string.Empty);
            foreach (var type in ConcreteTypes(InBase))
            {
                var captured = type;
                bool on = string.IsNullOrEmpty(current) == false && TypeOf(current) == type;
                menu.AddItem(new GUIContent(DisplayName(type)), on, () => Assign(target, path, Activator.CreateInstance(captured)));
            }

            menu.ShowAsContext();
        }

        private static void Assign(SerializedObject InTarget, string InPath, object InValue)
        {
            InTarget.Update();
            var property = InTarget.FindProperty(InPath);
            if (property == null)
            {
                return;
            }

            property.managedReferenceValue = InValue;
            InTarget.ApplyModifiedProperties();
        }

        /// <summary>"NearestSelector" → "Nearest". 클래스 설명은 툴에서 Describe() 로 보여 준다.</summary>
        public static string DisplayName(Type InType)
        {
            string name = InType.Name;
            return name.EndsWith("Selector", StringComparison.Ordinal) == true && name.Length > "Selector".Length ? name.Substring(0, name.Length - "Selector".Length) : name;
        }

        /// <summary>managedReference*Typename ("어셈블리 네임스페이스.클래스") → Type.</summary>
        public static Type TypeOf(string InTypename)
        {
            if (string.IsNullOrEmpty(InTypename) == true)
            {
                return null;
            }

            int split = InTypename.IndexOf(' ');
            if (split < 0)
            {
                return Type.GetType(InTypename);
            }

            string assembly = InTypename.Substring(0, split);
            string typeName = InTypename.Substring(split + 1).Replace('/', '+');
            return Type.GetType($"{typeName}, {assembly}");
        }
    }
}
