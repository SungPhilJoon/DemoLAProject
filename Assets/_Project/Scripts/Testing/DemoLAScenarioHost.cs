using System.Collections;
using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Boss.Telegraph;
using DemoLA.Character;
using DemoLA.Combat;
using DemoLA.Content;
using DemoLA.Core;
using DemoLA.Data;
using DemoLA.Effects;
using DemoLA.World;
using Framework;
using Framework.Ability;
using Framework.Testing;
using UnityEngine;

namespace DemoLA.Testing
{
    /// <summary>
    /// DemoLA 용 시나리오 호스트. 빈 씬(UnityTest)에서는 DB · 경기장을 직접 만들고,
    /// 게임 중(Raid 씬)에서는 진행 중인 레이드를 걷어 내고 기존 경기장을 쓴 뒤 끝나면 대기실로 돌려보낸다.
    ///
    /// 기록하는 이벤트 (Recorder):
    ///   Damage.ToParty / Damage.ToBoss (값 = 피해) · Hit.Party / Hit.PartyCrit · Pattern.Started(.ID) · Pattern.Ended(.ID) (값 1 = 취소)
    ///   Telegraph (값 = 범위 길이) · Knockback (값 = 거리) · Structure.Broken(.Pillar/.Wall) · Died.Party / Died.Boss · Message
    /// 관측값 (Probe):
    ///   Boss.HpLines · Boss.HpPercent · Boss.Moved · Boss.Groggy · Party.Alive · Party.MinHpPercent · Party.KnockedBack ·
    ///   Party.CritRate · Party0.DistanceFromCenter · Telegraph.Visible · Structures.Destroyed · Pickups
    /// </summary>
    public class DemoLAScenarioHost : IScenarioHost
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private ScenarioContext context;
        private bool builtArena;
        private int hits;
        private int crits;
        private Vector3 bossSpawn;

        public GameDatabase Database { get; private set; }
        public ArenaFloor Arena { get; private set; }
        public BossActor Boss { get; private set; }
        public readonly List<PartyCharacter> Party = new List<PartyCharacter>();

        public PartyCharacter Member(int InIndex)
        {
            return InIndex >= 0 && InIndex < Party.Count ? Party[InIndex] : null;
        }

        // ------------------------------------------------------------------ setup

        public IEnumerator SetUp(ScenarioContext InContext)
        {
            context = InContext;
            var bootstrap = GameBootstrap.Current;
            if (bootstrap != null)
            {
                // 게임 중 실행: 진행 중인 레이드 · 대기실을 걷어 낸다.
                bootstrap.ClearRaid();
            }

            CombatRegistry.Clear();
            GameTags.RegisterAll();
            GameEffects.Initialize();
            GameRandom.SetSeed(InContext.Scenario.RandomSeed);
            Database = loadDatabase(bootstrap);
            StatFormula.Settings = Database.StatFormula;

            Arena = Object.FindAnyObjectByType<ArenaFloor>();
            if (Arena == null)
            {
                Arena = ArenaBuilder.Build(null);
                Arena.Surface.BuildNavMesh();
                builtArena = true;
            }

            Arena.RestoreAll();
            for (int i = 0; i < ArenaStructure.Structures.Count; ++i)
            {
                ArenaStructure.Structures[i].Restore();
            }

            subscribe();
            registerProbes(InContext);
            yield return null;
        }

        private static GameDatabase loadDatabase(GameBootstrap InBootstrap)
        {
            if (InBootstrap != null && InBootstrap.Session != null)
            {
                return InBootstrap.Session.Database;
            }

            string errors;
            return DefaultContent.CreateDatabase(DemoLATables.FromCsv(TablePaths.ReadCsv, out errors));
        }

        public void TearDown(ScenarioContext InContext)
        {
            unsubscribe();
            for (int i = 0; i < spawned.Count; ++i)
            {
                if (spawned[i] != null)
                {
                    Object.Destroy(spawned[i]);
                }
            }

            spawned.Clear();
            Party.Clear();
            Boss = null;
            foreach (var view in new List<TelegraphView>(TelegraphView.ActiveViews))
            {
                view.Despawn();
            }

            foreach (var pickup in Object.FindObjectsByType<RaidPickup>(FindObjectsSortMode.None))
            {
                Object.Destroy(pickup.gameObject);
            }

            for (int i = 0; i < ArenaStructure.Structures.Count; ++i)
            {
                ArenaStructure.Structures[i].Restore();
            }

            if (Arena != null)
            {
                Arena.RestoreAll();
                if (builtArena == true)
                {
                    Object.Destroy(Arena.gameObject);
                }
            }

            CombatRegistry.Clear();
            var bootstrap = GameBootstrap.Current;
            if (bootstrap != null && bootstrap.Session != null)
            {
                bootstrap.EnterLobby();
            }
        }

        // ------------------------------------------------------------------ spawning (단계가 부른다)

        public BossActor SpawnBoss(string InBossId, Vector3 InPosition, Vector3 InFaceTowards, bool InEnableAI)
        {
            var definition = Database.GetBoss(InBossId);
            if (definition == null)
            {
                return null;
            }

            var facing = InFaceTowards - InPosition;
            facing.y = 0.0f;
            Boss = RaidSpawner.SpawnBoss(definition, InPosition, Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing : Vector3.back), 4);
            spawned.Add(Boss.gameObject);
            bossSpawn = InPosition;
            if (InEnableAI == false)
            {
                var brain = Boss.GetComponent<DemoLA.AI.CombatBrain>();
                if (brain != null)
                {
                    brain.Stop();
                }

                // 생성 직후 AI 가 이미 고른 패턴(예: 1관문 G1-05 분열)이 있으면 취소한다.
                Boss.Asc.CancelAbilitiesWithTags(GameTags.Container(ETag.Ability_BossPattern), null);
            }

            Boss.Asc.OnAbilityActivated += handleBossAbility;
            Boss.OnPatternEnded += handlePatternEnded;
            return Boss;
        }

        public PartyCharacter SpawnMember(ECharacterClass InClass, Vector3 InPosition, bool InUseAI)
        {
            var cls = Database.GetClass(InClass);
            if (cls == null)
            {
                return null;
            }

            var tree = InUseAI ? DemoLA.Content.DefaultTrees.CreatePartyAI() : null;
            var member = RaidSpawner.SpawnCharacter(cls, DefaultContent.CreateDefaultBuild(Database, InClass), Database, InPosition, tree, $"{(InUseAI ? "AI" : "더미")} {cls.DisplayName} {Party.Count}");
            spawned.Add(member.gameObject);
            Party.Add(member);
            return member;
        }

        // ------------------------------------------------------------------ events

        private void subscribe()
        {
            EventBus<DamageAppliedEvent>.Subscribe(handleDamage);
            EventBus<ActorDiedEvent>.Subscribe(handleDied);
            EventBus<RaidMessageEvent>.Subscribe(handleMessage);
            BossPatternAbility.TelegraphStarted += handleTelegraph;
            ActorMotor.Knockbacked += handleKnockback;
            ArenaStructure.Broken += handleBroken;
        }

        private void unsubscribe()
        {
            EventBus<DamageAppliedEvent>.Unsubscribe(handleDamage);
            EventBus<ActorDiedEvent>.Unsubscribe(handleDied);
            EventBus<RaidMessageEvent>.Unsubscribe(handleMessage);
            BossPatternAbility.TelegraphStarted -= handleTelegraph;
            ActorMotor.Knockbacked -= handleKnockback;
            ArenaStructure.Broken -= handleBroken;
            if (Boss != null && Boss.Asc != null)
            {
                Boss.Asc.OnAbilityActivated -= handleBossAbility;
                Boss.OnPatternEnded -= handlePatternEnded;
            }
        }

        private void handleDamage(DamageAppliedEvent InEvent)
        {
            var target = InEvent.Target != null ? InEvent.Target.OwnerActor as CombatActor : null;
            var source = InEvent.Source != null ? InEvent.Source.OwnerActor as CombatActor : null;
            if (target != null && target.Team == ETeam.Player)
            {
                context.Recorder.Record("Damage.ToParty", InEvent.RawAmount, target.name);
            }
            else if (target != null)
            {
                context.Recorder.Record("Damage.ToBoss", InEvent.HealthLost + InEvent.ShieldAbsorbed, source != null ? source.name : null);
            }

            if (source != null && source.Team == ETeam.Player && target != null && target.Team != ETeam.Player)
            {
                hits += 1;
                context.Recorder.Record("Hit.Party", 1.0f);
                if (InEvent.IsCrit == true)
                {
                    crits += 1;
                    context.Recorder.Record("Hit.PartyCrit", 1.0f);
                }
            }
        }

        private void handleDied(ActorDiedEvent InEvent)
        {
            if (InEvent.Actor == null)
            {
                return;
            }

            context.Recorder.Record(InEvent.Actor.Team == ETeam.Player ? "Died.Party" : "Died.Boss", 1.0f, InEvent.Actor.name);
        }

        private void handleMessage(RaidMessageEvent InEvent)
        {
            context.Recorder.Record("Message", InEvent.IsWarning ? 1.0f : 0.0f, InEvent.Message);
        }

        private void handleTelegraph(HitArea InArea, Vector3 InOrigin, Vector3 InForward, float InTime)
        {
            context.Recorder.Record("Telegraph", InArea.Reach);
        }

        private void handleKnockback(CombatActor InActor, float InDistance)
        {
            context.Recorder.Record("Knockback", InDistance, InActor != null ? InActor.name : null);
        }

        private void handleBroken(ArenaStructure InStructure, CombatActor InBreaker)
        {
            context.Recorder.Record("Structure.Broken", 1.0f);
            context.Recorder.Record(InStructure.Kind == EStructureKind.Wall ? "Structure.Broken.Wall" : "Structure.Broken.Pillar", 1.0f);
        }

        private void handleBossAbility(GameplayAbility InAbility)
        {
            var pattern = InAbility as BossPatternAbility;
            if (pattern == null)
            {
                return;
            }

            context.Recorder.Record("Pattern.Started", 1.0f, pattern.Definition.PatternId);
            context.Recorder.Record($"Pattern.Started.{pattern.Definition.PatternId}", 1.0f);
        }

        private void handlePatternEnded(BossPatternAbility InAbility, bool InCancelled)
        {
            context.Recorder.Record("Pattern.Ended", InCancelled ? 1.0f : 0.0f, InAbility.Definition.PatternId);
            context.Recorder.Record($"Pattern.Ended.{InAbility.Definition.PatternId}", InCancelled ? 1.0f : 0.0f);
        }

        // ------------------------------------------------------------------ probes

        private void registerProbes(ScenarioContext InContext)
        {
            InContext.RegisterProbe("Boss.HpLines", () => Boss != null ? Boss.HpLinesExact : float.NaN);
            InContext.RegisterProbe("Boss.HpPercent", () => Boss != null ? Boss.Stats.HealthRatio * 100.0f : float.NaN);
            InContext.RegisterProbe("Boss.Moved", () =>
            {
                if (Boss == null)
                {
                    return float.NaN;
                }

                var delta = Boss.Position - bossSpawn;
                delta.y = 0.0f;
                return delta.magnitude;
            });
            InContext.RegisterProbe("Boss.Groggy", () => Boss != null && Boss.HasTag((int)ETag.State_Groggy) ? 1.0f : 0.0f);
            InContext.RegisterProbe("Party.Alive", () =>
            {
                int alive = 0;
                for (int i = 0; i < Party.Count; ++i)
                {
                    alive += Party[i] != null && Party[i].IsDead == false ? 1 : 0;
                }

                return alive;
            });
            InContext.RegisterProbe("Party.MinHpPercent", () =>
            {
                float min = 100.0f;
                for (int i = 0; i < Party.Count; ++i)
                {
                    if (Party[i] != null)
                    {
                        min = Mathf.Min(min, Party[i].Stats.HealthRatio * 100.0f);
                    }
                }

                return min;
            });
            InContext.RegisterProbe("Party.KnockedBack", () =>
            {
                int count = 0;
                for (int i = 0; i < Party.Count; ++i)
                {
                    count += Party[i] != null && Party[i].Motor != null && Party[i].Motor.IsKnockedBack ? 1 : 0;
                }

                return count;
            });
            InContext.RegisterProbe("Party.CritRate", () => hits > 0 ? crits * 100.0f / hits : float.NaN);
            InContext.RegisterProbe("Party0.DistanceFromCenter", () =>
            {
                var member = Member(0);
                if (member == null || Arena == null)
                {
                    return float.NaN;
                }

                var delta = member.Position - Arena.transform.position;
                delta.y = 0.0f;
                return delta.magnitude;
            });
            InContext.RegisterProbe("Telegraph.Visible", () =>
            {
                int visible = 0;
                foreach (var view in TelegraphView.ActiveViews)
                {
                    visible += view != null && view.gameObject.activeSelf ? 1 : 0;
                }

                return visible;
            });
            InContext.RegisterProbe("Structures.Destroyed", () =>
            {
                int destroyed = 0;
                for (int i = 0; i < ArenaStructure.Structures.Count; ++i)
                {
                    destroyed += ArenaStructure.Structures[i].IsDestroyed ? 1 : 0;
                }

                return destroyed;
            });
            InContext.RegisterProbe("Pickups", () => Object.FindObjectsByType<RaidPickup>(FindObjectsSortMode.None).Length);
        }
    }
}
