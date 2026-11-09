using System;
using System.Collections.Generic;
using System.Linq;
using DemoLA.AI;
using DemoLA.Boss;
using DemoLA.Core;
using DemoLA.Skills;
using Framework.StateTree;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor.Gas
{
    /// <summary>
    /// [Ability] 탭. 언리얼 GameplayAbility 의 Details 패널 순서로 편집한다 (원본 GAS Tool 과 같은 순서).
    ///  보스 패턴: 기본 → Tags → 대상 → 카운터(패턴 공통) → ActivateAbility 단계 → 타임라인 → 무력화 체크 → 사용처
    ///  플레이어 스킬: 기본 + 모든 필드 (SkillDefinition)
    /// 패턴을 이 탭에서 고치면 ToolEdited 가 켜져 CSV 재가져오기 · 전체 셋업이 실행 데이터를 덮어쓰지 않는다.
    /// </summary>
    public partial class GasToolWindow
    {
        private const float TimelineHeight = 58.0f;
        private static readonly Color TelegraphColor = new Color(0.95f, 0.6f, 0.2f, 0.55f);
        private static readonly Color HitColor = new Color(1.0f, 0.35f, 0.3f);
        private static readonly Color CounterColor = new Color(0.35f, 0.65f, 1.0f, 0.6f);

        [SerializeField] private string SelectedPatternId;
        private SkillDefinition SelectedSkill;
        private SerializedObject AbilitySerialized;
        private UnityEngine.Object AbilityTarget;
        private bool ShowSkills;

        // ------------------------------------------------------------------ list

        private void drawAbilityList()
        {
            foreach (var boss in Database.Bosses)
            {
                if (boss == null)
                {
                    continue;
                }

                listHeader($"보스 패턴 — {boss.DisplayName}");
                foreach (var pattern in boss.Patterns.Where(p => p != null).OrderBy(p => p.PatternId))
                {
                    string mark = pattern.IsCounterable == true ? " ◆" : string.Empty;
                    string label = $"[{pattern.PatternId}] {pattern.DisplayName}{mark}";
                    if (matchesFilter(label) == false)
                    {
                        continue;
                    }

                    if (listButton(SelectedSkill == null && pattern.PatternId == SelectedPatternId, label, pattern.Implemented == false) == true)
                    {
                        SelectedPatternId = pattern.PatternId;
                        SelectedSkill = null;
                        GUI.FocusControl(null);
                    }
                }
            }

            ShowSkills = EditorGUILayout.Foldout(ShowSkills, "플레이어 스킬 (SkillDefinition)", true);
            if (ShowSkills == false)
            {
                return;
            }

            foreach (var cls in Database.Classes)
            {
                if (cls == null)
                {
                    continue;
                }

                listHeader(cls.DisplayName);
                foreach (var skill in cls.Skills.Where(s => s != null))
                {
                    string label = $"[{skill.SkillId}] {skill.DisplayName}";
                    if (matchesFilter(label) == true && listButton(SelectedSkill == skill, label, false) == true)
                    {
                        SelectedSkill = skill;
                        GUI.FocusControl(null);
                    }
                }
            }
        }

        /// <summary>목록 아래 "새 Ability" 버튼 → 별도 창(CreateAbilityWindow)에서 템플릿 · 이름을 고른다.</summary>
        private void drawCreateAbility()
        {
            if (GUILayout.Button("+ 새 Ability…", GUILayout.Height(28.0f)) == true)
            {
                CreateAbilityWindow.Open(this);
            }
        }

        private BossPatternDefinition selectedPattern(out BossDefinition OutBoss)
        {
            OutBoss = null;
            if (string.IsNullOrEmpty(SelectedPatternId) == true)
            {
                return null;
            }

            foreach (var boss in Database.Bosses)
            {
                var pattern = boss != null ? boss.GetPattern(SelectedPatternId) : null;
                if (pattern != null)
                {
                    OutBoss = boss;
                    return pattern;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ editor

        private void drawAbilityEditor()
        {
            if (SelectedSkill != null)
            {
                drawSkillEditor(SelectedSkill);
                return;
            }

            BossDefinition boss;
            var pattern = selectedPattern(out boss);
            if (pattern == null)
            {
                EditorGUILayout.HelpBox("왼쪽에서 어빌리티를 고르거나 '새 Ability' 로 만드세요.\n" +
                                        "어빌리티 = 태그 규칙 + 쿨다운 + 실행 단계 (언리얼 GameplayAbility). 보스 패턴은 단계(타격)마다 장판 · 피해 · 카운터 여부를 정합니다.", MessageType.Info);
                return;
            }

            var so = serializedFor(pattern);
            so.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField($"[{pattern.PatternId}] {pattern.DisplayName}", EditorStyles.largeLabel);
            if (pattern.ToolEdited == true)
            {
                EditorGUILayout.LabelField("GAS Tool 편집됨 — 실행 데이터는 이 에셋이 원본 (CSV 재가져오기는 이름 · 분류 · 메모만 갱신)", EditorStyles.miniLabel);
            }

            drawPatternBasics(so, pattern);
            EditorGUILayout.Space();
            drawPatternTags(so, pattern);
            EditorGUILayout.Space();
            drawPatternTarget(so, pattern);
            EditorGUILayout.Space();
            drawPatternCounter(so, pattern);
            EditorGUILayout.Space();
            drawPatternSteps(so, pattern);
            EditorGUILayout.Space();
            drawPatternTimeline(pattern);
            EditorGUILayout.Space();
            drawStagger(so);
            EditorGUILayout.Space();
            drawPatternUsers(pattern, boss);

            if (EditorGUI.EndChangeCheck() == true)
            {
                so.FindProperty(nameof(BossPatternDefinition.ToolEdited)).boolValue = true;
            }

            so.ApplyModifiedProperties();
        }

        private SerializedObject serializedFor(UnityEngine.Object InTarget)
        {
            if (AbilitySerialized == null || AbilityTarget != InTarget || AbilitySerialized.targetObject == null)
            {
                AbilitySerialized = new SerializedObject(InTarget);
                AbilityTarget = InTarget;
            }

            return AbilitySerialized;
        }

        private static void field(SerializedObject InSo, string InName, string InLabel, string InTip = null)
        {
            EditorGUILayout.PropertyField(InSo.FindProperty(InName), new GUIContent(InLabel, InTip), true);
        }

        /// <summary>패턴 목록(CSV) 값 + 선택(쿨다운 · 가중치 · 거리) — AbilityTable 행에 해당.</summary>
        private static void drawPatternBasics(SerializedObject InSo, BossPatternDefinition InPattern)
        {
            EditorGUILayout.LabelField("기본 (패턴 목록 CSV · 선택 규칙)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(new GUIContent("이름 · 분류 · 대응", "CSV(BossPatternTable.csv) 가 원본"), $"{InPattern.DisplayName} · {InPattern.Category} · {InPattern.Response}");
                EditorGUILayout.TextField("페이즈", InPattern.PhaseId);
            }

            field(InSo, nameof(BossPatternDefinition.Cooldown), "쿨다운 (초)", "커밋 시 쿨다운 GE 로 적용");
            field(InSo, nameof(BossPatternDefinition.Weight), "랜덤 가중치", "UseRandomPatternTask 가 고를 확률 비중");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("AI 사용 거리 (m)", "대상과의 거리가 이 범위일 때만 랜덤 선택 후보"));
                var min = InSo.FindProperty(nameof(BossPatternDefinition.MinRange));
                var max = InSo.FindProperty(nameof(BossPatternDefinition.MaxRange));
                min.floatValue = EditorGUILayout.FloatField(min.floatValue, GUILayout.Width(60));
                GUILayout.Label("~", GUILayout.Width(12));
                max.floatValue = EditorGUILayout.FloatField(max.floatValue, GUILayout.Width(60));
                GUILayout.FlexibleSpace();
            }

            field(InSo, nameof(BossPatternDefinition.RandomSelectable), "랜덤 선택 대상", "끄면 StateTree 의 UsePatternTask 로만 쓴다 (기믹 · 연출 패턴)");
            field(InSo, nameof(BossPatternDefinition.Recovery), "후딜 (초)");
            field(InSo, nameof(BossPatternDefinition.Implemented), "구현 완료");
            EditorGUILayout.EndVertical();
        }

        /// <summary>언리얼 GameplayAbility 의 Tags 카테고리. 패턴 어빌리티의 태그는 BossPatternAbility 생성자가 정한다.</summary>
        private static void drawPatternTags(SerializedObject InSo, BossPatternDefinition InPattern)
        {
            EditorGUILayout.LabelField("Tags (발동 규칙)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GasEditorGUI.TagChips(new GUIContent("Ability Tags", "모든 보스 패턴 공통"), new[] { ETag.Ability_BossPattern });
            GasEditorGUI.TagChips(new GUIContent("Block Abilities With Tag", "실행 중 다른 패턴 발동을 막음"), new[] { ETag.Ability_BossPattern });
            var owned = new List<ETag> { ETag.Boss_PatternBusy };
            if (InPattern.SuperArmor == true)
            {
                owned.Add(ETag.State_SuperArmor);
            }

            if (InPattern.UntargetableDuring == true)
            {
                owned.Add(ETag.Boss_Untargetable);
            }

            GasEditorGUI.TagChips(new GUIContent("Activation Owned Tags", "실행 중 보스에게 붙는 태그. 아래 두 칸으로 켜고 끈다"), owned.ToArray());
            EditorGUI.indentLevel += 1;
            field(InSo, nameof(BossPatternDefinition.SuperArmor), "State_SuperArmor (경직 면역)");
            field(InSo, nameof(BossPatternDefinition.UntargetableDuring), "Boss_Untargetable (대상 지정 불가)");
            EditorGUI.indentLevel -= 1;
            GasEditorGUI.TagChips(new GUIContent("Activation Blocked Tags", "이 중 하나라도 있으면 발동 불가 (무력화 · 기절 등)"), GameTags.IncapacitatedTags);
            if (InPattern.IsCounterable == true)
            {
                GasEditorGUI.TagChips(new GUIContent("구간 태그 (카운터)", "카운터 가능한 타격의 구간 동안 붙는다 — 아래 단계에서 정함"), new[] { ETag.Boss_CounterWindow });
            }

            EditorGUILayout.EndVertical();
        }

        private static void drawPatternTarget(SerializedObject InSo, BossPatternDefinition InPattern)
        {
            EditorGUILayout.LabelField("대상 (누구를 노리는가)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            field(InSo, nameof(BossPatternDefinition.TargetSelector), "기본 대상 규칙", "StateTree SelectTargetTask 가 지정하지 않을 때. 비우면 현재 어그로 대상");
            EditorGUILayout.LabelField("우선순위: 도발 > StateTree 지정(SelectTargetTask) > 이 규칙 > 어그로 · 단계 Targeting 이 AggroTarget / BossTowardAggro 인 타격에만 적용", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private static void drawPatternCounter(SerializedObject InSo, BossPatternDefinition InPattern)
        {
            EditorGUILayout.LabelField("카운터 (패턴 공통)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            int counterSteps = InPattern.Steps.Count(s => s != null && s.Counterable);
            EditorGUILayout.LabelField(counterSteps > 0
                ? $"카운터 가능한 타격 {counterSteps} / {InPattern.Steps.Count} — 아래 단계에서 타격마다 켜고 끈다"
                : "카운터 가능한 타격 없음 — 아래 단계의 '카운터 가능' 을 켜면 그 타격의 전조 중 카운터할 수 있다", EditorStyles.wordWrappedMiniLabel);
            field(InSo, nameof(BossPatternDefinition.GroggyOnCounter), "성공 시 그로기 (초)", "카운터에 성공하면 패턴 취소 + 이 시간 무력화");
            field(InSo, nameof(BossPatternDefinition.CounterRepeat), "연속 회차", "3연 카운터 = 3. 회차마다 단계 전체 반복 (카운터 가능한 패턴만)");
            field(InSo, nameof(BossPatternDefinition.FailEscalationFraction), "실패당 피해 +", "카운터 구간을 놓칠 때마다 이후 타격 피해 비율(최대 체력 대비) 증가");
            if (InPattern.Counterable == true && counterSteps == 0)
            {
                EditorGUILayout.HelpBox("이전 방식(패턴 단위 구간)으로 카운터가 켜져 있습니다. 타격에 카운터를 켜면 이 값은 무시됩니다.", MessageType.Info);
                field(InSo, nameof(BossPatternDefinition.Counterable), "패턴 단위 카운터 (이전 방식)");
                field(InSo, nameof(BossPatternDefinition.CounterWindowStart), "구간 시작 (회차 기준)");
                field(InSo, nameof(BossPatternDefinition.CounterWindowEnd), "구간 끝 (회차 기준)");
            }

            EditorGUILayout.EndVertical();
        }

        // ------------------------------------------------------------------ steps

        private void drawPatternSteps(SerializedObject InSo, BossPatternDefinition InPattern)
        {
            var list = InSo.FindProperty(nameof(BossPatternDefinition.Steps));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"ActivateAbility 단계 ({list.arraySize}) — 위에서부터 실행, 단계마다 [대기 → 전조 장판 → 판정]", EditorStyles.boldLabel);
            if (GUILayout.Button("+ 단계", GUILayout.Width(70.0f)) == true)
            {
                showStepMenu(InPattern);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("다단 공격은 단계를 여러 개 둔다. ◆ = 이 타격의 전조 중 카운터 가능 (보스 몸이 파랗게 빛남)", EditorStyles.miniLabel);

            float cursor = 0.0f;
            for (int i = 0; i < list.arraySize; ++i)
            {
                var element = list.GetArrayElementAtIndex(i);
                var step = i < InPattern.Steps.Count ? InPattern.Steps[i] : null;
                float hit = 0.0f;
                float end;
                float telegraph = 0.0f;
                if (step != null)
                {
                    telegraph = InPattern.StepTiming(i, ref cursor, out hit, out end);
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                string title = step != null ? $"{i + 1}. [{telegraph:0.##}s → 판정 {hit:0.##}s] {describe(step)}" : $"{i + 1}.";
                element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, title, true);
                var counterable = element.FindPropertyRelative(nameof(PatternStep.Counterable));
                counterable.boolValue = GUILayout.Toggle(counterable.boolValue, new GUIContent(" ◆ 카운터", "이 타격의 전조 중 카운터 가능"), EditorStyles.miniButton, GUILayout.Width(70.0f));
                if (stepButtons(list, i) == true)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();
                if (counterable.boolValue == true && step != null)
                {
                    drawStepCounter(element, step);
                }

                if (element.isExpanded == true)
                {
                    drawStepFields(element);
                }

                EditorGUILayout.EndVertical();
            }
        }

        private static string describe(PatternStep InStep)
        {
            string area = InStep.Area.Shape.ToString();
            string offset = InStep.ForwardOffset > 0.0f ? $" · 앞 {InStep.ForwardOffset:0.#}m" : (InStep.ForwardOffset < 0.0f ? $" · 뒤 {-InStep.ForwardOffset:0.#}m" : string.Empty);
            string counter = InStep.Counterable == true ? $" · ◆ {InStep.CounterWindowStart:0.##}~{InStep.CounterWindowEnd:0.##}s" : string.Empty;
            return $"{InStep.Label} — {InStep.Targeting} · {area}{offset} · {InStep.DamageTier}{counter}";
        }

        /// <summary>카운터 구간 (전조 시작 기준) 슬라이더 + 경고.</summary>
        private static void drawStepCounter(SerializedProperty InElement, PatternStep InStep)
        {
            var start = InElement.FindPropertyRelative(nameof(PatternStep.CounterWindowStart));
            var end = InElement.FindPropertyRelative(nameof(PatternStep.CounterWindowEnd));
            float telegraph = Mathf.Max(0.05f, InStep.TelegraphTime);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("  카운터 구간 (전조 기준 초)", "전조(장판) 시작부터. 판정 = 전조 시간"));
                float s = EditorGUILayout.FloatField(start.floatValue, GUILayout.Width(48));
                float e = end.floatValue;
                EditorGUILayout.MinMaxSlider(ref s, ref e, 0.0f, telegraph + Mathf.Max(0.0f, InStep.LeapDuration));
                e = EditorGUILayout.FloatField(e, GUILayout.Width(48));
                start.floatValue = Mathf.Max(0.0f, (float)Math.Round(s, 2));
                end.floatValue = Mathf.Max(start.floatValue, (float)Math.Round(e, 2));
            }

            if (end.floatValue <= start.floatValue)
            {
                EditorGUILayout.LabelField("    ⚠ 구간 길이가 0 — 카운터할 수 없음", EditorStyles.miniLabel);
            }
            else if (end.floatValue > telegraph + 0.001f)
            {
                EditorGUILayout.LabelField($"    ⚠ 판정({telegraph:0.##}s) 뒤까지 열려 있음 — 맞은 뒤에도 카운터 가능", EditorStyles.miniLabel);
            }
        }

        /// <summary>단계 필드: 카운터 칸(위에서 그림)을 빼고 나머지를 기본 필드로 그린다.</summary>
        private static void drawStepFields(SerializedProperty InElement)
        {
            var iterator = InElement.Copy();
            var endProperty = InElement.GetEndProperty();
            bool enterChildren = true;
            EditorGUI.indentLevel += 1;
            while (iterator.NextVisible(enterChildren) == true && SerializedProperty.EqualContents(iterator, endProperty) == false)
            {
                enterChildren = false;
                if (iterator.name == nameof(PatternStep.Counterable) || iterator.name == nameof(PatternStep.CounterWindowStart) || iterator.name == nameof(PatternStep.CounterWindowEnd))
                {
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, new GUIContent(iterator.displayName, iterator.tooltip), true);
            }

            EditorGUI.indentLevel -= 1;
        }

        /// <summary>위/아래/복제/삭제. 배열 구조가 바뀌면 true.</summary>
        private static bool stepButtons(SerializedProperty InList, int InIndex)
        {
            if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(22.0f)) == true && InIndex > 0)
            {
                InList.MoveArrayElement(InIndex, InIndex - 1);
                return true;
            }

            if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(22.0f)) == true && InIndex < InList.arraySize - 1)
            {
                InList.MoveArrayElement(InIndex, InIndex + 1);
                return true;
            }

            if (GUILayout.Button("⧉", EditorStyles.miniButtonMid, GUILayout.Width(22.0f)) == true)
            {
                InList.InsertArrayElementAtIndex(InIndex);
                return true;
            }

            if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(22.0f)) == true)
            {
                InList.DeleteArrayElementAtIndex(InIndex);
                return true;
            }

            return false;
        }

        private void showStepMenu(BossPatternDefinition InPattern)
        {
            var menu = new GenericMenu();
            for (int i = 0; i < AbilityTemplates.StepLabels.Length; ++i)
            {
                int captured = i;
                menu.AddItem(new GUIContent(AbilityTemplates.StepLabels[i]), false, () =>
                {
                    Undo.RecordObject(InPattern, "단계 추가");
                    InPattern.Steps.Add(AbilityTemplates.CreateStep(captured));
                    InPattern.ToolEdited = true;
                    EditorUtility.SetDirty(InPattern);
                    AbilitySerialized = null;
                });
            }

            menu.ShowAsContext();
        }

        // ------------------------------------------------------------------ timeline

        /// <summary>회차 1번을 시간 순으로 펼친 막대: 전조(주황) · 판정(빨강) · 카운터 구간(파랑) · 종료(흰색).</summary>
        private static void drawPatternTimeline(BossPatternDefinition InPattern)
        {
            float length = InPattern.RepeatDuration();
            float span = Mathf.Max(0.5f, length + InPattern.Recovery) * 1.05f;
            int repeats = InPattern.IsCounterable == true ? Mathf.Max(1, InPattern.CounterRepeat) : 1;
            EditorGUILayout.LabelField($"타임라인 — 회차 {length:0.##}s{(repeats > 1 ? $" × {repeats}회" : string.Empty)} + 후딜 {InPattern.Recovery:0.##}s", EditorStyles.boldLabel);
            var rect = GUILayoutUtility.GetRect(10.0f, TimelineHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.15f));
            Func<float, float> x = t => rect.x + rect.width * Mathf.Clamp01(t / span);
            var style = new GUIStyle(EditorStyles.miniLabel);
            float cursor = 0.0f;
            for (int i = 0; i < InPattern.Steps.Count; ++i)
            {
                var step = InPattern.Steps[i];
                float hit;
                float end;
                float telegraph = InPattern.StepTiming(i, ref cursor, out hit, out end);
                EditorGUI.DrawRect(new Rect(x(telegraph), rect.y + 18, Mathf.Max(1.0f, x(hit) - x(telegraph)), 12), TelegraphColor);
                if (step.Counterable == true)
                {
                    EditorGUI.DrawRect(new Rect(x(telegraph + step.CounterWindowStart), rect.y + 14, Mathf.Max(2.0f, x(telegraph + step.CounterWindowEnd) - x(telegraph + step.CounterWindowStart)), 20), CounterColor);
                }

                EditorGUI.DrawRect(new Rect(x(hit) - 1.0f, rect.y, 2.0f, rect.height), HitColor);
                style.normal.textColor = step.Counterable == true ? new Color(0.55f, 0.8f, 1.0f) : HitColor;
                GUI.Label(new Rect(x(hit) + 3.0f, rect.y + 36.0f + (i % 2) * 11.0f, 200.0f, 14.0f), $"{hit:0.##}s {step.Label}{(step.Counterable ? " ◆" : string.Empty)}", style);
            }

            if (InPattern.Counterable == true && InPattern.HasStepCounter == false)
            {
                EditorGUI.DrawRect(new Rect(x(InPattern.CounterWindowStart), rect.y + 14, Mathf.Max(2.0f, x(InPattern.CounterWindowEnd) - x(InPattern.CounterWindowStart)), 20), CounterColor);
            }

            float endX = x(length + InPattern.Recovery);
            EditorGUI.DrawRect(new Rect(endX - 1.0f, rect.y, 2.0f, rect.height), Color.white);
            if (InPattern.Steps.Count == 0 && InPattern.StaggerCheck == false)
            {
                EditorGUILayout.HelpBox("단계가 없습니다 — 발동해도 판정이 없습니다 (기믹이 따로 처리하는 패턴이면 정상).", MessageType.Info);
            }
        }

        private static void drawStagger(SerializedObject InSo)
        {
            var on = InSo.FindProperty(nameof(BossPatternDefinition.StaggerCheck));
            EditorGUILayout.PropertyField(on, new GUIContent("무력화 체크", "시작 시 무력화 게이지를 채우고 시간 안에 0 이 되면 성공"));
            if (on.boolValue == false)
            {
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            field(InSo, nameof(BossPatternDefinition.StaggerGauge), "게이지");
            field(InSo, nameof(BossPatternDefinition.StaggerTime), "제한 시간 (초)");
            field(InSo, nameof(BossPatternDefinition.StaggerFailOutcome), "실패 결과");
            field(InSo, nameof(BossPatternDefinition.StaggerFailDamage), "실패 피해");
            field(InSo, nameof(BossPatternDefinition.StaggerFailChainPatternId), "실패 시 이어지는 패턴");
            field(InSo, nameof(BossPatternDefinition.GroggyOnStaggerSuccess), "성공 그로기 (초)");
            EditorGUILayout.EndVertical();
        }

        /// <summary>사용처: 보스 · 페이즈 · 이 패턴을 쓰는 StateTree 상태 (원본의 "사용처").</summary>
        private void drawPatternUsers(BossPatternDefinition InPattern, BossDefinition InBoss)
        {
            var owners = patternOwners(InPattern.PatternId);
            EditorGUILayout.LabelField("사용처", owners.Count > 0 ? string.Join(", ", owners) : "아직 없음", EditorStyles.wordWrappedLabel);
            if (InBoss != null && InBoss.Brain != null)
            {
                var states = new List<string>();
                foreach (var state in InBoss.Brain.States)
                {
                    if (state.Tasks.Any(t => t is UsePatternTask use && use.PatternId == InPattern.PatternId) == true)
                    {
                        states.Add(state.Name);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("StateTree", states.Count > 0 ? string.Join(", ", states) : "UsePatternTask 없음 (랜덤 패턴 공격으로만 사용)", EditorStyles.wordWrappedLabel);
                    if (GUILayout.Button("StateTree Editor", GUILayout.Width(120.0f)) == true)
                    {
                        Framework.Editor.StateTreeEditorWindow.Open(InBoss.Brain);
                    }
                }
            }

            if (EditorApplication.isPlaying == true)
            {
                var live = FindAnyObjectByType<BossActor>();
                if (live != null && live.Definition != null && live.Definition.GetPattern(InPattern.PatternId) != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool running = live.ActivePattern != null && live.ActivePattern.IsActive && live.ActivePattern.Definition.PatternId == InPattern.PatternId;
                        EditorGUILayout.LabelField("플레이", running ? (live.HasTag((int)ETag.Boss_CounterWindow) ? "실행 중 ◆ 카운터 구간 열림" : "실행 중") : "대기");
                        if (GUILayout.Button("지금 실행", GUILayout.Width(120.0f)) == true)
                        {
                            Framework.Ability.EActivateFailReason reason;
                            if (live.TryActivatePattern(InPattern.PatternId, null, out reason) == false)
                            {
                                Debug.Log($"[GAS Tool] {InPattern.PatternId} 실행 실패: {reason}");
                            }
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------ player skill

        private void drawSkillEditor(SkillDefinition InSkill)
        {
            var so = serializedFor(InSkill);
            so.Update();
            EditorGUILayout.LabelField($"[{InSkill.SkillId}] {InSkill.DisplayName} (플레이어 스킬)", EditorStyles.largeLabel);
            EditorGUILayout.HelpBox("플레이어 스킬은 Data/Generated 에셋이라 전체 셋업(setup overwrite) 시 코드(DefaultContent) 값으로 돌아갑니다. 영구 변경은 DefaultContent.cs 에 반영하세요.", MessageType.Info);
            var iterator = so.GetIterator();
            bool enter = true;
            while (iterator.NextVisible(enter) == true)
            {
                enter = false;
                if (iterator.name == "m_Script")
                {
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            so.ApplyModifiedProperties();
        }
    }
}
