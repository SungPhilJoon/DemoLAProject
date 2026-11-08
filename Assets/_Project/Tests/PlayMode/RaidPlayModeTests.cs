using System.Collections;
using System.Collections.Generic;
using DemoLA.AI;
using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Character;
using DemoLA.Combat;
using DemoLA.Content;
using DemoLA.Core;
using DemoLA.Data;
using DemoLA.Effects;
using DemoLA.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DemoLA.Tests
{
    /// <summary>
    /// 실제 Update 루프로 도는 시나리오 (UnityTest). 시간 배율을 올려 빠르게 돌린다.
    ///  - 파티 AI(StateTree) 가 보스에게 피해를 주는가
    ///  - 보스 AI(StateTree) 가 패턴을 쓰는가
    /// </summary>
    public class RaidPlayModeTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private GameDatabase database;
        private ArenaFloor arena;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            CombatRegistry.Clear();
            GameTags.RegisterAll();
            GameEffects.Initialize();
            GameRandom.SetSeed(3);
            string errors;
            database = DefaultContent.CreateDatabase(DemoLATables.FromCsv(TablePaths.ReadCsv, out errors));
            Assert.Greater(database.Bosses.Count, 0, "Assets/_Project/Data/CSV 가 있어야 한다");
            StatFormula.Settings = database.StatFormula;
            arena = ArenaBuilder.Build(null);
            spawned.Add(arena.gameObject);
            arena.Surface.BuildNavMesh();
            Time.timeScale = 4.0f;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1.0f;
            foreach (var go in spawned)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }

            spawned.Clear();
            CombatRegistry.Clear();
            yield return null;
        }

        private PartyCharacter spawnAlly(ECharacterClass InClass, Vector3 InPosition)
        {
            var cls = database.GetClass(InClass);
            var ally = RaidSpawner.SpawnCharacter(cls, DefaultContent.CreateDefaultBuild(database, InClass), database, InPosition, DefaultTrees.CreatePartyAI(), $"AI {InClass}");
            spawned.Add(ally.gameObject);
            return ally;
        }

        [UnityTest]
        public IEnumerator PartyAI_DamagesBoss()
        {
            var bossDef = database.GetBoss("Gate1");
            var boss = RaidSpawner.SpawnBoss(bossDef, new Vector3(0.0f, 0.0f, 6.0f), Quaternion.identity);
            spawned.Add(boss.gameObject);
            // 보스 AI 는 끄고 아군만 본다.
            boss.GetComponent<CombatBrain>().Stop();

            spawnAlly(ECharacterClass.Slayer, new Vector3(-2.0f, 0.0f, -6.0f));
            spawnAlly(ECharacterClass.Infighter, new Vector3(0.0f, 0.0f, -6.0f));
            spawnAlly(ECharacterClass.Artist, new Vector3(2.0f, 0.0f, -6.0f));

            float start = boss.Stats.Get(EAttr.Health);
            yield return new WaitForSeconds(10.0f);
            float dealt = start - boss.Stats.Get(EAttr.Health);
            Debug.Log($"[PlayMode] 파티 AI 10초(게임 시간) 피해: {dealt:N0}");
            Assert.Greater(dealt, 0.0f, "아군 AI 가 보스에게 다가가 스킬을 써야 한다");
        }

        [UnityTest]
        public IEnumerator BossAI_UsesPatterns()
        {
            var bossDef = database.GetBoss("Gate1");
            var boss = RaidSpawner.SpawnBoss(bossDef, new Vector3(0.0f, 0.0f, 4.0f), Quaternion.identity);
            spawned.Add(boss.gameObject);
            var player = RaidSpawner.SpawnCharacter(database.GetClass(ECharacterClass.Infighter), DefaultContent.CreateDefaultBuild(database, ECharacterClass.Infighter), database, Vector3.zero, null, "Dummy Player");
            spawned.Add(player.gameObject);

            int patterns = 0;
            boss.OnPatternEnded += (InAbility, InCancelled) => patterns += 1;
            yield return new WaitForSeconds(12.0f);
            Debug.Log($"[PlayMode] 보스 패턴 사용 {patterns}회, 플레이어 체력 {player.Stats.HealthRatio * 100.0f:0}%");
            Assert.Greater(patterns, 0, "보스 StateTree 가 패턴을 골라 써야 한다");
        }

        [UnityTest]
        /// <summary>
        /// 지형 파괴: 넉백 없이 발밑이 무너지면 죽지 않고 남은 바닥으로 옮겨진다 (낙사는 몬스터 공격에 날아갔을 때만).
        /// 넉백 낙사는 시나리오 TS_낙사_넉백 이 확인한다.
        /// </summary>
        public IEnumerator TerrainCollapse_RescuesPlayerWithoutKnockback()
        {
            var bossDef = database.GetBoss("Gate2");
            var boss = RaidSpawner.SpawnBoss(bossDef, new Vector3(0.0f, 0.0f, 0.0f), Quaternion.identity);
            spawned.Add(boss.gameObject);
            boss.GetComponent<CombatBrain>().Stop();
            var victim = RaidSpawner.SpawnCharacter(database.GetClass(ECharacterClass.Slayer), new DemoLA.Build.CharacterBuildData(), database, new Vector3(-16.0f, 0.0f, 0.0f), null, "Left");
            var survivor = RaidSpawner.SpawnCharacter(database.GetClass(ECharacterClass.Slayer), new DemoLA.Build.CharacterBuildData(), database, new Vector3(0.0f, 0.0f, -3.0f), null, "Center");
            spawned.Add(victim.gameObject);
            spawned.Add(survivor.gameObject);

            var gimmick = boss.GetGimmick("G2-06") as TerrainCollapseGimmick;
            Assert.IsNotNull(gimmick);
            gimmick.WarningTime = 0.5f;
            Assert.IsTrue(boss.StartGimmick("G2-06"));
            yield return new WaitForSeconds(3.0f);

            Assert.IsFalse(victim.IsDead, "넉백 없이 발밑이 무너지면 낙사하지 않는다");
            Assert.IsTrue(arena.IsSolid(victim.Position), "남은 바닥 위로 옮겨져야 한다");
            Assert.IsFalse(survivor.IsDead, "중앙 판은 남는다");
        }
    }
}
