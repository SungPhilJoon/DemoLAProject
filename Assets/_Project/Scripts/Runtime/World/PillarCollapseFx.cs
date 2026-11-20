using System.Collections.Generic;
using DemoLA.Combat;
using DemoLA.Core;
using Framework;
using UnityEngine;

namespace DemoLA.World
{
    /// <summary>
    /// 기둥 붕괴 연출. 원본 기둥은 즉시 비활성(판정/내비 장애물 해제)하고, 복제 메시를 충돌 반대 방향으로
    /// 기울이며 바닥으로 가라앉힌다. 동시에 파편이 튀고 바닥에 먼지 고리가 퍼진다. 판정에는 관여하지 않는다.
    /// 오브젝트(Body · Dust · 파편 템플릿 · 먼지 머티리얼 에셋)는 프리팹 WorldPrefabs.PillarCollapseFx 에 미리 구워 두고,
    /// 런타임에는 원본 기둥의 메시 · 머티리얼 · 위치만 넣고 파편을 템플릿에서 복제한다. 프리팹이 없으면 Build 로 코드 조립.
    /// </summary>
    public class PillarCollapseFx : BaseBehaviour
    {
        public float TopplingTime = 0.9f;
        public float SinkTime = 0.8f;
        public float MaxTiltAngle = 75.0f;
        public int ChunkCount = 12;
        public float ChunkLifetime = 2.2f;

        private static Material dustMaterial;

        [SerializeField] private MeshFilter bodyFilter;
        [SerializeField] private MeshRenderer bodyRenderer;
        [SerializeField] private Renderer dustTemplate;
        [SerializeField] private GameObject chunkTemplate;

        private Transform body;
        private Vector3 pivot;
        private Vector3 tiltAxis;
        private Quaternion startRotation;
        private Vector3 startPosition;
        private float height;
        private float elapsed;
        private Transform dust;
        private Renderer dustRenderer;
        private float dustRadius;
        private MaterialPropertyBlock block;
        private readonly List<Transform> chunks = new List<Transform>();
        private readonly List<Vector3> chunkScales = new List<Vector3>();

        /// <summary>InSource 의 메시를 복제해 붕괴시킨다. InPushDirection = 기둥이 쓰러질 방향 (보스 돌진 방향).</summary>
        public static void Play(GameObject InSource, Vector3 InPushDirection)
        {
            var sourceRenderer = InSource.GetComponent<MeshRenderer>();
            var sourceFilter = InSource.GetComponent<MeshFilter>();
            if (sourceRenderer == null || sourceFilter == null)
            {
                return;
            }

            var fx = WorldPrefabs.Instantiate<PillarCollapseFx>(WorldPrefabs.PillarCollapseFx, Vector3.zero, Quaternion.identity);
            if (fx == null)
            {
                fx = new GameObject().AddComponent<PillarCollapseFx>();
                fx.Build(null);
            }

            fx.gameObject.name = $"{InSource.name}_Collapse";
            fx.setup(InSource, sourceFilter.sharedMesh, sourceRenderer.sharedMaterial, sourceRenderer.bounds, InPushDirection);
        }

        /// <summary>
        /// Body(메시는 비움) · Dust(원판) · 파편 템플릿(꺼 둔 큐브 + Rigidbody)을 만든다 (프리팹 굽기 · 코드 조립 공용).
        /// InDustMaterial 이 null 이면 런타임 머티리얼 (Sprites/Default).
        /// </summary>
        public void Build(Material InDustMaterial)
        {
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            bodyFilter = bodyGo.AddComponent<MeshFilter>();
            bodyRenderer = bodyGo.AddComponent<MeshRenderer>();

            var dustGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dustGo.name = "Dust";
            ObjectUtil.SafeDestroy(dustGo.GetComponent<Collider>());
            dustGo.transform.SetParent(transform, false);
            dustTemplate = dustGo.GetComponent<Renderer>();
            if (InDustMaterial == null && dustMaterial == null)
            {
                dustMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.75f, 0.7f, 0.62f, 0.5f) };
                dustMaterial.renderQueue = 3002;
            }

            dustTemplate.sharedMaterial = InDustMaterial != null ? InDustMaterial : dustMaterial;
            dustTemplate.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            chunkTemplate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chunkTemplate.name = "ChunkTemplate";
            chunkTemplate.layer = 2; // Ignore Raycast — 클릭 이동 레이캐스트를 막지 않는다.
            chunkTemplate.transform.SetParent(transform, false);
            var rigidbody = chunkTemplate.AddComponent<Rigidbody>();
            rigidbody.mass = 0.3f;
            chunkTemplate.SetActive(false);
        }

        private void setup(GameObject InSource, Mesh InMesh, Material InMaterial, Bounds InBounds, Vector3 InPushDirection)
        {
            body = bodyFilter.transform;
            body.SetPositionAndRotation(InSource.transform.position, InSource.transform.rotation);
            body.localScale = InSource.transform.lossyScale;
            bodyFilter.sharedMesh = InMesh;
            bodyRenderer.sharedMaterial = InMaterial;

            height = InBounds.size.y;
            pivot = new Vector3(InBounds.center.x, InBounds.min.y, InBounds.center.z);
            var push = HitGeometry.Flat(InPushDirection);
            if (push.sqrMagnitude < 0.0001f)
            {
                push = Vector3.forward;
            }

            push.Normalize();
            tiltAxis = Vector3.Cross(Vector3.up, push);
            startRotation = body.rotation;
            startPosition = body.position;

            spawnChunks(InBounds, InMaterial, push);
            spawnDust(pivot, Mathf.Max(InBounds.extents.x, InBounds.extents.z));
        }

        private void spawnChunks(Bounds InBounds, Material InMaterial, Vector3 InPush)
        {
            for (int i = 0; i < ChunkCount; ++i)
            {
                var chunk = Instantiate(chunkTemplate, transform, false);
                chunk.name = "Chunk";
                chunk.SetActive(true);
                float size = Random.Range(0.25f, 0.6f);
                var scale = new Vector3(size, size * Random.Range(0.6f, 1.2f), size * Random.Range(0.6f, 1.2f));
                chunk.transform.localScale = scale;
                chunk.transform.position = new Vector3(
                    InBounds.center.x + Random.Range(-InBounds.extents.x, InBounds.extents.x) * 0.8f,
                    InBounds.min.y + Random.Range(0.3f, InBounds.size.y * 0.9f),
                    InBounds.center.z + Random.Range(-InBounds.extents.z, InBounds.extents.z) * 0.8f);
                chunk.transform.rotation = Random.rotation;
                chunk.GetComponent<Renderer>().sharedMaterial = InMaterial;

                var rigidbody = chunk.GetComponent<Rigidbody>();
                var outward = HitGeometry.Flat(chunk.transform.position - InBounds.center);
                if (outward.sqrMagnitude < 0.0001f)
                {
                    outward = Random.insideUnitSphere;
                    outward.y = 0.0f;
                }

                // 돌진 방향으로 더 많이 튄다.
                var direction = (outward.normalized + InPush * 1.2f).normalized;
                rigidbody.linearVelocity = direction * Random.Range(1.5f, 4.0f) + Vector3.up * Random.Range(2.0f, 4.5f);
                rigidbody.angularVelocity = Random.insideUnitSphere * 10.0f;
                chunks.Add(chunk.transform);
                chunkScales.Add(scale);
            }
        }

        private void spawnDust(Vector3 InPosition, float InRadius)
        {
            var go = dustTemplate.gameObject;
            go.transform.position = new Vector3(InPosition.x, 0.06f, InPosition.z);
            go.transform.localScale = new Vector3(InRadius * 2.0f, 0.01f, InRadius * 2.0f);
            dustRenderer = dustTemplate;
            dust = go.transform;
            dustRadius = InRadius;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            // 기둥: 밑동 기준으로 기울다가 가라앉는다.
            if (body != null)
            {
                float tilt = Mathf.Clamp01(elapsed / TopplingTime);
                tilt = tilt * tilt; // 처음엔 천천히, 점점 빠르게 넘어간다.
                var rotation = Quaternion.AngleAxis(MaxTiltAngle * tilt, tiltAxis);
                float sink = Mathf.Clamp01((elapsed - TopplingTime * 0.6f) / SinkTime);
                body.rotation = rotation * startRotation;
                body.position = pivot + rotation * (startPosition - pivot) + Vector3.down * (height * sink);
                if (sink >= 1.0f)
                {
                    ObjectUtil.SafeDestroy(body.gameObject);
                    body = null;
                }
            }

            // 먼지 고리: 빠르게 퍼지며 옅어진다.
            if (dust != null)
            {
                float t = Mathf.Clamp01(elapsed / 1.2f);
                float grow = 1.0f + 1.2f * (1.0f - (1.0f - t) * (1.0f - t));
                dust.localScale = new Vector3(dustRadius * 2.0f * grow, 0.01f, dustRadius * 2.0f * grow);
                if (block == null)
                {
                    block = new MaterialPropertyBlock();
                }

                block.SetColor("_Color", new Color(0.75f, 0.7f, 0.62f, 0.3f * (1.0f - t) * (1.0f - t)));
                dustRenderer.SetPropertyBlock(block);
                if (t >= 1.0f)
                {
                    ObjectUtil.SafeDestroy(dust.gameObject);
                    dust = null;
                }
            }

            // 파편: 수명 마지막 0.5초 동안 작아지며 사라진다.
            float shrink = Mathf.Clamp01((ChunkLifetime - elapsed) / 0.5f);
            for (int i = 0; i < chunks.Count; ++i)
            {
                if (chunks[i] != null)
                {
                    chunks[i].localScale = chunkScales[i] * shrink;
                }
            }

            if (elapsed >= ChunkLifetime && body == null && dust == null)
            {
                ObjectUtil.SafeDestroy(gameObject);
            }
        }
    }
}
