using DemoLA.Core;
using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// 크게 터지는 착지 연출: 바닥을 따라 퍼지는 충격파 고리 2겹(LineRenderer) + 가운데 섬광 기둥 + 강한 화면 흔들림.
    /// BossPatternAbility(BigImpact 단계)가 서버에서 부르고, 네트워크 클라이언트는 NetworkRaidDirector 의 RPC 로 같은 연출을 본다.
    /// 오브젝트(고리 · 기둥 · 머티리얼 에셋)는 프리팹 WorldPrefabs.BossImpactFx 에 미리 구워 두고, 세기에 따른 크기 · 색만 런타임에 정한다.
    /// 프리팹이 없으면 Build 로 코드 조립 (Sprites/Default 런타임 머티리얼).
    /// </summary>
    public class BossImpactFx : MonoBehaviour
    {
        private const float Duration = 0.9f;
        private const int Segments = 72;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color PillarColor = new Color(1.0f, 0.95f, 0.8f, 0.7f);

        [SerializeField] private LineRenderer outer;
        [SerializeField] private LineRenderer inner;
        [SerializeField] private Renderer pillar;

        private MaterialPropertyBlock block;
        private Material runtimeLineMaterial;
        private Material runtimePillarMaterial;
        private float radius;
        private float strength;
        private float elapsed;

        /// <summary>InRadius 까지 퍼지는 충격파. InStrength 1 = 즉사기 착지, 0.5 = 재도약 착지.</summary>
        public static void Play(Vector3 InPosition, float InRadius, float InStrength)
        {
            DemoLA.Player.QuarterViewCamera.Shake(1.4f * InStrength, 0.6f + 0.6f * InStrength);
            if (Application.isBatchMode == true || Application.isPlaying == false)
            {
                return;
            }

            var position = new Vector3(InPosition.x, 0.12f, InPosition.z);
            var fx = WorldPrefabs.Instantiate<BossImpactFx>(WorldPrefabs.BossImpactFx, position, Quaternion.identity);
            if (fx == null)
            {
                var go = new GameObject("BossImpactFx");
                go.transform.position = position;
                fx = go.AddComponent<BossImpactFx>();
                fx.Build(null, null);
            }

            fx.begin(Mathf.Max(2.0f, InRadius), Mathf.Clamp01(InStrength));
        }

        /// <summary>
        /// 고리 2개 · 기둥을 만든다 (프리팹 굽기 · 코드 조립 공용). InLineMaterial / InPillarMaterial 이 null 이면 런타임 머티리얼.
        /// 크기는 세기에 따라 begin 이 정한다.
        /// </summary>
        public void Build(Material InLineMaterial, Material InPillarMaterial)
        {
            var lineMaterial = InLineMaterial;
            var pillarMaterial = InPillarMaterial;
            if (lineMaterial == null || pillarMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                runtimeLineMaterial = lineMaterial == null ? new Material(shader) : null;
                runtimePillarMaterial = pillarMaterial == null ? new Material(shader) { color = PillarColor } : null;
                lineMaterial = lineMaterial != null ? lineMaterial : runtimeLineMaterial;
                pillarMaterial = pillarMaterial != null ? pillarMaterial : runtimePillarMaterial;
            }

            outer = createRing("Outer", lineMaterial);
            inner = createRing("Inner", lineMaterial);

            var pillarGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillarGo.name = "Pillar";
            ObjectUtil.SafeDestroy(pillarGo.GetComponent<Collider>());
            pillarGo.transform.SetParent(transform, false);
            pillar = pillarGo.GetComponent<Renderer>();
            pillar.sharedMaterial = pillarMaterial;
            pillar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private LineRenderer createRing(string InName, Material InMaterial)
        {
            var go = new GameObject(InName);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = InMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Segments;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.TransformZ;
            go.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        private void begin(float InRadius, float InStrength)
        {
            radius = InRadius;
            strength = InStrength;
            elapsed = 0.0f;
            outer.widthMultiplier = 1.6f * (0.5f + strength);
            inner.widthMultiplier = 0.8f * (0.5f + strength);
            pillar.transform.localScale = new Vector3(1.6f * strength + 0.4f, 6.0f * strength, 1.6f * strength + 0.4f);
            pillar.transform.localPosition = Vector3.up * 6.0f * strength;
            block = new MaterialPropertyBlock();
            setPillarAlpha(PillarColor.a);
        }

        private static void setRing(LineRenderer InLine, float InRadius, Color InColor)
        {
            for (int i = 0; i < Segments; ++i)
            {
                float angle = i * Mathf.PI * 2.0f / Segments;
                InLine.SetPosition(i, new Vector3(Mathf.Cos(angle) * InRadius, Mathf.Sin(angle) * InRadius, 0.0f));
            }

            InLine.startColor = InColor;
            InLine.endColor = InColor;
        }

        private void setPillarAlpha(float InAlpha)
        {
            var color = PillarColor;
            color.a = InAlpha;
            pillar.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            pillar.SetPropertyBlock(block);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            float eased = 1.0f - (1.0f - t) * (1.0f - t);
            float fade = 1.0f - t;
            setRing(outer, Mathf.Max(0.5f, radius * eased), new Color(1.0f, 0.7f, 0.35f, 0.85f * fade));
            setRing(inner, Mathf.Max(0.3f, radius * eased * 0.7f), new Color(1.0f, 0.92f, 0.75f, 0.6f * fade));

            float pillarT = Mathf.Clamp01(elapsed / (Duration * 0.45f));
            setPillarAlpha(PillarColor.a * (1.0f - pillarT));
            if (t >= 1.0f)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (runtimeLineMaterial != null)
            {
                Destroy(runtimeLineMaterial);
            }

            if (runtimePillarMaterial != null)
            {
                Destroy(runtimePillarMaterial);
            }
        }
    }
}
