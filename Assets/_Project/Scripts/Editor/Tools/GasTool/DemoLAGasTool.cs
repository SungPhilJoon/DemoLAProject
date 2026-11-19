using System.Collections.Generic;
using DemoLA.Boss;
using DemoLA.Core;
using DemoLA.Data;
using Framework.Editor;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor.Gas
{
    /// <summary>
    /// DemoLA 의 GAS Tool 탭들 — 프레임워크 GAS Tool(Framework.Editor.GasToolWindow) 에 붙는다.
    /// 언리얼 GAS 방식으로 어빌리티와 게임플레이 이펙트를 만든다.
    ///  [Ability]         보스 패턴(BossPatternDefinition = GameplayAbility 기본값) · 플레이어 스킬(SkillDefinition).
    ///                    Details 순서: 기본 → Tags → 대상 → 카운터 → ActivateAbility 단계(타격마다 카운터 여부) → 타임라인 → 무력화 체크 → 사용처
    ///  [GameplayEffect]  GameplayEffectAsset (지속 방식 · 모디파이어 · 부여 태그 · 실행 · 중첩)
    ///  [Ability Set]     보스가 가진 패턴 목록(페이즈별) · 직업이 가진 스킬 목록
    ///  (프레임워크 탭)   GameplayTag (ETag 계층) · GameplayCue (스킬 셰이더 큐 등)
    /// 어빌리티를 AI 가 언제 쓰는지는 StateTree Editor 에서 정한다 (UsePatternTask · SelectTargetTask, 패턴 ID 칸에서 바로 이 창을 연다).
    /// 값은 바로 에셋에 기록되고 Undo 된다. "저장" 은 AssetDatabase.SaveAssets.
    /// 이 클래스는 세 탭이 같이 쓰는 상태(데이터베이스 · 선택)이고, 다른 툴은 Open · OpenPattern · OpenAbilitySet 으로 연다.
    /// 사용법: Documentation/md파일/사용법/GAS Tool (보스 패턴 · 카운터).md
    /// </summary>
    public partial class DemoLAGasTool
    {
        private const float ListWidth = 290.0f;
        private static DemoLAGasTool instance;

        private GameDatabase Database;
        private GasToolTab activeTab;

        public static DemoLAGasTool Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DemoLAGasTool();
                }

                return instance;
            }
        }

        private string Filter
        {
            get { return activeTab != null && activeTab.Window != null ? activeTab.Window.Filter : string.Empty; }
        }

        [MenuItem("Window/DemoLA/GAS Tool")]
        [MenuItem("DemoLA/Tools/GAS Tool", priority = 200)]
        public static void Open()
        {
            GasToolWindow.OpenTab<GasAbilityTab>();
        }

        /// <summary>다른 툴(StateTree Editor 의 패턴 ID 칸 · 컨텍스트 패널)에서 특정 보스 패턴을 열 때.</summary>
        public static void OpenPattern(ETag InPatternTag)
        {
            GasToolWindow.OpenTab<GasAbilityTab>();
            Instance.SelectedPatternTag = InPatternTag;
            Instance.SelectedSkill = null;
        }

        /// <summary>보스의 Ability Set (패턴 목록) 을 열 때.</summary>
        public static void OpenAbilitySet(BossDefinition InBoss)
        {
            GasToolWindow.OpenTab<GasAbilitySetTab>();
            Instance.SelectedSetOwner = InBoss;
        }

        [MenuItem("CONTEXT/BossPatternDefinition/GAS Tool 에서 열기")]
        private static void OpenFromContext(MenuCommand InCommand)
        {
            var pattern = InCommand.context as BossPatternDefinition;
            if (pattern != null)
            {
                OpenPattern(pattern.PatternTag);
            }
        }

        /// <summary>탭이 그리기 전에 부른다 — 데이터베이스를 읽고, 없으면 오류 문구를 돌려준다.</summary>
        internal string Prepare(GasToolTab InTab)
        {
            activeTab = InTab;
            if (Database == null)
            {
                Database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DemoLAProjectSetup.DatabasePath);
            }

            return Database == null ? $"GameDatabase 에셋이 없습니다: {DemoLAProjectSetup.DatabasePath} — DemoLA/Setup/0. 전체 셋업 을 실행하세요." : null;
        }

        private void repaint()
        {
            if (activeTab != null)
            {
                activeTab.Repaint();
            }
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
            string filter = Filter;
            return string.IsNullOrEmpty(filter) == true || InText.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void listHeader(string InText)
        {
            EditorGUILayout.LabelField(InText, EditorStyles.miniBoldLabel);
        }

        /// <summary>InPatternTag 를 가진 보스 · 페이즈 이름들.</summary>
        private List<string> patternOwners(ETag InPatternTag)
        {
            var owners = new List<string>();
            foreach (var boss in Database.Bosses)
            {
                if (boss == null || boss.GetPattern(InPatternTag) == null)
                {
                    continue;
                }

                var phases = new List<string>();
                foreach (var phase in boss.Phases)
                {
                    if (phase.PatternTags.Contains(InPatternTag) == true)
                    {
                        phases.Add(phase.PhaseId);
                    }
                }

                owners.Add($"{boss.DisplayName} ({(phases.Count > 0 ? string.Join(", ", phases) : "페이즈 목록에 없음 — StateTree 로만 사용")})");
            }

            return owners;
        }
    }

    /// <summary>DemoLA 탭 공통: 공유 상태(DemoLAGasTool) 준비.</summary>
    public abstract class DemoLAGasTab : GasToolTab
    {
        protected DemoLAGasTool Gas
        {
            get { return DemoLAGasTool.Instance; }
        }

        public override string Unavailable
        {
            get { return Gas.Prepare(this); }
        }

        public override void OnEnable()
        {
            Undo.undoRedoPerformed += Repaint;
        }

        public override void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
        }
    }

    /// <summary>[Ability] 보스 패턴 · 플레이어 스킬.</summary>
    public class GasAbilityTab : DemoLAGasTab
    {
        public override string Title
        {
            get { return "Ability"; }
        }

        public override int Order
        {
            get { return 0; }
        }

        public override void DrawToolbar()
        {
            if (GUILayout.Button("+ 새 Ability", EditorStyles.toolbarButton, GUILayout.Width(90.0f)) == true)
            {
                CreateAbilityWindow.Open(Gas);
            }
        }

        public override void DrawList()
        {
            Gas.drawAbilityList();
        }

        public override void DrawListFooter()
        {
            Gas.drawCreateAbility();
        }

        public override void DrawBody()
        {
            Gas.drawAbilityEditor();
        }
    }

    /// <summary>[GameplayEffect] GameplayEffectAsset.</summary>
    public class GasEffectTab : DemoLAGasTab
    {
        public override string Title
        {
            get { return "GameplayEffect"; }
        }

        public override int Order
        {
            get { return 10; }
        }

        public override void DrawList()
        {
            Gas.drawEffectList();
        }

        public override void DrawListFooter()
        {
            Gas.drawCreateEffect();
        }

        public override void DrawBody()
        {
            Gas.drawEffectEditor();
        }
    }

    /// <summary>[Ability Set] 보스 패턴 목록 · 직업 스킬 목록.</summary>
    public class GasAbilitySetTab : DemoLAGasTab
    {
        public override string Title
        {
            get { return "Ability Set"; }
        }

        public override int Order
        {
            get { return 20; }
        }

        public override void DrawList()
        {
            Gas.drawSetList();
        }

        public override void DrawBody()
        {
            Gas.drawSetEditor();
        }
    }
}
