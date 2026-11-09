using System.Collections.Generic;
using DemoLA.Boss;
using DemoLA.Data;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor.Gas
{
    /// <summary>
    /// GAS Tool (DemoLA/Tools/GAS Tool) — ActionRPGProject 의 GAS Tool 을 가져와 DemoLA 데이터에 연결한 것.
    /// 언리얼 GAS 방식으로 어빌리티와 게임플레이 이펙트를 만든다.
    ///  [Ability]         보스 패턴(BossPatternDefinition = GameplayAbility 기본값) · 플레이어 스킬(SkillDefinition).
    ///                    Details 순서: 기본 → Tags → 대상 → 카운터 → ActivateAbility 단계(타격마다 카운터 여부) → 타임라인 → 무력화 체크 → 사용처
    ///  [GameplayEffect]  GameplayEffectAsset (지속 방식 · 모디파이어 · 부여 태그 · 실행 · 중첩)
    ///  [Ability Set]     보스가 가진 패턴 목록(페이즈별) · 직업이 가진 스킬 목록
    /// 어빌리티를 AI 가 언제 쓰는지는 StateTree Editor 에서 정한다 (UsePatternTask · SelectTargetTask, 패턴 ID 칸에서 바로 이 창을 연다).
    /// 원본과 다른 점: 데이터가 CSV 가 아니라 에셋이라 값은 바로 에셋에 기록되고 Undo 된다. "저장" 은 AssetDatabase.SaveAssets.
    /// 사용법: Documentation/md파일/사용법/GAS Tool (보스 패턴 · 카운터).md
    /// </summary>
    public partial class GasToolWindow : EditorWindow
    {
        private static readonly string[] Tabs = { "Ability", "GameplayEffect", "Ability Set" };
        private const int SetTab = 2;
        private const float ListWidth = 290.0f;

        private GameDatabase Database;
        [SerializeField] private int Tab;
        private Vector2 ListScroll;
        private Vector2 BodyScroll;
        private string Filter = string.Empty;

        [MenuItem("Window/DemoLA/GAS Tool")]
        [MenuItem("DemoLA/Tools/GAS Tool", priority = 200)]
        public static void Open()
        {
            GetWindow<GasToolWindow>("GAS Tool").minSize = new Vector2(900.0f, 500.0f);
        }

        /// <summary>다른 툴(StateTree Editor 의 패턴 ID 칸 · 컨텍스트 패널)에서 특정 보스 패턴을 열 때.</summary>
        public static void OpenPattern(string InPatternId)
        {
            Open();
            var window = GetWindow<GasToolWindow>();
            window.Tab = 0;
            window.SelectedPatternId = InPatternId;
            window.SelectedSkill = null;
            window.Focus();
        }

        /// <summary>보스의 Ability Set (패턴 목록) 을 열 때.</summary>
        public static void OpenAbilitySet(BossDefinition InBoss)
        {
            Open();
            var window = GetWindow<GasToolWindow>();
            window.Tab = SetTab;
            window.SelectedSetOwner = InBoss;
        }

        [MenuItem("CONTEXT/BossPatternDefinition/GAS Tool 에서 열기")]
        private static void OpenFromContext(MenuCommand InCommand)
        {
            var pattern = InCommand.context as BossPatternDefinition;
            if (pattern != null)
            {
                OpenPattern(pattern.PatternId);
            }
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying == true)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            if (Database == null)
            {
                Database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DemoLAProjectSetup.DatabasePath);
            }

            if (Database == null)
            {
                EditorGUILayout.HelpBox($"GameDatabase 에셋이 없습니다: {DemoLAProjectSetup.DatabasePath} — DemoLA/Setup/0. 전체 셋업 을 실행하세요.", MessageType.Error);
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            int tab = GUILayout.Toolbar(Tab, Tabs, EditorStyles.toolbarButton, GUILayout.Width(360.0f));
            if (tab != Tab)
            {
                Tab = tab;
                GUI.FocusControl(null);
            }

            if (Tab == 0 && GUILayout.Button("+ 새 Ability", EditorStyles.toolbarButton, GUILayout.Width(90.0f)) == true)
            {
                CreateAbilityWindow.Open(this);
            }

            GUILayout.FlexibleSpace();
            Filter = EditorGUILayout.TextField(Filter, EditorStyles.toolbarSearchField, GUILayout.Width(180.0f));
            if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(60.0f)) == true)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[GAS Tool] saved");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUIUtility.labelWidth = 190.0f;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(ListWidth));
            ListScroll = EditorGUILayout.BeginScrollView(ListScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
            switch (Tab)
            {
                case 0:
                    drawAbilityList();
                    break;
                case 1:
                    drawEffectList();
                    break;
                default:
                    drawSetList();
                    break;
            }

            EditorGUILayout.EndScrollView();
            if (Tab == 0)
            {
                drawCreateAbility();
            }
            else if (Tab == 1)
            {
                drawCreateEffect();
            }

            EditorGUILayout.EndVertical();

            BodyScroll = EditorGUILayout.BeginScrollView(BodyScroll);
            switch (Tab)
            {
                case 0:
                    drawAbilityEditor();
                    break;
                case 1:
                    drawEffectEditor();
                    break;
                default:
                    drawSetEditor();
                    break;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();
            EditorGUIUtility.labelWidth = 0.0f;
        }

        // ------------------------------------------------------------------ list helpers (원본 그대로)

        private GUIStyle ListStyle;
        private GUIStyle ListStyleDim;

        /// <summary>목록 버튼: 왼쪽 정렬 + 길면 잘라서 한 줄로 (전체 이름은 툴팁).</summary>
        private bool listButton(bool InSelected, string InLabel, bool InDim)
        {
            if (ListStyle == null)
            {
                ListStyle = new GUIStyle(EditorStyles.toolbarButton) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
                ListStyleDim = new GUIStyle(ListStyle) { fontStyle = FontStyle.Italic };
                ListStyleDim.normal.textColor = Color.gray;
            }

            const int maxLength = 40;
            string shown = InLabel.Length > maxLength ? InLabel.Substring(0, maxLength - 1) + "…" : InLabel;
            bool clicked = GUILayout.Toggle(InSelected, new GUIContent(shown, InLabel), InDim == true ? ListStyleDim : ListStyle, GUILayout.Width(ListWidth - 26.0f));
            return clicked == true && InSelected == false;
        }

        private bool matchesFilter(string InText)
        {
            return string.IsNullOrEmpty(Filter) == true || InText.IndexOf(Filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void listHeader(string InText)
        {
            EditorGUILayout.LabelField(InText, EditorStyles.miniBoldLabel);
        }

        /// <summary>InPatternId 를 가진 보스 · 페이즈 이름들.</summary>
        private List<string> patternOwners(string InPatternId)
        {
            var owners = new List<string>();
            foreach (var boss in Database.Bosses)
            {
                if (boss == null || boss.GetPattern(InPatternId) == null)
                {
                    continue;
                }

                var phases = new List<string>();
                foreach (var phase in boss.Phases)
                {
                    if (phase.PatternIds.Contains(InPatternId) == true)
                    {
                        phases.Add(phase.PhaseId);
                    }
                }

                owners.Add($"{boss.DisplayName} ({(phases.Count > 0 ? string.Join(", ", phases) : "페이즈 목록에 없음 — StateTree 로만 사용")})");
            }

            return owners;
        }
    }
}
