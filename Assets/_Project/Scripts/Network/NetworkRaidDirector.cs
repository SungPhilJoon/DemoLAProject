using System.Collections.Generic;
using DemoLA.Boss;
using DemoLA.Boss.Telegraph;
using DemoLA.Character;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.World;
using Framework;
using Unity.Netcode;
using UnityEngine;

namespace DemoLA.Network
{
    /// <summary>
    /// 네트워크 레이드 진행 (씬에 놓인 NetworkObject).
    /// 서버(호스트): "시작" 시 플레이어가 고르지 않은 직업을 AI 아군으로 채우고(최대 4인), 보스를 스폰한다.
    /// 서버가 쓰는 장판 · 안내 문구를 클라이언트에 RPC 로 복제한다.
    /// 모든 피어: 로컬 플레이어/보스가 준비되면 GameBootstrap 의 HUD · 카메라 · 입력을 네트워크 쪽으로 다시 연결한다.
    /// </summary>
    public class NetworkRaidDirector : NetworkBehaviour
    {
        [SerializeField] private GameObject partyMemberPrefab;
        [SerializeField] private GameObject bossPrefab;

        private readonly List<NetworkPartyMember> members = new List<NetworkPartyMember>();
        private NetworkBossProxy boss;
        private bool raidStarted;

        public static NetworkRaidDirector Instance { get; private set; }

        public GameObject PartyMemberPrefab
        {
            get { return partyMemberPrefab; }
            set { partyMemberPrefab = value; }
        }

        public GameObject BossPrefab
        {
            get { return bossPrefab; }
            set { bossPrefab = value; }
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            NetworkPartyMember.OnCharacterReady += handleMemberReady;
            NetworkBossProxy.OnBossReady += handleBossReady;
            var bootstrap = GameBootstrap.Current;
            if (bootstrap != null)
            {
                bootstrap.NetworkOwnsRaid = true;
                bootstrap.ClearRaid();
            }

            if (IsServer == true)
            {
                BossPatternAbility.TelegraphStarted += serverTelegraph;
                EventBus<RaidMessageEvent>.Subscribe(serverMessage);
            }
        }

        public override void OnNetworkDespawn()
        {
            NetworkPartyMember.OnCharacterReady -= handleMemberReady;
            NetworkBossProxy.OnBossReady -= handleBossReady;
            BossPatternAbility.TelegraphStarted -= serverTelegraph;
            EventBus<RaidMessageEvent>.Unsubscribe(serverMessage);
            if (Instance == this)
            {
                Instance = null;
            }

            var bootstrap = GameBootstrap.Current;
            if (bootstrap != null)
            {
                bootstrap.NetworkOwnsRaid = false;
            }
        }

        public void ServerOnLoadoutChanged()
        {
        }

        /// <summary>서버: AI 채우기 + 보스 스폰. 호스트의 로비 "시작" 버튼이 부른다.</summary>
        public void ServerStartRaid(string InBossId)
        {
            if (IsServer == false || raidStarted == true)
            {
                return;
            }

            raidStarted = true;
            var bootstrap = GameBootstrap.Current;
            var taken = new HashSet<ECharacterClass>();
            int players = 0;
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null)
                {
                    continue;
                }

                var member = client.PlayerObject.GetComponent<NetworkPartyMember>();
                if (member != null && member.ClassId.Value >= 0)
                {
                    taken.Add((ECharacterClass)member.ClassId.Value);
                }

                players += 1;
            }

            // 플레이어가 고르지 않은 직업 = AI (도화가 우선 → 딜러 3 + 서포터 1).
            var order = new[] { ECharacterClass.Artist, ECharacterClass.Slayer, ECharacterClass.Infighter, ECharacterClass.Summoner };
            int partySize = players;
            for (int i = 0; i < order.Length && partySize < RelaySessionService.MaxPlayers; ++i)
            {
                if (taken.Contains(order[i]) == true)
                {
                    continue;
                }

                var spawn = bootstrap != null ? bootstrap.PlayerSpawn : Vector3.back * 10.0f;
                var go = Instantiate(partyMemberPrefab, spawn + new Vector3((partySize - 2) * 2.5f, 0.0f, -1.5f), Quaternion.identity);
                var network = go.GetComponent<NetworkObject>();
                network.Spawn(true);
                go.GetComponent<NetworkPartyMember>().ServerInitAI(order[i]);
                taken.Add(order[i]);
                partySize += 1;
            }

            var bossPosition = bootstrap != null ? bootstrap.BossSpawn : Vector3.forward * 6.0f;
            var bossGo = Instantiate(bossPrefab, bossPosition, Quaternion.LookRotation(Vector3.back));
            bossGo.GetComponent<NetworkObject>().Spawn(true);
            bossGo.GetComponent<NetworkBossProxy>().ServerInit(InBossId, partySize);
            Raid.Message($"네트워크 레이드 시작 · {players}명 + AI {partySize - players}명", 3.0f, false);
        }

        // ------------------------------------------------------------------ replication (server → clients)

        private void serverTelegraph(HitArea InArea, Vector3 InOrigin, Vector3 InForward, float InDuration)
        {
            // HitArea 는 Netcode 를 모르는 런타임 어셈블리 타입이라 필드를 풀어 보낸다.
            TelegraphRpc((int)InArea.Shape, new Vector4(InArea.Radius, InArea.InnerRadius, InArea.Angle, InArea.ForwardOffset), new Vector2(InArea.Width, InArea.Length), InOrigin, InForward, InDuration);
        }

        [Rpc(SendTo.NotServer)]
        private void TelegraphRpc(int InShape, Vector4 InRadial, Vector2 InRect, Vector3 InOrigin, Vector3 InForward, float InDuration)
        {
            var area = new HitArea();
            area.Shape = (EHitShape)InShape;
            area.Radius = InRadial.x;
            area.InnerRadius = InRadial.y;
            area.Angle = InRadial.z;
            area.ForwardOffset = InRadial.w;
            area.Width = InRect.x;
            area.Length = InRect.y;
            var view = TelegraphView.Spawn(area, InOrigin, InForward);
            view.gameObject.AddComponent<TelegraphTimer>().Begin(view, InDuration);
        }

        private void serverMessage(RaidMessageEvent InEvent)
        {
            MessageRpc(InEvent.Message ?? string.Empty, InEvent.Duration, InEvent.IsWarning);
        }

        [Rpc(SendTo.NotServer)]
        private void MessageRpc(string InMessage, float InDuration, bool InWarning)
        {
            Raid.Message(InMessage, InDuration, InWarning);
        }

        // ------------------------------------------------------------------ local binding

        private void handleMemberReady(NetworkPartyMember InMember)
        {
            if (members.Contains(InMember) == false)
            {
                members.Add(InMember);
            }

            rebind();
        }

        private void handleBossReady(NetworkBossProxy InBoss)
        {
            boss = InBoss;
            rebind();
        }

        private void rebind()
        {
            var bootstrap = GameBootstrap.Current;
            if (bootstrap == null)
            {
                return;
            }

            NetworkPartyMember local = null;
            var party = new List<PartyCharacter>();
            for (int i = 0; i < members.Count; ++i)
            {
                if (members[i] == null || members[i].Character == null)
                {
                    continue;
                }

                party.Add(members[i].Character);
                if (members[i].IsOwner == true && members[i].IsAI.Value == false)
                {
                    local = members[i];
                }
            }

            if (local == null)
            {
                return;
            }

            bootstrap.BindRaid(local.Character, boss != null ? boss.Boss : null, party, IsServer == true ? null : new NetworkPartyCommands(local));
        }
    }

    /// <summary>클라이언트 장판: 서버가 알려 준 전조 시간 동안 진행도를 채우고 사라진다 (판정은 서버).</summary>
    public class TelegraphTimer : MonoBehaviour
    {
        private TelegraphView view;
        private float duration;
        private float elapsed;

        public void Begin(TelegraphView InView, float InDuration)
        {
            view = InView;
            duration = Mathf.Max(0.05f, InDuration);
            elapsed = 0.0f;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (view == null)
            {
                Destroy(this);
                return;
            }

            view.SetProgress(elapsed / duration);
            if (elapsed >= duration)
            {
                view.Despawn();
                Destroy(this);
            }
        }
    }
}
