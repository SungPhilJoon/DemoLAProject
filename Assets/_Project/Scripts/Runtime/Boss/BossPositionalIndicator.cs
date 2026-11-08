using DemoLA.Attributes;
using DemoLA.Combat;
using DemoLA.Core;
using Framework;
using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// 보스 발밑 백어택 / 헤드어택 범위 표시. 판정과 같은 각도(StatFormula.BackAttackAngle / HeadAttackAngle)로
    /// 보스 몸 바깥에 고리 조각 두 개를 그리고 보스와 함께 돈다.
    ///  - 뒤쪽(백어택) = 붉은 주황, 앞쪽(헤드어택) = 하늘색
    ///  - 내 캐릭터가 그 범위 안에 서 있으면 해당 조각이 밝아지며 맥동한다.
    /// </summary>
    public class BossPositionalIndicator : BaseBehaviour
    {
        public float RingWidth = 1.3f;
        public float Height = 0.04f;

        private static readonly Color BackColor = new Color(1.0f, 0.35f, 0.2f);
        private static readonly Color HeadColor = new Color(0.3f, 0.75f, 1.0f);
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static Material material;

        private BossActor boss;
        private MeshRenderer backRenderer;
        private MeshRenderer headRenderer;
        private MaterialPropertyBlock block;
        private float builtBackAngle = -1.0f;
        private float builtHeadAngle = -1.0f;
        private float builtRadius = -1.0f;

        protected override void OnUnityAwake()
        {
            // BossActor 는 이 컴포넌트보다 나중에 붙으므로 여기서 찾지 않는다 (LateUpdate 에서 찾는다).
            block = new MaterialPropertyBlock();
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                material.renderQueue = 2999; // 장판(3000~) 아래
            }

            backRenderer = createPart("BackAttackZone");
            headRenderer = createPart("HeadAttackZone");
        }

        private MeshRenderer createPart(string InName)
        {
            var go = new GameObject(InName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0.0f, Height, 0.0f);
            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private void LateUpdate()
        {
            if (boss == null)
            {
                boss = GetComponent<BossActor>();
            }

            bool visible = boss != null && boss.IsInitialized == true && boss.IsDead == false && StatFormula.Settings != null;
            backRenderer.enabled = visible;
            headRenderer.enabled = visible;
            if (visible == false)
            {
                return;
            }

            rebuildIfNeeded();

            // 내 캐릭터 위치로 강조.
            var positional = EPositional.None;
            var bootstrap = GameBootstrap.Current;
            var player = bootstrap != null ? bootstrap.Player : null;
            if (player != null && player.IsDead == false)
            {
                float distance = HitGeometry.Flat(player.Position - boss.Position).magnitude;
                if (distance <= boss.Radius + RingWidth + 4.0f)
                {
                    positional = HitGeometry.ResolvePositional(boss.Position, boss.Forward, player.Position);
                }
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 7.0f);
            paint(backRenderer, BackColor, positional == EPositional.BackAttack, pulse);
            paint(headRenderer, HeadColor, positional == EPositional.HeadAttack, pulse);
        }

        private void paint(MeshRenderer InRenderer, Color InColor, bool InActive, float InPulse)
        {
            var color = InColor;
            color.a = InActive ? Mathf.Lerp(0.55f, 0.85f, InPulse) : 0.28f;
            InRenderer.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            InRenderer.SetPropertyBlock(block);
        }

        private void rebuildIfNeeded()
        {
            var settings = StatFormula.Settings;
            float backHalf = Mathf.Clamp(180.0f - settings.BackAttackAngle, 0.0f, 180.0f);
            float headHalf = Mathf.Clamp(settings.HeadAttackAngle, 0.0f, 180.0f);
            float radius = boss.Radius;
            if (Mathf.Approximately(backHalf, builtBackAngle) && Mathf.Approximately(headHalf, builtHeadAngle) && Mathf.Approximately(radius, builtRadius))
            {
                return;
            }

            builtBackAngle = backHalf;
            builtHeadAngle = headHalf;
            builtRadius = radius;
            // 보스 Transform 이 커질 수 있으므로 로컬 크기를 월드 크기로 맞춘다.
            float scale = Mathf.Max(0.0001f, transform.lossyScale.x);
            float inner = radius / scale;
            float outer = (radius + RingWidth) / scale;
            backRenderer.GetComponent<MeshFilter>().sharedMesh = sector(180.0f, backHalf, inner, outer);
            headRenderer.GetComponent<MeshFilter>().sharedMesh = sector(0.0f, headHalf, inner, outer);
        }

        /// <summary>정면(+Z) 기준 InCenter 도 방향으로 ±InHalf 도인 고리 조각. 바깥쪽으로 갈수록 옅어진다(정점 색).</summary>
        private static Mesh sector(float InCenter, float InHalf, float InInner, float InOuter)
        {
            int segments = Mathf.Max(4, Mathf.CeilToInt(InHalf * 2.0f / 6.0f));
            var vertices = new Vector3[(segments + 1) * 2];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; ++i)
            {
                float angle = (InCenter - InHalf + InHalf * 2.0f * i / segments) * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Sin(angle), 0.0f, Mathf.Cos(angle));
                vertices[i * 2] = direction * InInner;
                vertices[i * 2 + 1] = direction * InOuter;
                // 양 끝(경계)은 조금 더 진하게 → 범위 경계가 잘 보인다.
                float edge = i == 0 || i == segments ? 1.0f : 0.8f;
                colors[i * 2] = new Color(1.0f, 1.0f, 1.0f, edge);
                colors[i * 2 + 1] = new Color(1.0f, 1.0f, 1.0f, 0.25f * edge);
                if (i < segments)
                {
                    int t = i * 6;
                    int v = i * 2;
                    triangles[t] = v;
                    triangles[t + 1] = v + 1;
                    triangles[t + 2] = v + 3;
                    triangles[t + 3] = v;
                    triangles[t + 4] = v + 3;
                    triangles[t + 5] = v + 2;
                }
            }

            var mesh = new Mesh { name = "PositionalSector" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
