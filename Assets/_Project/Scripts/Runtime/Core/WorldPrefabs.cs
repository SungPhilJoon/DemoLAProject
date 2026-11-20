using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DemoLA.Core
{
    /// <summary>
    /// 월드 프리팹 (캐릭터 · 보스 · 경기장 · 대기실 · 연출 · 장판 · 구슬 · 엄폐 기둥 · 카메라 리그 · 조명) 을 Addressables 로 불러 만든다.
    ///  - 프리팹은 에디터 WorldPrefabBaker 가 예전 런타임 생성 코드로 미리 만들어 Assets/_Project/Prefabs/World 에 둔다
    ///    (DemoLA/Prefabs/월드 프리팹 굽기 · 전체 셋업). 주소 = "World/…" (= 프리팹 경로에서 Prefabs/ 와 .prefab 을 뺀 것), 라벨 = Label.
    ///  - 게임 시작(GameBootstrap) · 시나리오 테스트(호스트 SetUp)에서 PreloadAsync 로 라벨 전체를 미리 받아 두고,
    ///    생성 코드는 Instantiate(주소) 를 동기로 부른다. 미리 받지 않은 주소는 그 자리에서 동기 로드한다 (경고).
    ///  - 에디터 정지 상태(EditMode 테스트 · 에디터 툴)는 Addressables 대신 AssetDatabase 로 같은 프리팹을 읽는다.
    ///  - 프리팹이 없으면(굽기 전) null — 호출한 쪽이 예전 코드 생성으로 대신한다.
    /// Handle 은 에셋 소유자 WorldPrefabLibrary(프레임워크 AssetOwner)의 컨테이너가 든다 — 로드 · 해제는 IAssetOwner 로만 부른다.
    /// ReleaseAll 은 게임 종료 때 (GameBootstrap 파괴).
    /// </summary>
    public static class WorldPrefabs
    {
        public const string Label = "DemoLA.WorldPrefab";
        public const string AssetRoot = "Assets/_Project/Prefabs";

        public const string Arena = "World/Arena";
        public const string Lobby = "World/Lobby";
        public const string DirectionalLight = "World/Env/DirectionalLight";
        public const string BossImpactFx = "World/Fx/BossImpactFx";
        public const string PillarCollapseFx = "World/Fx/PillarCollapseFx";
        public const string Telegraph = "World/Fx/Telegraph";
        public const string CueFlash = "World/Fx/CueFlash";
        public const string RaidPickup = "World/Gimmick/RaidPickup";
        public const string CoverPillar = "World/Gimmick/CoverPillar";
        public const string QuarterViewCamera = "World/Camera/QuarterViewCamera";
        public const string BossCinematicCamera = "World/Camera/BossCinematicCamera";

        private static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private static readonly HashSet<string> Missing = new HashSet<string>(StringComparer.Ordinal);
        private static WorldPrefabLibrary library;

        public static string Character(DemoLA.Character.ECharacterClass InClass)
        {
            return $"World/Actors/Character_{InClass}";
        }

        public static string Boss(string InBossId)
        {
            return $"World/Actors/Boss_{InBossId}";
        }

        /// <summary>주소 → 프리팹 에셋 경로 (굽기 · 에디터 정지 상태 로드).</summary>
        public static string AssetPath(string InAddress)
        {
            return $"{AssetRoot}/{InAddress}.prefab";
        }

        public static bool IsPreloaded { get; private set; }

        public static int LoadedCount
        {
            get { return Cache.Count; }
        }

        /// <summary>월드 프리팹 Handle 을 드는 에셋 소유자 (인터페이스로만 쓴다).</summary>
        private static IAssetOwner Owner
        {
            get
            {
                if (library == null)
                {
                    library = new WorldPrefabLibrary();
                }

                return library;
            }
        }

        /// <summary>라벨의 프리팹을 모두 받아 둔다 (여러 번 불러도 한 번만). 실패하면 경고만 남긴다 (생성 코드가 대신한다).</summary>
        public static async UniTask PreloadAsync()
        {
            if (IsPreloaded == true || Application.isPlaying == false)
            {
                return;
            }

            try
            {
                var loaded = await Owner.LoadAssetsByLabelAsync<GameObject>(Label);
                if (loaded.Count == 0)
                {
                    Log.Warning($"[WorldPrefabs] 라벨 '{Label}' 프리팹이 없습니다 — DemoLA/Prefabs/월드 프리팹 굽기 후 Addressables 빌드");
                    return;
                }

                foreach (var pair in loaded)
                {
                    Cache[pair.Key] = pair.Value;
                }

                IsPreloaded = true;
            }
            catch (Exception e)
            {
                Log.Warning($"[WorldPrefabs] 미리 로드 실패 → 필요할 때 동기 로드 / 코드 생성: {e.Message}");
            }
        }

        /// <summary>코루틴용 (시나리오 테스트 호스트 등 UniTask 를 참조하지 않는 어셈블리).</summary>
        public static IEnumerator PreloadRoutine()
        {
            return PreloadAsync().ToCoroutine();
        }

        /// <summary>프리팹 (없으면 null). 미리 받지 않았으면 동기로 받는다.</summary>
        public static GameObject Get(string InAddress)
        {
            if (string.IsNullOrEmpty(InAddress) == true)
            {
                return null;
            }

            GameObject prefab;
            if (Cache.TryGetValue(InAddress, out prefab) == true && prefab != null)
            {
                return prefab;
            }

#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath(InAddress));
            }
#endif
            if (Missing.Contains(InAddress) == true)
            {
                return null;
            }

            prefab = loadSync(InAddress);
            if (prefab == null)
            {
                Missing.Add(InAddress);
                Log.Warning($"[WorldPrefabs] '{InAddress}' 프리팹이 없습니다 → 코드로 만듭니다 (DemoLA/Prefabs/월드 프리팹 굽기)");
                return null;
            }

            Cache[InAddress] = prefab;
            return prefab;
        }

        private static GameObject loadSync(string InAddress)
        {
            // 위치가 없는 주소를 그냥 로드하면 Addressables 가 오류를 낸다 → 먼저 위치를 확인한다.
            return AssetHandleContainer.HasLocation(InAddress, typeof(GameObject)) == true ? Owner.LoadAssetSync<GameObject>(InAddress) : null;
        }

        /// <summary>프리팹을 InPosition · InRotation 에 만든다 (없으면 null). 이름은 프리팹 이름 그대로 ("(Clone)" 없이).</summary>
        public static GameObject Instantiate(string InAddress, Vector3 InPosition, Quaternion InRotation, Transform InParent = null)
        {
            var prefab = Get(InAddress);
            if (prefab == null)
            {
                return null;
            }

            var instance = Object.Instantiate(prefab, InPosition, InRotation, InParent);
            instance.name = prefab.name;
            return instance;
        }

        public static T Instantiate<T>(string InAddress, Vector3 InPosition, Quaternion InRotation, Transform InParent = null) where T : Component
        {
            var instance = Instantiate(InAddress, InPosition, InRotation, InParent);
            if (instance == null)
            {
                return null;
            }

            var component = instance.GetComponent<T>();
            if (component == null)
            {
                Log.Warning($"[WorldPrefabs] '{InAddress}' 프리팹에 {typeof(T).Name} 이 없습니다 — 다시 구우세요");
                ObjectUtil.SafeDestroy(instance);
            }

            return component;
        }

        /// <summary>받아 둔 프리팹 Handle 을 모두 반납한다 (게임 종료 · 테스트 정리).</summary>
        public static void ReleaseAll()
        {
            if (library != null)
            {
                Owner.ReleaseAllAssets();
            }

            Cache.Clear();
            Missing.Clear();
            IsPreloaded = false;
        }

        /// <summary>월드 프리팹 Handle 을 드는 에셋 소유자.</summary>
        private sealed class WorldPrefabLibrary : AssetOwner
        {
            public WorldPrefabLibrary() : base("WorldPrefabs")
            {
            }
        }
    }
}
