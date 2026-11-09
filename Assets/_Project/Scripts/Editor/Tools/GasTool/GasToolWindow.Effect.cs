using System.Collections.Generic;
using System.Linq;
using DemoLA.Attributes;
using DemoLA.Core;
using DemoLA.Effects;
using Framework.Ability;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor.Gas
{
    /// <summary>
    /// [GameplayEffect] 탭: GameplayEffectAsset 편집 (언리얼 UGameplayEffect 의 Duration / Modifiers / Granted Tags / Application Tags / Stacking / Execution).
    /// 새 GE 는 Data/Authored/Effects 에 만든다 (전체 셋업이 지우지 않는 폴더). Data/Generated 의 GE 는 셋업 시 코드 값으로 돌아간다.
    /// </summary>
    public partial class GasToolWindow
    {
        public const string AuthoredRoot = "Assets/_Project/Data/Authored";
        public const string EffectAssetFolder = AuthoredRoot + "/Effects";

        public enum EffectPreset
        {
            Buff = 0,
            Shield = 1,
            Invulnerable = 2,
            Debuff = 3,
            Empty = 4,
        }

        private static readonly string[] PresetLabels = { "피해 증가 버프 (+20%, 10초)", "보호막 표시 (Buff_Armor, 6초)", "무적 3초", "받는 피해 증가 디버프 (10초)", "빈 GE" };

        [SerializeField] private GameplayEffectAsset SelectedEffect;
        private bool ShowCreateEffect;
        private EffectPreset NewPreset = EffectPreset.Buff;
        private string NewEffectName = "GE_New";
        private List<GameplayEffectAsset> EffectCache;

        // ------------------------------------------------------------------ list

        private void drawEffectList()
        {
            if (EffectCache == null)
            {
                EffectCache = AssetDatabase.FindAssets("t:GameplayEffectAsset", new[] { "Assets/_Project" })
                    .Select(g => AssetDatabase.LoadAssetAtPath<GameplayEffectAsset>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(a => a != null).OrderBy(a => a.name).ToList();
            }

            foreach (var effect in EffectCache)
            {
                if (effect == null)
                {
                    continue;
                }

                string label = $"{effect.name} — {GasEditorGUI.EffectSummary(effect)}";
                if (matchesFilter(label) == false)
                {
                    continue;
                }

                bool generated = AssetDatabase.GetAssetPath(effect).Contains("/Generated/");
                if (listButton(effect == SelectedEffect, label, generated) == true)
                {
                    SelectedEffect = effect;
                    GUI.FocusControl(null);
                }
            }
        }

        private void drawCreateEffect()
        {
            ShowCreateEffect = EditorGUILayout.Foldout(ShowCreateEffect, "새 GameplayEffect", true);
            if (ShowCreateEffect == false)
            {
                return;
            }

            NewPreset = (EffectPreset)EditorGUILayout.Popup("프리셋", (int)NewPreset, PresetLabels);
            NewEffectName = EditorGUILayout.TextField("에셋 이름", NewEffectName);
            if (GUILayout.Button("만들기") == true)
            {
                SelectedEffect = CreateEffect(NewPreset, NewEffectName);
                EffectCache = null;
                ShowCreateEffect = false;
            }
        }

        /// <summary>프리셋으로 새 GE 에셋 (Data/Authored/Effects).</summary>
        public static GameplayEffectAsset CreateEffect(EffectPreset InPreset, string InName)
        {
            DemoLAProjectSetup.ensureFolder(EffectAssetFolder);
            var asset = CreateInstance<GameplayEffectAsset>();
            asset.DurationPolicy = EDurationPolicy.HasDuration;
            switch (InPreset)
            {
                case EffectPreset.Buff:
                    asset.Duration = 10.0f;
                    asset.Description = "피해 증가 +20%";
                    asset.Modifiers.Add(EffectModifierEntry.Char(EAttr.DamageIncrease, EModifierOp.Add, 20.0f));
                    break;
                case EffectPreset.Shield:
                    asset.Duration = 6.0f;
                    asset.Description = "보호막 표시 태그";
                    asset.GrantedTags = new[] { ETag.Buff_Armor };
                    break;
                case EffectPreset.Invulnerable:
                    asset.Duration = 3.0f;
                    asset.Description = "무적";
                    asset.GrantedTags = new[] { ETag.State_Invulnerable };
                    break;
                case EffectPreset.Debuff:
                    asset.Duration = 10.0f;
                    asset.Description = "받는 피해 +10%";
                    asset.Modifiers.Add(EffectModifierEntry.Char(EAttr.IncomingDamageAmplify, EModifierOp.Add, 10.0f));
                    break;
                default:
                    asset.Duration = 3.0f;
                    break;
            }

            string path = AssetDatabase.GenerateUniqueAssetPath($"{EffectAssetFolder}/{InName}.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        // ------------------------------------------------------------------ editor

        private void drawEffectEditor()
        {
            if (SelectedEffect == null)
            {
                EditorGUILayout.HelpBox("왼쪽에서 GameplayEffect 를 고르세요. GE = 지속 방식 + 모디파이어 + 부여 태그 + 실행 (언리얼 UGameplayEffect).\n" +
                                        "흐린 이름 = Data/Generated (전체 셋업 시 코드 값으로 돌아감).", MessageType.Info);
                return;
            }

            var so = serializedFor(SelectedEffect);
            so.Update();
            EditorGUILayout.LabelField(SelectedEffect.name, EditorStyles.largeLabel);
            if (AssetDatabase.GetAssetPath(SelectedEffect).Contains("/Generated/") == true)
            {
                EditorGUILayout.HelpBox("Data/Generated 의 GE 입니다. 전체 셋업(setup overwrite) 시 코드 값으로 돌아갑니다.", MessageType.Warning);
            }

            field(so, nameof(GameplayEffectAsset.Description), "설명");
            EditorGUILayout.LabelField("Duration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            field(so, nameof(GameplayEffectAsset.DurationPolicy), "Duration Policy");
            field(so, nameof(GameplayEffectAsset.Duration), "Duration (초)");
            field(so, nameof(GameplayEffectAsset.Period), "Period (초)", "0 = 주기 실행 없음");
            field(so, nameof(GameplayEffectAsset.ExecutePeriodicOnApply), "적용 즉시 1회 실행");
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("Modifiers · Execution", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            field(so, nameof(GameplayEffectAsset.Modifiers), "Modifiers");
            field(so, nameof(GameplayEffectAsset.Execution), "Execution");
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("Tags", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var effect = SelectedEffect;
            GasEditorGUI.TagList(new GUIContent("Granted Tags", "적용 중 대상에게 붙는 태그"), effect.GrantedTags, t => setEffectTags(effect, () => effect.GrantedTags = t));
            GasEditorGUI.TagList(new GUIContent("Application Required Tags"), effect.ApplicationRequiredTags, t => setEffectTags(effect, () => effect.ApplicationRequiredTags = t));
            GasEditorGUI.TagList(new GUIContent("Application Blocked Tags"), effect.ApplicationBlockedTags, t => setEffectTags(effect, () => effect.ApplicationBlockedTags = t));
            GasEditorGUI.TagList(new GUIContent("Remove Effects With Tags"), effect.RemoveEffectsWithTags, t => setEffectTags(effect, () => effect.RemoveEffectsWithTags = t));
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("Stacking · Cue", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            field(so, nameof(GameplayEffectAsset.StackPolicy), "Stack Policy");
            field(so, nameof(GameplayEffectAsset.MaxStack), "Max Stack");
            field(so, nameof(GameplayEffectAsset.Cue), "Gameplay Cue");
            EditorGUILayout.EndVertical();
            so.ApplyModifiedProperties();
        }

        private void setEffectTags(GameplayEffectAsset InEffect, System.Action InAssign)
        {
            Undo.RecordObject(InEffect, "GE Tags");
            InAssign();
            EditorUtility.SetDirty(InEffect);
            AbilitySerialized = null;
            Repaint();
        }
    }
}
