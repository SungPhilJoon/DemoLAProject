using DemoLA.AI;
using DemoLA.Boss;
using DemoLA.Build;
using DemoLA.Character;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Data;
using Framework.StateTree;
using UnityEngine;
using UnityEngine.AI;

namespace DemoLA.World
{
    /// <summary>
    /// 캐릭터/보스를 기본 메시로 조립해 만든다 (문서: 에셋 미정 → 캡슐/기본 메시 조합).
    /// 프리팹을 쓰게 되면 이 클래스의 Create* 를 프리팹 Instantiate 로 바꾼다.
    /// </summary>
    public static class RaidSpawner
    {
        public static PartyCharacter SpawnCharacter(CharacterClassDefinition InClass, CharacterBuildData InBuild, GameDatabase InDatabase, Vector3 InPosition, StateTreeAsset InAITree, string InName)
        {
            var go = new GameObject(InName);
            go.transform.position = InPosition;
            return AttachCharacter(go, InClass, InBuild, InDatabase, InAITree);
        }

        /// <summary>이미 있는 오브젝트(네트워크 프리팹 등)에 파티원 몸 · 이동 · 전투 컴포넌트를 붙인다.</summary>
        public static PartyCharacter AttachCharacter(GameObject InRoot, CharacterClassDefinition InClass, CharacterBuildData InBuild, GameDatabase InDatabase, StateTreeAsset InAITree)
        {
            var go = InRoot;
            addBody(go.transform, PrimitiveType.Capsule, InClass.BodyColor, new Vector3(1.0f, 1.0f, 1.0f), 1.0f);
            addNose(go.transform, new Vector3(0.0f, 1.5f, 0.45f), new Vector3(0.25f, 0.15f, 0.35f), Color.white);
            addAgent(go, 0.45f, 2.0f, false);
            go.AddComponent<ActorMotor>();
            var character = go.AddComponent<PartyCharacter>();
            character.Radius = 0.45f;
            character.Initialize(InClass, InBuild, InDatabase);

            if (InAITree != null)
            {
                var brain = go.AddComponent<CombatBrain>();
                brain.SetTree(InAITree, true);
            }

            return character;
        }

        public static BossActor SpawnBoss(BossDefinition InDefinition, Vector3 InPosition, Quaternion InRotation, int InPartySize = 0)
        {
            var go = new GameObject(InDefinition.DisplayName);
            go.transform.SetPositionAndRotation(InPosition, InRotation);
            return AttachBoss(go, InDefinition, InPartySize, true);
        }

        /// <summary>이미 있는 오브젝트에 보스를 붙인다. InWithBrain = false 면 AI 없이 (네트워크 클라이언트의 표시용 보스).</summary>
        public static BossActor AttachBoss(GameObject InRoot, BossDefinition InDefinition, int InPartySize, bool InWithBrain)
        {
            float scale = InDefinition.Radius;
            var go = InRoot;
            addBody(go.transform, PrimitiveType.Capsule, InDefinition.BodyColor, new Vector3(scale, scale * 1.2f, scale), scale * 1.2f);
            // 앞뒤 구분용 머리/뿔 (백어택 · 헤드어택 판정 방향을 눈으로 확인).
            addNose(go.transform, new Vector3(0.0f, scale * 2.2f, scale * 0.55f), new Vector3(scale * 0.8f, scale * 0.6f, scale * 0.8f), InDefinition.BodyColor * 1.4f);
            addNose(go.transform, new Vector3(-scale * 0.35f, scale * 2.7f, scale * 0.7f), new Vector3(0.15f, 0.5f, 0.15f) * scale, Color.white);
            addNose(go.transform, new Vector3(scale * 0.35f, scale * 2.7f, scale * 0.7f), new Vector3(0.15f, 0.5f, 0.15f) * scale, Color.white);
            // 카운터 가능 구간에 몸이 파랗게 빛난다 (발밑 표시보다 먼저 모아 몸 렌더러만 넘긴다).
            go.AddComponent<CounterGlowView>().Bind(go.GetComponentsInChildren<Renderer>());
            addAgent(go, InDefinition.Radius * 0.8f, scale * 2.4f, true);
            go.AddComponent<ActorMotor>();
            // 발밑 백어택 / 헤드어택 범위 표시.
            go.AddComponent<BossPositionalIndicator>();

            for (int i = 0; i < InDefinition.Gimmicks.Count; ++i)
            {
                addGimmick(go, InDefinition.Gimmicks[i]);
            }

            var boss = go.AddComponent<BossActor>();
            boss.Initialize(InDefinition, InPartySize > 0 ? InPartySize : InDefinition.DesignPartySize);
            // 관문마다 에스더 게이지를 새로 모은다 (그 관문에서 쓸 수 있는 에스더만).
            DemoLA.Character.EstherSystem.Reset(InDefinition.BossId);
            if (InDefinition.Brain != null && InWithBrain == true)
            {
                var brain = go.AddComponent<CombatBrain>();
                brain.SetTree(InDefinition.Brain, true);
            }

            return boss;
        }

        private static void addGimmick(GameObject InBoss, GimmickSpec InSpec)
        {
            BossGimmick gimmick = null;
            switch (InSpec.Kind)
            {
                case EGimmickKind.OrbAlternation:
                    gimmick = InBoss.AddComponent<OrbAlternationGimmick>();
                    break;
                case EGimmickKind.DeathWardOrbs:
                    gimmick = InBoss.AddComponent<DeathWardOrbGimmick>();
                    break;
                case EGimmickKind.TerrainCollapseHalf:
                    var half = InBoss.AddComponent<TerrainCollapseGimmick>();
                    half.Stage = ECollapseStage.RandomHalf;
                    gimmick = half;
                    break;
                case EGimmickKind.TerrainCollapseCenter:
                    var center = InBoss.AddComponent<TerrainCollapseGimmick>();
                    center.Stage = ECollapseStage.AllExceptCenter;
                    gimmick = center;
                    break;
                case EGimmickKind.ArmorStack:
                    gimmick = InBoss.AddComponent<ArmorStackGimmick>();
                    break;
            }

            if (gimmick != null)
            {
                gimmick.GimmickId = InSpec.GimmickId;
                gimmick.PatternId = InSpec.GimmickId;
            }
        }

        private static void addBody(Transform InRoot, PrimitiveType InType, Color InColor, Vector3 InScale, float InHeightOffset)
        {
            var body = GameObject.CreatePrimitive(InType);
            body.name = "Body";
            ObjectUtil.SafeDestroy(body.GetComponent<Collider>());
            body.transform.SetParent(InRoot, false);
            body.transform.localScale = InScale;
            body.transform.localPosition = new Vector3(0.0f, InHeightOffset, 0.0f);
            body.GetComponent<Renderer>().material.color = InColor;
        }

        private static void addNose(Transform InParent, Vector3 InLocalPosition, Vector3 InScale, Color InColor)
        {
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Facing";
            ObjectUtil.SafeDestroy(nose.GetComponent<Collider>());
            nose.transform.SetParent(InParent, false);
            nose.transform.localPosition = InLocalPosition;
            nose.transform.localScale = InScale;
            nose.GetComponent<Renderer>().material.color = InColor;
        }

        /// <param name="InImmovable">true(보스/몬스터) 면 다른 에이전트에게 밀리지 않는다.
        /// 회피를 끄고 우선순위를 최상(0)으로 둬서 플레이어 에이전트가 길을 비키게 한다.
        /// 플레이어가 몸통에 겹치는 경우는 ActorMotor 가 플레이어 쪽만 밀어낸다.</param>
        private static void addAgent(GameObject InGo, float InRadius, float InHeight, bool InImmovable)
        {
            var agent = InGo.AddComponent<NavMeshAgent>();
            agent.radius = InRadius;
            agent.height = InHeight;
            agent.baseOffset = 0.0f;
            agent.stoppingDistance = 0.2f;
            if (InImmovable == true)
            {
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                agent.avoidancePriority = 0;
            }
            else
            {
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                agent.avoidancePriority = 50;
            }
        }
    }
}
