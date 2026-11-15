using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor
{
    /// <summary>
    /// Mixamo 식 본 이름(mixamorig:Hips, Hips 등)의 FBX 에 Humanoid 매핑을 코드로 만든다.
    /// 본 이름이 바뀐 FBX 를 다시 내보내면 .meta 의 예전 매핑이 남아 "Transform 'Hips' not found" 로 아바타 생성이 실패한다 →
    /// 실제 계층에서 매핑과 스켈레톤(바인드 포즈)을 다시 만든다.
    /// </summary>
    public static class MixamoHumanoid
    {
        private static readonly (string Human, string Bone)[] Map =
        {
            ("Hips", "Hips"), ("Spine", "Spine"), ("Chest", "Spine1"), ("UpperChest", "Spine2"), ("Neck", "Neck"), ("Head", "Head"),
            ("LeftShoulder", "LeftShoulder"), ("LeftUpperArm", "LeftArm"), ("LeftLowerArm", "LeftForeArm"), ("LeftHand", "LeftHand"),
            ("RightShoulder", "RightShoulder"), ("RightUpperArm", "RightArm"), ("RightLowerArm", "RightForeArm"), ("RightHand", "RightHand"),
            ("LeftUpperLeg", "LeftUpLeg"), ("LeftLowerLeg", "LeftLeg"), ("LeftFoot", "LeftFoot"), ("LeftToes", "LeftToeBase"),
            ("RightUpperLeg", "RightUpLeg"), ("RightLowerLeg", "RightLeg"), ("RightFoot", "RightFoot"), ("RightToes", "RightToeBase"),
        };

        private static readonly (string Human, string Bone)[] Fingers =
        {
            ("Thumb", "Thumb"), ("Index", "Index"), ("Middle", "Middle"), ("Ring", "Ring"), ("Little", "Pinky"),
        };

        private static readonly string[] Segments = { "Proximal", "Intermediate", "Distal" };

        /// <summary>아바타가 Humanoid 가 아니면 매핑을 다시 만들고 재임포트한다. 반환: 매핑을 고쳤는가.</summary>
        public static bool EnsureHumanoid(string InFbxPath)
        {
            if (AssetImporter.GetAtPath(InFbxPath) is not ModelImporter importer)
            {
                return false;
            }

            if (importer.animationType == ModelImporterAnimationType.Human && isHumanAvatar(InFbxPath) == true)
            {
                return false;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(InFbxPath);
            if (model == null)
            {
                return false;
            }

            var byName = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                byName[strip(t.name)] = t;
            }

            if (byName.ContainsKey("Hips") == false)
            {
                return false; // Mixamo 식 리그가 아님
            }

            var human = new List<HumanBone>();
            void add(string InHuman, string InBone)
            {
                if (byName.TryGetValue(InBone, out var t) == true)
                {
                    var hb = new HumanBone { humanName = InHuman, boneName = t.name };
                    hb.limit.useDefaultValues = true;
                    human.Add(hb);
                }
            }

            foreach (var (h, b) in Map)
            {
                add(h, b);
            }

            foreach (string side in new[] { "Left", "Right" })
            {
                foreach (var (h, b) in Fingers)
                {
                    for (int i = 0; i < 3; ++i)
                    {
                        add($"{side} {h} {Segments[i]}", $"{side}Hand{b}{i + 1}");
                    }
                }
            }

            var skeleton = new List<SkeletonBone>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                skeleton.Add(new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
            }

            var description = importer.humanDescription;
            description.human = human.ToArray();
            description.skeleton = skeleton.ToArray();
            description.hasTranslationDoF = false;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription = description;
            importer.SaveAndReimport();

            bool ok = isHumanAvatar(InFbxPath);
            Debug.Log($"[DemoLA] {InFbxPath}: Mixamo Humanoid 매핑 {human.Count}개 재생성 → {(ok ? "성공" : "실패")}");
            return ok;
        }

        [MenuItem("DemoLA/Models/Mixamo Humanoid 매핑 다시 만들기 (Assets/Models 전체)", priority = 61)]
        public static void FixAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) == true)
                {
                    EnsureHumanoid(path);
                }
            }
        }

        private static bool isHumanAvatar(string InPath)
        {
            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(InPath))
            {
                if (o is Avatar avatar)
                {
                    return avatar.isHuman == true && avatar.isValid == true;
                }
            }

            return false;
        }

        private static string strip(string InName)
        {
            int i = InName.LastIndexOf(':');
            return i >= 0 ? InName.Substring(i + 1) : InName;
        }
    }
}
