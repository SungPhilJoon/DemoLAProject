using Unity.AI.Navigation;
using UnityEngine;

namespace DemoLA.World
{
    /// <summary>
    /// 레이드 입장 대기실 (경기장과 떨어진 원형 단상). 파티원이 모여 설정을 마치고 준비하는 공간.
    /// 북쪽 끝에 레이드 입구(관문)가 있다. 자체 NavMesh 를 가진다 (경기장 NavMesh 와 별개).
    /// </summary>
    public static class LobbyArea
    {
        public static readonly Vector3 Center = new Vector3(0.0f, 0.0f, -80.0f);
        public const float Radius = 11.0f;

        private static GameObject root;

        /// <summary>파티원 InIndex 번째 대기 위치 (입구를 바라보는 반원).</summary>
        public static Vector3 SpawnPoint(int InIndex, int InCount)
        {
            float t = InCount <= 1 ? 0.5f : (float)InIndex / (InCount - 1);
            float angle = Mathf.Lerp(-40.0f, 40.0f, t) * Mathf.Deg2Rad;
            return Center + new Vector3(Mathf.Sin(angle), 0.0f, -Mathf.Cos(angle)) * 3.5f;
        }

        /// <summary>대기 위치에서 입구를 바라보는 방향.</summary>
        public static Vector3 GateDirection
        {
            get { return Vector3.forward; }
        }

        public static void Ensure()
        {
            if (root != null)
            {
                return;
            }

            root = new GameObject("Lobby");
            root.transform.position = Center;

            // 바닥 단상
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(0.0f, -0.25f, 0.0f);
            floor.transform.localScale = new Vector3(Radius * 2.0f, 0.25f, Radius * 2.0f);
            paint(floor, ArenaBuilder.CenterMaterial, new Color(0.36f, 0.34f, 0.33f));

            // 안쪽 원 무늬
            var inlay = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            inlay.name = "Inlay";
            Object.Destroy(inlay.GetComponent<Collider>());
            inlay.transform.SetParent(root.transform, false);
            inlay.transform.localPosition = new Vector3(0.0f, 0.01f, 0.0f);
            inlay.transform.localScale = new Vector3(7.0f, 0.005f, 7.0f);
            paint(inlay, null, new Color(0.55f, 0.45f, 0.28f));

            // 가장자리 기둥 (등불)
            for (int i = 0; i < 8; ++i)
            {
                float angle = (22.5f + 45.0f * i) * Mathf.Deg2Rad;
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = $"Post_{i}";
                post.transform.SetParent(root.transform, false);
                post.transform.localPosition = new Vector3(Mathf.Sin(angle), 0.0f, Mathf.Cos(angle)) * (Radius - 0.8f) + Vector3.up * 1.2f;
                post.transform.localScale = new Vector3(0.5f, 1.2f, 0.5f);
                paint(post, ArenaBuilder.PillarMaterial, new Color(0.45f, 0.42f, 0.5f));

                var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lamp.name = "Lamp";
                Object.Destroy(lamp.GetComponent<Collider>());
                lamp.transform.SetParent(post.transform, false);
                lamp.transform.localPosition = new Vector3(0.0f, 1.2f, 0.0f);
                lamp.transform.localScale = new Vector3(1.0f, 0.4f, 1.0f);
                paint(lamp, null, new Color(1.0f, 0.8f, 0.45f));
            }

            buildGate(root.transform);

            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
            surface.BuildNavMesh();
        }

        /// <summary>북쪽 끝 레이드 입구: 돌 문틀 + 안쪽 빛 막.</summary>
        private static void buildGate(Transform InParent)
        {
            var gate = new GameObject("RaidGate");
            gate.transform.SetParent(InParent, false);
            gate.transform.localPosition = new Vector3(0.0f, 0.0f, Radius - 1.2f);

            for (int side = -1; side <= 1; side += 2)
            {
                var column = GameObject.CreatePrimitive(PrimitiveType.Cube);
                column.name = "Column";
                column.transform.SetParent(gate.transform, false);
                column.transform.localPosition = new Vector3(side * 2.2f, 2.0f, 0.0f);
                column.transform.localScale = new Vector3(0.8f, 4.0f, 0.8f);
                paint(column, ArenaBuilder.PillarMaterial, new Color(0.4f, 0.38f, 0.45f));
            }

            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = "Lintel";
            lintel.transform.SetParent(gate.transform, false);
            lintel.transform.localPosition = new Vector3(0.0f, 4.3f, 0.0f);
            lintel.transform.localScale = new Vector3(5.4f, 0.7f, 0.9f);
            paint(lintel, ArenaBuilder.PillarMaterial, new Color(0.4f, 0.38f, 0.45f));

            var veil = GameObject.CreatePrimitive(PrimitiveType.Quad);
            veil.name = "Veil";
            Object.Destroy(veil.GetComponent<Collider>());
            veil.transform.SetParent(gate.transform, false);
            veil.transform.localPosition = new Vector3(0.0f, 2.0f, 0.0f);
            veil.transform.localRotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);
            veil.transform.localScale = new Vector3(3.6f, 4.0f, 1.0f);
            var material = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.45f, 0.3f, 0.95f, 0.55f) };
            veil.GetComponent<Renderer>().sharedMaterial = material;
            veil.AddComponent<GateVeil>();
        }

        private static void paint(GameObject InGo, Material InTextured, Color InColor)
        {
            var renderer = InGo.GetComponent<Renderer>();
            if (InTextured != null)
            {
                renderer.sharedMaterial = InTextured;
            }
            else if (ArenaBuilder.MaterialFactory != null)
            {
                renderer.sharedMaterial = ArenaBuilder.MaterialFactory(InColor);
            }
            else
            {
                renderer.material.color = InColor;
            }
        }
    }

    /// <summary>입구 빛 막이 천천히 일렁인다. 모두 준비되면 밝아진다 (RaidLobby 가 Charged 를 켠다).</summary>
    public class GateVeil : MonoBehaviour
    {
        public static bool Charged;
        private Renderer cachedRenderer;
        private MaterialPropertyBlock block;

        private void Update()
        {
            if (cachedRenderer == null)
            {
                cachedRenderer = GetComponent<Renderer>();
                block = new MaterialPropertyBlock();
            }

            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * (Charged ? 6.0f : 1.5f));
            var color = Charged ? new Color(1.0f, 0.85f, 0.4f, Mathf.Lerp(0.55f, 0.9f, wave)) : new Color(0.45f, 0.3f, 0.95f, Mathf.Lerp(0.35f, 0.6f, wave));
            cachedRenderer.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            cachedRenderer.SetPropertyBlock(block);
        }
    }
}
