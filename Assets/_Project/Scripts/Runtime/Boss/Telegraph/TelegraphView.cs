using System.Collections.Generic;
using DemoLA.Combat;
using DemoLA.Core;
using Framework;
using UnityEngine;

namespace DemoLA.Boss.Telegraph
{
    /// <summary>
    /// 바닥 장판 (전조). 바깥 윤곽 + 진행도만큼 커지는 안쪽 채움. 직접 만든 풀로 재사용한다.
    /// 오브젝트(윤곽 · Fill · 머티리얼 에셋 3종)는 프리팹 WorldPrefabs.Telegraph 에 미리 구워 두고, 풀이 비면 그 프리팹을 Instantiate 한다.
    /// 범위 메시는 모양마다 런타임에 만든다 (TelegraphMeshBuilder). 프리팹이 없으면 Build 로 코드 조립 (Sprites/Default 런타임 머티리얼).
    /// </summary>
    public class TelegraphView : BaseBehaviour
    {
        private static readonly Stack<TelegraphView> Pool = new Stack<TelegraphView>();
        private static readonly List<TelegraphView> Active = new List<TelegraphView>();
        private static Material runtimeOutline;
        private static Material runtimeFill;
        private static Material runtimeSafe;

        [SerializeField] private Material outlineMaterial;
        [SerializeField] private Material fillMaterial;
        [SerializeField] private Material safeMaterial;
        [SerializeField] private MeshFilter outlineFilter;
        [SerializeField] private MeshFilter fillFilter;
        [SerializeField] private MeshRenderer outlineRenderer;
        [SerializeField] private MeshRenderer fillRenderer;
        [SerializeField] private Transform fillTransform;
        private HitArea area;
        private bool safeZone;
        private float flashRemaining;
        private float progress;
        private MaterialPropertyBlock block;
        private static int nextNetId;

        /// <summary>Setup 마다 새 번호 (풀에서 다시 쓰여도 다른 장판으로 본다) — 네트워크 복제 짝 맞추기.</summary>
        public int NetId { get; private set; }

        public bool IsSafeZone
        {
            get { return safeZone; }
        }

        public float Progress
        {
            get { return progress; }
        }

        public float LengthScale
        {
            get { return transform.localScale.z; }
        }

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color OutlineColor = new Color(1.0f, 0.15f, 0.1f, 0.22f);
        private static readonly Color FillColor = new Color(1.0f, 0.25f, 0.1f, 0.45f);
        private static readonly Color FlashColor = new Color(1.0f, 0.95f, 0.8f, 0.85f);
        private const float FlashTime = 0.12f;

        public HitArea Area
        {
            get { return area; }
        }

        public static IReadOnlyList<TelegraphView> ActiveViews
        {
            get { return Active; }
        }

        public static TelegraphView Spawn(in HitArea InArea, Vector3 InOrigin, Vector3 InForward, bool InSafeZone = false)
        {
            TelegraphView view = null;
            while (Pool.Count > 0 && view == null)
            {
                view = Pool.Pop();
            }

            if (view == null)
            {
                view = WorldPrefabs.Instantiate<TelegraphView>(WorldPrefabs.Telegraph, Vector3.zero, Quaternion.identity);
                if (view == null)
                {
                    view = new GameObject("Telegraph").AddComponent<TelegraphView>();
                    view.Build(null, null, null);
                }
            }

            view.gameObject.SetActive(true);
            view.Setup(InArea, InOrigin, InForward, InSafeZone);
            Active.Add(view);
            return view;
        }

        public void Setup(in HitArea InArea, Vector3 InOrigin, Vector3 InForward, bool InSafeZone)
        {
            NetId = ++nextNetId;
            progress = 0.0f;
            area = InArea;
            safeZone = InSafeZone;
            flashRemaining = 0.0f;
            transform.localScale = Vector3.one;
            var mesh = TelegraphMeshBuilder.Get(InArea);
            outlineFilter.sharedMesh = mesh;
            fillFilter.sharedMesh = mesh;
            outlineRenderer.sharedMaterial = InSafeZone == true ? safeMaterial : outlineMaterial;
            fillRenderer.enabled = InSafeZone == false;
            Place(InOrigin, InForward);
            SetProgress(0.0f);
        }

        /// <summary>직선 장판 길이 배율 (돌진 장판을 기둥까지 자를 때). 판정 범위(Area)는 바뀌지 않는다.</summary>
        public void SetLengthScale(float InScale)
        {
            transform.localScale = new Vector3(1.0f, 1.0f, Mathf.Clamp(InScale, 0.05f, 1.0f));
        }

        /// <summary>판정 순간: 장판 전체를 밝게 번쩍인 뒤 사라진다.</summary>
        public void FlashAndDespawn()
        {
            Active.Remove(this);
            fillTransform.localScale = Vector3.one;
            fillRenderer.enabled = true;
            setColor(outlineRenderer, FlashColor);
            setColor(fillRenderer, FlashColor);
            flashRemaining = FlashTime;
        }

        private void Update()
        {
            if (flashRemaining <= 0.0f)
            {
                return;
            }

            flashRemaining -= Time.deltaTime;
            var color = FlashColor;
            color.a *= Mathf.Clamp01(flashRemaining / FlashTime);
            setColor(outlineRenderer, color);
            setColor(fillRenderer, color);
            if (flashRemaining <= 0.0f)
            {
                Despawn();
            }
        }

        private void setColor(Renderer InRenderer, Color InColor)
        {
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }

            InRenderer.GetPropertyBlock(block);
            block.SetColor(ColorId, InColor);
            InRenderer.SetPropertyBlock(block);
        }

        private void clearColor(Renderer InRenderer)
        {
            InRenderer.SetPropertyBlock(null);
        }

        public void Place(Vector3 InOrigin, Vector3 InForward)
        {
            var forward = HitGeometry.Flat(InForward);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            transform.SetPositionAndRotation(new Vector3(InOrigin.x, 0.05f, InOrigin.z), Quaternion.LookRotation(forward.normalized, Vector3.up));
        }

        public Vector3 Origin
        {
            get { return transform.position; }
        }

        public Vector3 ForwardDirection
        {
            get { return transform.forward; }
        }

        /// <summary>0 ~ 1. 원/부채꼴/도넛은 중심에서 커지고 직선은 앞으로 뻗는다.</summary>
        public void SetProgress(float InRatio)
        {
            float ratio = Mathf.Clamp01(InRatio);
            progress = ratio;
            if (flashRemaining > 0.0f)
            {
                return;
            }

            // 판정 직전(마지막 25%)에는 윤곽이 점점 빠르게 깜빡여 타이밍을 알려 준다.
            if (safeZone == false && ratio > 0.75f)
            {
                float k = (ratio - 0.75f) / 0.25f;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(12.0f, 30.0f, k));
                var outline = OutlineColor;
                outline.a = Mathf.Lerp(OutlineColor.a, 0.6f, pulse * k);
                setColor(outlineRenderer, outline);
            }
            else
            {
                clearColor(outlineRenderer);
                clearColor(fillRenderer);
            }

            if (area.Shape == EHitShape.Rect)
            {
                fillTransform.localScale = new Vector3(1.0f, 1.0f, Mathf.Max(0.001f, ratio));
            }
            else
            {
                fillTransform.localScale = new Vector3(Mathf.Max(0.001f, ratio), 1.0f, Mathf.Max(0.001f, ratio));
            }
        }

        public void Despawn()
        {
            Active.Remove(this);
            flashRemaining = 0.0f;
            if (outlineRenderer != null)
            {
                clearColor(outlineRenderer);
                clearColor(fillRenderer);
            }

            if (this == null)
            {
                return;
            }

            gameObject.SetActive(false);
            Pool.Push(this);
        }

        /// <summary>점이 이 장판 안에 있는가 (아군 AI 회피용).</summary>
        public bool Contains(Vector3 InPoint, float InRadius)
        {
            return HitGeometry.Contains(area, transform.position, transform.forward, InPoint, InRadius);
        }

        protected override void OnUnityDestroy()
        {
            Active.Remove(this);
        }

        /// <summary>
        /// 윤곽(자기 자신) · Fill 자식을 만든다 (프리팹 굽기 · 코드 조립 공용). 머티리얼이 null 이면 런타임 머티리얼 (Sprites/Default).
        /// </summary>
        public void Build(Material InOutline, Material InFill, Material InSafe)
        {
            if (InOutline == null || InFill == null || InSafe == null)
            {
                ensureRuntimeMaterials();
            }

            outlineMaterial = InOutline != null ? InOutline : runtimeOutline;
            fillMaterial = InFill != null ? InFill : runtimeFill;
            safeMaterial = InSafe != null ? InSafe : runtimeSafe;

            outlineFilter = gameObject.AddComponent<MeshFilter>();
            outlineRenderer = gameObject.AddComponent<MeshRenderer>();
            outlineRenderer.sharedMaterial = outlineMaterial;
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;

            var fill = new GameObject("Fill");
            fillTransform = fill.transform;
            fillTransform.SetParent(transform, false);
            fillTransform.localPosition = new Vector3(0.0f, 0.01f, 0.0f);
            fillFilter = fill.AddComponent<MeshFilter>();
            fillRenderer = fill.AddComponent<MeshRenderer>();
            fillRenderer.sharedMaterial = fillMaterial;
            fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fillRenderer.receiveShadows = false;
        }

        /// <summary>굽기용 머티리얼 색 · 렌더 큐 (윤곽 · 채움 · 안전 구역).</summary>
        public static void ConfigureMaterials(Material InOutline, Material InFill, Material InSafe)
        {
            InOutline.color = OutlineColor;
            InFill.color = FillColor;
            InSafe.color = SafeColor;
            InOutline.renderQueue = 3000;
            InFill.renderQueue = 3001;
            InSafe.renderQueue = 3000;
        }

        private static readonly Color SafeColor = new Color(0.2f, 0.6f, 1.0f, 0.3f);

        private static void ensureRuntimeMaterials()
        {
            if (runtimeOutline != null)
            {
                return;
            }

            // Sprites/Default 는 기본 Always Included 셰이더라 빌드에서도 빠지지 않는다. URP 에서도 Unlit 으로 그려진다.
            var shader = Shader.Find("Sprites/Default");
            runtimeOutline = new Material(shader);
            runtimeFill = new Material(shader);
            runtimeSafe = new Material(shader);
            ConfigureMaterials(runtimeOutline, runtimeFill, runtimeSafe);
        }
    }
}
