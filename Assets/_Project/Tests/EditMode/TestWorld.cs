using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Character;
using DemoLA.Combat;
using DemoLA.Content;
using DemoLA.Core;
using DemoLA.Data;
using DemoLA.Effects;
using Framework;
using UnityEngine;
using UnityEngine.AI;

namespace DemoLA.Tests
{
    /// <summary>테스트용 최소 전투 액터.</summary>
    public class TestCombatant : CombatActor
    {
        public void Init(ETeam InTeam, float InMaxHealth, float InAttackPower, float InDefense)
        {
            Team = InTeam;
            var stats = new CombatAttributeSet();
            InitializeCombat(stats);
            stats.InitializeDefaults(InMaxHealth, 1000.0f, InAttackPower, InDefense);
        }
    }

    /// <summary>
    /// EditMode 시나리오 도우미. Update 가 돌지 않으므로 ASC.Tick 을 고정 간격으로 직접 부른다 (결정적).
    /// </summary>
    public class TestWorld : System.IDisposable
    {
        private static GameDatabase cachedDatabase;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<CombatActor> actors = new List<CombatActor>();

        public TestWorld(int InSeed = 7)
        {
            CombatRegistry.Clear();
            GameTags.RegisterAll();
            GameEffects.Initialize();
            GameRandom.SetSeed(InSeed);
            // 공식 단위 테스트는 밸런스 배율 없이 계산값을 그대로 본다 (밸런스는 시나리오 TS_밸런스_* 가 본다).
            StatFormula.Settings = new StatFormulaSettings { PlayerDamageScale = 1.0f };
        }

        /// <summary>기본 콘텐츠 DB (CSV 는 프로젝트 파일에서 읽는다). 테스트 간 공유.</summary>
        public static GameDatabase Database
        {
            get
            {
                if (cachedDatabase == null)
                {
                    string errors;
                    var tables = DemoLATables.FromCsv(TablePaths.ReadCsv, out errors);
                    UnityEngine.Assertions.Assert.IsTrue(string.IsNullOrEmpty(errors), errors);
                    cachedDatabase = DefaultContent.CreateDatabase(tables);
                }

                return cachedDatabase;
            }
        }

        public TestCombatant Combatant(ETeam InTeam, Vector3 InPosition, float InMaxHealth = 100000.0f, float InAttack = 10000.0f, float InDefense = 0.0f)
        {
            var go = new GameObject($"Test_{InTeam}");
            objects.Add(go);
            go.transform.position = InPosition;
            var actor = go.AddComponent<TestCombatant>();
            actor.Init(InTeam, InMaxHealth, InAttack, InDefense);
            actors.Add(actor);
            return actor;
        }

        public PartyCharacter Character(ECharacterClass InClass, Vector3 InPosition, bool InWithBuild)
        {
            var db = Database;
            var go = new GameObject($"Test_{InClass}");
            objects.Add(go);
            go.transform.position = InPosition;
            go.AddComponent<NavMeshAgent>();
            go.AddComponent<ActorMotor>();
            var character = go.AddComponent<PartyCharacter>();
            var build = InWithBuild == true ? DefaultContent.CreateDefaultBuild(db, InClass) : new DemoLA.Build.CharacterBuildData { Class = InClass };
            character.Initialize(db.GetClass(InClass), build, db);
            actors.Add(character);
            return character;
        }

        public BossActor Boss(string InBossId, Vector3 InPosition, Vector3 InFaceTowards, int InPartySize = 0)
        {
            var db = Database;
            var go = new GameObject($"Test_Boss_{InBossId}");
            objects.Add(go);
            go.transform.position = InPosition;
            go.transform.rotation = Quaternion.LookRotation(InFaceTowards - InPosition);
            go.AddComponent<NavMeshAgent>();
            go.AddComponent<ActorMotor>();
            var boss = go.AddComponent<BossActor>();
            boss.Initialize(db.GetBoss(InBossId), InPartySize > 0 ? InPartySize : db.GetBoss(InBossId).DesignPartySize);
            actors.Add(boss);
            return boss;
        }

        /// <summary>모든 액터의 ASC 를 InDelta 간격으로 InSeconds 동안 진행.</summary>
        public void Simulate(float InSeconds, float InDelta = 0.05f, System.Action<float> InEachStep = null)
        {
            float time = 0.0f;
            while (time < InSeconds)
            {
                InEachStep?.Invoke(time);
                for (int i = 0; i < actors.Count; ++i)
                {
                    if (actors[i] != null)
                    {
                        actors[i].Asc.Tick(InDelta);
                    }
                }

                time += InDelta;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < objects.Count; ++i)
            {
                if (objects[i] != null)
                {
                    Object.DestroyImmediate(objects[i]);
                }
            }

            foreach (var view in Object.FindObjectsByType<DemoLA.Boss.Telegraph.TelegraphView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(view.gameObject);
            }

            CombatRegistry.Clear();
        }
    }

    /// <summary>DamageAppliedEvent 를 출처별로 모은다.</summary>
    public class DamageMeter : System.IDisposable
    {
        private readonly Framework.Ability.AbilitySystemComponent source;
        public float Total;
        public int Hits;
        public int Crits;

        public DamageMeter(Framework.Ability.AbilitySystemComponent InSource)
        {
            source = InSource;
            EventBus<DamageAppliedEvent>.Subscribe(handle);
        }

        private void handle(DamageAppliedEvent InEvent)
        {
            if (InEvent.Source != source)
            {
                return;
            }

            Total += InEvent.HealthLost + InEvent.ShieldAbsorbed;
            Hits += 1;
            Crits += InEvent.IsCrit == true ? 1 : 0;
        }

        public void Dispose()
        {
            EventBus<DamageAppliedEvent>.Unsubscribe(handle);
        }
    }
}
