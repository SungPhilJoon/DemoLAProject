using System;
using DemoLA.Combat;
using DemoLA.Skills;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace DemoLA.Editor.SkillTool
{
    /// <summary>
    /// Skill Ability Tool 의 미리보기: 직업 모델(ModelPrefab)에 스킬 시전 클립을 PlayableGraph 로 샘플링해 그린다.
    ///  - 시간 축 = 스킬 시간 (시전 속도 1배). 클립 시간 = AnimationStartTime + 스킬 시간 × AnimationSpeed — 게임과 같은 대응.
    ///  - 바닥에 1m 격자, 타격 시점 앞뒤로 그 타격의 판정 범위(HitArea)를 그리고, 돌진 · 타격 전진 거리만큼 모델을 앞으로 옮긴다.
    ///  - 마우스: 왼쪽 드래그 = 회전, 휠 = 줌, 오른쪽/가운데 드래그 = 위아래 이동.
    /// </summary>
    public class SkillAnimationPreview : IDisposable
    {
        private const int PreviewLayer = 31;
        private static readonly Color ActiveHitColor = new Color(1.0f, 0.3f, 0.25f, 0.45f);
        private static readonly Color FaintHitColor = new Color(1.0f, 0.55f, 0.2f, 0.12f);
        private static readonly Color GridColor = new Color(1.0f, 1.0f, 1.0f, 0.08f);
        private static readonly Color AxisColor = new Color(0.3f, 0.7f, 1.0f, 0.6f);

        private PreviewRenderUtility preview;
        private GameObject instance;
        private Animator animator;
        private GameObject sourcePrefab;
        private AnimationClip sourceClip;
        private PlayableGraph graph;
        private AnimationClipPlayable clipPlayable;
        private Material lineMaterial;
        private Mesh overlayMesh;
        private float modelScale = 1.0f;

        private Vector2 orbit = new Vector2(150.0f, 15.0f);
        private float distance = 5.0f;
        private float height;
        private Bounds modelBounds = new Bounds(new Vector3(0.0f, 0.9f, 0.0f), new Vector3(0.6f, 1.8f, 0.6f));

        public bool ShowAllHits;
        public bool ShowGrid = true;
        public bool FollowRoot = true;

        public bool HasModel => instance != null;
        public bool IsHumanoid => animator != null && animator.isHuman == true;

        public void Dispose()
        {
            destroyGraph();
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            if (preview != null)
            {
                preview.Cleanup();
                preview = null;
            }

            if (lineMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(lineMaterial);
            }

            if (overlayMesh != null)
            {
                UnityEngine.Object.DestroyImmediate(overlayMesh);
            }
        }

        /// <summary>격자 반경 (m). 보스처럼 판정이 큰 경우 늘린다.</summary>
        public int GridExtent = 10;
        /// <summary>모델 크기 대비 카메라 거리 (모델을 바꿀 때 적용).</summary>
        public float DistanceFactor = 1.15f;

        /// <summary>카메라 기본 각도 (InYaw: 0 = 뒤에서, 180 = 정면에서 / InPitch: 위에서 내려다보는 각).</summary>
        public void SetView(float InYaw, float InPitch)
        {
            orbit = new Vector2(InYaw, InPitch);
        }

        /// <summary>미리보기 모델을 바꾼다 (같은 프리팹 · 배율이면 그대로).</summary>
        public void SetModel(GameObject InPrefab, float InScale = 1.0f)
        {
            ensurePreview();
            if (InPrefab == sourcePrefab && instance != null && Mathf.Approximately(modelScale, InScale) == true)
            {
                return;
            }

            modelScale = InScale;

            destroyGraph();
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            sourcePrefab = InPrefab;
            sourceClip = null;
            animator = null;
            if (InPrefab == null)
            {
                return;
            }

            instance = preview.InstantiatePrefabInScene(InPrefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // 미리보기 카메라는 PreviewCullingLayer(31) 만 그린다 (Camera.PreviewCullingLayer 는 internal).
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = PreviewLayer;
            }

            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false; // 미리보기 씬에서는 스크립트(SpringBone 등)를 돌리지 않는다.
            }

            animator = instance.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            // 스킨 메시 bounds 는 그려지기 전엔 갱신되지 않으므로, 배율 1 에서 재고 배율은 직접 곱한다.
            modelBounds = computeBounds(instance);
            float scale = Mathf.Max(0.01f, InScale);
            instance.transform.localScale = InPrefab.transform.localScale * scale;
            modelBounds = new Bounds(modelBounds.center * scale, modelBounds.size * scale);
            distance = Mathf.Clamp(modelBounds.size.magnitude * DistanceFactor, 1.5f, 60.0f);
            height = 0.0f;
        }

        /// <summary>InSkillTime(초, 시전 속도 1배) 의 자세로 샘플링한다.</summary>
        public void Sample(SkillDefinition InSkill, float InSkillTime)
        {
            if (instance == null || animator == null)
            {
                return;
            }

            var clip = InSkill != null ? InSkill.CastAnimation : null;
            float forward = InSkill != null ? ForwardOffsetAt(InSkill, InSkillTime) : 0.0f;
            SampleClip(clip, ClipTimeAt(InSkill, InSkillTime), FollowRoot == true ? Vector3.forward * forward : Vector3.zero);
        }

        /// <summary>InClip 을 InClipTime(초)에서 샘플링하고 모델 루트를 InRootPosition 에 둔다. 클립이 없으면 바인드 포즈.</summary>
        public void SampleClip(AnimationClip InClip, double InClipTime, Vector3 InRootPosition)
        {
            if (instance == null || animator == null)
            {
                return;
            }

            if (InClip != sourceClip)
            {
                buildGraph(InClip);
            }

            instance.transform.position = InRootPosition;
            if (graph.IsValid() == false)
            {
                return;
            }

            clipPlayable.SetTime(InClipTime);
            clipPlayable.SetTime(InClipTime);
            graph.Evaluate(0.0f);
        }

        /// <summary>스킬 시간 → 클립 시간 (게임과 같은 대응: 시작 위치 + 시간 × 속도, 루프가 아니면 끝에서 멈춤).</summary>
        public static double ClipTimeAt(SkillDefinition InSkill, float InSkillTime)
        {
            var clip = InSkill != null ? InSkill.CastAnimation : null;
            if (clip == null)
            {
                return 0.0;
            }

            double t = InSkill.AnimationStartTime + InSkillTime * Mathf.Max(0.01f, InSkill.AnimationSpeed);
            return clip.isLooping == true ? t % Mathf.Max(0.0001f, clip.length) : Math.Min(t, clip.length);
        }

        /// <summary>시전 시작부터 InSkillTime 까지 정면으로 이동한 거리 (돌진 DashDistance + 타격 MoveForward).</summary>
        public static float ForwardOffsetAt(SkillDefinition InSkill, float InSkillTime)
        {
            float offset = 0.0f;
            if (InSkill.DashDistance > 0.0f)
            {
                offset += InSkill.DashDistance * Mathf.Clamp01(InSkillTime / Mathf.Max(0.01f, InSkill.DashDuration));
            }

            for (int i = 0; i < InSkill.Hits.Count; ++i)
            {
                float hitTime = InSkill.HitTime(i);
                if (InSkill.Hits[i].MoveForward > 0.0f && InSkillTime >= hitTime)
                {
                    offset += InSkill.Hits[i].MoveForward * Mathf.Clamp01((InSkillTime - hitTime) / 0.08f);
                }
            }

            return offset;
        }

        // ------------------------------------------------------------------ draw

        public void Draw(Rect InRect, SkillDefinition InSkill, float InSkillTime)
        {
            float follow = InSkill != null && FollowRoot == true ? ForwardOffsetAt(InSkill, InSkillTime) : 0.0f;
            Draw(InRect, follow * 0.5f, () => skillOverlay(InSkill, InSkillTime));
        }

        /// <summary>
        /// 일반 그리기: 카메라는 (0, 모델 중심, InFollowZ) 를 본다. InOverlay 안에서 DrawArea · DrawLine 으로 바닥 표시를 추가한다.
        /// </summary>
        public void Draw(Rect InRect, float InFollowZ, Action InOverlay)
        {
            ensurePreview();
            handleInput(InRect);
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            preview.BeginPreview(InRect, GUIStyle.none);
            var camera = preview.camera;
            var rotation = Quaternion.Euler(orbit.y, orbit.x, 0.0f);
            var target = new Vector3(0.0f, modelBounds.center.y + height, InFollowZ);
            camera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200.0f;
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = EditorGUIUtility.isProSkin == true ? new Color(0.17f, 0.17f, 0.18f) : new Color(0.62f, 0.62f, 0.64f);

            preview.lights[0].intensity = 1.1f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40.0f, 140.0f, 0.0f);
            preview.lights[1].intensity = 0.6f;
            preview.ambientColor = new Color(0.35f, 0.35f, 0.38f);

            buildGrid();
            InOverlay?.Invoke();
            Ov.Matrix = Matrix4x4.identity;
            if (Ov.ToMesh(ref overlayMesh) == true)
            {
                var material = overlayMaterial();
                preview.DrawMesh(overlayMesh, Matrix4x4.identity, material, 0);
                preview.DrawMesh(overlayMesh, Matrix4x4.identity, material, 1);
            }

            preview.Render(true, false);
            preview.EndAndDrawPreview(InRect);

            if (instance == null)
            {
                GUI.Label(InRect, "미리보기 모델 없음 — 직업 ModelPrefab 을 넣거나 위 '모델' 칸에 프리팹을 넣으세요", centered());
            }
        }

        /// <summary>격자 (메시로 모아 미리보기 씬에서 모델과 깊이 비교되도록 카메라가 함께 그린다).</summary>
        private void buildGrid()
        {
            Ov.Reset();
            Ov.Matrix = Matrix4x4.identity;
            if (ShowGrid == true)
            {
                int n = Mathf.Max(1, GridExtent);
                Ov.Begin(Ov.LINES);
                Ov.SetColor(GridColor);
                for (int i = -n; i <= n; ++i)
                {
                    Ov.Vertex3(i, 0.002f, -n);
                    Ov.Vertex3(i, 0.002f, n);
                    Ov.Vertex3(-n, 0.002f, i);
                    Ov.Vertex3(n, 0.002f, i);
                }

                // 정면(+Z) 방향 표시
                Ov.SetColor(AxisColor);
                Ov.Vertex3(0.0f, 0.004f, 0.0f);
                Ov.Vertex3(0.0f, 0.004f, 1.5f);
                Ov.End();
            }
        }

        /// <summary>판정 범위 하나를 InOrigin 에서 InYaw(도, 0 = +Z) 방향으로 그린다 (Draw 의 InOverlay 안에서).</summary>
        public static void DrawArea(HitArea InArea, Vector3 InOrigin, float InYaw, Color InColor)
        {
            Ov.Matrix = Matrix4x4.TRS(InOrigin, Quaternion.Euler(0.0f, InYaw, 0.0f), Vector3.one);
            drawArea(InArea, Vector3.zero, InColor);
            Ov.Matrix = Matrix4x4.identity;
        }

        /// <summary>바닥 선 (Draw 의 InOverlay 안에서).</summary>
        public static void DrawLine(Vector3 InFrom, Vector3 InTo, Color InColor)
        {
            Ov.Matrix = Matrix4x4.identity;
            Ov.Begin(Ov.LINES);
            Ov.SetColor(InColor);
            Ov.Vertex(InFrom);
            Ov.Vertex(InTo);
            Ov.End();
        }

        public static Color HitColor(float InActive)
        {
            return Color.Lerp(FaintHitColor, ActiveHitColor, InActive);
        }

        private void skillOverlay(SkillDefinition InSkill, float InSkillTime)
        {
            if (InSkill != null)
            {
                for (int i = 0; i < InSkill.Hits.Count; ++i)
                {
                    float hitTime = InSkill.HitTime(i);
                    // 타격 0.15초 전부터 강조 → 0.35초 뒤까지 서서히 사라진다.
                    float dt = InSkillTime - hitTime;
                    float active = dt < -0.15f ? 0.0f : dt < 0.0f ? 1.0f + dt / 0.15f * 0.5f : Mathf.Clamp01(1.0f - dt / 0.35f);
                    if (active <= 0.0f && ShowAllHits == false)
                    {
                        continue;
                    }

                    var color = Color.Lerp(FaintHitColor, ActiveHitColor, active);
                    var origin = Vector3.forward * ForwardOffsetAt(InSkill, hitTime + 0.0001f);
                    drawArea(InSkill.Hits[i].Area, origin, color);
                }
            }

        }

        private static void drawArea(HitArea InArea, Vector3 InOrigin, Color InColor)
        {
            var center = InOrigin + Vector3.forward * InArea.ForwardOffset;
            const float y = 0.01f;
            const int segments = 48;
            var edge = new Color(InColor.r, InColor.g, InColor.b, Mathf.Min(1.0f, InColor.a * 2.2f));
            switch (InArea.Shape)
            {
                case EHitShape.Circle:
                    fan(center, InArea.Radius, 0.0f, 360.0f, segments, y, InColor, edge);
                    break;
                case EHitShape.Fan:
                    fan(center, InArea.Radius, -InArea.Angle * 0.5f, InArea.Angle * 0.5f, segments, y, InColor, edge);
                    break;
                case EHitShape.Donut:
                    ring(center, InArea.InnerRadius, InArea.Radius, segments, y, InColor, edge);
                    break;
                case EHitShape.Rect:
                    var a = center + new Vector3(-InArea.Width * 0.5f, y, 0.0f);
                    var b = center + new Vector3(InArea.Width * 0.5f, y, 0.0f);
                    var c = center + new Vector3(InArea.Width * 0.5f, y, InArea.Length);
                    var d = center + new Vector3(-InArea.Width * 0.5f, y, InArea.Length);
                    Ov.Begin(Ov.TRIANGLES);
                    Ov.SetColor(InColor);
                    Ov.Vertex(a); Ov.Vertex(b); Ov.Vertex(c);
                    Ov.Vertex(a); Ov.Vertex(c); Ov.Vertex(d);
                    Ov.End();
                    Ov.Begin(Ov.LINES);
                    Ov.SetColor(edge);
                    Ov.Vertex(a); Ov.Vertex(b); Ov.Vertex(b); Ov.Vertex(c); Ov.Vertex(c); Ov.Vertex(d); Ov.Vertex(d); Ov.Vertex(a);
                    Ov.End();
                    break;
                case EHitShape.Global:
                    fan(center, 10.0f, 0.0f, 360.0f, segments, y, new Color(InColor.r, InColor.g, InColor.b, InColor.a * 0.4f), edge);
                    break;
            }
        }

        /// <summary>부채꼴/원 (각도 0 = 정면 +Z, 시계 방향 +).</summary>
        private static void fan(Vector3 InCenter, float InRadius, float InFrom, float InTo, int InSegments, float InY, Color InFill, Color InEdge)
        {
            bool full = InTo - InFrom >= 359.9f;
            Ov.Begin(Ov.TRIANGLES);
            Ov.SetColor(InFill);
            for (int i = 0; i < InSegments; ++i)
            {
                Ov.Vertex(InCenter + new Vector3(0.0f, InY, 0.0f));
                Ov.Vertex(InCenter + dir(Mathf.Lerp(InFrom, InTo, i / (float)InSegments), InRadius, InY));
                Ov.Vertex(InCenter + dir(Mathf.Lerp(InFrom, InTo, (i + 1) / (float)InSegments), InRadius, InY));
            }

            Ov.End();
            Ov.Begin(Ov.LINES);
            Ov.SetColor(InEdge);
            for (int i = 0; i < InSegments; ++i)
            {
                Ov.Vertex(InCenter + dir(Mathf.Lerp(InFrom, InTo, i / (float)InSegments), InRadius, InY));
                Ov.Vertex(InCenter + dir(Mathf.Lerp(InFrom, InTo, (i + 1) / (float)InSegments), InRadius, InY));
            }

            if (full == false)
            {
                Ov.Vertex(InCenter + new Vector3(0.0f, InY, 0.0f));
                Ov.Vertex(InCenter + dir(InFrom, InRadius, InY));
                Ov.Vertex(InCenter + new Vector3(0.0f, InY, 0.0f));
                Ov.Vertex(InCenter + dir(InTo, InRadius, InY));
            }

            Ov.End();
        }

        private static void ring(Vector3 InCenter, float InInner, float InOuter, int InSegments, float InY, Color InFill, Color InEdge)
        {
            Ov.Begin(Ov.TRIANGLES);
            Ov.SetColor(InFill);
            for (int i = 0; i < InSegments; ++i)
            {
                float a0 = 360.0f * i / InSegments;
                float a1 = 360.0f * (i + 1) / InSegments;
                var i0 = InCenter + dir(a0, InInner, InY);
                var i1 = InCenter + dir(a1, InInner, InY);
                var o0 = InCenter + dir(a0, InOuter, InY);
                var o1 = InCenter + dir(a1, InOuter, InY);
                Ov.Vertex(i0); Ov.Vertex(o0); Ov.Vertex(o1);
                Ov.Vertex(i0); Ov.Vertex(o1); Ov.Vertex(i1);
            }

            Ov.End();
            Ov.Begin(Ov.LINES);
            Ov.SetColor(InEdge);
            for (int i = 0; i < InSegments; ++i)
            {
                float a0 = 360.0f * i / InSegments;
                float a1 = 360.0f * (i + 1) / InSegments;
                Ov.Vertex(InCenter + dir(a0, InOuter, InY));
                Ov.Vertex(InCenter + dir(a1, InOuter, InY));
                Ov.Vertex(InCenter + dir(a0, InInner, InY));
                Ov.Vertex(InCenter + dir(a1, InInner, InY));
            }

            Ov.End();
        }

        private Material overlayMaterial()
        {
            if (lineMaterial == null)
            {
                // 내장 Internal-Colored 는 LightMode 태그가 없어 URP 에서도 SRPDefaultUnlit 으로 그려진다 (정점 색 · 알파).
                lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                lineMaterial.SetInt("_Cull", (int)CullMode.Off);
                lineMaterial.SetInt("_ZWrite", 0);
                lineMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
                lineMaterial.renderQueue = (int)RenderQueue.Transparent;
            }

            return lineMaterial;
        }

        /// <summary>GL 즉시 모드와 같은 호출로 삼각형(submesh 0) · 선(submesh 1)을 모아 메시로 만든다.</summary>
        private static class Ov
        {
            public const int LINES = 1;
            public const int TRIANGLES = 4;
            private static readonly System.Collections.Generic.List<Vector3> vertices = new System.Collections.Generic.List<Vector3>();
            private static readonly System.Collections.Generic.List<Color> colors = new System.Collections.Generic.List<Color>();
            private static readonly System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();
            private static readonly System.Collections.Generic.List<int> lines = new System.Collections.Generic.List<int>();
            private static int mode;
            private static Color color = Color.white;
            /// <summary>Vertex 에 적용하는 변환 (DrawArea 가 위치 · 방향을 넣는다).</summary>
            public static Matrix4x4 Matrix = Matrix4x4.identity;

            public static void Reset()
            {
                vertices.Clear();
                colors.Clear();
                triangles.Clear();
                lines.Clear();
            }

            public static void Begin(int InMode)
            {
                mode = InMode;
            }

            public static void End()
            {
            }

            public static void SetColor(Color InColor)
            {
                color = InColor;
            }

            public static void Vertex3(float InX, float InY, float InZ)
            {
                Vertex(new Vector3(InX, InY, InZ));
            }

            public static void Vertex(Vector3 InPosition)
            {
                (mode == TRIANGLES ? triangles : lines).Add(vertices.Count);
                vertices.Add(Matrix.MultiplyPoint3x4(InPosition));
                colors.Add(color);
            }

            public static bool ToMesh(ref Mesh InOutMesh)
            {
                if (vertices.Count == 0)
                {
                    return false;
                }

                if (InOutMesh == null)
                {
                    InOutMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
                    InOutMesh.MarkDynamic();
                }

                InOutMesh.Clear();
                InOutMesh.SetVertices(vertices);
                InOutMesh.SetColors(colors);
                InOutMesh.subMeshCount = 2;
                InOutMesh.SetIndices(triangles, MeshTopology.Triangles, 0, false);
                InOutMesh.SetIndices(lines, MeshTopology.Lines, 1, false);
                InOutMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 200.0f);
                return true;
            }
        }

        private static Vector3 dir(float InDegrees, float InRadius, float InY)
        {
            float r = InDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r) * InRadius, InY, Mathf.Cos(r) * InRadius);
        }

        // ------------------------------------------------------------------ input / internals

        private void handleInput(Rect InRect)
        {
            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive, InRect);
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (InRect.Contains(e.mousePosition) == true)
                    {
                        GUIUtility.hotControl = id;
                        e.Use();
                    }

                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }

                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        if (e.button == 0)
                        {
                            orbit.x += e.delta.x * 0.5f;
                            orbit.y = Mathf.Clamp(orbit.y + e.delta.y * 0.5f, -80.0f, 85.0f);
                        }
                        else
                        {
                            height += e.delta.y * 0.01f * distance * 0.2f;
                        }

                        e.Use();
                    }

                    break;
                case EventType.ScrollWheel:
                    if (InRect.Contains(e.mousePosition) == true)
                    {
                        distance = Mathf.Clamp(distance * (1.0f + e.delta.y * 0.05f), 0.8f, 60.0f);
                        e.Use();
                    }

                    break;
            }
        }

        private void ensurePreview()
        {
            if (preview == null)
            {
                preview = new PreviewRenderUtility();
            }
        }

        private void buildGraph(AnimationClip InClip)
        {
            destroyGraph();
            sourceClip = InClip;
            if (InClip == null || animator == null)
            {
                // 클립이 없으면 바인드 포즈로.
                if (animator != null)
                {
                    animator.Rebind();
                }

                return;
            }

            graph = PlayableGraph.Create("SkillAbilityTool.Preview");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            clipPlayable = AnimationClipPlayable.Create(graph, InClip);
            clipPlayable.SetApplyFootIK(true);
            output.SetSourcePlayable(clipPlayable);
            graph.Play();
        }

        private void destroyGraph()
        {
            if (graph.IsValid() == true)
            {
                graph.Destroy();
            }

            graph = default;
            clipPlayable = default;
            sourceClip = null;
        }

        private static Bounds computeBounds(GameObject InRoot)
        {
            var renderers = InRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(new Vector3(0.0f, 0.9f, 0.0f), new Vector3(0.6f, 1.8f, 0.6f));
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; ++i)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static GUIStyle centered()
        {
            var style = new GUIStyle(EditorStyles.wordWrappedLabel) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(1.0f, 1.0f, 1.0f, 0.7f);
            return style;
        }
    }
}
