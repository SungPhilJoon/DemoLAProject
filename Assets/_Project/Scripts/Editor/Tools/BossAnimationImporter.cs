using System.IO;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor
{
    /// <summary>
    /// 보스 애니메이션 폴더(Assets/_Project/Animation/Boss/{관문}/{모델}/)의 FBX 임포트 규칙.
    ///  - 폴더 이름 = 모델 이름 (Assets/Models/{모델}.fbx). Humanoid 아바타는 각 파일에서 만든다 (아래 OnPreprocessModel 참고)
    ///  - 애니메이션 전용: 머티리얼 · 블렌드셰이프 임포트 안 함, 단위 변환 켬
    ///  - 클립 이름에 Idle · Walk · Run · Loop 가 있으면 루프
    /// Blender 쪽 내보내기: BlenderSource/scripts/export_unity_anims_ironhorn.py
    /// </summary>
    public class BossAnimationImporter : AssetPostprocessor
    {
        public const string Root = "Assets/_Project/Animation/Boss/";
        public const string ModelRoot = "Assets/Models/";
        private static readonly string[] LoopKeywords = { "Idle", "Walk", "Run", "Loop" };

        /// <summary>InAssetPath 가 보스 애니메이션이면 대상 모델 경로 (Assets/Models/{폴더 이름}.fbx), 아니면 null.</summary>
        public static string ModelPathFor(string InAssetPath)
        {
            if (InAssetPath.StartsWith(Root) == false || InAssetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) == false)
            {
                return null;
            }

            string folder = Path.GetFileName(Path.GetDirectoryName(InAssetPath));
            return $"{ModelRoot}{folder}.fbx";
        }

        private void OnPreprocessModel()
        {
            string modelPath = ModelPathFor(assetPath);
            if (modelPath == null)
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importBlendShapes = false;
            importer.importAnimation = true;
            importer.useFileScale = true;
            importer.animationType = ModelImporterAnimationType.Human;

            // 애니메이션 전용 FBX 는 루트가 하나라 Unity 가 스켈레톤 노드를 파일 루트로 접는다
            // → 모델 아바타(Hips 부모 = IronhornWarlord_Skeleton)와 계층이 달라 Copy From Other 가 실패한다.
            // 그래서 이 파일로 Humanoid 아바타를 만든다 (같은 Mixamo 뼈 이름이라 자동 매핑, Humanoid 끼리 리타깃).
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
        }

        private void OnPreprocessAnimation()
        {
            if (ModelPathFor(assetPath) == null)
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            string file = Path.GetFileNameWithoutExtension(assetPath);
            for (int i = 0; i < clips.Length; ++i)
            {
                // 클립 이름 = 파일의 @ 뒤 (IronhornWarlord@Dead → Dead)
                int at = file.IndexOf('@');
                clips[i].name = at >= 0 ? file.Substring(at + 1) : clips[i].name;
                bool loop = false;
                foreach (var key in LoopKeywords)
                {
                    loop |= clips[i].name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0;
                }

                clips[i].loopTime = loop;
                clips[i].loopPose = loop;
                // 보스 위치는 ActorMotor 가 옮긴다 → 루트 이동은 포즈에 굽는다.
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = clips;
        }

        [MenuItem("DemoLA/Models/보스 애니메이션 임포트 설정 다시 적용", priority = 62)]
        public static void ReimportAll()
        {
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root.TrimEnd('/') }))
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
                count += 1;
            }

            Debug.Log($"[DemoLA] 보스 애니메이션 {count}개 다시 임포트");
        }
    }
}
