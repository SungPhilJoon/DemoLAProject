using System.IO;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor
{
    /// <summary>
    /// Blender 에서 내보낸 캐릭터 FBX (Assets/Models/*.fbx) 용 텍스처 임포트 규칙 + URP Lit 머티리얼 자동 생성.
    ///  - 텍스처 (Assets/Models/Textures/T_&lt;Part&gt;_*.png):
    ///      _Normal → Normal Map, _MetallicSmoothness → Linear(sRGB 끔), _BaseColor/_Emission → sRGB.
    ///  - 머티리얼: FBX 안의 M_&lt;Part&gt;_Baked 마다 Assets/Models/Materials/M_&lt;Part&gt;_Baked.mat (URP Lit) 를 만들고
    ///      BaseMap / BumpMap / MetallicGlossMap(알파 = Smoothness) / EmissionMap 을 연결한 뒤 FBX 머티리얼을 리맵한다.
    ///  - 리그는 Generic (DEF-spine 루트 스켈레톤 그대로) 로 둔다.
    /// </summary>
    public static class CharacterModelSetup
    {
        public const string ModelRoot = "Assets/Models";
        public const string TextureRoot = ModelRoot + "/Textures";
        public const string MaterialRoot = ModelRoot + "/Materials";
        private const float EmissionIntensity = 4f;

        [MenuItem("DemoLA/Models/캐릭터 머티리얼 설정 (Assets/Models 전체)", priority = 60)]
        public static void SetupAll()
        {
            if (!AssetDatabase.IsValidFolder(MaterialRoot))
                AssetDatabase.CreateFolder(ModelRoot, "Materials");

            // 텍스처 임포트 설정을 먼저 확정 (포스트프로세서가 처리하도록 강제 재임포트)
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureRoot }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);

            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                count += SetupModel(path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[DemoLA] 캐릭터 머티리얼 {count}개 설정 완료 → {MaterialRoot}");
        }

        public static int SetupModel(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return 0;

            int n = 0;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (obj is not Material src) continue;
                string part = PartName(src.name);
                if (part == null || !File.Exists($"{TextureRoot}/T_{part}_BaseColor.png"))
                {
                    Debug.LogWarning($"[DemoLA] {fbxPath}: 머티리얼 '{src.name}' 에 맞는 T_{part}_BaseColor 텍스처가 없어 건너뜀");
                    continue;
                }
                var mat = CreateOrUpdateMaterial(part);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), mat);
                n++;
            }
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.SaveAndReimport();
            return n;
        }

        /// <summary>"M_SorceressBody_Baked" → "SorceressBody"</summary>
        private static string PartName(string matName)
        {
            string s = matName;
            if (s.StartsWith("M_")) s = s.Substring(2);
            if (s.EndsWith("_Baked")) s = s.Substring(0, s.Length - "_Baked".Length);
            return string.IsNullOrEmpty(s) ? null : s;
        }

        private static Material CreateOrUpdateMaterial(string part)
        {
            string matPath = $"{MaterialRoot}/M_{part}_Baked.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = $"M_{part}_Baked" };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader) mat.shader = shader;

            var baseMap = Tex(part, "BaseColor");
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetColor("_BaseColor", Color.white);

            var normal = Tex(part, "Normal");
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 1f);
            SetKeyword(mat, "_NORMALMAP", normal != null);

            var ms = Tex(part, "MetallicSmoothness");
            mat.SetTexture("_MetallicGlossMap", ms);
            mat.SetFloat("_Smoothness", 1f);            // 알파 채널 값을 그대로 사용
            mat.SetFloat("_SmoothnessTextureChannel", 0); // 0 = Metallic 맵 알파
            SetKeyword(mat, "_METALLICSPECGLOSSMAP", ms != null);

            var emission = Tex(part, "Emission");
            if (emission != null)
            {
                mat.SetTexture("_EmissionMap", emission);
                mat.SetColor("_EmissionColor", Color.white * EmissionIntensity);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                SetKeyword(mat, "_EMISSION", true);
            }
            else
            {
                mat.SetTexture("_EmissionMap", null);
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                SetKeyword(mat, "_EMISSION", false);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Texture2D Tex(string part, string key) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureRoot}/T_{part}_{key}.png");

        private static void SetKeyword(Material m, string kw, bool on)
        {
            if (on) m.EnableKeyword(kw); else m.DisableKeyword(kw);
        }
    }

    /// <summary>Assets/Models/Textures 에 들어오는 베이크 텍스처의 임포트 타입 자동 지정.</summary>
    public class CharacterTexturePostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(CharacterModelSetup.TextureRoot + "/")) return;
            var ti = (TextureImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            if (name.EndsWith("_Normal"))
            {
                ti.textureType = TextureImporterType.NormalMap;
            }
            else if (name.EndsWith("_MetallicSmoothness"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = false;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = false;
            }
            else
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
            }
        }
    }
}
