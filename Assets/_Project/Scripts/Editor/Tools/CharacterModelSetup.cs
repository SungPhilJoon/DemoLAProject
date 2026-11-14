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
    ///  - 리그: Blender Rigify(DEF-*) 스켈레톤은 Generic, Meshy 자동 리깅(Mixamo 식 Hips 본)은 Humanoid.
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
            if (IsMixamoStyleRig(fbxPath))
            {
                // Meshy 자동 리깅 (Hips/Spine/LeftUpLeg...) → Humanoid. 이미 Humanoid 면 사용자가 손본 아바타 매핑을 유지.
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                }
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.Generic; // Blender Rigify DEF-* 스켈레톤
            }
            importer.SaveAndReimport();
            return n;
        }

        /// <summary>Mixamo 식 본 이름(Meshy 리그)이면 true.</summary>
        private static bool IsMixamoStyleRig(string fbxPath)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (go == null) return false;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == "Hips" || t.name.EndsWith(":Hips")) return true;
            return false;
        }

        /// <summary>"M_SorceressBody_Baked" → "SorceressBody"</summary>
        internal static string PartName(string matName)
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

    /// <summary>
    /// Assets/Models/*.fbx 가 임포트되면 머티리얼 생성/리맵을 자동 실행 (메뉴를 누르지 않아도 됨).
    /// 이미 모든 머티리얼이 리맵된 FBX 는 건너뛰므로 재임포트 루프가 생기지 않는다.
    /// </summary>
    public class CharacterModelAutoSetup : AssetPostprocessor
    {
        private static readonly System.Collections.Generic.HashSet<string> Pending = new();

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (!path.StartsWith(CharacterModelSetup.ModelRoot + "/")) continue;
                if (path.StartsWith(CharacterModelSetup.ModelRoot + "/Animations/")) continue;
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (!NeedsSetup(path)) continue;
                if (Pending.Count == 0) EditorApplication.delayCall += RunPending;
                Pending.Add(path);
            }
        }

        private static bool NeedsSetup(string fbxPath)
        {
            if (AssetImporter.GetAtPath(fbxPath) is not ModelImporter importer) return false;
            var map = importer.GetExternalObjectMap();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (obj is not Material m) continue;
                // 텍스처가 없는 머티리얼은 SetupModel 이 건너뛰므로 대상에서 제외 (재임포트 무한루프 방지)
                string part = CharacterModelSetup.PartName(m.name);
                if (part == null || !File.Exists($"{CharacterModelSetup.TextureRoot}/T_{part}_BaseColor.png")) continue;
                if (!map.ContainsKey(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name))) return true;
            }
            return false;
        }

        private static void RunPending()
        {
            var paths = new System.Collections.Generic.List<string>(Pending);
            Pending.Clear();
            if (!AssetDatabase.IsValidFolder(CharacterModelSetup.MaterialRoot))
                AssetDatabase.CreateFolder(CharacterModelSetup.ModelRoot, "Materials");
            foreach (string p in paths)
            {
                int n = CharacterModelSetup.SetupModel(p);
                Debug.Log($"[DemoLA] {p}: 머티리얼 {n}개 자동 설정");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
