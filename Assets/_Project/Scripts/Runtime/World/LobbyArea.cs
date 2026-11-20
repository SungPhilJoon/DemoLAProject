using Unity.AI.Navigation;
using UnityEngine;

namespace DemoLA.World
{
    /// <summary>
    /// 레이드 입장 대기실 (경기장과 떨어진 원형 단상). 파티원이 모여 설정을 마치고 준비하는 공간.
    /// 북쪽 끝에 레이드 입구(관문)가 있다. 자체 NavMesh 를 가진다 (경기장 NavMesh 와 별개).
    /// 대기실 전체(단상 · 등불 · 관문 · 머티리얼 · NavMesh 데이터)는 프리팹 WorldPrefabs.Lobby 에 미리 구워 두고 Ensure 가 Instantiate 한다.
    /// 프리팹이 없으면 Build 로 코드 조립 후 NavMesh 를 런타임에 굽는다.
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

            root = DemoLA.Core.WorldPrefabs.Instantiate(DemoLA.Core.WorldPrefabs.Lobby, Center, Quaternion.identity);
            if (root == null)
            {
                root = Build(null, null);
                root.transform.position = Center;
            }

            var surface = root.GetComponent<NavMeshSurface>();
            if (surface != null && surface.navMeshData == null)
            {
                surface.BuildNavMesh();
            }
        }

        /// <summary>
        /// 대기실 오브젝트를 원점에 만든다 (프리팹 굽기 · 코드 조립 공용). NavMesh 는 굽지 않는다.
        /// InMaterials: 색 → 머티리얼 (굽기 때 에셋, null 이면 ArenaBuilder.MaterialFactory 또는 런타임 머티리얼), InVeil: 관문 빛 막 머티리얼.
        /// </summary>
        public static GameObject Build(System.Func<Color, Material> InMaterials, Material InVeil)
        {
            var root = new GameObject("Lobby");

            // 바닥 단상
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(0.0f, -0.25f, 0.0f);
            floor.transform.localScale = new Vector3(Radius * 2.0f, 0.25f, Radius * 2.0f);
            paint(floor, ArenaBuilder.CenterMaterial, new Color(0.36f, 0.34f, 0.33f), InMaterials);

            // 안쪽 원 무늬
            var inlay = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            inlay.name = "Inlay";
            DemoLA.Core.ObjectUtil.SafeDestroy(inlay.GetComponent<Collider>());
            inlay.transform.SetParent(root.transform, false);
            inlay.transform.localPosition = new Vector3(0.0f, 0.01f, 0.0f);
            inlay.transform.localScale = new Vector3(7.0f, 0.005f, 7.0f);
            paint(inlay, null, new Color(0.55f, 0.45f, 0.28f), InMaterials);

            // 가장자리 기둥 (등불)
            for (int i = 0; i < 8; ++i)
            {
                float angle = (22.5f + 45.0f * i) * Mathf.Deg2Rad;
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = $"Post_{i}";
                post.transform.SetParent(root.transform, false);
                post.transform.localPosition = new Vector3(Mathf.Sin(angle), 0.0f, Mathf.Cos(angle)) * (Radius - 0.8f) + Vector3.up * 1.2f;
                post.transform.localScale = new Vector3(0.5f, 1.2f, 0.5f);
                paint(post, ArenaBuilder.PillarMaterial, new Color(0.45f, 0.42f, 0.5f), InMaterials);

                var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lamp.name = "Lamp";
                DemoLA.Core.ObjectUtil.SafeDestroy(lamp.GetComponent<Collider>());
                lamp.transform.SetParent(post.transform, false);
                lamp.transform.localPosition = new Vector3(0.0f, 1.2f, 0.0f);
                lamp.transform.localScale = new Vector3(1.0f, 0.4f, 1.0f);
                paint(lamp, null, new Color(1.0f, 0.8f, 0.45f), InMaterials);
            }

            buildGate(root.transform, InMaterials, InVeil);

            var navSurface = root.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.Children;
            navSurface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
            return root;
        }

        /// <summary>관문 빛 막 기본 색 (굽기용 머티리얼에도 쓴다).</summary>
        public static readonly Color VeilColor = new Color(0.45f, 0.3f, 0.95f, 0.55f);

        /// <summary>북쪽 끝 레이드 입구: 돌 문틀 + 안쪽 빛 막.</summary>
        private static void buildGate(Transform InParent, System.Func<Color, Material> InMaterials, Material InVeil)
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
                paint(column, ArenaBuilder.PillarMaterial, new Color(0.4f, 0.38f, 0.45f), InMaterials);
            }

            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = "Lintel";
            lintel.transform.SetParent(gate.transform, false);
            lintel.transform.localPosition = new Vector3(0.0f, 4.3f, 0.0f);
            lintel.transform.localScale = new Vector3(5.4f, 0.7f, 0.9f);
            paint(lintel, ArenaBuilder.PillarMaterial, new Color(0.4f, 0.38f, 0.45f), InMaterials);

            var veil = GameObject.CreatePrimitive(PrimitiveType.Quad);
            veil.name = "Veil";
            DemoLA.Core.ObjectUtil.SafeDestroy(veil.GetComponent<Collider>());
            veil.transform.SetParent(gate.transform, false);
            veil.transform.localPosition = new Vector3(0.0f, 2.0f, 0.0f);
            veil.transform.localRotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);
            veil.transform.localScale = new Vector3(3.6f, 4.0f, 1.0f);
            var material = InVeil != null ? InVeil : new Material(Shader.Find("Sprites/Default")) { color = VeilColor };
            veil.GetComponent<Renderer>().sharedMaterial = material;
            veil.AddComponent<GateVeil>();
        }

        private static void paint(GameObject InGo, Material InTextured, Color InColor, System.Func<Color, Material> InMaterials)
        {
            var renderer = InGo.GetComponent<Renderer>();
            if (InTextured != null)
            {
                renderer.sharedMaterial = InTextured;
            }
            else if (InMaterials != null)
            {
                renderer.sharedMaterial = InMaterials(InColor);
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
}
