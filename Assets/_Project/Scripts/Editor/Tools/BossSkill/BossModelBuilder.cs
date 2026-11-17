using System.Collections.Generic;
using DemoLA.Boss;
using DemoLA.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DemoLA.Editor.BossTool
{
    /// <summary>
    /// 보스 모델 프리팹 만들기: BossDefinition 의 SourceModel(FBX) + IdleAnimation/MoveAnimation → 프리팹 + 컨트롤러(Speed).
    ///  - Assets/_Project/Art/Characters/Bosses/{BossId}/Boss_{BossId}.prefab, AC_Boss_{BossId}.controller
    ///  - 기본 설정(Presets): 2관문 = IronhornWarlord (대기 Combat_Stance · 이동 Run_03 · 사망 Dead)
    ///  - 보스 정의(Data/Generated)는 전체 셋업 때 다시 만들어지므로 모델 설정은 BossId 별로 옮겨 붙인다 (Collect/Restore).
    /// </summary>
    [InitializeOnLoad]
    public static class BossModelBuilder
    {
        public const string Folder = "Assets/_Project/Art/Characters/Bosses";
        public const float RunThreshold = 0.1f;

        private struct Preset
        {
            public string BossId;
            public string Model;
            public string AnimFolder;
            public string Idle;
            public string Move;
            public string Death;
            public float Scale;
        }

        private static readonly Preset[] Presets =
        {
            new Preset
            {
                BossId = "Gate2",
                Model = "Assets/Models/IronhornWarlord.fbx",
                AnimFolder = "Assets/_Project/Animation/Boss/Gate2/IronhornWarlord",
                Idle = "Combat_Stance",
                Move = "Run_03",
                Death = "Dead",
                Scale = 2.6f,
            },
        };

        static BossModelBuilder()
        {
            EditorApplication.delayCall += autoApply;
        }

        /// <summary>프리셋이 있고 아직 모델이 없는 보스에 기본 모델을 붙인다.</summary>
        private static void autoApply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode == true || Application.isBatchMode == true)
            {
                return;
            }

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DemoLAProjectSetup.DatabasePath);
            if (database == null)
            {
                return;
            }

            foreach (var preset in Presets)
            {
                var boss = database.GetBoss(preset.BossId);
                if (boss != null && boss.ModelPrefab == null && AssetDatabase.LoadAssetAtPath<GameObject>(preset.Model) != null)
                {
                    ApplyPreset(boss);
                }
            }
        }

        [MenuItem("DemoLA/Characters/2관문 보스 (IronhornWarlord) 모델 다시 만들기", priority = 71)]
        private static void RebuildGate2Menu()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DemoLAProjectSetup.DatabasePath);
            var boss = database != null ? database.GetBoss("Gate2") : null;
            if (boss != null)
            {
                ApplyPreset(boss);
            }
        }

        /// <summary>프리셋(원본 모델 · 기본 클립 · 배율)을 채우고 프리팹을 만든다. 프리셋이 없으면 false.</summary>
        public static bool ApplyPreset(BossDefinition InBoss)
        {
            foreach (var preset in Presets)
            {
                if (preset.BossId != InBoss.BossId)
                {
                    continue;
                }

                Undo.RecordObject(InBoss, "보스 모델 기본 설정");
                InBoss.SourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(preset.Model);
                InBoss.IdleAnimation = clip(preset.AnimFolder, preset.Idle);
                InBoss.MoveAnimation = clip(preset.AnimFolder, preset.Move);
                InBoss.DeathAnimation = clip(preset.AnimFolder, preset.Death);
                InBoss.ModelScale = preset.Scale;
                Build(InBoss);
                return true;
            }

            return false;
        }

        public static bool HasPreset(string InBossId)
        {
            foreach (var preset in Presets)
            {
                if (preset.BossId == InBossId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>SourceModel + Idle/Move 클립으로 프리팹 · 컨트롤러를 (다시) 만들어 ModelPrefab 에 넣는다.</summary>
        public static GameObject Build(BossDefinition InBoss)
        {
            if (InBoss.SourceModel == null)
            {
                Debug.LogError($"[DemoLA] 보스 {InBoss.BossId}: SourceModel(원본 FBX)이 없습니다");
                return null;
            }

            string folder = $"{Folder}/{InBoss.BossId}";
            DemoLAProjectSetup.ensureFolder(folder);
            var controller = buildController($"{folder}/AC_Boss_{InBoss.BossId}.controller", InBoss.IdleAnimation, InBoss.MoveAnimation);

            var root = (GameObject)PrefabUtility.InstantiatePrefab(InBoss.SourceModel);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            root.name = $"Boss_{InBoss.BossId}";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                animator = root.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/Boss_{InBoss.BossId}.prefab");
            Object.DestroyImmediate(root);

            InBoss.ModelPrefab = prefab;
            EditorUtility.SetDirty(InBoss);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DemoLA] 보스 {InBoss.BossId} 모델 → {AssetDatabase.GetAssetPath(prefab)} (대기 {clipName(InBoss.IdleAnimation)} · 이동 {clipName(InBoss.MoveAnimation)} · 사망 {clipName(InBoss.DeathAnimation)}, 배율 {InBoss.ModelScale})");
            return prefab;
        }

        private static string clipName(AnimationClip InClip)
        {
            return InClip != null ? InClip.name : "-";
        }

        private static AnimationClip clip(string InFolder, string InClipName)
        {
            string path = $"{InFolder}/{InFolder.Substring(InFolder.LastIndexOf('/') + 1)}@{InClipName}.fbx";
            foreach (var asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            {
                if (asset is AnimationClip c && c.name.StartsWith("__preview__") == false)
                {
                    return c;
                }
            }

            Debug.LogWarning($"[DemoLA] 보스 클립 없음: {path}");
            return null;
        }

        private static AnimatorController buildController(string InPath, AnimationClip InIdle, AnimationClip InMove)
        {
            AssetDatabase.DeleteAsset(InPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(InPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var sm = controller.layers[0].stateMachine;
            var idle = sm.AddState("Idle", new Vector3(250.0f, 0.0f, 0.0f));
            idle.motion = InIdle;
            var run = sm.AddState("Run", new Vector3(250.0f, 120.0f, 0.0f));
            run.motion = InMove != null ? InMove : InIdle;
            sm.defaultState = idle;

            var toRun = idle.AddTransition(run);
            toRun.hasExitTime = false;
            toRun.duration = 0.15f;
            toRun.AddCondition(AnimatorConditionMode.Greater, RunThreshold, "Speed");
            var toIdle = run.AddTransition(idle);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.2f;
            toIdle.AddCondition(AnimatorConditionMode.Less, RunThreshold, "Speed");
            EditorUtility.SetDirty(controller);
            return controller;
        }

        // ------------------------------------------------------------------ 전체 셋업 보존

        public struct Saved
        {
            public GameObject SourceModel;
            public GameObject ModelPrefab;
            public float ModelScale;
            public AnimationClip Idle;
            public AnimationClip Move;
            public AnimationClip Death;
        }

        /// <summary>전체 셋업이 보스 정의를 지우기 전에 BossId 별 모델 설정을 모은다.</summary>
        public static Dictionary<string, Saved> Collect(string InFolder)
        {
            var kept = new Dictionary<string, Saved>();
            if (AssetDatabase.IsValidFolder(InFolder) == false)
            {
                return kept;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:BossDefinition", new[] { InFolder }))
            {
                var boss = AssetDatabase.LoadAssetAtPath<BossDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (boss != null && boss.ModelPrefab != null)
                {
                    kept[boss.BossId] = new Saved
                    {
                        SourceModel = boss.SourceModel, ModelPrefab = boss.ModelPrefab, ModelScale = boss.ModelScale,
                        Idle = boss.IdleAnimation, Move = boss.MoveAnimation, Death = boss.DeathAnimation,
                    };
                }
            }

            return kept;
        }

        public static void Restore(IEnumerable<ScriptableObject> InCreated, Dictionary<string, Saved> InKept)
        {
            foreach (var created in InCreated)
            {
                Saved saved;
                if (created is BossDefinition boss && InKept.TryGetValue(boss.BossId, out saved) == true)
                {
                    boss.SourceModel = saved.SourceModel;
                    boss.ModelPrefab = saved.ModelPrefab;
                    boss.ModelScale = saved.ModelScale;
                    boss.IdleAnimation = saved.Idle;
                    boss.MoveAnimation = saved.Move;
                    boss.DeathAnimation = saved.Death;
                }
            }
        }
    }
}
