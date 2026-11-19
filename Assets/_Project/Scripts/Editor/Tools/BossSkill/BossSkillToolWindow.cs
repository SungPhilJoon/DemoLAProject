using System.Collections.Generic;
using System.Linq;
using DemoLA.Boss;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Data;
using DemoLA.Editor.SkillTool;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor.BossTool
{
    /// <summary>
    /// Boss Skill Tool (DemoLA/Tools/Boss Skill Tool) — 보스 몬스터의 스킬(패턴 = BossPatternDefinition)과 모델을 세팅하는 창.
    ///  [왼쪽]    보스 → "모델 · 기본 애니메이션" + 패턴 목록 (▶ = 단계 동작 있음, ◆ = 카운터 가능)
    ///  [미리보기] 보스 모델(배율 포함)에 단계별 동작 클립을 게임과 같은 시각으로 재생 · 스크럽.
    ///            장판(전조 → 판정) · 십자 범위 · 돌진 이동 · 가상 대상(어그로 대상 위치)을 바닥에 그린다.
    ///  [타임라인] 단계마다 전조(주황) · 판정(빨강) · 돌진(노랑) · 카운터 구간(파랑) · 동작 클립(하늘색), 회복(회색).
    ///  [세부]    모델: 원본 FBX · 배율 · 대기/이동/사망 클립 → 프리팹 만들기.
    ///            패턴: 블렌드 · 쿨다운 · 회복, 단계별 동작(클립 · 속도 · 시작 · 지연, "현재 프레임 = 판정") + 전조/판정 세부.
    /// 패턴을 고치면 ToolEdited 가 켜져 CSV 재가져오기 · 전체 셋업이 실행 데이터를 덮지 않는다 (GAS Tool 과 같은 규칙).
    /// 카운터 · 대상 규칙 · 무력화 체크 같은 GAS 설정은 GAS Tool 에서 (상단 "GAS Tool" 버튼).
    /// </summary>
    public class BossSkillToolWindow : EditorWindow
    {
        private const float ListWidth = 270.0f;
        private const float TimelineHeight = 58.0f;
        private static readonly float[] PlaybackRates = { 0.1f, 0.25f, 0.5f, 1.0f, 2.0f };
        private static readonly string[] PlaybackLabels = { "0.1×", "0.25×", "0.5×", "1×", "2×" };
        private static readonly Color TelegraphColor = new Color(0.95f, 0.6f, 0.2f, 0.55f);
        private static readonly Color HitColor = new Color(1.0f, 0.35f, 0.3f);
        private static readonly Color ChargeColor = new Color(1.0f, 0.85f, 0.25f, 0.6f);
        private static readonly Color CounterColor = new Color(0.35f, 0.65f, 1.0f, 0.6f);
        private static readonly Color ClipColor = new Color(0.4f, 0.8f, 1.0f, 0.45f);
        private static readonly Color RecoveryColor = new Color(0.6f, 0.6f, 0.6f, 0.35f);
        private static readonly Color CursorColor = new Color(1.0f, 0.9f, 0.3f);
        private static readonly Color TargetColor = new Color(0.3f, 1.0f, 0.5f, 0.6f);

        private enum EModelPreview
        {
            Idle = 0,
            Move = 1,
            Death = 2,
        }

        [SerializeField] private int BossIndex;
        [SerializeField] private BossPatternDefinition Selected;
        [SerializeField] private bool ModelSelected;
        [SerializeField] private float PreviewHeight = 340.0f;
        [SerializeField] private int RateIndex = 3;
        [SerializeField] private bool Loop = true;
        [SerializeField] private float TargetDistance = 6.0f;
        [SerializeField] private EModelPreview ModelPreview;

        private GameDatabase Database;
        private SkillAnimationPreview Preview;
        private SerializedObject Serialized;
        private Vector2 ListScroll;
        private Vector2 DetailScroll;
        private string Filter = string.Empty;
        private float Time;
        private bool Playing;
        private double LastTick;
        private bool ResizingPreview;
        private readonly HashSet<int> OpenSteps = new HashSet<int> { 0 };

        [MenuItem("Window/DemoLA/Boss Skill Tool")]
        [MenuItem("DemoLA/Tools/Boss Skill Tool", priority = 202)]
        public static void Open()
        {
            GetWindow<BossSkillToolWindow>("Boss Skill Tool").minSize = new Vector2(980.0f, 600.0f);
        }

        public static void OpenPattern(BossPatternDefinition InPattern)
        {
            Open();
            var window = GetWindow<BossSkillToolWindow>();
            window.select(InPattern);
            window.Focus();
        }

        [MenuItem("CONTEXT/BossPatternDefinition/Boss Skill Tool 에서 열기")]
        private static void OpenFromContext(MenuCommand InCommand)
        {
            OpenPattern(InCommand.context as BossPatternDefinition);
        }

        private void OnEnable()
        {
            // 보스는 크고 장판이 넓다 → 더 멀리 · 위에서 비스듬히.
            Preview = new SkillAnimationPreview { GridExtent = 25, FollowRoot = true, DistanceFactor = 2.6f };
            Preview.SetView(140.0f, 38.0f);
            Undo.undoRedoPerformed += Repaint;
            EditorApplication.update += tick;
            LastTick = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
            EditorApplication.update -= tick;
            Preview?.Dispose();
            Preview = null;
        }

        private void tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - LastTick);
            LastTick = now;
            if (Playing == false)
            {
                return;
            }

            Time += dt * PlaybackRates[Mathf.Clamp(RateIndex, 0, PlaybackRates.Length - 1)];
            float end = timelineLength();
            if (Time > end)
            {
                if (Loop == true)
                {
                    Time = 0.0f;
                }
                else
                {
                    Time = end;
                    Playing = false;
                }
            }

            Repaint();
        }

        // ------------------------------------------------------------------ GUI

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

            var bosses = Database.Bosses.Where(b => b != null).ToList();
            if (bosses.Count == 0)
            {
                EditorGUILayout.HelpBox("GameDatabase 에 보스가 없습니다.", MessageType.Warning);
                return;
            }

            BossIndex = Mathf.Clamp(BossIndex, 0, bosses.Count - 1);
            var boss = bosses[BossIndex];
            drawToolbar(bosses);
            EditorGUILayout.BeginHorizontal();
            drawList(boss);
            EditorGUILayout.BeginVertical();
            Preview.SetModel(boss.ModelPrefab != null ? boss.ModelPrefab : boss.SourceModel, Mathf.Max(0.01f, boss.ModelScale));
            if (ModelSelected == true)
            {
                drawPreviewArea(boss, null);
                drawModelDetails(boss);
            }
            else if (Selected != null && boss.Patterns.Contains(Selected) == true)
            {
                drawPreviewArea(boss, Selected);
                drawPatternDetails(boss, Selected);
            }
            else
            {
                EditorGUILayout.HelpBox("왼쪽에서 '모델 · 기본 애니메이션' 이나 패턴을 고르세요.\n" +
                                        "보스 스킬 = 패턴(단계마다 전조 → 판정). 단계마다 동작 클립을 넣고, 미리보기에서 판정 시점과 맞춥니다.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void drawToolbar(List<BossDefinition> InBosses)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            int next = EditorGUILayout.Popup(BossIndex, InBosses.Select(b => $"{b.DisplayName} ({b.BossId})").ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(200.0f));
            if (next != BossIndex)
            {
                BossIndex = next;
                Selected = null;
                ModelSelected = false;
                GUI.FocusControl(null);
            }

            GUILayout.FlexibleSpace();
            Filter = EditorGUILayout.TextField(Filter, EditorStyles.toolbarSearchField, GUILayout.Width(180.0f));
            using (new EditorGUI.DisabledScope(Selected == null))
            {
                if (GUILayout.Button(new GUIContent("GAS Tool", "이 패턴의 카운터 · 대상 · 무력화 설정"), EditorStyles.toolbarButton, GUILayout.Width(70.0f)) == true)
                {
                    Gas.DemoLAGasTool.OpenPattern(Selected.PatternTag);
                }
            }

            if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(50.0f)) == true)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[Boss Skill Tool] saved");
            }

            EditorGUILayout.EndHorizontal();
        }

        private void drawList(BossDefinition InBoss)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(ListWidth));
            ListScroll = EditorGUILayout.BeginScrollView(ListScroll);
            if (listButton(ModelSelected, $"모델 · 기본 애니메이션{(InBoss.ModelPrefab != null ? string.Empty : "  (캡슐)")}", true) == true)
            {
                ModelSelected = true;
                Selected = null;
                Time = 0.0f;
                Playing = true;
                GUI.FocusControl(null);
            }

            EditorGUILayout.Space(4.0f);
            EditorGUILayout.LabelField($"패턴 ({InBoss.Patterns.Count})", EditorStyles.miniBoldLabel);
            foreach (var pattern in InBoss.Patterns.Where(p => p != null).OrderBy(p => p.PatternTag))
            {
                bool animated = pattern.Steps.Any(s => s != null && s.Animation != null);
                string label = $"{(animated ? "▶ " : "   ")}{GameTags.PatternAssetName(pattern.PatternTag)}  {pattern.DisplayName}{(pattern.IsCounterable ? " ◆" : string.Empty)}";
                if (string.IsNullOrEmpty(Filter) == false && label.IndexOf(Filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (listButton(ModelSelected == false && Selected == pattern, label, false) == true)
                {
                    select(pattern);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static bool listButton(bool InSelected, string InLabel, bool InBold)
        {
            var style = new GUIStyle(EditorStyles.label) { fontStyle = InBold || InSelected ? FontStyle.Bold : FontStyle.Normal };
            if (InSelected == true)
            {
                style.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.5f, 0.8f, 1.0f) : new Color(0.1f, 0.3f, 0.8f);
            }

            return GUILayout.Button(InLabel, style);
        }

        private void select(BossPatternDefinition InPattern)
        {
            Selected = InPattern;
            ModelSelected = false;
            Serialized = null;
            Time = 0.0f;
            Playing = InPattern != null && InPattern.Steps.Any(s => s != null && s.Animation != null);
            OpenSteps.Clear();
            OpenSteps.Add(0);
            GUI.FocusControl(null);
            if (Database == null)
            {
                Database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DemoLAProjectSetup.DatabasePath);
            }

            if (InPattern != null && Database != null)
            {
                var bosses = Database.Bosses.Where(b => b != null).ToList();
                int index = bosses.FindIndex(b => b.Patterns.Contains(InPattern));
                if (index >= 0)
                {
                    BossIndex = index;
                }
            }
        }

        // ------------------------------------------------------------------ timing

        private struct StepTime
        {
            public float TelegraphStart;
            public float Hit;
            public float End;
            public float AnimStart;
        }

        private static List<StepTime> stepTimes(BossPatternDefinition InPattern)
        {
            var list = new List<StepTime>();
            float cursor = 0.0f;
            for (int i = 0; i < InPattern.Steps.Count; ++i)
            {
                float hit;
                float end;
                float telegraph = InPattern.StepTiming(i, ref cursor, out hit, out end);
                list.Add(new StepTime { TelegraphStart = telegraph, Hit = hit, End = end, AnimStart = telegraph + Mathf.Max(0.0f, InPattern.Steps[i].AnimationDelay) });
            }

            return list;
        }

        private float timelineLength()
        {
            if (ModelSelected == true)
            {
                var boss = currentBoss();
                var clip = modelClip(boss);
                return clip != null ? Mathf.Max(0.5f, clip.length) : 2.0f;
            }

            if (Selected == null)
            {
                return 1.0f;
            }

            return Mathf.Max(0.5f, Selected.RepeatDuration() + Mathf.Max(0.0f, Selected.Recovery));
        }

        private BossDefinition currentBoss()
        {
            var bosses = Database != null ? Database.Bosses.Where(b => b != null).ToList() : new List<BossDefinition>();
            return bosses.Count > 0 ? bosses[Mathf.Clamp(BossIndex, 0, bosses.Count - 1)] : null;
        }

        private AnimationClip modelClip(BossDefinition InBoss)
        {
            if (InBoss == null)
            {
                return null;
            }

            switch (ModelPreview)
            {
                case EModelPreview.Move: return InBoss.MoveAnimation;
                case EModelPreview.Death: return InBoss.DeathAnimation;
            }

            return InBoss.IdleAnimation;
        }

        /// <summary>패턴 시간 InTime 에 재생 중인 단계 클립 (게임과 같이: 클립 없는 단계는 앞 동작 유지, 시작 전 · 끝난 뒤는 대기 클립).</summary>
        private static void clipAt(BossDefinition InBoss, BossPatternDefinition InPattern, List<StepTime> InTimes, float InTime, out AnimationClip OutClip, out double OutClipTime)
        {
            OutClip = null;
            OutClipTime = 0.0;
            float patternEnd = InTimes.Count > 0 ? InTimes[InTimes.Count - 1].End : 0.0f;
            if (InTime <= patternEnd + Mathf.Max(0.0f, InPattern.Recovery))
            {
                for (int i = InTimes.Count - 1; i >= 0; --i)
                {
                    var step = InPattern.Steps[i];
                    if (step.Animation != null && InTime >= InTimes[i].AnimStart)
                    {
                        OutClip = step.Animation;
                        double t = step.AnimationStartTime + (InTime - InTimes[i].AnimStart) * Mathf.Max(0.01f, step.AnimationSpeed);
                        OutClipTime = OutClip.isLooping ? t % Mathf.Max(0.0001f, OutClip.length) : System.Math.Min(t, OutClip.length);
                        return;
                    }
                }
            }

            OutClip = InBoss.IdleAnimation;
            OutClipTime = OutClip != null ? InTime % Mathf.Max(0.0001f, OutClip.length) : 0.0;
        }

        /// <summary>돌진(BossMoveDistance)으로 보스가 정면으로 이동한 거리.</summary>
        private static float bossForwardAt(BossPatternDefinition InPattern, List<StepTime> InTimes, float InTime)
        {
            float offset = 0.0f;
            for (int i = 0; i < InTimes.Count; ++i)
            {
                var step = InPattern.Steps[i];
                if (step.BossMoveDistance <= 0.0f)
                {
                    continue;
                }

                float start = step.ChargeSweepHit == true ? InTimes[i].Hit : InTimes[i].Hit - step.BossMoveDuration;
                offset += step.BossMoveDistance * Mathf.Clamp01((InTime - start) / Mathf.Max(0.01f, step.BossMoveDuration));
            }

            return offset;
        }

        // ------------------------------------------------------------------ preview

        private void drawPreviewArea(BossDefinition InBoss, BossPatternDefinition InPattern)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(Playing ? "❚❚" : "▶", EditorStyles.toolbarButton, GUILayout.Width(28.0f)) == true)
            {
                Playing = !Playing;
                if (Playing == true && Time >= timelineLength())
                {
                    Time = 0.0f;
                }
            }

            if (GUILayout.Button("⏮", EditorStyles.toolbarButton, GUILayout.Width(26.0f)) == true)
            {
                Time = 0.0f;
            }

            Loop = GUILayout.Toggle(Loop, "반복", EditorStyles.toolbarButton, GUILayout.Width(40.0f));
            RateIndex = EditorGUILayout.Popup(RateIndex, PlaybackLabels, EditorStyles.toolbarPopup, GUILayout.Width(56.0f));
            if (InPattern != null)
            {
                Preview.ShowAllHits = GUILayout.Toggle(Preview.ShowAllHits, "모든 단계 범위", EditorStyles.toolbarButton, GUILayout.Width(90.0f));
                Preview.FollowRoot = GUILayout.Toggle(Preview.FollowRoot, "돌진 이동", EditorStyles.toolbarButton, GUILayout.Width(64.0f));
                GUILayout.Label("대상 거리", EditorStyles.miniLabel);
                TargetDistance = EditorGUILayout.Slider(TargetDistance, 0.0f, 25.0f, GUILayout.Width(150.0f));
            }
            else
            {
                ModelPreview = (EModelPreview)GUILayout.Toolbar((int)ModelPreview, new[] { "대기", "이동", "사망" }, EditorStyles.toolbarButton, GUILayout.Width(150.0f));
            }

            Preview.ShowGrid = GUILayout.Toggle(Preview.ShowGrid, "격자", EditorStyles.toolbarButton, GUILayout.Width(40.0f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            var rect = GUILayoutUtility.GetRect(10.0f, PreviewHeight, GUILayout.ExpandWidth(true));
            if (InPattern == null)
            {
                var clip = modelClip(InBoss);
                double clipTime = clip != null ? (clip.isLooping ? Time % Mathf.Max(0.0001f, clip.length) : Mathf.Min(Time, clip.length)) : 0.0;
                Preview.SampleClip(clip, clipTime, Vector3.zero);
                Preview.Draw(rect, 0.0f, null);
                label(rect, clip != null ? $"{ModelPreview} · {clip.name}  {clipTime:0.00}/{clip.length:0.00}s" : $"{ModelPreview} 클립 없음", null);
                drawSimpleTimeline(GUILayoutUtility.GetRect(10.0f, 26.0f, GUILayout.ExpandWidth(true)), clip);
            }
            else
            {
                var times = stepTimes(InPattern);
                handleClipDrop(rect, InPattern, times);
                AnimationClip clip;
                double clipTime;
                clipAt(InBoss, InPattern, times, Time, out clip, out clipTime);
                float forward = Preview.FollowRoot == true ? bossForwardAt(InPattern, times, Time) : 0.0f;
                Preview.SampleClip(clip, clipTime, Vector3.forward * forward);
                Preview.Draw(rect, forward * 0.5f + TargetDistance * 0.3f, () => patternOverlay(InPattern, times, forward));
                int active = activeStep(times, Time);
                label(rect, $"{Time:0.00}s  ·  단계 {(active >= 0 ? (active + 1).ToString() : "-")}  ·  {(clip != null ? $"{clip.name} {clipTime:0.00}/{clip.length:0.00}s" : "클립 없음")}",
                    active >= 0 && InPattern.Steps[active].Animation == null ? "이 단계는 클립이 없어 앞 동작 · 대기 자세를 유지합니다 — 클립을 여기에 끌어다 놓으면 이 단계에 들어갑니다" : null);
                drawTimeline(GUILayoutUtility.GetRect(10.0f, TimelineHeight, GUILayout.ExpandWidth(true)), InPattern, times);
            }

            drawSplitter();
        }

        private static int activeStep(List<StepTime> InTimes, float InTime)
        {
            for (int i = InTimes.Count - 1; i >= 0; --i)
            {
                if (InTime >= InTimes[i].TelegraphStart)
                {
                    return i;
                }
            }

            return InTimes.Count > 0 ? 0 : -1;
        }

        private static void label(Rect InRect, string InText, string InWarning)
        {
            GUI.Label(new Rect(InRect.x + 6.0f, InRect.y + 4.0f, InRect.width - 12.0f, 18.0f), InText, EditorStyles.whiteMiniLabel);
            if (string.IsNullOrEmpty(InWarning) == false)
            {
                GUI.Label(new Rect(InRect.x + 6.0f, InRect.y + 20.0f, InRect.width - 12.0f, 18.0f), InWarning, EditorStyles.whiteMiniLabel);
            }
        }

        /// <summary>바닥 표시: 가상 대상(어그로 대상) + 단계 장판 (전조 중 연하게 → 판정 때 진하게).</summary>
        private void patternOverlay(BossPatternDefinition InPattern, List<StepTime> InTimes, float InBossForward)
        {
            var target = new Vector3(0.0f, 0.0f, TargetDistance);
            SkillAnimationPreview.DrawArea(HitArea.Circle(0.5f), target, 0.0f, TargetColor);
            for (int i = 0; i < InTimes.Count; ++i)
            {
                var step = InPattern.Steps[i];
                var t = InTimes[i];
                float active;
                if (Time < t.TelegraphStart)
                {
                    active = 0.0f;
                }
                else if (Time < t.Hit)
                {
                    active = 0.25f + 0.5f * Mathf.InverseLerp(t.TelegraphStart, t.Hit, Time);
                }
                else
                {
                    active = Mathf.Clamp01(1.0f - (Time - Mathf.Max(t.Hit, t.End)) / 0.4f);
                }

                if (active <= 0.0f && Preview.ShowAllHits == false)
                {
                    continue;
                }

                if (step.ShowTelegraph == false && Time < t.Hit)
                {
                    // 게임에서는 장판 없이 바로 판정하는 단계 — 편집용으로 전조 동안 아주 옅게만 보여 준다.
                    active = Mathf.Min(active, 0.12f);
                }

                Vector3 origin;
                float yaw;
                placement(step, InPattern, InTimes, i, target, out origin, out yaw);
                var color = SkillAnimationPreview.HitColor(active);
                if (step.ArmCount > 0)
                {
                    for (int a = 0; a < step.ArmCount; ++a)
                    {
                        SkillAnimationPreview.DrawArea(HitArea.Rect(step.ArmLength, step.ArmWidth), origin, yaw + step.ArmAngleOffset + a * 360.0f / step.ArmCount, color);
                    }
                }
                else
                {
                    SkillAnimationPreview.DrawArea(step.Area, origin, yaw, color);
                }
            }
        }

        /// <summary>게임의 BossPatternAbility.placement 를 미리보기용으로 단순화 (보스 = 원점 +Z, 대상 = InTarget, 경기장 중심 = 원점).</summary>
        private static void placement(PatternStep InStep, BossPatternDefinition InPattern, List<StepTime> InTimes, int InIndex, Vector3 InTarget, out Vector3 OutOrigin, out float OutYaw)
        {
            float bossZ = bossForwardAt(InPattern, InTimes, InTimes[InIndex].TelegraphStart);
            var boss = new Vector3(0.0f, 0.0f, bossZ);
            OutYaw = 0.0f;
            switch (InStep.Targeting)
            {
                case EPatternTargeting.BossForward:
                case EPatternTargeting.BossTowardAggro:
                    OutOrigin = boss;
                    break;
                case EPatternTargeting.ArenaCenter:
                    OutOrigin = Vector3.zero;
                    break;
                case EPatternTargeting.ArenaEdgeAwayFromParty:
                    OutOrigin = new Vector3(0.0f, 0.0f, -InStep.EdgeDistance);
                    break;
                default:
                    OutOrigin = InTarget;
                    break;
            }

            OutOrigin += Vector3.forward * InStep.ForwardOffset;
        }

        private void handleClipDrop(Rect InRect, BossPatternDefinition InPattern, List<StepTime> InTimes)
        {
            var e = Event.current;
            if ((e.type != EventType.DragUpdated && e.type != EventType.DragPerform) || InRect.Contains(e.mousePosition) == false || InPattern.Steps.Count == 0)
            {
                return;
            }

            var clip = SkillAbilityToolWindow.DraggedClip();
            if (clip == null)
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                int step = Mathf.Max(0, activeStep(InTimes, Time));
                Undo.RecordObject(InPattern, "단계 동작");
                InPattern.Steps[step].Animation = clip;
                if (InPattern.Steps[step].AnimationSpeed <= 0.0f)
                {
                    InPattern.Steps[step].AnimationSpeed = 1.0f;
                }

                markEdited(InPattern);
                OpenSteps.Add(step);
            }

            e.Use();
        }

        private void drawSimpleTimeline(Rect InRect, AnimationClip InClip)
        {
            float length = timelineLength();
            EditorGUI.DrawRect(InRect, EditorGUIUtility.isProSkin ? new Color(0.13f, 0.13f, 0.14f) : new Color(0.75f, 0.75f, 0.76f));
            var bar = new Rect(InRect.x + 8.0f, InRect.y + 6.0f, InRect.width - 16.0f, InRect.height - 12.0f);
            if (InClip != null)
            {
                EditorGUI.DrawRect(bar, ClipColor);
            }

            EditorGUI.DrawRect(new Rect(bar.x + bar.width * Mathf.Clamp01(Time / length) - 1.0f, InRect.y, 2.0f, InRect.height), CursorColor);
            scrub(InRect, bar, length);
        }

        private void drawTimeline(Rect InRect, BossPatternDefinition InPattern, List<StepTime> InTimes)
        {
            float length = timelineLength() * 1.03f;
            var bar = new Rect(InRect.x + 8.0f, InRect.y + 14.0f, InRect.width - 16.0f, InRect.height - 18.0f);
            EditorGUI.DrawRect(InRect, EditorGUIUtility.isProSkin ? new Color(0.13f, 0.13f, 0.14f) : new Color(0.75f, 0.75f, 0.76f));
            System.Func<float, float> x = t => bar.x + bar.width * Mathf.Clamp01(t / length);
            for (int s = 0; s <= Mathf.FloorToInt(length); ++s)
            {
                EditorGUI.DrawRect(new Rect(x(s), InRect.y + 2.0f, 1.0f, 8.0f), new Color(1, 1, 1, 0.25f));
                GUI.Label(new Rect(x(s) + 2.0f, InRect.y - 1.0f, 30.0f, 14.0f), $"{s}s", EditorStyles.miniLabel);
            }

            float laneH = bar.height / 2.0f;
            for (int i = 0; i < InTimes.Count; ++i)
            {
                var step = InPattern.Steps[i];
                var t = InTimes[i];
                // 1단: 전조 · 판정 · 돌진 · 카운터
                EditorGUI.DrawRect(new Rect(x(t.TelegraphStart), bar.y, x(t.Hit) - x(t.TelegraphStart), laneH - 2.0f), TelegraphColor);
                if (t.End > t.Hit)
                {
                    EditorGUI.DrawRect(new Rect(x(t.Hit), bar.y, x(t.End) - x(t.Hit), laneH - 2.0f), ChargeColor);
                }

                if (step.Counterable == true)
                {
                    float cs = t.TelegraphStart + step.CounterWindowStart;
                    float ce = t.TelegraphStart + step.CounterWindowEnd;
                    EditorGUI.DrawRect(new Rect(x(cs), bar.y + laneH * 0.5f - 3.0f, x(ce) - x(cs), 6.0f), CounterColor);
                }

                EditorGUI.DrawRect(new Rect(x(t.Hit) - 1.0f, bar.y, 3.0f, laneH), HitColor);
                GUI.Label(new Rect(x(t.TelegraphStart) + 2.0f, bar.y - 1.0f, 40.0f, 14.0f), $"{i + 1}", EditorStyles.miniBoldLabel);

                // 2단: 동작 클립 (다음 클립 단계 또는 패턴 끝까지)
                if (step.Animation != null)
                {
                    float clipLength = step.Animation.isLooping ? float.MaxValue : (step.Animation.length - step.AnimationStartTime) / Mathf.Max(0.01f, step.AnimationSpeed);
                    float next = timelineLength();
                    for (int j = i + 1; j < InTimes.Count; ++j)
                    {
                        if (InPattern.Steps[j].Animation != null)
                        {
                            next = InTimes[j].AnimStart;
                            break;
                        }
                    }

                    float end = Mathf.Min(next, t.AnimStart + clipLength);
                    var clipRect = new Rect(x(t.AnimStart), bar.y + laneH, x(end) - x(t.AnimStart), laneH - 2.0f);
                    EditorGUI.DrawRect(clipRect, ClipColor);
                    GUI.Label(new Rect(clipRect.x + 2.0f, clipRect.y - 2.0f, Mathf.Max(60.0f, clipRect.width), 14.0f), step.Animation.name, EditorStyles.miniLabel);
                    if (end < next)
                    {
                        // 클립이 끝난 뒤 마지막 자세 유지 구간
                        EditorGUI.DrawRect(new Rect(x(end), bar.y + laneH + laneH * 0.5f - 1.0f, x(next) - x(end), 2.0f), ClipColor);
                    }
                }
            }

            float patternEnd = InTimes.Count > 0 ? InTimes[InTimes.Count - 1].End : 0.0f;
            EditorGUI.DrawRect(new Rect(x(patternEnd), bar.y, x(patternEnd + InPattern.Recovery) - x(patternEnd), laneH - 2.0f), RecoveryColor);
            EditorGUI.DrawRect(new Rect(x(Time) - 1.0f, InRect.y, 2.0f, InRect.height), CursorColor);
            scrub(InRect, bar, length);
        }

        private void scrub(Rect InRect, Rect InBar, float InLength)
        {
            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive, InRect);
            if ((e.type == EventType.MouseDown && InRect.Contains(e.mousePosition)) || (e.type == EventType.MouseDrag && GUIUtility.hotControl == id))
            {
                GUIUtility.hotControl = id;
                Playing = false;
                Time = Mathf.Clamp((e.mousePosition.x - InBar.x) / InBar.width * InLength, 0.0f, InLength);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id)
            {
                GUIUtility.hotControl = 0;
                e.Use();
            }
        }

        private void drawSplitter()
        {
            var rect = GUILayoutUtility.GetRect(10.0f, 5.0f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2.0f, rect.width, 1.0f), new Color(0, 0, 0, 0.4f));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
            var e = Event.current;
            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                ResizingPreview = true;
                e.Use();
            }
            else if (ResizingPreview == true && e.type == EventType.MouseDrag)
            {
                PreviewHeight = Mathf.Clamp(PreviewHeight + e.delta.y, 160.0f, position.height - 220.0f);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp)
            {
                ResizingPreview = false;
            }
        }

        // ------------------------------------------------------------------ details: 모델

        private void drawModelDetails(BossDefinition InBoss)
        {
            var so = serializedFor(InBoss);
            so.Update();
            EditorGUIUtility.labelWidth = 160.0f;
            DetailScroll = EditorGUILayout.BeginScrollView(DetailScroll);
            EditorGUILayout.LabelField($"{InBoss.DisplayName} ({InBoss.BossId}) — 모델 · 기본 애니메이션", EditorStyles.largeLabel);
            EditorGUILayout.HelpBox("원본 모델(FBX, Humanoid)과 대기 · 이동 클립으로 프리팹(Animator 컨트롤러 포함)을 만들어 보스에 연결합니다. 비어 있으면 게임에서 캡슐로 그립니다.\n" +
                                    "대기 · 이동은 컨트롤러가 이동 속도로 고르고, 패턴 동작은 단계마다 위에 섞습니다. 사망 클립은 마지막 자세로 멈춥니다.", MessageType.None);
            objectField(so, nameof(BossDefinition.SourceModel), "원본 모델 (FBX)", typeof(GameObject));
            EditorGUILayout.PropertyField(so.FindProperty(nameof(BossDefinition.ModelScale)), new GUIContent("배율", "판정 반경(Radius)과 별개로 보이는 크기"));
            objectField(so, nameof(BossDefinition.IdleAnimation), "대기 (루프)", typeof(AnimationClip));
            objectField(so, nameof(BossDefinition.MoveAnimation), "이동 (루프)", typeof(AnimationClip));
            objectField(so, nameof(BossDefinition.DeathAnimation), "사망", typeof(AnimationClip));
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("모델 프리팹", InBoss.ModelPrefab, typeof(GameObject), false);
            }

            EditorGUILayout.LabelField("판정 반경 (Radius)", $"{InBoss.Radius:0.0}m — 미리보기 바닥의 1m 격자로 크기를 맞추세요", EditorStyles.miniLabel);
            bool changed = so.ApplyModifiedProperties();
            EditorGUILayout.Space(4.0f);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(InBoss.SourceModel == null))
            {
                if (GUILayout.Button(InBoss.ModelPrefab != null ? "프리팹 다시 만들기" : "프리팹 만들기 · 보스에 연결", GUILayout.Height(26.0f)) == true)
                {
                    BossModelBuilder.Build(InBoss);
                }
            }

            using (new EditorGUI.DisabledScope(BossModelBuilder.HasPreset(InBoss.BossId) == false))
            {
                if (GUILayout.Button(new GUIContent("기본 설정으로", "프로젝트에 정해 둔 모델 · 클립 · 배율로 되돌리고 프리팹을 다시 만든다"), GUILayout.Height(26.0f), GUILayout.Width(110.0f)) == true)
                {
                    BossModelBuilder.ApplyPreset(InBoss);
                }
            }

            using (new EditorGUI.DisabledScope(InBoss.ModelPrefab == null))
            {
                if (GUILayout.Button(new GUIContent("연결 해제 (캡슐)", "ModelPrefab 을 비운다 — 게임에서 캡슐로 그린다"), GUILayout.Height(26.0f), GUILayout.Width(110.0f)) == true)
                {
                    Undo.RecordObject(InBoss, "보스 모델 해제");
                    InBoss.ModelPrefab = null;
                    EditorUtility.SetDirty(InBoss);
                }
            }

            EditorGUILayout.EndHorizontal();
            if (changed == true && InBoss.ModelPrefab != null)
            {
                EditorGUILayout.HelpBox("대기 · 이동 클립을 바꿨다면 '프리팹 다시 만들기' 로 컨트롤러에 반영하세요.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ details: 패턴

        private void drawPatternDetails(BossDefinition InBoss, BossPatternDefinition InPattern)
        {
            var so = serializedFor(InPattern);
            so.Update();
            EditorGUIUtility.labelWidth = 170.0f;
            DetailScroll = EditorGUILayout.BeginScrollView(DetailScroll);
            EditorGUILayout.LabelField($"[{InPattern.TagName}] {InPattern.DisplayName}", EditorStyles.largeLabel);
            EditorGUILayout.LabelField($"{InPattern.Category} · {InPattern.Response}{(InPattern.ToolEdited ? " · 툴 편집됨 (CSV · 전체 셋업이 실행 데이터를 덮지 않음)" : string.Empty)}", EditorStyles.miniLabel);
            if (InBoss.ModelPrefab == null)
            {
                EditorGUILayout.HelpBox("이 보스는 아직 모델이 없어 게임에서 캡슐로 그립니다 — 왼쪽 '모델 · 기본 애니메이션' 에서 연결하세요. (동작 클립은 미리 넣어 둘 수 있습니다)", MessageType.Warning);
            }

            EditorGUILayout.Space(4.0f);
            EditorGUILayout.LabelField("패턴", EditorStyles.boldLabel);
            prop(so, nameof(BossPatternDefinition.Cooldown), "쿨다운 (초)");
            prop(so, nameof(BossPatternDefinition.Recovery), "회복 (마지막 단계 후)");
            prop(so, nameof(BossPatternDefinition.AnimationBlendIn), "동작 블렌드 인 (초)");
            prop(so, nameof(BossPatternDefinition.AnimationBlendOut), "동작 블렌드 아웃 (초)");
            prop(so, nameof(BossPatternDefinition.SuperArmor), "슈퍼아머");
            EditorGUILayout.LabelField("길이", $"{InPattern.RepeatDuration():0.00}초 + 회복 {InPattern.Recovery:0.00}초{(InPattern.IsCounterable && InPattern.CounterRepeat > 1 ? $" (카운터 {InPattern.CounterRepeat}회차 반복)" : string.Empty)}", EditorStyles.miniLabel);

            var steps = so.FindProperty(nameof(BossPatternDefinition.Steps));
            var times = stepTimes(InPattern);
            EditorGUILayout.Space(6.0f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"단계 ({steps.arraySize}) — 전조 → 판정. 동작 클립은 전조 시작(+지연)에 재생", EditorStyles.boldLabel);
            if (GUILayout.Button("+ 단계", EditorStyles.miniButton, GUILayout.Width(60.0f)) == true)
            {
                steps.arraySize += 1;
                OpenSteps.Add(steps.arraySize - 1);
            }

            EditorGUILayout.EndHorizontal();

            int remove = -1;
            for (int i = 0; i < steps.arraySize; ++i)
            {
                var step = steps.GetArrayElementAtIndex(i);
                var t = i < times.Count ? times[i] : default;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                bool open = OpenSteps.Contains(i);
                var clipName = step.FindPropertyRelative(nameof(PatternStep.Animation)).objectReferenceValue;
                bool nextOpen = EditorGUILayout.Foldout(open, $"단계 {i + 1}  {step.FindPropertyRelative(nameof(PatternStep.Label)).stringValue}   전조 {t.TelegraphStart:0.00}s → 판정 {t.Hit:0.00}s   {(clipName != null ? "▶ " + clipName.name : "(동작 없음)")}", true);
                if (nextOpen != open)
                {
                    if (nextOpen)
                    {
                        OpenSteps.Add(i);
                    }
                    else
                    {
                        OpenSteps.Remove(i);
                    }
                }

                if (GUILayout.Button(new GUIContent("◉", "이 단계 판정 시점으로 이동"), EditorStyles.miniButtonLeft, GUILayout.Width(24.0f)) == true)
                {
                    Playing = false;
                    Time = t.Hit;
                }

                if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(24.0f)) == true)
                {
                    remove = i;
                }

                EditorGUILayout.EndHorizontal();
                if (nextOpen == true)
                {
                    drawStep(InPattern, step, i, t);
                }

                EditorGUILayout.EndVertical();
            }

            if (remove >= 0 && EditorUtility.DisplayDialog("Boss Skill Tool", $"단계 {remove + 1} 을(를) 지울까요?", "지우기", "취소") == true)
            {
                steps.DeleteArrayElementAtIndex(remove);
                OpenSteps.Clear();
            }

            if (so.ApplyModifiedProperties() == true)
            {
                markEdited(InPattern);
                Repaint();
            }

            EditorGUILayout.EndScrollView();
        }

        private void drawStep(BossPatternDefinition InPattern, SerializedProperty InStep, int InIndex, StepTime InTime)
        {
            EditorGUI.indentLevel += 1;
            EditorGUILayout.PropertyField(InStep.FindPropertyRelative(nameof(PatternStep.Label)), new GUIContent("이름"));

            // 동작
            var clipProp = InStep.FindPropertyRelative(nameof(PatternStep.Animation));
            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(rect, GUIContent.none, clipProp);
            clipProp.objectReferenceValue = EditorGUI.ObjectField(rect, new GUIContent("동작 클립", "AnimationClip 또는 클립이 든 FBX 를 끌어다 놓는다"), clipProp.objectReferenceValue, typeof(AnimationClip), false);
            EditorGUI.EndProperty();
            var e = Event.current;
            if ((e.type == EventType.DragUpdated || e.type == EventType.DragPerform) && rect.Contains(e.mousePosition) == true && DragAndDrop.objectReferences.Any(o => o is AnimationClip) == false)
            {
                var dragged = SkillAbilityToolWindow.DraggedClip();
                if (dragged != null)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (e.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        clipProp.objectReferenceValue = dragged;
                    }

                    e.Use();
                }
            }

            var speedProp = InStep.FindPropertyRelative(nameof(PatternStep.AnimationSpeed));
            var startProp = InStep.FindPropertyRelative(nameof(PatternStep.AnimationStartTime));
            var delayProp = InStep.FindPropertyRelative(nameof(PatternStep.AnimationDelay));
            EditorGUILayout.PropertyField(speedProp, new GUIContent("재생 배율"));
            EditorGUILayout.PropertyField(startProp, new GUIContent("시작 위치 (초)"));
            EditorGUILayout.PropertyField(delayProp, new GUIContent("전조 후 지연 (초)"));
            var clip = clipProp.objectReferenceValue as AnimationClip;
            if (clip != null)
            {
                float hitOffset = InTime.Hit - InTime.TelegraphStart - Mathf.Max(0.0f, delayProp.floatValue);
                float speed = Mathf.Max(0.01f, speedProp.floatValue);
                EditorGUILayout.LabelField("클립", $"{clip.length:0.00}초 · {(clip.humanMotion ? "Humanoid" : "Generic")} · 판정 때 클립 {startProp.floatValue + hitOffset * speed:0.00}초 지점", EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(EditorGUIUtility.labelWidth);
                if (GUILayout.Button(new GUIContent("현재 프레임 = 판정", "미리보기의 지금 클립 자세(타격 순간)가 이 단계 판정 시점에 오도록 재생 배율을 맞춘다. 먼저 타임라인을 스크럽해 타격 프레임을 찾으세요"), EditorStyles.miniButtonLeft) == true)
                {
                    var boss = currentBoss();
                    AnimationClip current;
                    double clipTime;
                    clipAt(boss, InPattern, stepTimes(InPattern), Time, out current, out clipTime);
                    if (current == clip && hitOffset > 0.01f && clipTime > startProp.floatValue)
                    {
                        speedProp.floatValue = (float)(clipTime - startProp.floatValue) / hitOffset;
                        Time = InTime.Hit;
                    }
                    else
                    {
                        Debug.LogWarning("[Boss Skill Tool] 타임라인에서 이 단계 클립이 재생 중인 시점(타격 프레임)으로 스크럽한 뒤 누르세요");
                    }
                }

                if (GUILayout.Button(new GUIContent("판정까지 = 클립 끝", "판정 시점에 클립이 끝나도록 재생 배율을 맞춘다"), EditorStyles.miniButtonMid) == true && hitOffset > 0.01f)
                {
                    speedProp.floatValue = Mathf.Max(0.05f, (clip.length - startProp.floatValue) / hitOffset);
                }

                if (GUILayout.Button("1×", EditorStyles.miniButtonRight, GUILayout.Width(32.0f)) == true)
                {
                    speedProp.floatValue = 1.0f;
                }

                EditorGUILayout.EndHorizontal();
            }

            // 전조 · 판정
            EditorGUILayout.PropertyField(InStep.FindPropertyRelative(nameof(PatternStep.StartDelay)), new GUIContent("시작 대기 (초)"));
            EditorGUILayout.PropertyField(InStep.FindPropertyRelative(nameof(PatternStep.TelegraphTime)), new GUIContent("전조 (초)"));
            EditorGUILayout.PropertyField(InStep.FindPropertyRelative(nameof(PatternStep.Targeting)), new GUIContent("위치"));
            EditorGUILayout.PropertyField(InStep.FindPropertyRelative(nameof(PatternStep.Area)), new GUIContent("판정 범위"), true);
            EditorGUILayout.PropertyField(InStep.FindPropertyRelative(nameof(PatternStep.ForwardOffset)), new GUIContent("정면 오프셋 (m)"));
            EditorGUILayout.PropertyField(InStep.FindPropertyRelative(nameof(PatternStep.DamageTier)), new GUIContent("피해 등급"));
            EditorGUILayout.PropertyField(InStep.FindPropertyRelative(nameof(PatternStep.BossMoveDistance)), new GUIContent("돌진 거리 (m)"));
            EditorGUILayout.PropertyField(InStep.FindPropertyRelative(nameof(PatternStep.BossMoveDuration)), new GUIContent("돌진 시간 (초)"));
            EditorGUILayout.PropertyField(InStep.FindPropertyRelative(nameof(PatternStep.LeapDuration)), new GUIContent("도약 시간 (초)"));
            EditorGUILayout.PropertyField(InStep.FindPropertyRelative(nameof(PatternStep.Counterable)), new GUIContent("카운터 가능"));
            EditorGUI.indentLevel -= 1;
        }

        // ------------------------------------------------------------------ util

        private SerializedObject serializedFor(Object InTarget)
        {
            if (Serialized == null || Serialized.targetObject != InTarget)
            {
                Serialized = new SerializedObject(InTarget);
            }

            return Serialized;
        }

        private static void markEdited(BossPatternDefinition InPattern)
        {
            if (InPattern.ToolEdited == false)
            {
                Undo.RecordObject(InPattern, "툴 편집");
                InPattern.ToolEdited = true;
            }

            EditorUtility.SetDirty(InPattern);
        }

        private static void prop(SerializedObject InSo, string InName, string InLabel)
        {
            var property = InSo.FindProperty(InName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(InLabel, property.tooltip));
            }
        }

        /// <summary>[Header] 장식 없이 오브젝트 칸을 그린다 (FBX 를 끌면 안의 클립을 넣는다).</summary>
        private static void objectField(SerializedObject InSo, string InName, string InLabel, System.Type InType)
        {
            var property = InSo.FindProperty(InName);
            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(rect, GUIContent.none, property);
            property.objectReferenceValue = EditorGUI.ObjectField(rect, new GUIContent(InLabel, property.tooltip), property.objectReferenceValue, InType, false);
            EditorGUI.EndProperty();
            var e = Event.current;
            if (InType == typeof(AnimationClip) && (e.type == EventType.DragUpdated || e.type == EventType.DragPerform) && rect.Contains(e.mousePosition) == true
                && DragAndDrop.objectReferences.Any(o => o is AnimationClip) == false)
            {
                var dragged = SkillAbilityToolWindow.DraggedClip();
                if (dragged != null)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (e.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        property.objectReferenceValue = dragged;
                    }

                    e.Use();
                }
            }
        }
    }
}
