using System;
using System.Collections.Generic;
using System.IO;
using DemoLA.Boss;
using DemoLA.Boss.Telegraph;
using DemoLA.Core;
using DemoLA.Cue;
using DemoLA.Data;
using DemoLA.Player;
using DemoLA.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DemoLA.Editor
{
    /// <summary>
    /// 월드 프리팹 굽기 — 예전에 런타임에 코드로 조립하던 오브젝트를 프리팹으로 미리 만들어 Addressables 에 등록한다.
    ///  - 조립 코드는 각 클래스의 Build / Compose* (게임의 대체 경로와 같은 코드)를 그대로 부르고, 머티리얼은 에셋으로 만들어 넣는다.
    ///  - 결과: Assets/_Project/Prefabs/World/{Actors · Fx · Gimmick · Camera · Env}/*.prefab + Arena · Lobby (NavMesh 데이터 포함)
    ///  - Addressables 그룹 "DemoLA World Prefabs" (로컬 번들), 주소 = WorldPrefabs 주소, 라벨 = WorldPrefabs.Label
    /// 게임은 WorldPrefabs.PreloadAsync 로 라벨 전체를 받아 두고 Instantiate 한다.
    /// 메뉴: DemoLA/Prefabs/월드 프리팹 굽기 · 전체 셋업에서도 자동 실행 (직업 · 보스 데이터를 다시 만들면 다시 굽는다).
    /// 조립은 임시 추가 씬에서 하고 그 씬을 저장 없이 닫는다 — 열려 있는 씬을 "수정됨" 으로 만들지 않는다.
    /// </summary>
    public static class WorldPrefabBaker
    {
        public const string GroupName = "DemoLA World Prefabs";
        public const string MaterialFolder = WorldPrefabs.AssetRoot + "/World/Materials";

        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        [MenuItem("DemoLA/Prefabs/월드 프리팹 굽기", priority = 60)]
        public static void BakeMenu()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DemoLAProjectSetup.DatabasePath);
            Debug.Log($"[WorldPrefabBaker] {BakeAll(database)}");
        }

        /// <summary>모든 월드 프리팹을 다시 굽고 Addressables 에 등록한다. 반환 = 요약.</summary>
        public static string BakeAll(GameDatabase InDatabase)
        {
            var previousActive = SceneManager.GetActiveScene();
            var workScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(workScene);
            try
            {
                return bakeAll(InDatabase);
            }
            finally
            {
                if (previousActive.IsValid() == true)
                {
                    SceneManager.SetActiveScene(previousActive);
                }

                EditorSceneManager.CloseScene(workScene, true);
            }
        }

        private static string bakeAll(GameDatabase InDatabase)
        {
            MaterialCache.Clear();
            var baked = new List<string>();
            ensureFolder(MaterialFolder);

            Func<Color, Material> colors = colorMaterial;

            // ---------------------------------------------------------------- actors
            if (InDatabase != null)
            {
                foreach (var cls in InDatabase.Classes)
                {
                    if (cls == null)
                    {
                        continue;
                    }

                    string address = WorldPrefabs.Character(cls.Class);
                    save(address, () =>
                    {
                        var go = new GameObject(Path.GetFileName(address));
                        RaidSpawner.ComposeCharacter(go, cls, colors);
                        return go;
                    }, baked);
                }

                BossPositionalIndicator.BakedMaterial = spriteMaterial("M_BossPositional", Color.white, 2999);
                try
                {
                    foreach (var boss in InDatabase.Bosses)
                    {
                        if (boss == null)
                        {
                            continue;
                        }

                        string address = WorldPrefabs.Boss(boss.BossId);
                        save(address, () =>
                        {
                            var go = new GameObject(Path.GetFileName(address));
                            RaidSpawner.ComposeBoss(go, boss, colors);
                            return go;
                        }, baked);
                    }
                }
                finally
                {
                    BossPositionalIndicator.BakedMaterial = null;
                }
            }

            // ---------------------------------------------------------------- arena · lobby (NavMesh 데이터 포함)
            ArenaMeshBuilder.WithArenaMaterials(() =>
            {
                save(WorldPrefabs.Arena, () =>
                {
                    ArenaBuilder.MaterialFactory = colors;
                    try
                    {
                        var floor = ArenaBuilder.Build(null);
                        bakeNavMesh(floor.Surface, "Arena");
                        return floor.gameObject;
                    }
                    finally
                    {
                        ArenaBuilder.MaterialFactory = null;
                    }
                }, baked);

                save(WorldPrefabs.Lobby, () =>
                {
                    var lobby = LobbyArea.Build(colors, spriteMaterial("M_LobbyVeil", LobbyArea.VeilColor, -1));
                    bakeNavMesh(lobby.GetComponent<NavMeshSurface>(), "Lobby");
                    return lobby;
                }, baked);
            });

            // ---------------------------------------------------------------- fx · gimmick
            save(WorldPrefabs.BossImpactFx, () =>
            {
                var go = new GameObject("BossImpactFx");
                go.AddComponent<BossImpactFx>().Build(spriteMaterial("M_ImpactLine", Color.white, -1), spriteMaterial("M_ImpactPillar", new Color(1.0f, 0.95f, 0.8f, 0.7f), -1));
                return go;
            }, baked);

            save(WorldPrefabs.PillarCollapseFx, () =>
            {
                var go = new GameObject("PillarCollapseFx");
                go.AddComponent<PillarCollapseFx>().Build(spriteMaterial("M_CollapseDust", new Color(0.75f, 0.7f, 0.62f, 0.5f), 3002));
                return go;
            }, baked);

            save(WorldPrefabs.Telegraph, () =>
            {
                var outline = spriteMaterial("M_TelegraphOutline", Color.white, -1);
                var fill = spriteMaterial("M_TelegraphFill", Color.white, -1);
                var safe = spriteMaterial("M_TelegraphSafe", Color.white, -1);
                TelegraphView.ConfigureMaterials(outline, fill, safe);
                EditorUtility.SetDirty(outline);
                EditorUtility.SetDirty(fill);
                EditorUtility.SetDirty(safe);
                var go = new GameObject("Telegraph");
                go.AddComponent<TelegraphView>().Build(outline, fill, safe);
                return go;
            }, baked);

            save(WorldPrefabs.CueFlash, () => CueFlash.Build(spriteMaterial("M_CueFlash", Color.white, -1)).gameObject, baked);
            save(WorldPrefabs.RaidPickup, () => RaidPickup.Build(colorMaterial(Color.white)).gameObject, baked);
            save(WorldPrefabs.CoverPillar, () => CoverPillars.Build(colorMaterial(CoverPillars.PillarColor)).gameObject, baked);

            // ---------------------------------------------------------------- camera · env
            save(WorldPrefabs.QuarterViewCamera, () =>
            {
                var go = new GameObject("[Camera]");
                go.AddComponent<QuarterViewCamera>().BuildRig();
                return go;
            }, baked);

            save(WorldPrefabs.BossCinematicCamera, () =>
            {
                var go = new GameObject("CM BossCinematic");
                go.AddComponent<BossCinematicCamera>().BuildRig();
                return go;
            }, baked);

            save(WorldPrefabs.DirectionalLight, () =>
            {
                var go = GameBootstrap.BuildDefaultLight();
                go.transform.rotation = Quaternion.identity; // 회전은 Instantiate 때 준다
                return go;
            }, baked);

            AssetDatabase.SaveAssets();
            int registered = registerAddressables(baked);
            return $"월드 프리팹 {baked.Count}개 → {WorldPrefabs.AssetRoot}/World, Addressables '{GroupName}' {registered}개 (라벨 {WorldPrefabs.Label})";
        }

        // ------------------------------------------------------------------ save

        private static void save(string InAddress, Func<GameObject> InBuild, List<string> OutBaked)
        {
            GameObject go = null;
            try
            {
                go = InBuild();
                if (go == null)
                {
                    return;
                }

                string path = WorldPrefabs.AssetPath(InAddress);
                ensureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                go.transform.SetPositionAndRotation(Vector3.zero, go.transform.rotation);
                bool success;
                PrefabUtility.SaveAsPrefabAsset(go, path, out success);
                if (success == true)
                {
                    OutBaked.Add(InAddress);
                }
                else
                {
                    Debug.LogError($"[WorldPrefabBaker] 저장 실패: {path}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WorldPrefabBaker] {InAddress} 굽기 실패: {e}");
            }
            finally
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        /// <summary>NavMesh 를 구워 에셋으로 저장하고 Surface 에 연결한다 (게임에서 다시 굽지 않는다).</summary>
        private static void bakeNavMesh(NavMeshSurface InSurface, string InName)
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

            string folder = WorldPrefabs.AssetRoot + "/World/NavMesh";
            ensureFolder(folder);
            string path = $"{folder}/NavMesh-{InName}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(InSurface.navMeshData, path);
        }

        // ------------------------------------------------------------------ materials

        /// <summary>색 머티리얼 (URP Lit, 색마다 1개) — 기본 도형 몸 · 기둥 등.</summary>
        private static Material colorMaterial(Color InColor)
        {
            string name = "M_Color_" + ColorUtility.ToHtmlStringRGBA(InColor);
            return material(name, () =>
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var created = new Material(shader) { color = InColor };
                if (created.HasProperty("_BaseColor") == true)
                {
                    created.SetColor("_BaseColor", InColor);
                }

                return created;
            });
        }

        /// <summary>Sprites/Default 머티리얼 (연출 · 장판 — 항상 포함 셰이더, MaterialPropertyBlock 으로 색을 바꾼다). InQueue &lt; 0 이면 기본 큐.</summary>
        private static Material spriteMaterial(string InName, Color InColor, int InQueue)
        {
            var result = material(InName, () => new Material(Shader.Find("Sprites/Default")));
            result.color = InColor;
            if (InQueue >= 0)
            {
                result.renderQueue = InQueue;
            }

            EditorUtility.SetDirty(result);
            return result;
        }

        private static Material material(string InName, Func<Material> InCreate)
        {
            Material cached;
            if (MaterialCache.TryGetValue(InName, out cached) == true && cached != null)
            {
                return cached;
            }

            string path = $"{MaterialFolder}/{InName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                existing = InCreate();
                existing.name = InName;
                AssetDatabase.CreateAsset(existing, path);
            }

            MaterialCache[InName] = existing;
            return existing;
        }

        // ------------------------------------------------------------------ addressables

        private static int registerAddressables(List<string> InAddresses)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                Debug.LogWarning("[WorldPrefabBaker] Addressables 설정이 없습니다.");
                return 0;
            }

            var group = settings.FindGroup(GroupName);
            if (group == null)
            {
                group = settings.CreateGroup(GroupName, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                var bundled = group.GetSchema<BundledAssetGroupSchema>();
                bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                // 프리팹마다 따로 받을 수 있게 묶음을 나눈다 (캐릭터 · 보스 모델이 크다).
                bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            }

            settings.AddLabel(WorldPrefabs.Label);
            int count = 0;
            foreach (var address in InAddresses)
            {
                string guid = AssetDatabase.AssetPathToGUID(WorldPrefabs.AssetPath(address));
                if (string.IsNullOrEmpty(guid) == true)
                {
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.address = address;
                entry.SetLabel(WorldPrefabs.Label, true, true, false);
                count += 1;
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            return count;
        }

        private static void ensureFolder(string InPath)
        {
            if (string.IsNullOrEmpty(InPath) == true || AssetDatabase.IsValidFolder(InPath) == true)
            {
                return;
            }

            string parent = Path.GetDirectoryName(InPath).Replace('\\', '/');
            ensureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(InPath));
        }
    }
}
