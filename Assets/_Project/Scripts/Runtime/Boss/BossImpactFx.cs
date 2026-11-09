using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// 크게 터지는 착지 연출: 바닥을 따라 퍼지는 충격파 고리 2겹(LineRenderer) + 가운데 섬광 기둥 + 강한 화면 흔들림.
    /// BossPatternAbility(BigImpact 단계)가 서버에서 부르고, 네트워크 클라이언트는 NetworkRaidDirector 의 RPC 로 같은 연출을 본다.
    /// 재질은 런타임에 만든다 (Sprites/Default — 별도 셰이더 없이 빌드에 포함).
    /// </summary>
    public class BossImpactFx : MonoBehaviour
    {
        private const float Duration = 0.9f;
        private const int Segments = 72;

        private LineRenderer outer;
        private LineRenderer inner;
        private Transform pillar;
        private Material lineMaterial;
        private Material pillarMaterial;
        private float radius;
        private float strength;
        private float elapsed;

        /// <summary>InRadius 까지 퍼지는 충격파. InStrength 1 = 즉사기 착지, 0.5 = 재도약 착지.</summary>
        public static void Play(Vector3 InPosition, float InRadius, float InStrength)
        {
            DemoLA.Player.QuarterViewCamera.Shake(1.4f * InStrength, 0.6f + 0.6f * InStrength);
            if (Application.isBatchMode == true)
            {
                return;
            }

            var go = new GameObject("BossImpactFx");
            go.transform.position = new Vector3(InPosition.x, 0.12f, InPosition.z);
            var fx = go.AddComponent<BossImpactFx>();
            fx.radius = Mathf.Max(2.0f, InRadius);
            fx.strength = Mathf.Clamp01(InStrength);
            fx.build();
        }

        private void build()
        {
            var shader = Shader.Find("Sprites/Default");
            lineMaterial = new Material(shader);
            pillarMaterial = new Material(shader) { color = new Color(1.0f, 0.95f, 0.8f, 0.7f) };
            outer = createRing("Outer", 1.6f * (0.5f + strength));
            inner = createRing("Inner", 0.8f * (0.5f + strength));

            pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            Destroy(pillar.GetComponent<Collider>());
            pillar.SetParent(transform, false);
            pillar.localScale = new Vector3(1.6f * strength + 0.4f, 6.0f * strength, 1.6f * strength + 0.4f);
            pillar.localPosition = Vector3.up * 6.0f * strength;
            pillar.GetComponent<Renderer>().sharedMaterial = pillarMaterial;
        }

        private LineRenderer createRing(string InName, float InWidth)
        {
            var go = new GameObject(InName);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Segments;
            line.widthMultiplier = InWidth;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.TransformZ;
            go.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
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

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            float eased = 1.0f - (1.0f - t) * (1.0f - t);
            float fade = 1.0f - t;
            setRing(outer, Mathf.Max(0.5f, radius * eased), new Color(1.0f, 0.7f, 0.35f, 0.85f * fade));
            setRing(inner, Mathf.Max(0.3f, radius * eased * 0.7f), new Color(1.0f, 0.92f, 0.75f, 0.6f * fade));

            float pillarT = Mathf.Clamp01(elapsed / (Duration * 0.45f));
            var pillarColor = pillarMaterial.color;
            pillarColor.a = 0.7f * (1.0f - pillarT);
            pillarMaterial.color = pillarColor;
            if (t >= 1.0f)
            {
                Destroy(lineMaterial);
                Destroy(pillarMaterial);
                Destroy(gameObject);
            }
        }
    }
}
