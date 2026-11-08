using System;
using System.Collections.Generic;
using System.IO;
using DemoLA.Content;
using DemoLA.Core;
using DemoLA.Data;
using DemoLA.Player;
using DemoLA.World;
using Framework;
using Framework.Editor;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemoLA.Editor
{
    /// <summary>
    /// 프로젝트 셋업. 메뉴 · Pipeline CLI(demola setup) · CI(BuildCommand) 가 같은 함수를 쓴다 (대화창 없는 경로).
    ///  1) CSV → TableAsset (Assets/_Project/Data/Tables) — 정적 데이터는 Resources 를 쓰지 않는다
    ///  2) 기본 데이터 에셋 (GameDatabase + 직업/스킬/장비/보스/패턴/StateTree)
    ///  3) InputActionAsset (Assets/_Project/Input/DemoLAControls.inputactions)
    ///  4) Addressables 로컬 그룹 "DemoLA Data" 등록 (카탈로그는 기본값)
    ///  5) 레이드 씬 (경기장 + NavMesh + GameBootstrap)
    /// </summary>
    public static class DemoLAProjectSetup
    {
        public const string DataRoot = "Assets/_Project/Data/Generated";
        public const string SceneRoot = "Assets/_Project/Scenes";
        public const string ScenePath = SceneRoot + "/Raid.unity";
        public const string MaterialRoot = "Assets/_Project/Materials";
        public const string DatabasePath = DataRoot + "/GameDatabase.asset";

        [MenuItem("DemoLA/Setup/0. 전체 셋업 (데이터 + 씬)", priority = 0)]
        public static void FullSetupMenu()
        {
            bool overwrite = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath) == null
                || EditorUtility.DisplayDialog("DemoLA", "생성된 데이터 에셋을 다시 만들까요? (에디터에서 바꾼 직업/스킬/패턴 값이 사라집니다)", "다시 만들기", "데이터 유지");
            RunFullSetup(overwrite, true);
        }

        /// <summary>대화창 없는 전체 셋업. InOverwriteData 가 false 면 기존 데이터 에셋을 유지한다.</summary>
        public static string RunFullSetup(bool InOverwriteData, bool InCreateScene)
        {
            if (InCreateScene == true && Application.isBatchMode == false)
            {
                for (int i = 0; i < SceneManager.sceneCount; ++i)
                {
                    if (SceneManager.GetSceneAt(i).isDirty == true)
                    {
                        return $"중단: 저장하지 않은 씬이 있습니다 ({SceneManager.GetSceneAt(i).path}). 저장 후 다시 실행하세요.";
                    }
                }
            }

            var tables = ImportTables();
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (database == null || InOverwriteData == true)
            {
                database = CreateDataAssets(tables);
            }
            else if (database.Tables != tables)
            {
                database.Tables = tables;
                EditorUtility.SetDirty(database);
            }

            var controls = EnsureInputAsset();
            RegisterAddressables(database, tables, controls);
            if (InCreateScene == true)
            {
                CreateRaidScene(database, controls);
            }

            AssetDatabase.SaveAssets();
            bool hasDatabase = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath) != null;
            bool hasControls = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(InputBindings.AssetPath) != null;
            return $"tables {tables.GetAll().Length} · database {(hasDatabase ? DatabasePath : "없음")} · input {(hasControls ? InputBindings.AssetPath : "없음")}{(InCreateScene ? " · scene " + ScenePath : string.Empty)}";
        }

        // ------------------------------------------------------------------ 1. tables

        [MenuItem("DemoLA/Data/CSV → 테이블 다시 가져오기", priority = 19)]
        public static void ImportTablesMenu()
        {
            ImportTables();
            Debug.Log("[DemoLA] CSV 테이블 가져오기 완료");
        }

        public static DemoLATables ImportTables()
        {
            ensureFolder(TablePaths.TablesFolder);
            var created = new Dictionary<Type, TableAssetBase>();
            foreach (var type in DemoLATables.TableTypes)
            {
                var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(TablePaths.CsvPath(type.Name));
                if (csv == null)
                {
                    Debug.LogWarning($"[DemoLA] {TablePaths.CsvPath(type.Name)} 없음 — {type.Name} 건너뜀");
                    continue;
                }

                string path = $"{TablePaths.TablesFolder}/{type.Name}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<TableAssetBase>(path);
                if (asset == null)
                {
                    asset = (TableAssetBase)ScriptableObject.CreateInstance(type);
                    AssetDatabase.CreateAsset(asset, path);
                }

                asset.Source = csv;
                TableImportUtility.Import(asset);
                created[type] = asset;
            }

            var root = AssetDatabase.LoadAssetAtPath<DemoLATables>(TablePaths.TablesRootAsset);
            if (root == null)
            {
                root = ScriptableObject.CreateInstance<DemoLATables>();
                AssetDatabase.CreateAsset(root, TablePaths.TablesRootAsset);
            }

            root.Assign(created);
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();
            return root;
        }

        // ------------------------------------------------------------------ 2. data assets

        public static GameDatabase CreateDataAssets(DemoLATables InTables)
        {
            // 폴더는 남기고 안의 에셋만 지운다 (같은 폴더를 지웠다 바로 만들면 AssetDatabase 가 어긋난다).
            if (AssetDatabase.IsValidFolder(DataRoot) == true)
            {
                var paths = new List<string>();
                foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { DataRoot }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.IsValidFolder(path) == false)
                    {
                        paths.Add(path);
                    }
                }

                var failed = new List<string>();
                AssetDatabase.DeleteAssets(paths.ToArray(), failed);
            }

            ensureFolder(DataRoot);
            var database = DefaultContent.CreateDatabase(InTables);
            var used = new HashSet<string>();
            // 참조되는 쪽부터 에셋으로 만든다 (이펙트 → 스킬 · 패턴 · AI · 세트 → 직업 · 보스).
            // 참조하는 에셋을 먼저 저장하면 아직 에셋이 아닌 대상을 가리켜 fileID 0 으로 저장되고,
            // 나중에 SetDirty · 재직렬화를 해도 고쳐지지 않는 경우가 있었다.
            var ordered = new List<ScriptableObject>(DefaultContent.CreatedObjects);
            ordered.Sort((x, y) => creationOrder(x).CompareTo(creationOrder(y)));
            foreach (var created in ordered)
            {
                string folder = $"{DataRoot}/{categoryOf(created)}";
                ensureFolder(folder);
                string name = sanitize(created.name);
                string path = $"{folder}/{name}.asset";
                int suffix = 1;
                while (used.Add(path) == false)
                {
                    path = $"{folder}/{name}_{suffix++}.asset";
                }

                AssetDatabase.CreateAsset(created, path);
            }

            ensureFolder($"{DataRoot}/StateTree");
            var partyTree = DefaultTrees.CreatePartyAI();
            AssetDatabase.CreateAsset(partyTree, $"{DataRoot}/StateTree/ST_Party.asset");
            AssetDatabase.CreateAsset(database, DatabasePath);
            // 먼저 저장된 에셋(직업 등)이 나중에 에셋이 된 객체(스킬 등)를 가리키도록 전부 다시 직렬화한다.
            foreach (var created in DefaultContent.CreatedObjects)
            {
                EditorUtility.SetDirty(created);
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            // SetDirty + SaveAssets 만으로는 "먼저 저장된 에셋 → 나중에 생긴 에셋" 참조가 가끔 fileID 0 으로 남는다
            // (직업의 Skills, 보스의 Patterns/Brain 이 전부 null 이 되는 경우가 있었다). 메모리의 객체를 기준으로 전부 다시 쓴다.
            var written = new List<string>();
            foreach (var created in DefaultContent.CreatedObjects)
            {
                string assetPath = AssetDatabase.GetAssetPath(created);
                if (string.IsNullOrEmpty(assetPath) == false && written.Contains(assetPath) == false)
                {
                    written.Add(assetPath);
                }
            }

            written.Add(AssetDatabase.GetAssetPath(partyTree));
            written.Add(DatabasePath);
            AssetDatabase.ForceReserializeAssets(written, ForceReserializeAssetsOptions.ReserializeAssets);
            Debug.Log($"[DemoLA] 데이터 에셋 {DefaultContent.CreatedObjects.Count + 2}개 생성 → {DataRoot}");
            return database;
        }

        // ------------------------------------------------------------------ 3. input

        public static UnityEngine.InputSystem.InputActionAsset EnsureInputAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(InputBindings.AssetPath);
            if (existing != null)
            {
                return existing;
            }

            ensureFolder(Path.GetDirectoryName(InputBindings.AssetPath).Replace('\\', '/'));
            var asset = InputBindings.CreateDefaultAsset();
            File.WriteAllText(InputBindings.AssetPath, asset.ToJson());
            UnityEngine.Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(InputBindings.AssetPath);
            return AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(InputBindings.AssetPath);
        }

        // ------------------------------------------------------------------ 4. addressables

        public static void RegisterAddressables(GameDatabase InDatabase, DemoLATables InTables, UnityEngine.Object InControls)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                Debug.LogWarning("[DemoLA] Addressables 설정을 만들지 못했습니다.");
                return;
            }

            var group = settings.FindGroup(TablePaths.AddressableGroup);
            if (group == null)
            {
                // 로컬 번들 (빌드에 포함 · 기본 카탈로그).
                group = settings.CreateGroup(TablePaths.AddressableGroup, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                var bundled = group.GetSchema<BundledAssetGroupSchema>();
                bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            }

            register(settings, group, InDatabase, GameDatabase.Address);
            register(settings, group, InTables, "DemoLATables");
            register(settings, group, InControls, "DemoLAControls");
            AssetDatabase.SaveAssets();
        }

        private static void register(AddressableAssetSettings InSettings, AddressableAssetGroup InGroup, UnityEngine.Object InAsset, string InAddress)
        {
            if (InAsset == null)
            {
                return;
            }

            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(InAsset));
            var entry = InSettings.CreateOrMoveEntry(guid, InGroup, false, false);
            entry.address = InAddress;
            InSettings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }

        // ------------------------------------------------------------------ 5. scene

        [MenuItem("DemoLA/Setup/레이드 씬만 다시 만들기", priority = 2)]
        public static void CreateRaidSceneMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (database == null)
            {
                RunFullSetup(true, true);
                return;
            }

            CreateRaidScene(database, EnsureInputAsset());
        }

        public static void CreateRaidScene(GameDatabase InDatabase, UnityEngine.InputSystem.InputActionAsset InControls)
        {
            string databasePath = AssetDatabase.GetAssetPath(InDatabase);
            string controlsPath = AssetDatabase.GetAssetPath(InControls);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            // NewScene 이 사용하지 않는 에셋을 내리므로 넘겨받은 참조 대신 경로로 다시 읽는다.
            InDatabase = AssetDatabase.LoadAssetAtPath<GameDatabase>(string.IsNullOrEmpty(databasePath) ? DatabasePath : databasePath);
            InControls = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(string.IsNullOrEmpty(controlsPath) ? InputBindings.AssetPath : controlsPath);
            ensureFolder(MaterialRoot);
            var floor = ArenaMeshBuilder.BuildArena();
            bakeNavMesh(floor.Surface);

            NetworkSetup.AddToScene();

            var bootstrapGo = new GameObject("GameBootstrap");
            var bootstrap = bootstrapGo.AddComponent<GameBootstrap>();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("database").objectReferenceValue = InDatabase;
            serialized.FindProperty("arena").objectReferenceValue = floor;
            serialized.FindProperty("controls").objectReferenceValue = InControls;
            serialized.FindProperty("partyTree").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Framework.StateTree.StateTreeAsset>($"{DataRoot}/StateTree/ST_Party.asset");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ensureFolder(SceneRoot);
            EditorSceneManager.SaveScene(scene, ScenePath);
            addToBuildSettings(ScenePath);
            Debug.Log($"[DemoLA] 레이드 씬 → {ScenePath}");
        }

        [MenuItem("DemoLA/Save/로컬 저장 파일 열기", priority = 50)]
        public static void RevealSave()
        {
            EditorUtility.RevealInFinder(new DemoLA.Save.LocalJsonSaveService().FilePath);
        }

        [MenuItem("DemoLA/Save/로컬 저장 파일 삭제", priority = 51)]
        public static void DeleteSave()
        {
            string path = new DemoLA.Save.LocalJsonSaveService().FilePath;
            if (File.Exists(path) == true && EditorUtility.DisplayDialog("DemoLA", $"{path} 를 삭제할까요?", "삭제", "취소") == true)
            {
                File.Delete(path);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static void bakeNavMesh(NavMeshSurface InSurface)
        {
            if (InSurface == null)
            {
                return;
            }

            InSurface.BuildNavMesh();
            if (InSurface.navMeshData == null)
            {
                return;
            }

            string folder = $"{SceneRoot}/Raid";
            ensureFolder(folder);
            string path = $"{folder}/NavMesh-Arena.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(InSurface.navMeshData, path);
            EditorUtility.SetDirty(InSurface);
        }

        private static int creationOrder(ScriptableObject InObject)
        {
            switch (InObject)
            {
                case DemoLA.Effects.GameplayEffectAsset _: return 0;
                case DemoLA.Character.CharacterClassDefinition _: return 2;
                case DemoLA.Boss.BossDefinition _: return 2;
            }

            return 1;
        }

        private static string categoryOf(ScriptableObject InObject)
        {
            switch (InObject)
            {
                case DemoLA.Skills.SkillDefinition _: return "Skills";
                case DemoLA.Character.CharacterClassDefinition _: return "Classes";
                case DemoLA.Effects.GameplayEffectAsset _: return "Effects";
                case DemoLA.Boss.BossPatternDefinition _: return "Boss/Patterns";
                case DemoLA.Boss.BossDefinition _: return "Boss";
                case Framework.StateTree.StateTreeAsset _: return "StateTree";
            }

            return "Build";
        }

        private static string sanitize(string InName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                InName = InName.Replace(c, '_');
            }

            return InName.Replace(' ', '_');
        }

        public static void ensureFolder(string InPath)
        {
            if (AssetDatabase.IsValidFolder(InPath) == true)
            {
                return;
            }

            string parent = Path.GetDirectoryName(InPath).Replace('\\', '/');
            ensureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(InPath));
        }

        private static void addToBuildSettings(string InScenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == InScenePath) == false)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(InScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
