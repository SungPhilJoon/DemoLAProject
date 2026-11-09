using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DemoLA.AI;
using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Boss.Telegraph;
using DemoLA.Character;
using DemoLA.Combat;
using DemoLA.Content;
using DemoLA.Cue;
using DemoLA.Data;
using DemoLA.Effects;
using DemoLA.Player;
using DemoLA.Save;
using DemoLA.UI;
using DemoLA.World;
using Framework;
using Framework.StateTree;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;

namespace DemoLA.Core
{
    /// <summary>
    /// 씬 진입점. 데이터 로드(직접 참조 → Addressables → 코드 기본값) → 저장 로드 → 경기장 · 대기실 생성 → HUD/빌드 창.
    /// 흐름: 레이드 입장 대기실(관문 선택 · 설정 · 전원 준비) → 카운트다운 → 레이드. F5 = 대기실로 돌아가기.
    /// 파티는 플레이어 1 + 아군 AI 3 (최대 4인, NetCode 전).
    /// </summary>
    public class GameBootstrap : BaseBehaviour
    {
        [SerializeField] private GameDatabase database;
        [SerializeField] private bool loadFromAddressables = true;
        [SerializeField] private StateTreeAsset partyTree;
        [SerializeField] private UnityEngine.InputSystem.InputActionAsset controls;
        [SerializeField] private ArenaFloor arena;
        [SerializeField] private Vector3 playerSpawn = new Vector3(0.0f, 0.0f, -10.0f);
        [SerializeField] private Vector3 bossSpawn = new Vector3(0.0f, 0.0f, 6.0f);
        [SerializeField] private int partySize = 4;

        private GameSession session;
        private PartyCharacter player;
        private BossActor boss;
        private readonly List<PartyCharacter> party = new List<PartyCharacter>();
        private HudView hud;
        private BuildWindow window;
        private PlayerController controller;
        private QuarterViewCamera cameraRig;
        private MobileControls mobileControls;
        private DemoLA.Tools.DpsMeterView dpsView;
        private bool ready;
        private RaidLobby lobby;
        private LobbyView lobbyView;
        private bool inLobby;

        /// <summary>레이드 입장 대기실에 있는가.</summary>
        public bool InLobby
        {
            get { return inLobby; }
        }

        public static GameBootstrap Current { get; private set; }

        /// <summary>true 면 네트워크 세션이 레이드를 맡는다 (F5/F6 오프라인 재시작 비활성).</summary>
        public bool NetworkOwnsRaid { get; set; }

        public GameSession Session
        {
            get { return session; }
        }

        public StateTreeAsset PartyTree
        {
            get { return partyTree; }
        }

        public Vector3 PlayerSpawn
        {
            get { return playerSpawn; }
        }

        public Vector3 BossSpawn
        {
            get { return bossSpawn; }
        }

        public PartyCharacter Player
        {
            get { return player; }
        }

        public BossActor Boss
        {
            get { return boss; }
        }

        private void Start()
        {
            Current = this;
            startAsync().Forget();
        }

        private async UniTaskVoid startAsync()
        {
            GameTags.RegisterAll();
            GameEffects.Initialize();
            GameplayCueHandlers.RegisterDefaults();

            var db = database;
            if (db == null && loadFromAddressables == true)
            {
                db = await loadAddressable();
            }

            if (db == null)
            {
                db = createFallbackDatabase();
            }

            if (db == null)
            {
                Log.Error("[Bootstrap] GameDatabase 를 찾지 못했습니다. DemoLA/Setup/0. 전체 셋업 을 실행하고 Addressables 를 빌드하세요.");
                return;
            }

            // 도메인 리로드를 끈 상태로 다시 플레이하면 이전 세션이 남아 있다 → 그 세션을 그대로 쓴다.
            session = Managers.Get<GameSession>();
            if (session == null)
            {
                session = Managers.Regist(new GameSession(db, new LocalJsonSaveService()));
            }

            StatFormula.Settings = session.Database.StatFormula;
            if (session.Database.Icons != null)
            {
                IconFactory.Library = session.Database.Icons;
            }

            EstherSystem.Configure(session.Database.Esthers, session.Database.EstherHpLinesPerGauge);
            if (session.Database.CounterGlowMaterial != null)
            {
                CounterGlowView.SharedMaterial = session.Database.CounterGlowMaterial;
            }

            await session.LoadAsync();

            if (arena == null)
            {
                arena = FindAnyObjectByType<ArenaFloor>();
            }

            if (arena == null)
            {
                arena = ArenaBuilder.Build(null);
            }

            if (arena.Surface != null && arena.Surface.navMeshData == null)
            {
                arena.Surface.BuildNavMesh();
            }

            if (partyTree == null)
            {
                partyTree = DefaultTrees.CreatePartyAI();
            }

            ensureLight();
            hud = new GameObject("[HUD]").AddComponent<HudView>();
            window = new GameObject("[BuildWindow]").AddComponent<BuildWindow>();
            cameraRig = new GameObject("[Camera]").AddComponent<QuarterViewCamera>();
            controller = new GameObject("[PlayerController]").AddComponent<PlayerController>();
            controller.Setup(controls);
            mobileControls = new GameObject("[MobileControls]").AddComponent<MobileControls>();
            mobileControls.Build();
            dpsView = new GameObject("[DpsMeter]").AddComponent<DemoLA.Tools.DpsMeterView>();
            dpsView.Build();
            PlayerController.IsInputBlocked = () => window != null && window.IsOpen == true;
            PlayerController.OnBuildWindowToggle += toggleWindow;

            LobbyArea.Ensure();
            lobby = gameObject.AddComponent<RaidLobby>();
            lobby.OnEnter += enterRaid;
            lobbyView = new GameObject("[Lobby]").AddComponent<LobbyView>();
            lobbyView.Build(lobby, gateLabel, stepGate, toggleWindow);
            BuildWindow.OnApplied += handleBuildApplied;
            // HUD 의 스킬 · 배틀 아이템 칸에 끌어 놓은 것도 빌드에 반영한다 (창과 같은 경로).
            hud.SkillSlotSetter = (slot, id) => window.SetSkillSlot(slot, id);
            hud.ItemSlotSetter = (slot, index) => window.SetItemSlot(slot, index);

            EnterLobby();
            ready = true;
        }

        /// <summary>
        /// 에디터 전용 대체 경로: 데이터 에셋이 없으면 CSV 를 직접 읽어 메모리 DB 를 만든다.
        /// 플레이어 빌드에서는 Addressables(로컬 번들)만 쓴다 — Resources 는 쓰지 않는다.
        /// </summary>
        private static GameDatabase createFallbackDatabase()
        {
#if UNITY_EDITOR
            var tables = UnityEditor.AssetDatabase.LoadAssetAtPath<DemoLATables>(TablePaths.TablesRootAsset);
            if (tables == null)
            {
                string errors;
                tables = DemoLATables.FromCsv(InType => TablePaths.ReadCsv(InType), out errors);
                if (string.IsNullOrEmpty(errors) == false)
                {
                    Log.Warning($"[Bootstrap] CSV 경고: {errors}");
                }
            }

            Log.Message("[Bootstrap] GameDatabase 에셋이 없어 CSV 로 기본 콘텐츠를 만든다 (에디터 전용).");
            return DefaultContent.CreateDatabase(tables);
#else
            return null;
#endif
        }

        private async UniTask<GameDatabase> loadAddressable()
        {
            try
            {
                var locations = await Addressables.LoadResourceLocationsAsync(GameDatabase.Address, typeof(GameDatabase)).Task;
                if (locations == null || locations.Count == 0)
                {
                    return null;
                }

                var loader = Managers.Get<AssetLoader>();
                if (loader == null)
                {
                    loader = Managers.Regist(new AssetLoader());
                }

                return await loader.LoadAsync<GameDatabase>(GameDatabase.Address, this);
            }
            catch (System.Exception e)
            {
                Log.Warning($"[Bootstrap] Addressables 로드 실패 → 기본 콘텐츠 사용: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 레이드 입장 대기실로 간다: 보스 없이 파티만 대기실에 세우고 준비 확인을 시작한다.
        /// 관문 · 직업 · 빌드는 여기서 바꾼다. 전원 준비 → 카운트다운 → enterRaid.
        /// </summary>
        public void EnterLobby()
        {
            clearRaid();
            GameRandom.SetSeed(System.Environment.TickCount);
            spawnParty(true);
            DemoLA.Tools.DpsMeter.Reset();
            cameraRig.SetTarget(player.transform);
            hud.Build(player, null, party);
            window.Build(session, player, changeClass);
            inLobby = true;
            lobby.Begin(party, player);
            lobbyView.SetVisible(true);
            Raid.Message("레이드 입장 대기실 — 설정을 마치고 준비 완료를 누르세요", 3.0f, false);
        }

        private void enterRaid()
        {
            if (window.IsOpen == true)
            {
                window.CloseAll();
            }

            SpawnRaid();
        }

        /// <summary>대기실 · 레이드 공통: 플레이어 + AI 파티를 만든다. 대기실에서는 AI 두뇌 없이 입구를 보고 선다.</summary>
        private void spawnParty(bool InLobby)
        {
            var db = session.Database;
            var save = session.Save;
            var playerClass = db.GetClass(save.PlayerClass) ?? db.Classes[0];
            var classes = new List<CharacterClassDefinition> { playerClass };
            if (save.SpawnPartyAI == true)
            {
                var order = allyOrder(playerClass.Class);
                for (int i = 0; i < order.Count && classes.Count < partySize; ++i)
                {
                    var cls = db.GetClass(order[i]);
                    if (cls != null)
                    {
                        classes.Add(cls);
                    }
                }
            }

            for (int i = 0; i < classes.Count; ++i)
            {
                var cls = classes[i];
                Vector3 position;
                if (InLobby == true)
                {
                    position = LobbyArea.SpawnPoint(i, classes.Count);
                }
                else
                {
                    position = i == 0 ? playerSpawn : playerSpawn + new Vector3((i - 2) * 2.5f, 0.0f, -1.5f);
                }

                bool isPlayer = i == 0;
                var character = RaidSpawner.SpawnCharacter(cls, session.GetBuild(cls.Class), db, position,
                    isPlayer || InLobby ? null : partyTree, isPlayer ? $"플레이어 ({cls.DisplayName})" : $"AI {cls.DisplayName}");
                if (InLobby == true)
                {
                    character.transform.rotation = Quaternion.LookRotation(LobbyArea.GateDirection, Vector3.up);
                }

                party.Add(character);
                if (isPlayer == true)
                {
                    player = character;
                    controller.Bind(player);
                }
            }
        }

        /// <summary>현재 저장 설정(직업/관문)으로 레이드를 처음부터 다시 만든다 (대기실 준비 확인 없이 — 입장 · 네트워크용).</summary>
        public void SpawnRaid()
        {
            clearRaid();
            inLobby = false;
            if (lobby != null)
            {
                lobby.End();
            }

            if (lobbyView != null)
            {
                lobbyView.SetVisible(false);
            }

            GameRandom.SetSeed(System.Environment.TickCount);
            var db = session.Database;
            var save = session.Save;
            spawnParty(false);

            var bossDef = db.GetBoss(save.SelectedBoss) ?? (db.Bosses.Count > 0 ? db.Bosses[0] : null);
            if (bossDef != null)
            {
                boss = RaidSpawner.SpawnBoss(bossDef, bossSpawn, Quaternion.LookRotation(playerSpawn - bossSpawn), party.Count);
            }

            DemoLA.Tools.DpsMeter.Reset();
            cameraRig.SetTarget(player.transform);
            hud.Build(player, boss, party);
            window.Build(session, player, changeClass);
            Raid.Message(bossDef != null ? $"{bossDef.DisplayName} 레이드 시작" : "보스 데이터 없음 (Resources/Data CSV 확인)", 3.0f, false);
        }

        private void Update()
        {
            if (ready == false)
            {
                return;
            }

            Managers.Update(Time.deltaTime);
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (NetworkOwnsRaid == true)
            {
                return;
            }

            if (keyboard.f5Key.wasPressedThisFrame == true && inLobby == false)
            {
                // 레이드 중 / 끝난 뒤: 대기실로 돌아간다 (다시 들어가려면 전원 준비).
                EnterLobby();
            }
            else if (keyboard.escapeKey.wasPressedThisFrame == true && window != null && window.IsOpen == true)
            {
                window.CloseAll();
            }
        }

        protected override void OnUnityDestroy()
        {
            BuildWindow.OnApplied -= handleBuildApplied;
            PlayerController.OnBuildWindowToggle -= toggleWindow;
            PlayerController.IsInputBlocked = null;
            CombatRegistry.Clear();
        }

        private void toggleWindow()
        {
            if (window != null)
            {
                window.Toggle();
            }
        }

        private void changeClass(ECharacterClass InClass)
        {
            session.Save.PlayerClass = InClass;
            session.SaveAsync().Forget();
            // 직업은 대기실에서만 바꾼다 (레이드 중이면 대기실로 돌아간다).
            EnterLobby();
        }

        private string gateLabel()
        {
            var bosses = session.Database.Bosses;
            int index = bosses.FindIndex(b => b.BossId == session.Save.SelectedBoss);
            index = Mathf.Max(0, index);
            return bosses.Count > 0 ? $"{index + 1}관문 · {bosses[index].DisplayName}" : "-";
        }

        private void stepGate(int InStep)
        {
            var bosses = session.Database.Bosses;
            if (inLobby == false || bosses.Count == 0)
            {
                return;
            }

            int index = Mathf.Max(0, bosses.FindIndex(b => b.BossId == session.Save.SelectedBoss));
            session.Save.SelectedBoss = bosses[UIKit.Wrap(index + InStep, bosses.Count)].BossId;
            session.SaveAsync().Forget();
            lobby.ResetAll($"관문 변경: {gateLabel()} — 모든 파티원의 준비가 해제되었습니다");
        }

        /// <summary>대기실에서 설정을 적용하면 내 준비가 풀린다 (바뀐 설정으로 다시 준비).</summary>
        private void handleBuildApplied()
        {
            if (inLobby == true && lobby.IsPlayerReady == true)
            {
                lobby.SetPlayerReady(false);
                Raid.Message("설정이 바뀌어 준비가 해제되었습니다", 2.0f, false);
            }
        }

        private static List<ECharacterClass> allyOrder(ECharacterClass InPlayer)
        {
            // 서포터(도화가)를 먼저 넣어 4인 파티 = 딜러 3 + 서포터 1 구성을 만든다.
            var order = new List<ECharacterClass> { ECharacterClass.Artist, ECharacterClass.Slayer, ECharacterClass.Infighter, ECharacterClass.Summoner };
            order.Remove(InPlayer);
            return order;
        }

        /// <summary>오프라인 레이드 · 대기실을 걷어 낸다 (네트워크 세션이 시작될 때).</summary>
        public void ClearRaid()
        {
            clearRaid();
            inLobby = false;
            if (lobby != null)
            {
                lobby.End();
            }

            if (lobbyView != null)
            {
                lobbyView.SetVisible(false);
            }
        }

        /// <summary>네트워크 레이드의 로컬 플레이어 · 보스 · 파티로 HUD/카메라/입력을 다시 연결한다.</summary>
        public void BindRaid(PartyCharacter InPlayer, BossActor InBoss, IReadOnlyList<PartyCharacter> InParty, IPartyCommands InCommands)
        {
            player = InPlayer;
            boss = InBoss;
            party.Clear();
            party.AddRange(InParty);
            if (InPlayer != null)
            {
                controller.Bind(InPlayer);
                if (InCommands != null)
                {
                    controller.Commands = InCommands;
                }

                cameraRig.SetTarget(InPlayer.transform);
                window.Build(session, InPlayer, null);
            }

            hud.Build(InPlayer, InBoss, party);
        }

        private void clearRaid()
        {
            for (int i = 0; i < party.Count; ++i)
            {
                // 네트워크 오브젝트는 네트워크가 지운다.
                if (party[i] != null && NetworkOwnsRaid == false)
                {
                    ObjectUtil.SafeDestroy(party[i].gameObject);
                }
            }

            party.Clear();
            if (boss != null)
            {
                ObjectUtil.SafeDestroy(boss.gameObject);
            }

            boss = null;
            var views = new List<TelegraphView>(TelegraphView.ActiveViews);
            for (int i = 0; i < views.Count; ++i)
            {
                views[i].Despawn();
            }

            foreach (var pickup in FindObjectsByType<RaidPickup>(FindObjectsSortMode.None))
            {
                ObjectUtil.SafeDestroy(pickup.gameObject);
            }

            if (arena != null)
            {
                arena.RestoreAll();
            }

            for (int i = 0; i < ArenaStructure.Structures.Count; ++i)
            {
                ArenaStructure.Structures[i].Restore();
            }

            CombatRegistry.Clear();
        }

        private static void ensureLight()
        {
            if (FindAnyObjectByType<Light>() != null)
            {
                return;
            }

            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            go.transform.rotation = Quaternion.Euler(50.0f, -30.0f, 0.0f);
        }
    }
}
