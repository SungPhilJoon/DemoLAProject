using DemoLA.Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DemoLA.Editor
{
    /// <summary>
    /// 슬레이어 = ElegantFemale 모델 + 대검 + Animator(Idle/Run).
    ///  - AC_Slayer: Speed(float) &gt; 0.1 → Run (Great Sword Run), 아니면 Idle (같은 클립 첫 프레임 정지)
    ///  - Slayer_ElegantFemale.prefab: 모델 + Animator + 오른손 소켓에 대검
    ///  - CLS_Slayer.ModelPrefab 에 연결 (전체 셋업이 Generated 를 다시 만들어도 다시 연결된다)
    /// 에셋이 없으면 에디터 로드 시 자동으로 만든다. 메뉴로 다시 만들 수 있다.
    /// </summary>
    [InitializeOnLoad]
    public static class SlayerCharacterBuilder
    {
        public const string Folder = "Assets/_Project/Art/Characters/Slayer";
        public const string ControllerPath = Folder + "/AC_Slayer.controller";
        public const string PrefabPath = Folder + "/Slayer_ElegantFemale.prefab";
        public const string ModelPath = "Assets/Models/ElegantFemale.fbx";
        public const string RunClipPath = "Assets/Models/Animations/ElegantFemale/Maw J Laygo@Great Sword Run.fbx";
        public const string WeaponPath = "Assets/Models/Weapons/Greatsword.fbx";
        public const string ClassPath = DemoLAProjectSetup.DataRoot + "/Classes/CLS_Slayer.asset";
        public const float RunThreshold = 0.1f;

        static SlayerCharacterBuilder()
        {
            EditorApplication.delayCall += autoBuild;
        }

        private static void autoBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode == true || Application.isBatchMode == true)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null || AssetDatabase.LoadAssetAtPath<GameObject>(RunClipPath) == null)
            {
                return; // 모델/클립이 아직 임포트되지 않음
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Build();
            }
            else
            {
                AssignToClass(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            }
        }

        [MenuItem("DemoLA/Characters/슬레이어 (ElegantFemale) 다시 만들기", priority = 70)]
        public static void Build()
        {
            ensureFolder(Folder);
            var clip = prepareRunClip();
            if (clip == null)
            {
                Debug.LogError($"[DemoLA] 슬레이어: {RunClipPath} 에서 애니메이션 클립을 찾지 못했습니다");
                return;
            }

            ensureHumanoid(ModelPath);
            var controller = buildController(clip);
            var prefab = buildPrefab(controller);
            AssignToClass(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DemoLA] 슬레이어 캐릭터 생성: {PrefabPath} (Animator {ControllerPath}, Run = {clip.name})");
        }

        /// <summary>CLS_Slayer 에 모델 프리팹을 연결한다 (전체 셋업 뒤에도 호출됨).</summary>
        public static void AssignToClass(GameObject InPrefab)
        {
            var cls = AssetDatabase.LoadAssetAtPath<CharacterClassDefinition>(ClassPath);
            if (cls == null || InPrefab == null || cls.ModelPrefab == InPrefab)
            {
                return;
            }

            cls.ModelPrefab = InPrefab;
            EditorUtility.SetDirty(cls);
            AssetDatabase.SaveAssetIfDirty(cls);
        }

        // ------------------------------------------------------------------ clip

        /// <summary>Humanoid · 루프 · 제자리(루트 이동은 포즈에 굽기)로 임포트하고 클립을 돌려준다.</summary>
        private static AnimationClip prepareRunClip()
        {
            var importer = AssetImporter.GetAtPath(RunClipPath) as ModelImporter;
            if (importer == null)
            {
                return null;
            }

            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            for (int i = 0; i < clips.Length; ++i)
            {
                var c = clips[i];
                if (c.loopTime == false || c.loopPose == false || c.lockRootRotation == false || c.lockRootHeightY == false || c.lockRootPositionXZ == false)
                {
                    c.loopTime = true;
                    c.loopPose = true;
                    c.lockRootRotation = true;      // Root Transform Rotation: Bake Into Pose
                    c.lockRootHeightY = true;       // Root Transform Position (Y): Bake Into Pose
                    c.lockRootPositionXZ = true;    // Root Transform Position (XZ): Bake Into Pose (제자리 달리기)
                    c.keepOriginalOrientation = true;
                    c.keepOriginalPositionY = true;
                    c.keepOriginalPositionXZ = true;
                    c.name = "Slayer_Run";
                    changed = true;
                }
            }

            if (changed == true)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }

            foreach (var asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(RunClipPath))
            {
                if (asset is AnimationClip clip && clip.name.StartsWith("__preview__") == false)
                {
                    return clip;
                }
            }

            return null;
        }

        private static void ensureHumanoid(string InPath)
        {
            MixamoHumanoid.EnsureHumanoid(InPath);
        }

        // ------------------------------------------------------------------ controller

        private static AnimatorController buildController(AnimationClip InRun)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var sm = controller.layers[0].stateMachine;
            var idle = sm.AddState("Idle", new Vector3(250.0f, 0.0f, 0.0f));
            idle.motion = InRun;
            idle.speed = 0.0f;          // 대기 클립이 생기기 전까지 달리기 첫 프레임에 멈춘 자세
            idle.cycleOffset = 0.0f;
            var run = sm.AddState("Run", new Vector3(250.0f, 120.0f, 0.0f));
            run.motion = InRun;
            sm.defaultState = idle;

            var toRun = idle.AddTransition(run);
            toRun.hasExitTime = false;
            toRun.duration = 0.12f;
            toRun.AddCondition(AnimatorConditionMode.Greater, RunThreshold, "Speed");

            var toIdle = run.AddTransition(idle);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.Less, RunThreshold, "Speed");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // ------------------------------------------------------------------ prefab

        private static GameObject buildPrefab(AnimatorController InController)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            root.name = "Slayer_ElegantFemale";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                animator = root.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = InController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            attachWeapon(root, animator);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// 오른손에 대검을 쥐인다. 손 방향은 바인드 포즈에서 계산한다:
        /// 날 = 엄지 쪽(손가락 방향에 수직), 날의 폭 = 손가락 방향, 손잡이 원점 = 손바닥 중심.
        /// </summary>
        private static void attachWeapon(GameObject InRoot, Animator InAnimator)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPath);
            Transform hand = InAnimator.isHuman == true ? InAnimator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            hand = hand != null ? hand : findDeep(InRoot.transform, "mixamorig:RightHand");
            if (weapon == null || hand == null)
            {
                Debug.LogWarning("[DemoLA] 슬레이어: 대검 또는 오른손 본을 찾지 못해 무기를 붙이지 않았습니다");
                return;
            }

            Transform middle = findDeep(hand, "mixamorig:RightHandMiddle1");
            Transform thumb = findDeep(hand, "mixamorig:RightHandThumb1");
            Vector3 fingers = middle != null ? (middle.position - hand.position).normalized : hand.TransformDirection(Vector3.up);
            Vector3 thumbDir = thumb != null ? (thumb.position - hand.position) : Vector3.forward;
            Vector3 blade = Vector3.ProjectOnPlane(thumbDir, fingers).normalized;
            if (blade.sqrMagnitude < 0.5f)
            {
                blade = Vector3.forward;
            }

            float palm = middle != null ? Vector3.Distance(hand.position, middle.position) * 0.55f : 0.05f;

            var socket = new GameObject("WeaponSocket_R").transform;
            socket.SetParent(hand, false);
            socket.position = hand.position + fingers * palm;
            // 대검 메시는 +Y 가 날 방향, +X 가 날 폭.
            socket.rotation = Quaternion.LookRotation(Vector3.Cross(fingers, blade), blade);

            var sword = (GameObject)PrefabUtility.InstantiatePrefab(weapon);
            sword.name = "Greatsword";
            sword.transform.SetParent(socket, false);
            sword.transform.localPosition = Vector3.zero;
            sword.transform.localRotation = Quaternion.identity;

            // FBX 축 변환이 어떻게 들어왔든, 실제 메시에서 날 끝 방향을 재서 소켓 +Y 로 맞춘다.
            Vector3 tip = farthestVertex(sword.transform, socket);
            if (tip.sqrMagnitude > 1e-4f)
            {
                sword.transform.localRotation = Quaternion.FromToRotation(tip.normalized, Vector3.up) * sword.transform.localRotation;
            }
        }

        /// <summary>InRoot 아래 메시 정점 중 InSpace 원점에서 가장 먼 점 (InSpace 로컬 좌표).</summary>
        private static Vector3 farthestVertex(Transform InRoot, Transform InSpace)
        {
            Vector3 best = Vector3.zero;
            foreach (var mf in InRoot.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                foreach (var v in mesh.vertices)
                {
                    Vector3 p = InSpace.InverseTransformPoint(mf.transform.TransformPoint(v));
                    if (p.sqrMagnitude > best.sqrMagnitude)
                    {
                        best = p;
                    }
                }
            }

            return best;
        }

        private static Transform findDeep(Transform InRoot, string InName)
        {
            if (InRoot.name == InName)
            {
                return InRoot;
            }

            for (int i = 0; i < InRoot.childCount; ++i)
            {
                var found = findDeep(InRoot.GetChild(i), InName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void ensureFolder(string InPath)
        {
            if (AssetDatabase.IsValidFolder(InPath) == true)
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(InPath).Replace('\\', '/');
            ensureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(InPath));
        }
    }
}
