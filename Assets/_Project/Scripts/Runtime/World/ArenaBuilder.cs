using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace DemoLA.World
{
    /// <summary>
    /// 기본 레이드 경기장 (원형 타일 바닥 + 구조물 4개). ProBuilder 로 실제 맵을 만들기 전 임시 맵이며,
    /// 지형 파괴 기믹을 위해 바닥을 타일(Center/Left/Right)로 나눈다.
    /// 원작 맵을 사진 보고 재현하는 작업은 하지 않았다 (구현 보류 사항.md).
    /// </summary>
    public static class ArenaBuilder
    {
        public const float Radius = 26.0f;
        public const float TileSize = 4.0f;
        public const float CenterRadius = 8.0f;
        public const float StructureRadius = 14.0f;
        /// <summary>외곽 기둥 원 (안쪽 생명 구슬 기둥 4개와 외곽 벽 사이). 반 칸 비켜 8개.</summary>
        public const float OuterPillarRadius = 18.0f;
        public const int OuterPillarCount = 8;
        /// <summary>외곽 벽이 늘어선 원의 반지름 · 개수 · 벽 한 개 크기. 벽 사이 틈은 약 1.2m.</summary>
        public const float WallRingRadius = 21.0f;
        public const int WallCount = 20;
        public const float WallLength = 5.4f;
        public const float WallThickness = 1.2f;
        public const float WallHeight = 3.5f;

        /// <summary>에디터 셋업이 머티리얼 에셋을 넘길 때 쓴다. null 이면 런타임 인스턴스 머티리얼.</summary>
        public static System.Func<Color, Material> MaterialFactory;

        /// <summary>타일 / 구조물 메시 공급자 (에디터 셋업이 ProBuilder 메시를 넘긴다). null 이면 기본 도형.</summary>
        public static System.Func<Vector3, GameObject> TileFactory;
        public static System.Func<float, float, GameObject> PillarFactory;
        /// <summary>타일/구조물 대신 쓸 머티리얼 (텍스처 맵). null 이면 색 머티리얼.</summary>
        public static Material FloorMaterial;
        public static Material CenterMaterial;
        public static Material PillarMaterial;

        private static void paint(GameObject InGo, Color InColor)
        {
            var renderer = InGo.GetComponent<Renderer>();
            if (MaterialFactory != null)
            {
                renderer.sharedMaterial = MaterialFactory(InColor);
            }
            else
            {
                renderer.material.color = InColor;
            }
        }

        /// <summary>원기둥 하나 (InAngle 도, 중심에서 InRadius). 보스 돌진 · 벽 파괴 공격에 부서지고 InOrbs 개의 생명 구슬을 떨어뜨린다.</summary>
        private static void buildPillar(Transform InRoot, string InName, float InAngle, float InRadius, int InOrbs)
        {
            float angle = InAngle * Mathf.Deg2Rad;
            GameObject pillar;
            if (PillarFactory != null)
            {
                pillar = PillarFactory(1.2f, 4.0f);
            }
            else
            {
                pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.transform.localScale = new Vector3(2.4f, 2.0f, 2.4f);
            }

            pillar.name = InName;
            pillar.transform.SetParent(InRoot, false);
            pillar.transform.localPosition = new Vector3(Mathf.Sin(angle), 0.0f, Mathf.Cos(angle)) * InRadius + Vector3.up * 2.0f;
            if (PillarMaterial != null)
            {
                pillar.GetComponent<Renderer>().sharedMaterial = PillarMaterial;
            }
            else
            {
                paint(pillar, new Color(0.5f, 0.45f, 0.55f));
            }

            var structure = pillar.AddComponent<ArenaStructure>();
            structure.Radius = 1.2f;
            structure.LifeOrbCount = InOrbs;
            // 카메라와 캐릭터 사이를 가리면 반투명 페이드 (프레임워크 CameraOcclusionSystem).
            pillar.AddComponent<Framework.CameraOccluder>();
            var obstacle = pillar.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.carving = true;
        }

        /// <summary>
        /// 외곽 벽: 기둥보다 넓은 원(WallRingRadius)에 접선 방향으로 늘어선 직육면체 20개. 사이사이 좁은 틈이 있다.
        /// 보스의 휠윈드 · 3연 베기(BreaksWalls) 범위에 닿으면 무너진다 (같은 범위의 기둥도).
        /// </summary>
        private static void buildWalls(Transform InRoot)
        {
            for (int i = 0; i < WallCount; ++i)
            {
                // 기둥(45°+90°k)과 겹쳐 보이지 않게 반 칸 비켜 배치.
                float angle = (360.0f / WallCount * (i + 0.5f)) * Mathf.Deg2Rad;
                var radial = new Vector3(Mathf.Sin(angle), 0.0f, Mathf.Cos(angle));

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wall_{i}";
                wall.transform.SetParent(InRoot, false);
                wall.transform.localPosition = radial * WallRingRadius + Vector3.up * (WallHeight * 0.5f);
                wall.transform.localRotation = Quaternion.LookRotation(radial, Vector3.up);
                // LookRotation(radial) → 로컬 X 축이 접선 방향이 된다.
                wall.transform.localScale = new Vector3(WallLength, WallHeight, WallThickness);
                if (PillarMaterial != null)
                {
                    wall.GetComponent<Renderer>().sharedMaterial = PillarMaterial;
                }
                else
                {
                    paint(wall, new Color(0.42f, 0.38f, 0.36f));
                }

                var structure = wall.AddComponent<ArenaStructure>();
                structure.Kind = EStructureKind.Wall;
                structure.Radius = WallThickness * 0.5f;
                structure.HalfLength = WallLength * 0.5f;
                structure.LifeOrbCount = 0;
                structure.CrashShakeAmplitude = 0.3f;
                wall.AddComponent<Framework.CameraOccluder>();
                var obstacle = wall.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = Vector3.one;
                obstacle.carving = true;
            }
        }

        public static ArenaFloor Build(Transform InParent)
        {
            var root = new GameObject("Arena");
            if (InParent != null)
            {
                root.transform.SetParent(InParent, false);
            }

            var floorGo = new GameObject("Floor");
            floorGo.transform.SetParent(root.transform, false);
            var centerColor = new Color(0.42f, 0.4f, 0.37f);
            var sideColorA = new Color(0.33f, 0.31f, 0.3f);
            var sideColorB = new Color(0.3f, 0.29f, 0.29f);

            int cells = Mathf.CeilToInt(Radius / TileSize);
            for (int x = -cells; x <= cells; ++x)
            {
                for (int z = -cells; z <= cells; ++z)
                {
                    var position = new Vector3(x * TileSize, -0.25f, z * TileSize);
                    float distance = new Vector2(position.x, position.z).magnitude;
                    if (distance > Radius)
                    {
                        continue;
                    }

                    var tileSize = new Vector3(TileSize - 0.04f, 0.5f, TileSize - 0.04f);
                    GameObject tileGo;
                    if (TileFactory != null)
                    {
                        tileGo = TileFactory(tileSize);
                    }
                    else
                    {
                        tileGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        tileGo.transform.localScale = tileSize;
                    }

                    tileGo.name = $"Tile_{x}_{z}";
                    tileGo.transform.SetParent(floorGo.transform, false);
                    tileGo.transform.localPosition = position;
                    var tile = tileGo.AddComponent<ArenaTile>();
                    if (distance <= CenterRadius)
                    {
                        tile.Group = ETileGroup.Center;
                    }
                    else
                    {
                        tile.Group = x < 0 ? ETileGroup.Left : ETileGroup.Right;
                    }

                    var color = tile.Group == ETileGroup.Center ? centerColor : ((x + z) % 2 == 0 ? sideColorA : sideColorB);
                    var textured = tile.Group == ETileGroup.Center ? CenterMaterial : FloorMaterial;
                    if (textured != null)
                    {
                        tileGo.GetComponent<Renderer>().sharedMaterial = textured;
                    }
                    else
                    {
                        paint(tileGo, color);
                    }
                }
            }

            // 안쪽 생명 구슬 기둥 4개 (보스 돌진 기믹) + 외곽 기둥 8개 (외곽을 빼곡하게, 구슬 1개).
            for (int i = 0; i < 4; ++i)
            {
                buildPillar(root.transform, $"Structure_{i}", 45.0f + 90.0f * i, StructureRadius, 2);
            }

            for (int i = 0; i < OuterPillarCount; ++i)
            {
                buildPillar(root.transform, $"OuterPillar_{i}", 22.5f + 360.0f / OuterPillarCount * i, OuterPillarRadius, 1);
            }

            buildWalls(root.transform);

            var floor = root.AddComponent<ArenaFloor>();
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            floor.Surface = surface;
            floor.Rebuild();
            return floor;
        }
    }
}
