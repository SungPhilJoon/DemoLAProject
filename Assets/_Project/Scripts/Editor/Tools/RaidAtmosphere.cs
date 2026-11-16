using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DemoLA.Editor
{
    /// <summary>
    /// 레이드 씬 분위기 (발탄 레이드풍): 핏빛 스카이박스 + 검붉은 안개 + 붉은 주변광 + 주황빛 태양.
    ///  - 하늘 셰이더: Art/Environment/Sky/RaidSky.shader, 머티리얼: M_Sky_Raid.mat (색 · 구름은 머티리얼에서 조절)
    ///  - 레이드 씬을 새로 만들 때(CreateRaidScene) 자동으로 적용되고, 메뉴로 지금 열린 씬에 다시 적용할 수 있다.
    /// 텔레그래프(바닥 장판)가 묻히지 않도록 주변광 · 안개는 너무 어둡게 하지 않는다.
    /// </summary>
    public static class RaidAtmosphere
    {
        public const string Folder = "Assets/_Project/Art/Environment/Sky";
        public const string ShaderPath = Folder + "/RaidSky.shader";
        public const string MaterialPath = Folder + "/M_Sky_Raid.mat";

        public static readonly Color FogColor = new Color(0.20f, 0.06f, 0.05f);
        public const float FogDensity = 0.010f;
        public static readonly Color AmbientSky = new Color(0.42f, 0.16f, 0.13f);
        public static readonly Color AmbientEquator = new Color(0.30f, 0.12f, 0.09f);
        public static readonly Color AmbientGround = new Color(0.10f, 0.05f, 0.05f);
        public static readonly Color SunColor = new Color(1.0f, 0.66f, 0.50f);
        public const float SunIntensity = 1.15f;
        public static readonly Vector3 SunEuler = new Vector3(28.0f, -35.0f, 0.0f);

        [MenuItem("DemoLA/Setup/레이드 하늘 · 분위기 적용 (열린 씬)", priority = 30)]
        public static void ApplyMenu()
        {
            Apply();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[DemoLA] 레이드 분위기 적용 → {EditorSceneManager.GetActiveScene().path} (하늘 {MaterialPath})");
        }

        /// <summary>지금 활성 씬의 RenderSettings · 방향광에 적용한다.</summary>
        public static void Apply()
        {
            RenderSettings.skybox = EnsureMaterial();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = FogDensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            var sun = findSun();
            if (sun != null)
            {
                Undo.RecordObject(sun, "레이드 분위기");
                Undo.RecordObject(sun.transform, "레이드 분위기");
                sun.color = SunColor;
                sun.intensity = SunIntensity;
                sun.transform.rotation = Quaternion.Euler(SunEuler);
                RenderSettings.sun = sun;
            }

            DynamicGI.UpdateEnvironment();
        }

        public static Material EnsureMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[DemoLA] 하늘 셰이더가 없습니다: {ShaderPath}");
                return RenderSettings.skybox;
            }

            material = new Material(shader) { name = "M_Sky_Raid" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Light findSun()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    return light;
                }
            }

            return null;
        }
    }
}
