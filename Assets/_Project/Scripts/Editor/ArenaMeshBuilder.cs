using System.IO;
using DemoLA.World;
using UnityEditor;
using UnityEngine;
#if DEMOLA_PROBUILDER
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
#endif

namespace DemoLA.Editor
{
    /// <summary>
    /// 경기장 메시 · 텍스처 생성 (에디터 전용).
    ///  - 메시: ProBuilder (타일 = 큐브, 구조물 = 원기둥, 외곽 테두리 = 파이프). ProBuilder 가 없으면 기본 도형.
    ///  - 텍스처: 직접 생성한 Albedo / Height / Normal 맵 (석재 블록 + 균열 노이즈) → URP Lit 머티리얼.
    /// 레이아웃(타일 그룹 · 구조물 위치)은 기믹 규칙 그대로 ArenaBuilder 가 정한다.
    /// 특정 게임 맵을 사진으로 따라 만들지는 않았다 — 원형 석재 경기장이라는 이 프로젝트 고유 디자인이다.
    /// </summary>
    public static class ArenaMeshBuilder
    {
        public const string TextureRoot = "Assets/_Project/Textures/Arena";
        private const int Size = 512;

        [MenuItem("DemoLA/Map/경기장 텍스처 다시 생성", priority = 30)]
        public static void RegenerateTexturesMenu()
        {
            createMaterials(true);
            Debug.Log($"[DemoLA] 경기장 텍스처 생성 → {TextureRoot}");
        }

        public static ArenaFloor BuildArena()
        {
            var materials = createMaterials(false);
            ArenaBuilder.FloorMaterial = materials.Floor;
            ArenaBuilder.CenterMaterial = materials.Center;
            ArenaBuilder.PillarMaterial = materials.Pillar;
#if DEMOLA_PROBUILDER
            ArenaBuilder.TileFactory = createTile;
            ArenaBuilder.PillarFactory = createPillar;
#endif
            try
            {
                var floor = ArenaBuilder.Build(null);
#if DEMOLA_PROBUILDER
                createRim(floor.transform, materials.Pillar);
#endif
                return floor;
            }
            finally
            {
                ArenaBuilder.TileFactory = null;
                ArenaBuilder.PillarFactory = null;
                ArenaBuilder.FloorMaterial = null;
                ArenaBuilder.CenterMaterial = null;
                ArenaBuilder.PillarMaterial = null;
            }
        }

#if DEMOLA_PROBUILDER
        private static GameObject createTile(Vector3 InSize)
        {
            var mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, InSize);
            finish(mesh);
            return mesh.gameObject;
        }

        private static GameObject createPillar(float InRadius, float InHeight)
        {
            var mesh = ShapeGenerator.GenerateCylinder(PivotLocation.Center, 16, InRadius, InHeight, 2);
            finish(mesh);
            // 카메라 가림 판정(CameraOcclusionSystem)이 물리 광선을 쓰므로 콜라이더가 필요하다.
            mesh.gameObject.AddComponent<MeshCollider>();
            return mesh.gameObject;
        }

        /// <summary>바깥 테두리 벽 (바닥보다 높아 걸어 올라갈 수 없다 → 맵 밖으로 나가지 않는다).</summary>
        private static void createRim(Transform InParent, Material InMaterial)
        {
            var mesh = ShapeGenerator.GeneratePipe(PivotLocation.Center, ArenaBuilder.Radius + 3.0f, 1.2f, 2.4f, 48, 1);
            finish(mesh);
            mesh.gameObject.name = "Rim";
            mesh.transform.SetParent(InParent, false);
            mesh.transform.localPosition = new Vector3(0.0f, 0.3f, 0.0f);
            mesh.GetComponent<Renderer>().sharedMaterial = InMaterial;
        }

        private static void finish(ProBuilderMesh InMesh)
        {
            // 월드 좌표 기준 UV 로 타일 간 텍스처가 이어지게 한다.
            foreach (var face in InMesh.faces)
            {
                var uv = face.uv;
                uv.useWorldSpace = true;
                uv.scale = Vector2.one * 0.25f;
                face.uv = uv;
            }

            InMesh.ToMesh();
            InMesh.Refresh();
        }
#endif

        // ------------------------------------------------------------------ textures / materials

        private struct ArenaMaterials
        {
            public Material Floor;
            public Material Center;
            public Material Pillar;
        }

        private static ArenaMaterials createMaterials(bool InRegenerate)
        {
            DemoLAProjectSetup.ensureFolder(TextureRoot);
            var materials = new ArenaMaterials();
            materials.Floor = material("M_ArenaFloor", new Color(0.42f, 0.4f, 0.38f), 11, 6, InRegenerate);
            materials.Center = material("M_ArenaCenter", new Color(0.52f, 0.47f, 0.4f), 23, 4, InRegenerate);
            materials.Pillar = material("M_ArenaPillar", new Color(0.45f, 0.42f, 0.5f), 37, 3, InRegenerate);
            return materials;
        }

        private static Material material(string InName, Color InTint, int InSeed, int InBlocks, bool InRegenerate)
        {
            string materialPath = $"{DemoLAProjectSetup.MaterialRoot}/{InName}.mat";
            string albedoPath = $"{TextureRoot}/{InName}_Albedo.png";
            string heightPath = $"{TextureRoot}/{InName}_Height.png";
            string normalPath = $"{TextureRoot}/{InName}_Normal.png";
            if (InRegenerate == true || File.Exists(albedoPath) == false)
            {
                var height = heightField(InSeed, InBlocks);
                writePng(albedoPath, albedo(height, InTint, InSeed));
                writePng(heightPath, grayscale(height));
                writePng(normalPath, normalFromHeight(height, 4.0f));
                AssetDatabase.Refresh();
                setImport(normalPath, TextureImporterType.NormalMap, false);
                setImport(heightPath, TextureImporterType.Default, false);
                setImport(albedoPath, TextureImporterType.Default, true);
            }

            DemoLAProjectSetup.ensureFolder(DemoLAProjectSetup.MaterialRoot);
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader != null ? shader : Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            material.SetTexture("_ParallaxMap", AssetDatabase.LoadAssetAtPath<Texture2D>(heightPath));
            material.SetFloat("_Parallax", 0.02f);
            material.SetFloat("_Smoothness", 0.2f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_PARALLAXMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>석재 블록(모르타르 홈) + fBm 노이즈 + 균열. 값 0~1.</summary>
        private static float[,] heightField(int InSeed, int InBlocks)
        {
            var field = new float[Size, Size];
            float cell = Size / (float)InBlocks;
            var random = new System.Random(InSeed);
            float offsetX = random.Next(0, 1000);
            float offsetY = random.Next(0, 1000);
            for (int y = 0; y < Size; ++y)
            {
                int row = Mathf.FloorToInt(y / cell);
                float shift = row % 2 == 0 ? 0.0f : cell * 0.5f;
                for (int x = 0; x < Size; ++x)
                {
                    float localX = Mathf.Repeat(x + shift, cell);
                    float localY = Mathf.Repeat(y, cell);
                    float edge = Mathf.Min(Mathf.Min(localX, cell - localX), Mathf.Min(localY, cell - localY));
                    float mortar = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(edge / (cell * 0.06f)));
                    float u = x / (float)Size;
                    float v = y / (float)Size;
                    float noise = fbm(u * 8.0f + offsetX, v * 8.0f + offsetY, 5);
                    float crack = 1.0f - Mathf.Clamp01(Mathf.Abs(fbm(u * 3.0f + offsetY, v * 3.0f + offsetX, 3) - 0.5f) * 40.0f);
                    float blockTone = 0.85f + 0.15f * hash(Mathf.FloorToInt((x + shift) / cell), row, InSeed);
                    field[x, y] = Mathf.Clamp01(mortar * (0.55f + 0.35f * noise) * blockTone - crack * 0.25f);
                }
            }

            return field;
        }

        private static Color[] albedo(float[,] InHeight, Color InTint, int InSeed)
        {
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; ++y)
            {
                for (int x = 0; x < Size; ++x)
                {
                    float h = InHeight[x, y];
                    float grime = fbm(x / 64.0f + InSeed, y / 64.0f, 4);
                    var color = InTint * Mathf.Lerp(0.45f, 1.15f, h) * Mathf.Lerp(0.85f, 1.05f, grime);
                    color.a = 1.0f;
                    pixels[y * Size + x] = color;
                }
            }

            return pixels;
        }

        private static Color[] grayscale(float[,] InHeight)
        {
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; ++y)
            {
                for (int x = 0; x < Size; ++x)
                {
                    float h = InHeight[x, y];
                    pixels[y * Size + x] = new Color(h, h, h, 1.0f);
                }
            }

            return pixels;
        }

        /// <summary>Sobel 로 높이 → 탄젠트 공간 노멀.</summary>
        private static Color[] normalFromHeight(float[,] InHeight, float InStrength)
        {
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; ++y)
            {
                for (int x = 0; x < Size; ++x)
                {
                    float dx = sample(InHeight, x + 1, y) - sample(InHeight, x - 1, y);
                    float dy = sample(InHeight, x, y + 1) - sample(InHeight, x, y - 1);
                    var normal = new Vector3(-dx * InStrength, -dy * InStrength, 1.0f).normalized;
                    pixels[y * Size + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1.0f);
                }
            }

            return pixels;
        }

        private static float sample(float[,] InField, int InX, int InY)
        {
            return InField[(InX + Size) % Size, (InY + Size) % Size];
        }

        private static float fbm(float InX, float InY, int InOctaves)
        {
            float value = 0.0f;
            float amplitude = 0.5f;
            float frequency = 1.0f;
            for (int i = 0; i < InOctaves; ++i)
            {
                value += Mathf.PerlinNoise(InX * frequency, InY * frequency) * amplitude;
                amplitude *= 0.5f;
                frequency *= 2.0f;
            }

            return value;
        }

        private static float hash(int InX, int InY, int InSeed)
        {
            int h = InX * 374761393 + InY * 668265263 + InSeed * 982451653;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h & 0xFFFF) / 65535.0f;
        }

        private static void writePng(string InPath, Color[] InPixels)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels(InPixels);
            texture.Apply();
            File.WriteAllBytes(InPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static void setImport(string InPath, TextureImporterType InType, bool InSrgb)
        {
            var importer = AssetImporter.GetAtPath(InPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = InType;
            importer.sRGBTexture = InSrgb;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }
    }
}
