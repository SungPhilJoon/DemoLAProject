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
    /// 서버의 장판(20Hz 스냅샷) · 안내 문구 · 구조물 파괴 · 바닥 붕괴 · 바닥 구슬을 클라이언트에 복제한다.
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

        /// <summary>클라이언트가 서버의 장판 RPC 를 받았을 때 (원점, 전조 시간) — 동기화 측정(NetSyncProbe)용.</summary>
        public static event System.Action<Vector3, float> ClientTelegraph;

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
            // 클라이언트는 게임 규칙(낙사 · 구슬 · 에스더 게이지 등)을 진행하지 않는다 — 서버 결과만 반영.
            NetAuthority.IsReplica = IsServer == false;
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
                EventBus<RaidMessageEvent>.Subscribe(serverMessage);
                ArenaStructure.Broken += serverStructureBroken;
                BossPatternAbility.ImpactPlayed += serverImpact;
                BossPatternAbility.CinematicPlayed += serverCinematic;
                BossPatternAbility.LeapStarted += serverLeap;
                ArenaFloor.OnCollapsed += serverFloorCollapsed;
                ArenaFloor.OnCollapsedAllExceptCenter += serverFloorCollapsedCenter;
                ArenaFloor.OnRestored += serverFloorRestored;
            }
        }

        public override void OnNetworkDespawn()
        {
            NetworkPartyMember.OnCharacterReady -= handleMemberReady;
            NetworkBossProxy.OnBossReady -= handleBossReady;
            clearTelegraphReplicas();
            EventBus<RaidMessageEvent>.Unsubscribe(serverMessage);
            ArenaStructure.Broken -= serverStructureBroken;
            BossPatternAbility.ImpactPlayed -= serverImpact;
            BossPatternAbility.CinematicPlayed -= serverCinematic;
            BossPatternAbility.LeapStarted -= serverLeap;
            ArenaFloor.OnCollapsed -= serverFloorCollapsed;
            ArenaFloor.OnCollapsedAllExceptCenter -= serverFloorCollapsedCenter;
            ArenaFloor.OnRestored -= serverFloorRestored;
            clearPickupReplicas();
            if (Instance == this)
            {
                Instance = null;
            }

            NetAuthority.IsReplica = false;

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

        private void serverMessage(RaidMessageEvent InEvent)
        {
            MessageRpc(InEvent.Message ?? string.Empty, InEvent.Duration, InEvent.IsWarning);
        }

        [Rpc(SendTo.NotServer)]
        private void MessageRpc(string InMessage, float InDuration, bool InWarning)
        {
            Raid.Message(InMessage, InDuration, InWarning);
        }

        // ------------------------------------------------------------------ world state (server -> clients)
        // 구조물 · 바닥 · 구슬은 서버의 기믹 · 패턴이 바꾼다 → 클라이언트에 같은 결과를 적용한다.

        private void serverStructureBroken(ArenaStructure InStructure, CombatActor InBreaker)
        {
            StructureBrokenRpc(InStructure.name, InBreaker != null);
        }

        [Rpc(SendTo.NotServer)]
        private void StructureBrokenRpc(string InName, bool InByActor)
        {
            var structures = ArenaStructure.Structures;
            for (int i = 0; i < structures.Count; ++i)
            {
                if (structures[i] != null && structures[i].name == InName)
                {
                    // 클라이언트는 연출만 (구슬은 서버가 만들고 PickupsRpc 로 보인다).
                    structures[i].Break(null);
                    if (InByActor == true)
                    {
                        DemoLA.Player.QuarterViewCamera.Shake(structures[i].CrashShakeAmplitude, structures[i].CrashShakeTime);
                    }

                    return;
                }
            }
        }

        // 착지 충격파 · 도약 시네마틱 (연출만 — 판정은 서버).
        private void serverImpact(Vector3 InPosition, float InRadius, float InStrength)
        {
            ImpactRpc(InPosition, InRadius, InStrength);
        }

        [Rpc(SendTo.NotServer)]
        private void ImpactRpc(Vector3 InPosition, float InRadius, float InStrength)
        {
            BossImpactFx.Play(InPosition, InRadius, InStrength);
        }

        private void serverLeap(float InDuration, float InHeight)
        {
            LeapRpc(InDuration, InHeight);
        }

        [Rpc(SendTo.NotServer)]
        private void LeapRpc(float InDuration, float InHeight)
        {
            var boss = FindAnyObjectByType<BossActor>();
            if (boss != null && boss.Motor != null)
            {
                boss.Motor.PlayVisualArc(InDuration, InHeight);
            }
        }

        private void serverCinematic(float InDuration)
        {
            CinematicRpc(InDuration);
        }

        [Rpc(SendTo.NotServer)]
        private void CinematicRpc(float InDuration)
        {
            var boss = FindAnyObjectByType<BossActor>();
            if (boss != null)
            {
                DemoLA.Player.BossCinematicCamera.Play(boss.transform, InDuration);
            }
        }

        private void serverFloorCollapsed(ETileGroup InGroup, bool InKeepSurvivors)
        {
            FloorCollapsedRpc((int)InGroup, InKeepSurvivors);
        }

        private void serverFloorCollapsedCenter()
        {
            FloorCollapsedCenterRpc();
        }

        private void serverFloorRestored()
        {
            FloorRestoredRpc();
        }

        [Rpc(SendTo.NotServer)]
        private void FloorCollapsedRpc(int InGroup, bool InKeepSurvivors)
        {
            if (ArenaFloor.Current != null)
            {
                ArenaFloor.Current.Collapse((ETileGroup)InGroup, InKeepSurvivors);
            }
        }

        [Rpc(SendTo.NotServer)]
        private void FloorCollapsedCenterRpc()
        {
            if (ArenaFloor.Current != null)
            {
                ArenaFloor.Current.CollapseAllExceptCenter();
            }
        }

        [Rpc(SendTo.NotServer)]
        private void FloorRestoredRpc()
        {
            if (ArenaFloor.Current != null)
            {
                ArenaFloor.Current.RestoreAll();
            }
        }

        // 바닥 구슬: 서버의 구슬 목록(번호 · 위치 · 색 · 크기)을 10Hz 로 보내고 클라이언트는 보이기만 하는 복제본을 맞춘다.
        private readonly Dictionary<int, RaidPickup> pickupReplicas = new Dictionary<int, RaidPickup>();
        private readonly List<int> pickupRemove = new List<int>();
        private float pickupTimer;
        private int lastPickupCount;

        private void Update()
        {
            if (IsSpawned == false)
            {
                return;
            }

            if (IsServer == false)
            {
                smoothTelegraphReplicas();
                return;
            }

            serverTelegraphs();
            pickupTimer -= Time.deltaTime;
            if (pickupTimer > 0.0f)
            {
                return;
            }

            pickupTimer = 0.1f;
            var active = RaidPickup.Active;
            if (active.Count == 0 && lastPickupCount == 0)
            {
                return;
            }

            lastPickupCount = active.Count;
            var ids = new int[active.Count];
            var positions = new Vector3[active.Count];
            var colors = new Color[active.Count];
            var scales = new float[active.Count];
            for (int i = 0; i < active.Count; ++i)
            {
                ids[i] = active[i].NetId;
                positions[i] = active[i].transform.position;
                colors[i] = active[i].Color;
                scales[i] = active[i].transform.localScale.x;
            }

            PickupsRpc(ids, positions, colors, scales);
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void PickupsRpc(int[] InIds, Vector3[] InPositions, Color[] InColors, float[] InScales)
        {
            pickupRemove.Clear();
            foreach (var pair in pickupReplicas)
            {
                if (System.Array.IndexOf(InIds, pair.Key) < 0)
                {
                    pickupRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < pickupRemove.Count; ++i)
            {
                var gone = pickupReplicas[pickupRemove[i]];
                if (gone != null)
                {
                    gone.Consume();
                }

                pickupReplicas.Remove(pickupRemove[i]);
            }

            for (int i = 0; i < InIds.Length; ++i)
            {
                RaidPickup replica;
                if (pickupReplicas.TryGetValue(InIds[i], out replica) == false || replica == null)
                {
                    replica = RaidPickup.Create("PickupReplica", InPositions[i], InColors[i], InScales[i]);
                    replica.IsVisualOnly = true;
                    replica.SpinSpeed = 90.0f;
                    pickupReplicas[InIds[i]] = replica;
                }

                replica.transform.position = InPositions[i];
            }
        }

        private void clearPickupReplicas()
        {
            foreach (var pair in pickupReplicas)
            {
                if (pair.Value != null)
                {
                    pair.Value.Consume();
                }
            }

            pickupReplicas.Clear();
        }

        // ------------------------------------------------------------------ telegraphs (server -> clients)
        // 장판은 생성 때 한 번만 보내면 추적 장판(대상 따라가기) · 돌진 길이 자르기 · 추적 표시(파란 원)가 클라이언트에서 어긋난다.
        // → 서버에 보이는 장판 전체(번호 · 모양 · 위치 · 방향 · 진행도 · 길이 비율)를 20Hz 로 보내고, 클라이언트는 같은 장판을 맞춘다.
        //   서버 목록에서 빠진 장판은 판정 순간이므로 클라이언트도 번쩍이고 사라진다.

        private readonly Dictionary<int, TelegraphView> telegraphReplicas = new Dictionary<int, TelegraphView>();
        private readonly Dictionary<int, (Vector3 Origin, Vector3 Forward)> telegraphTargets = new Dictionary<int, (Vector3, Vector3)>();
        private readonly List<int> telegraphRemove = new List<int>();
        private readonly List<TelegraphView> telegraphBuffer = new List<TelegraphView>();
        private float telegraphTimer;
        private int lastTelegraphCount;

        private void serverTelegraphs()
        {
            telegraphTimer -= Time.deltaTime;
            if (telegraphTimer > 0.0f)
            {
                return;
            }

            telegraphTimer = 0.05f;
            telegraphBuffer.Clear();
            var views = TelegraphView.ActiveViews;
            for (int i = 0; i < views.Count; ++i)
            {
                if (views[i] != null && views[i].gameObject.activeSelf == true)
                {
                    telegraphBuffer.Add(views[i]);
                }
            }

            if (telegraphBuffer.Count == 0 && lastTelegraphCount == 0)
            {
                return;
            }

            lastTelegraphCount = telegraphBuffer.Count;
            int count = telegraphBuffer.Count;
            var ids = new int[count];
            var shapes = new int[count];
            var radial = new Vector4[count];
            var rect = new Vector3[count];
            var origins = new Vector3[count];
            var forwards = new Vector3[count];
            var progress = new float[count];
            var safe = new bool[count];
            for (int i = 0; i < count; ++i)
            {
                var view = telegraphBuffer[i];
                var area = view.Area;
                ids[i] = view.NetId;
                shapes[i] = (int)area.Shape;
                radial[i] = new Vector4(area.Radius, area.InnerRadius, area.Angle, area.ForwardOffset);
                rect[i] = new Vector3(area.Width, area.Length, view.LengthScale);
                origins[i] = view.Origin;
                forwards[i] = view.ForwardDirection;
                progress[i] = view.Progress;
                safe[i] = view.IsSafeZone;
            }

            TelegraphsRpc(ids, shapes, radial, rect, origins, forwards, progress, safe);
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void TelegraphsRpc(int[] InIds, int[] InShapes, Vector4[] InRadial, Vector3[] InRect, Vector3[] InOrigins, Vector3[] InForwards, float[] InProgress, bool[] InSafe)
        {
            telegraphRemove.Clear();
            foreach (var pair in telegraphReplicas)
            {
                if (System.Array.IndexOf(InIds, pair.Key) < 0)
                {
                    telegraphRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < telegraphRemove.Count; ++i)
            {
                var gone = telegraphReplicas[telegraphRemove[i]];
                if (gone != null && gone.gameObject.activeSelf == true)
                {
                    // 서버 장판이 사라짐 = 판정 순간 (추적 표시는 그냥 사라진다).
                    if (gone.IsSafeZone == true)
                    {
                        gone.Despawn();
                    }
                    else
                    {
                        gone.FlashAndDespawn();
                    }
                }

                telegraphReplicas.Remove(telegraphRemove[i]);
                telegraphTargets.Remove(telegraphRemove[i]);
            }

            for (int i = 0; i < InIds.Length; ++i)
            {
                TelegraphView view;
                if (telegraphReplicas.TryGetValue(InIds[i], out view) == false || view == null)
                {
                    var area = new HitArea();
                    area.Shape = (EHitShape)InShapes[i];
                    area.Radius = InRadial[i].x;
                    area.InnerRadius = InRadial[i].y;
                    area.Angle = InRadial[i].z;
                    area.ForwardOffset = InRadial[i].w;
                    area.Width = InRect[i].x;
                    area.Length = InRect[i].y;
                    view = TelegraphView.Spawn(area, InOrigins[i], InForwards[i], InSafe[i]);
                    telegraphReplicas[InIds[i]] = view;
                    ClientTelegraph?.Invoke(InOrigins[i], 0.0f);
                }

                telegraphTargets[InIds[i]] = (InOrigins[i], InForwards[i]);
                view.SetLengthScale(InRect[i].z);
                view.SetProgress(InProgress[i]);
            }
        }

        /// <summary>20Hz 로 받은 위치 사이를 부드럽게 (추적 장판이 뚝뚝 끊기지 않게).</summary>
        private void smoothTelegraphReplicas()
        {
            float k = 1.0f - Mathf.Exp(-25.0f * Time.deltaTime);
            foreach (var pair in telegraphReplicas)
            {
                (Vector3 Origin, Vector3 Forward) target;
                if (pair.Value == null || telegraphTargets.TryGetValue(pair.Key, out target) == false)
                {
                    continue;
                }

                var origin = Vector3.Lerp(pair.Value.Origin, target.Origin, k);
                var forward = Vector3.Slerp(pair.Value.ForwardDirection, target.Forward, k);
                pair.Value.Place(origin, forward);
            }
        }

        private void clearTelegraphReplicas()
        {
            foreach (var pair in telegraphReplicas)
            {
                if (pair.Value != null && pair.Value.gameObject.activeSelf == true)
                {
                    pair.Value.Despawn();
                }
            }

            telegraphReplicas.Clear();
            telegraphTargets.Clear();
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
}
