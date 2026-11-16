using System.Collections.Generic;
using System.Linq;
using DemoLA.Character;
using DemoLA.Data;
using DemoLA.Skills;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor.SkillTool
{
    /// <summary>
    /// Skill Ability Tool (DemoLA/Tools/Skill Ability Tool) — 캐릭터 스킬(SkillDefinition = SkillAbility 기본값)을 따로 세팅하는 창.
    ///  [왼쪽]  직업 → 스킬 · 각성기 목록 (▶ = 시전 애니메이션 있음)
    ///  [미리보기] 직업 모델에 시전 클립을 재생 · 스크럽. 타격 시점에 판정 범위가 바닥에 그려지고, 돌진 · 전진 거리만큼 모델이 움직인다.
    ///            AnimationClip(또는 클립이 든 FBX)을 미리보기나 '애니메이션' 칸에 끌어다 놓으면 바로 들어간다.
    ///  [타임라인] 타격(빨강) · 후딜 끝(회색) · 클립 구간(파랑, 블렌드 인/아웃 경사). 클릭/드래그로 시간 이동.
    ///  [세부]  애니메이션(클립 · 속도 · 시작 · 블렌드, "타격·후딜 길이에 맞추기") · 기본 · 시간 · 타격 · 전투 · 트라이포드.
    /// 값은 에셋에 바로 기록되고 Undo 된다. 시전 애니메이션은 전체 셋업 뒤에도 SkillId 별로 다시 붙는다 (SkillAnimationSettings).
    /// </summary>
    public class SkillAbilityToolWindow : EditorWindow
    {
        private const float ListWidth = 250.0f;
        private const float TimelineHeight = 46.0f;
        private static readonly float[] PlaybackRates = { 0.1f, 0.25f, 0.5f, 1.0f, 2.0f };
        private static readonly string[] PlaybackLabels = { "0.1×", "0.25×", "0.5×", "1×", "2×" };
        private static readonly Color HitColor = new Color(1.0f, 0.35f, 0.3f);
        private static readonly Color RecoveryColor = new Color(0.6f, 0.6f, 0.6f, 0.35f);
        private static readonly Color ClipColor = new Color(0.3f, 0.6f, 1.0f, 0.35f);
        private static readonly Color CursorColor = new Color(1.0f, 0.9f, 0.3f);

        [SerializeField] private int ClassIndex;
        [SerializeField] private SkillDefinition Selected;
        [SerializeField] private GameObject ModelOverride;
        [SerializeField] private float PreviewHeight = 340.0f;
        [SerializeField] private int RateIndex = 3;
        [SerializeField] private bool Loop = true;

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

        [MenuItem("Window/DemoLA/Skill Ability Tool")]
        [MenuItem("DemoLA/Tools/Skill Ability Tool", priority = 201)]
        public static void Open()
        {
            GetWindow<SkillAbilityToolWindow>("Skill Ability Tool").minSize = new Vector2(960.0f, 560.0f);
        }

        /// <summary>특정 스킬을 열어 둔다 (GAS Tool · 인스펙터 컨텍스트 메뉴).</summary>
        public static void OpenSkill(SkillDefinition InSkill)
        {
            Open();
            var window = GetWindow<SkillAbilityToolWindow>();
            window.select(InSkill);
            window.Focus();
        }

        [MenuItem("CONTEXT/SkillDefinition/Skill Ability Tool 에서 열기")]
        private static void OpenFromContext(MenuCommand InCommand)
        {
            OpenSkill(InCommand.context as SkillDefinition);
        }

        private void OnEnable()
        {
            Preview = new SkillAnimationPreview();
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
            if (Playing == false || Selected == null)
            {
                return;
            }

            Time += dt * PlaybackRates[Mathf.Clamp(RateIndex, 0, PlaybackRates.Length - 1)];
            float end = timelineLength(Selected);
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

            var classes = Database.Classes.Where(c => c != null).ToList();
            if (classes.Count == 0)
            {
                EditorGUILayout.HelpBox("GameDatabase 에 직업이 없습니다.", MessageType.Warning);
                return;
            }

            ClassIndex = Mathf.Clamp(ClassIndex, 0, classes.Count - 1);
            var cls = classes[ClassIndex];

            drawToolbar(classes);
            EditorGUILayout.BeginHorizontal();
            drawList(cls);
            EditorGUILayout.BeginVertical();
            if (Selected == null)
            {
                EditorGUILayout.HelpBox("왼쪽에서 스킬을 고르세요.\n스킬 Ability = 타격(시간 · 판정 · 계수) + 시전 애니메이션. 애니메이션 클립을 미리보기에 끌어다 놓아 넣고, 타임라인에서 타격 시점과 맞춥니다.", MessageType.Info);
            }
            else
            {
                drawPreviewArea(cls);
                drawDetails();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void drawToolbar(List<CharacterClassDefinition> InClasses)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            int next = EditorGUILayout.Popup(ClassIndex, InClasses.Select(c => $"{c.DisplayName} ({c.Class})").ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(200.0f));
            if (next != ClassIndex)
            {
                ClassIndex = next;
                Selected = null;
                GUI.FocusControl(null);
            }

            GUILayout.FlexibleSpace();
            Filter = EditorGUILayout.TextField(Filter, EditorStyles.toolbarSearchField, GUILayout.Width(180.0f));
            if (GUILayout.Button("GAS Tool", EditorStyles.toolbarButton, GUILayout.Width(70.0f)) == true)
            {
                Gas.GasToolWindow.Open();
            }

            if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(50.0f)) == true)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[Skill Ability Tool] saved");
            }

            EditorGUILayout.EndHorizontal();
        }

        private void drawList(CharacterClassDefinition InClass)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(ListWidth));
            ListScroll = EditorGUILayout.BeginScrollView(ListScroll);
            listSection("스킬", InClass.Skills);
            listSection("각성기", InClass.Awakenings);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void listSection(string InHeader, List<SkillDefinition> InSkills)
        {
            EditorGUILayout.LabelField(InHeader, EditorStyles.miniBoldLabel);
            foreach (var skill in InSkills.Where(s => s != null))
            {
                string label = $"{(skill.CastAnimation != null ? "▶ " : "   ")}[{skill.SkillId}] {skill.DisplayName}";
                if (string.IsNullOrEmpty(Filter) == false && label.IndexOf(Filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var style = new GUIStyle(EditorStyles.label) { richText = false };
                if (Selected == skill)
                {
                    style.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.5f, 0.8f, 1.0f) : new Color(0.1f, 0.3f, 0.8f);
                    style.fontStyle = FontStyle.Bold;
                }

                if (GUILayout.Button(label, style) == true)
                {
                    select(skill);
                }
            }
        }

        private void select(SkillDefinition InSkill)
        {
            Selected = InSkill;
            Serialized = null;
            Time = 0.0f;
            Playing = InSkill != null && InSkill.CastAnimation != null;
            GUI.FocusControl(null);
            if (InSkill != null && Database != null)
            {
                var classes = Database.Classes.Where(c => c != null).ToList();
                int index = classes.FindIndex(c => c.Skills.Contains(InSkill) || c.Awakenings.Contains(InSkill));
                if (index >= 0)
                {
                    ClassIndex = index;
                }
            }
        }

        // ------------------------------------------------------------------ preview

        private void drawPreviewArea(CharacterClassDefinition InClass)
        {
            var model = ModelOverride != null ? ModelOverride : InClass.ModelPrefab;
            if (model == null)
            {
                model = AssetDatabase.LoadAssetAtPath<GameObject>(SlayerCharacterBuilder.PrefabPath);
            }

            Preview.SetModel(model);

            // 미리보기 도구 막대
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(Playing ? "❚❚" : "▶", EditorStyles.toolbarButton, GUILayout.Width(28.0f)) == true)
            {
                Playing = !Playing;
                if (Playing == true && Time >= timelineLength(Selected))
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
            Preview.ShowAllHits = GUILayout.Toggle(Preview.ShowAllHits, "모든 타격 범위", EditorStyles.toolbarButton, GUILayout.Width(90.0f));
            Preview.FollowRoot = GUILayout.Toggle(Preview.FollowRoot, "돌진 · 전진", EditorStyles.toolbarButton, GUILayout.Width(72.0f));
            Preview.ShowGrid = GUILayout.Toggle(Preview.ShowGrid, "격자", EditorStyles.toolbarButton, GUILayout.Width(40.0f));
            GUILayout.FlexibleSpace();
            GUILayout.Label("모델", EditorStyles.miniLabel);
            ModelOverride = (GameObject)EditorGUILayout.ObjectField(ModelOverride, typeof(GameObject), false, GUILayout.Width(170.0f));
            EditorGUILayout.EndHorizontal();

            var rect = GUILayoutUtility.GetRect(10.0f, PreviewHeight, GUILayout.ExpandWidth(true));
            handleClipDrop(rect);
            Preview.Sample(Selected, Time);
            Preview.Draw(rect, Selected, Time);
            drawPreviewInfo(rect);

            var timeline = GUILayoutUtility.GetRect(10.0f, TimelineHeight, GUILayout.ExpandWidth(true));
            drawTimeline(timeline, Selected);
            drawSplitter();
        }

        private void drawPreviewInfo(Rect InRect)
        {
            var clip = Selected.CastAnimation;
            string text = clip != null
                ? $"{Time:0.00}s  ·  클립 {SkillAnimationPreview.ClipTimeAt(Selected, Time):0.00}/{clip.length:0.00}s  ·  {clip.name}"
                : $"{Time:0.00}s  ·  애니메이션 없음 — AnimationClip 을 여기에 끌어다 놓으세요";
            var label = new Rect(InRect.x + 6.0f, InRect.y + 4.0f, InRect.width - 12.0f, 18.0f);
            GUI.Label(label, text, EditorStyles.whiteMiniLabel);
            if (clip != null && Preview.IsHumanoid == true && clip.humanMotion == false)
            {
                var warn = new Rect(InRect.x + 6.0f, InRect.y + 20.0f, InRect.width - 12.0f, 18.0f);
                GUI.Label(warn, "⚠ Humanoid 클립이 아닙니다 — 모델 뼈 이름이 같아야 재생됩니다 (FBX Rig → Humanoid 권장)", EditorStyles.whiteMiniLabel);
            }
        }

        /// <summary>AnimationClip 이나 클립이 든 FBX 를 미리보기에 끌어다 놓으면 시전 애니메이션으로 넣는다.</summary>
        private void handleClipDrop(Rect InRect)
        {
            var e = Event.current;
            if ((e.type != EventType.DragUpdated && e.type != EventType.DragPerform) || InRect.Contains(e.mousePosition) == false)
            {
                return;
            }

            var clip = DraggedClip();
            if (clip == null)
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                SetClip(Selected, clip);
            }

            e.Use();
        }

        public static AnimationClip DraggedClip()
        {
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is AnimationClip direct && direct.name.StartsWith("__preview__") == false)
                {
                    return direct;
                }

                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path) == false)
                {
                    var inside = FirstClip(path);
                    if (inside != null)
                    {
                        return inside;
                    }
                }
            }

            return null;
        }

        /// <summary>모델(FBX) 안의 첫 애니메이션 클립.</summary>
        public static AnimationClip FirstClip(string InAssetPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(InAssetPath))
            {
                if (asset is AnimationClip clip && clip.name.StartsWith("__preview__") == false)
                {
                    return clip;
                }
            }

            return null;
        }

        public static void SetClip(SkillDefinition InSkill, AnimationClip InClip)
        {
            Undo.RecordObject(InSkill, "시전 애니메이션");
            InSkill.CastAnimation = InClip;
            if (InSkill.AnimationSpeed <= 0.0f)
            {
                InSkill.AnimationSpeed = 1.0f;
            }

            EditorUtility.SetDirty(InSkill);
        }

        // ------------------------------------------------------------------ timeline

        private static float timelineLength(SkillDefinition InSkill)
        {
            float length = InSkill.CastDuration;
            var clip = InSkill.CastAnimation;
            if (clip != null && clip.isLooping == false)
            {
                length = Mathf.Max(length, (clip.length - InSkill.AnimationStartTime) / Mathf.Max(0.01f, InSkill.AnimationSpeed));
            }

            return Mathf.Max(0.2f, length);
        }

        private void drawTimeline(Rect InRect, SkillDefinition InSkill)
        {
            float length = timelineLength(InSkill) * 1.05f;
            var bar = new Rect(InRect.x + 8.0f, InRect.y + 14.0f, InRect.width - 16.0f, InRect.height - 20.0f);
            EditorGUI.DrawRect(InRect, EditorGUIUtility.isProSkin ? new Color(0.13f, 0.13f, 0.14f) : new Color(0.75f, 0.75f, 0.76f));
            System.Func<float, float> x = t => bar.x + bar.width * Mathf.Clamp01(t / length);

            // 1초 눈금
            for (int s = 0; s <= Mathf.FloorToInt(length); ++s)
            {
                EditorGUI.DrawRect(new Rect(x(s), InRect.y + 2.0f, 1.0f, 8.0f), new Color(1, 1, 1, 0.25f));
                GUI.Label(new Rect(x(s) + 2.0f, InRect.y - 1.0f, 30.0f, 14.0f), $"{s}s", EditorStyles.miniLabel);
            }

            // 클립 구간 + 블렌드 인/아웃
            var clip = InSkill.CastAnimation;
            if (clip != null)
            {
                float speed = Mathf.Max(0.01f, InSkill.AnimationSpeed);
                float clipEnd = clip.isLooping ? InSkill.CastDuration : (clip.length - InSkill.AnimationStartTime) / speed;
                var clipRect = new Rect(x(0.0f), bar.y, x(clipEnd) - x(0.0f), 8.0f);
                EditorGUI.DrawRect(clipRect, ClipColor);
                float blendIn = Mathf.Max(0.0f, InSkill.AnimationBlendIn);
                float blendOutStart = Mathf.Min(clipEnd, InSkill.CastDuration) - Mathf.Max(0.0f, InSkill.AnimationBlendOut);
                EditorGUI.DrawRect(new Rect(x(0.0f), bar.y, x(blendIn) - x(0.0f), 8.0f), new Color(1, 1, 1, 0.25f));
                EditorGUI.DrawRect(new Rect(x(blendOutStart), bar.y, x(Mathf.Min(clipEnd, InSkill.CastDuration)) - x(blendOutStart), 8.0f), new Color(1, 1, 1, 0.25f));
            }

            // 후딜 (마지막 타격 → 시전 끝)
            float lastHit = InSkill.Hits.Count > 0 ? InSkill.HitTime(InSkill.Hits.Count - 1) : 0.0f;
            EditorGUI.DrawRect(new Rect(x(lastHit), bar.y + 10.0f, x(InSkill.CastDuration) - x(lastHit), bar.height - 10.0f), RecoveryColor);
            EditorGUI.DrawRect(new Rect(x(InSkill.CastDuration), bar.y, 2.0f, bar.height), new Color(0.8f, 0.8f, 0.8f));

            // 타격
            for (int i = 0; i < InSkill.Hits.Count; ++i)
            {
                float t = InSkill.HitTime(i);
                EditorGUI.DrawRect(new Rect(x(t) - 1.0f, bar.y + 8.0f, 3.0f, bar.height - 8.0f), HitColor);
                GUI.Label(new Rect(x(t) + 3.0f, bar.y + 10.0f, 24.0f, 14.0f), $"{i + 1}", EditorStyles.miniBoldLabel);
            }

            // 현재 시간
            EditorGUI.DrawRect(new Rect(x(Time) - 1.0f, InRect.y, 2.0f, InRect.height), CursorColor);

            // 클릭 · 드래그로 시간 이동
            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive, InRect);
            if ((e.type == EventType.MouseDown && InRect.Contains(e.mousePosition)) || (e.type == EventType.MouseDrag && GUIUtility.hotControl == id))
            {
                GUIUtility.hotControl = id;
                Playing = false;
                Time = Mathf.Clamp((e.mousePosition.x - bar.x) / bar.width * length, 0.0f, length);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id)
            {
                GUIUtility.hotControl = 0;
                e.Use();
            }
        }

        /// <summary>미리보기 높이 조절 막대.</summary>
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
                PreviewHeight = Mathf.Clamp(PreviewHeight + e.delta.y, 160.0f, position.height - 200.0f);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp)
            {
                ResizingPreview = false;
            }
        }

        // ------------------------------------------------------------------ details

        private void drawDetails()
        {
            if (Serialized == null || Serialized.targetObject != Selected)
            {
                Serialized = new SerializedObject(Selected);
            }

            var so = Serialized;
            so.Update();
            EditorGUIUtility.labelWidth = 170.0f;
            DetailScroll = EditorGUILayout.BeginScrollView(DetailScroll);
            EditorGUILayout.LabelField($"[{Selected.SkillId}] {Selected.DisplayName}{(Selected.IsAwakening ? " (각성기)" : string.Empty)}", EditorStyles.largeLabel);

            drawAnimationSection(so);
            section("기본");
            prop(so, nameof(SkillDefinition.SkillId));
            prop(so, nameof(SkillDefinition.DisplayName), "이름");
            prop(so, nameof(SkillDefinition.Description), "설명");
            prop(so, nameof(SkillDefinition.OwnerClass), "직업");
            prop(so, nameof(SkillDefinition.IconColor), "아이콘 색");

            // 아래 묶음 제목(Timing · Combat · Level · Tripod)은 SkillDefinition 의 [Header] 가 그린다.
            prop(so, nameof(SkillDefinition.Cooldown), "쿨다운 (초)");
            prop(so, nameof(SkillDefinition.ManaCost), "마나");
            prop(so, nameof(SkillDefinition.Recovery), "후딜 (마지막 타격 후)");
            prop(so, nameof(SkillDefinition.DashDistance), "시전 시작 돌진 거리");
            prop(so, nameof(SkillDefinition.DashDuration), "돌진 시간");
            prop(so, nameof(SkillDefinition.LocksMovement), "시전 중 이동 잠금");
            EditorGUILayout.LabelField("시전 시간", $"{Selected.CastDuration:0.00}초 (타격 {Selected.Hits.Count}회: {string.Join(" · ", Enumerable.Range(0, Selected.Hits.Count).Select(i => Selected.HitTime(i).ToString("0.00")))})", EditorStyles.miniLabel);

            prop(so, nameof(SkillDefinition.Hits), "타격 (Delay = 이전 타격으로부터의 지연)", true);
            prop(so, nameof(SkillDefinition.Positional), "백/헤드");
            prop(so, nameof(SkillDefinition.Element), "속성");
            prop(so, nameof(SkillDefinition.Protection), "보호 효과");
            prop(so, nameof(SkillDefinition.SuperArmor), "슈퍼아머");
            prop(so, nameof(SkillDefinition.IsCounter), "카운터");
            prop(so, nameof(SkillDefinition.IsAwakening), "각성기");
            prop(so, nameof(SkillDefinition.AiRange), "AI 사거리 (0 = 자동)");
            prop(so, nameof(SkillDefinition.SelfPartyHealCoefficient), "회복 계수");
            prop(so, nameof(SkillDefinition.PartyShieldAmount), "파티 보호막");
            prop(so, nameof(SkillDefinition.PartyBuffRadius), "파티 버프 반경");
            prop(so, nameof(SkillDefinition.PartyBuffs), "파티 버프", true);

            prop(so, nameof(SkillDefinition.MaxLevel), "최대 레벨");
            prop(so, nameof(SkillDefinition.DamagePerLevel), "레벨당 피해 %");
            prop(so, nameof(SkillDefinition.Tiers), "트라이포드", true);

            EditorGUILayout.Space(8.0f);
            EditorGUILayout.HelpBox("스킬 에셋은 Data/Generated 라 전체 셋업(overwrite) 때 코드(DefaultContent) 값으로 다시 만들어집니다.\n" +
                                    "시전 애니메이션(클립 · 속도 · 시작 · 블렌드)은 SkillId 별로 자동으로 다시 붙고, 그 밖의 수치를 영구히 바꾸려면 DefaultContent.cs 에도 반영하세요.", MessageType.None);
            EditorGUILayout.EndScrollView();
            if (so.ApplyModifiedProperties() == true)
            {
                Repaint();
            }
        }

        private void drawAnimationSection(SerializedObject InSo)
        {
            section("시전 애니메이션");
            var clipProp = InSo.FindProperty(nameof(SkillDefinition.CastAnimation));
            var rect = EditorGUILayout.GetControlRect();
            // PropertyField 는 [Header] 장식을 한 줄 안에 겹쳐 그리므로 ObjectField 로 직접 그린다.
            EditorGUI.BeginProperty(rect, GUIContent.none, clipProp);
            clipProp.objectReferenceValue = EditorGUI.ObjectField(rect, new GUIContent("클립", "AnimationClip 또는 클립이 든 FBX 를 끌어다 놓는다"), clipProp.objectReferenceValue, typeof(AnimationClip), false);
            EditorGUI.EndProperty();
            if ((Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform) && rect.Contains(Event.current.mousePosition) == true)
            {
                var dragged = DraggedClip();
                if (dragged != null && DragAndDrop.objectReferences.Any(o => o is AnimationClip) == false)
                {
                    // FBX 를 끌면 안의 클립을 넣는다 (ObjectField 는 FBX 자체를 받지 않는다).
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (Event.current.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        clipProp.objectReferenceValue = dragged;
                    }

                    Event.current.Use();
                }
            }

            prop(InSo, nameof(SkillDefinition.AnimationSpeed), "재생 배율");
            prop(InSo, nameof(SkillDefinition.AnimationStartTime), "시작 위치 (초)");
            prop(InSo, nameof(SkillDefinition.AnimationBlendIn), "블렌드 인 (초)");
            prop(InSo, nameof(SkillDefinition.AnimationBlendOut), "블렌드 아웃 (초)");

            var clip = clipProp.objectReferenceValue as AnimationClip;
            if (clip == null)
            {
                return;
            }

            float speed = Mathf.Max(0.01f, Selected.AnimationSpeed);
            float played = (clip.length - Selected.AnimationStartTime) / speed;
            EditorGUILayout.LabelField("클립", $"{clip.length:0.00}초 · {(clip.humanMotion ? "Humanoid" : "Generic")} · {(clip.isLooping ? "루프" : "1회")} · 재생 {played:0.00}초 / 시전 {Selected.CastDuration:0.00}초", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUIUtility.labelWidth);
            if (GUILayout.Button(new GUIContent("타격·후딜 길이에 맞추기", "재생 배율 = (클립 길이 − 시작) ÷ 시전 시간 → 클립이 후딜 끝에 끝난다"), EditorStyles.miniButtonLeft) == true && Selected.CastDuration > 0.01f)
            {
                InSo.FindProperty(nameof(SkillDefinition.AnimationSpeed)).floatValue = Mathf.Max(0.05f, (clip.length - Selected.AnimationStartTime) / Selected.CastDuration);
            }

            if (GUILayout.Button(new GUIContent("현재 프레임 = 첫 타격", "미리보기의 지금 클립 시간이 첫 타격 시점에 오도록 재생 배율을 맞춘다"), EditorStyles.miniButtonMid) == true && Selected.Hits.Count > 0)
            {
                float firstHit = Selected.HitTime(0);
                float clipTime = (float)SkillAnimationPreview.ClipTimeAt(Selected, Time);
                if (firstHit > 0.01f && clipTime > Selected.AnimationStartTime)
                {
                    InSo.FindProperty(nameof(SkillDefinition.AnimationSpeed)).floatValue = (clipTime - Selected.AnimationStartTime) / firstHit;
                    Time = firstHit;
                }
            }

            if (GUILayout.Button("1×", EditorStyles.miniButtonRight, GUILayout.Width(32.0f)) == true)
            {
                InSo.FindProperty(nameof(SkillDefinition.AnimationSpeed)).floatValue = 1.0f;
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void section(string InTitle)
        {
            EditorGUILayout.Space(6.0f);
            EditorGUILayout.LabelField(InTitle, EditorStyles.boldLabel);
        }

        private static void prop(SerializedObject InSo, string InName, string InLabel = null, bool InChildren = false)
        {
            var property = InSo.FindProperty(InName);
            if (property == null)
            {
                return;
            }

            if (InLabel == null)
            {
                EditorGUILayout.PropertyField(property, InChildren);
            }
            else
            {
                EditorGUILayout.PropertyField(property, new GUIContent(InLabel, property.tooltip), InChildren);
            }
        }
    }
}
