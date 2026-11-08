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
    /// </summary>
    public class PillarCollapseFx : BaseBehaviour
    {
        public float TopplingTime = 0.9f;
        public float SinkTime = 0.8f;
        public float MaxTiltAngle = 75.0f;
        public int ChunkCount = 12;
        public float ChunkLifetime = 2.2f;

        private static Material dustMaterial;

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

            var root = new GameObject($"{InSource.name}_Collapse");
            var fx = root.AddComponent<PillarCollapseFx>();
            fx.setup(InSource, sourceFilter.sharedMesh, sourceRenderer.sharedMaterial, sourceRenderer.bounds, InPushDirection);
        }

        private void setup(GameObject InSource, Mesh InMesh, Material InMaterial, Bounds InBounds, Vector3 InPushDirection)
        {
            var bodyGo = new GameObject("Body");
            body = bodyGo.transform;
            body.SetParent(transform, false);
            body.SetPositionAndRotation(InSource.transform.position, InSource.transform.rotation);
            body.localScale = InSource.transform.lossyScale;
            bodyGo.AddComponent<MeshFilter>().sharedMesh = InMesh;
            bodyGo.AddComponent<MeshRenderer>().sharedMaterial = InMaterial;

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
                var chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.name = "Chunk";
                chunk.layer = 2; // Ignore Raycast — 클릭 이동 레이캐스트를 막지 않는다.
                chunk.transform.SetParent(transform, false);
                float size = Random.Range(0.25f, 0.6f);
                var scale = new Vector3(size, size * Random.Range(0.6f, 1.2f), size * Random.Range(0.6f, 1.2f));
                chunk.transform.localScale = scale;
                chunk.transform.position = new Vector3(
                    InBounds.center.x + Random.Range(-InBounds.extents.x, InBounds.extents.x) * 0.8f,
                    InBounds.min.y + Random.Range(0.3f, InBounds.size.y * 0.9f),
                    InBounds.center.z + Random.Range(-InBounds.extents.z, InBounds.extents.z) * 0.8f);
                chunk.transform.rotation = Random.rotation;
                chunk.GetComponent<Renderer>().sharedMaterial = InMaterial;

                var body = chunk.AddComponent<Rigidbody>();
                body.mass = 0.3f;
                var outward = HitGeometry.Flat(chunk.transform.position - InBounds.center);
                if (outward.sqrMagnitude < 0.0001f)
                {
                    outward = Random.insideUnitSphere;
                    outward.y = 0.0f;
                }

                // 돌진 방향으로 더 많이 튄다.
                var direction = (outward.normalized + InPush * 1.2f).normalized;
                body.linearVelocity = direction * Random.Range(1.5f, 4.0f) + Vector3.up * Random.Range(2.0f, 4.5f);
                body.angularVelocity = Random.insideUnitSphere * 10.0f;
                chunks.Add(chunk.transform);
                chunkScales.Add(scale);
            }
        }

        private void spawnDust(Vector3 InPosition, float InRadius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Dust";
            ObjectUtil.SafeDestroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(InPosition.x, 0.06f, InPosition.z);
            go.transform.localScale = new Vector3(InRadius * 2.0f, 0.01f, InRadius * 2.0f);
            if (dustMaterial == null)
            {
                dustMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.75f, 0.7f, 0.62f, 0.5f) };
                dustMaterial.renderQueue = 3002;
            }

            dustRenderer = go.GetComponent<Renderer>();
            dustRenderer.sharedMaterial = dustMaterial;
            dustRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
