using DemoLA.Network;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor
{
    /// <summary>
    /// Netcode for GameObjects 구성 (레이드 씬 셋업이 부른다).
    ///  - 프리팹: NetPartyMember (플레이어 · AI 아군), NetBoss — 몸/전투 컴포넌트는 스폰 후 코드가 붙인다
    ///  - 씬: NetworkManager + UnityTransport (Relay 는 Multiplayer Services 세션이 설정), NetworkRaidDirector
    /// </summary>
    public static class NetworkSetup
    {
        public const string PrefabFolder = "Assets/_Project/Prefabs/Net";
        public const string MemberPrefabPath = PrefabFolder + "/NetPartyMember.prefab";
        public const string BossPrefabPath = PrefabFolder + "/NetBoss.prefab";
        public const string PrefabListPath = PrefabFolder + "/DemoLANetworkPrefabs.asset";

        public static void CreatePrefabs(out GameObject OutMember, out GameObject OutBoss)
        {
            DemoLAProjectSetup.ensureFolder(PrefabFolder);
            OutMember = createPrefab(MemberPrefabPath, "NetPartyMember", typeof(NetworkPartyMember));
            OutBoss = createPrefab(BossPrefabPath, "NetBoss", typeof(NetworkBossProxy));

            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, PrefabListPath);
            }

            if (list.Contains(OutBoss) == false)
            {
                list.Add(new NetworkPrefab { Prefab = OutBoss });
            }

            if (list.Contains(OutMember) == false)
            {
                list.Add(new NetworkPrefab { Prefab = OutMember });
            }

            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
        }

        /// <summary>현재 열린 씬에 NetworkManager 와 레이드 디렉터를 놓는다.</summary>
        public static void AddToScene()
        {
            GameObject member;
            GameObject boss;
            CreatePrefabs(out member, out boss);
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListPath);

            var managerGo = new GameObject("NetworkManager");
            var manager = managerGo.AddComponent<NetworkManager>();
            var transport = managerGo.AddComponent<UnityTransport>();
            manager.NetworkConfig = new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = member;
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
            manager.NetworkConfig.EnableSceneManagement = true;

            var directorGo = new GameObject("NetworkRaidDirector");
            directorGo.AddComponent<NetworkObject>();
            var director = directorGo.AddComponent<NetworkRaidDirector>();
            director.PartyMemberPrefab = member;
            director.BossPrefab = boss;
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(director);
        }

        private static GameObject createPrefab(string InPath, string InName, System.Type InBehaviour)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(InPath);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(InName);
            go.AddComponent<NetworkObject>();
            var networkTransform = go.AddComponent<NetworkTransform>();
            // 서버 권한 위치 (서버의 NavMeshAgent 가 움직인다).
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            go.AddComponent(InBehaviour);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, InPath);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
