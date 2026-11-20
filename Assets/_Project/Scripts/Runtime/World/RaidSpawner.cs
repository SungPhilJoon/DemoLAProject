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
    /// 캐릭터 · 보스를 만든다.
    ///  - 게임: Addressables 프리팹(WorldPrefabs.Character / Boss — 에디터 WorldPrefabBaker 가 Compose* 로 미리 구운 것)을 Instantiate 한 뒤 데이터만 연결한다.
    ///  - 네트워크 프리팹(NetPartyMember · NetBoss) 루트: Attach* 가 몸(Body)을 같은 프리팹에서 복제해 붙이고 컴포넌트를 채운다.
    ///  - 프리팹이 없으면(굽기 전) Compose* 로 코드 조립 (예전 방식).
    /// 데이터 의존 부분(직업 초기화 · 기믹 · AI · 이름 · 인원 배율 · 카운터 빛 머티리얼)은 항상 런타임에 붙인다.
    /// </summary>
    public static class RaidSpawner
    {
        private const string BodyName = "Body";

        /// <summary>플레이어 캐릭터를 만든다 (조작은 PlayerController / 네트워크 명령 — 아군 AI 없음).</summary>
        public static PartyCharacter SpawnCharacter(CharacterClassDefinition InClass, CharacterBuildData InBuild, GameDatabase InDatabase, Vector3 InPosition, string InName)
        {
            var go = WorldPrefabs.Instantiate(WorldPrefabs.Character(InClass.Class), InPosition, Quaternion.identity);
            if (go == null)
            {
                go = new GameObject();
                go.transform.position = InPosition;
                ComposeCharacter(go, InClass, null);
            }

            // GameObject 이름은 영문 식별자, InName 은 화면 표시 이름.
            go.name = $"Player_{InClass.Class}";
            var character = initializeCharacter(go, InClass, InBuild, InDatabase);
            character.SetName(InName);
            return character;
        }

        /// <summary>이미 있는 오브젝트(네트워크 프리팹 등)에 파티원 몸 · 이동 · 전투 컴포넌트를 붙인다. 몸은 직업 프리팹의 Body 를 복제한다.</summary>
        public static PartyCharacter AttachCharacter(GameObject InRoot, CharacterClassDefinition InClass, CharacterBuildData InBuild, GameDatabase InDatabase)
        {
            copyBodyFromPrefab(InRoot, WorldPrefabs.Get(WorldPrefabs.Character(InClass.Class)));
            ComposeCharacter(InRoot, InClass, null);
            return initializeCharacter(InRoot, InClass, InBuild, InDatabase);
        }

        /// <summary>
        /// 파티원 몸(모델 또는 캡슐 + 앞 표시) · NavMeshAgent · ActorMotor · PartyCharacter 를 붙인다. 이미 있는 것은 건너뛴다 (프리팹 굽기 · 코드 조립 공용).
        /// InMaterials: 색 → 머티리얼 (굽기 때 에셋, null 이면 런타임 인스턴스 머티리얼).
        /// </summary>
        public static void ComposeCharacter(GameObject InRoot, CharacterClassDefinition InClass, System.Func<Color, Material> InMaterials)
        {
            var go = InRoot;
            if (go.transform.Find(BodyName) == null)
            {
                if (InClass.ModelPrefab != null)
                {
                    addModel(go, InClass.ModelPrefab);
                }
                else
                {
                    addBody(go.transform, PrimitiveType.Capsule, InClass.BodyColor, new Vector3(1.0f, 1.0f, 1.0f), 1.0f, InMaterials);
                    addNose(go.transform, new Vector3(0.0f, 1.5f, 0.45f), new Vector3(0.25f, 0.15f, 0.35f), Color.white, InMaterials);
                }
            }

            addAgent(go, 0.45f, 2.0f, false);
            getOrAdd<ActorMotor>(go);
            getOrAdd<PartyCharacter>(go);
        }

        private static PartyCharacter initializeCharacter(GameObject InRoot, CharacterClassDefinition InClass, CharacterBuildData InBuild, GameDatabase InDatabase)
        {
            var character = InRoot.GetComponent<PartyCharacter>();
            character.Radius = 0.45f;
            character.Initialize(InClass, InBuild, InDatabase);
            return character;
        }

        public static BossActor SpawnBoss(BossDefinition InDefinition, Vector3 InPosition, Quaternion InRotation, int InPartySize = 0)
        {
            var go = WorldPrefabs.Instantiate(WorldPrefabs.Boss(InDefinition.BossId), InPosition, InRotation);
            if (go == null)
            {
                go = new GameObject();
                go.transform.SetPositionAndRotation(InPosition, InRotation);
                ComposeBoss(go, InDefinition, null);
            }

            go.name = $"Boss_{InDefinition.BossId}";
            return initializeBoss(go, InDefinition, InPartySize, true);
        }

        /// <summary>이미 있는 오브젝트에 보스를 붙인다. InWithBrain = false 면 AI 없이 (네트워크 클라이언트의 표시용 보스). 몸은 보스 프리팹의 Body 를 복제한다.</summary>
        public static BossActor AttachBoss(GameObject InRoot, BossDefinition InDefinition, int InPartySize, bool InWithBrain)
        {
            copyBodyFromPrefab(InRoot, WorldPrefabs.Get(WorldPrefabs.Boss(InDefinition.BossId)));
            ComposeBoss(InRoot, InDefinition, null);
            return initializeBoss(InRoot, InDefinition, InPartySize, InWithBrain);
        }

        /// <summary>
        /// 보스 몸(모델 또는 캡슐 + 머리 · 뿔) · CounterGlowView · NavMeshAgent · ActorMotor · BossPositionalIndicator(발밑 표시 조각) · BossActor 를 붙인다.
        /// 이미 있는 것은 건너뛴다 (프리팹 굽기 · 코드 조립 공용). 기믹 · AI 는 데이터에 따라 initializeBoss 가 붙인다.
        /// </summary>
        public static void ComposeBoss(GameObject InRoot, BossDefinition InDefinition, System.Func<Color, Material> InMaterials)
        {
            float scale = InDefinition.Radius;
            var go = InRoot;
            if (go.transform.Find(BodyName) == null)
            {
                if (InDefinition.ModelPrefab != null)
                {
                    // 모델 (Boss Skill Tool → 모델). 대기/이동은 컨트롤러, 패턴 동작은 BossAnimation.
                    addModel(go, InDefinition.ModelPrefab, InDefinition.ModelScale);
                }
                else
                {
                    addBody(go.transform, PrimitiveType.Capsule, InDefinition.BodyColor, new Vector3(scale, scale * 1.2f, scale), scale * 1.2f, InMaterials);
                    // 앞뒤 구분용 머리/뿔 (백어택 · 헤드어택 판정 방향을 눈으로 확인).
                    addNose(go.transform, new Vector3(0.0f, scale * 2.2f, scale * 0.55f), new Vector3(scale * 0.8f, scale * 0.6f, scale * 0.8f), InDefinition.BodyColor * 1.4f, InMaterials);
                    addNose(go.transform, new Vector3(-scale * 0.35f, scale * 2.7f, scale * 0.7f), new Vector3(0.15f, 0.5f, 0.15f) * scale, Color.white, InMaterials);
                    addNose(go.transform, new Vector3(scale * 0.35f, scale * 2.7f, scale * 0.7f), new Vector3(0.15f, 0.5f, 0.15f) * scale, Color.white, InMaterials);
                }
            }

            // 카운터 가능 구간에 몸이 파랗게 빛난다 (몸 렌더러 연결은 initializeBoss — 런타임 머티리얼).
            getOrAdd<CounterGlowView>(go);
            addAgent(go, InDefinition.Radius * 0.8f, scale * 2.4f, true);
            getOrAdd<ActorMotor>(go);
            // 발밑 백어택 / 헤드어택 범위 표시 (조각은 Awake 에서 찾거나 만든다 — 굽기 때는 Awake 가 돌지 않으므로 직접 만든다).
            getOrAdd<BossPositionalIndicator>(go).EnsureParts(InMaterials != null ? BossPositionalIndicator.BakedMaterial : null);
            getOrAdd<BossActor>(go);
        }

        private static BossActor initializeBoss(GameObject InRoot, BossDefinition InDefinition, int InPartySize, bool InWithBrain)
        {
            var go = InRoot;
            var bodyRenderers = new System.Collections.Generic.List<Renderer>();
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            {
                if (BossPositionalIndicator.IsPartName(renderer.name) == false)
                {
                    bodyRenderers.Add(renderer);
                }
            }

            go.GetComponent<CounterGlowView>().Bind(bodyRenderers);
            for (int i = 0; i < InDefinition.Gimmicks.Count; ++i)
            {
                addGimmick(go, InDefinition.Gimmicks[i]);
            }

            var boss = go.GetComponent<BossActor>();
            boss.SetName(InDefinition.DisplayName);
            if (InDefinition.DeathAnimation != null)
            {
                boss.OnActorDied += InDead => BossAnimation.PlayDeath(InDead as BossActor, true);
            }

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

        /// <summary>프리팹의 몸(Body · Facing 등, 발밑 표시 조각 제외)을 InRoot 아래로 복제한다 (네트워크 루트용).</summary>
        private static void copyBodyFromPrefab(GameObject InRoot, GameObject InPrefab)
        {
            if (InPrefab == null || InRoot.transform.Find(BodyName) != null)
            {
                return;
            }

            foreach (Transform child in InPrefab.transform)
            {
                if (BossPositionalIndicator.IsPartName(child.name) == true)
                {
                    continue;
                }

                var copy = Object.Instantiate(child.gameObject, InRoot.transform, false);
                copy.name = child.name;
                if (child.name == BodyName)
                {
                    bindAnimator(InRoot, copy);
                }
            }
        }

        private static T getOrAdd<T>(GameObject InGo) where T : Component
        {
            var component = InGo.GetComponent<T>();
            return component != null ? component : InGo.AddComponent<T>();
        }

        private static void bindAnimator(GameObject InRoot, GameObject InBody)
        {
            var animator = InBody.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return;
            }

            animator.applyRootMotion = false;
            var driver = getOrAdd<CharacterAnimatorDriver>(InRoot);
            if (Application.isPlaying == true)
            {
                driver.Bind(animator);
            }

            // 프리팹 굽기(에디터 정지 상태)는 그래프를 만들지 않는다 — 게임에서 Awake 가 자식 Animator 를 찾아 만든다.
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
                gimmick.GimmickTag = InSpec.GimmickTag;
            }
        }

        private static void addBody(Transform InRoot, PrimitiveType InType, Color InColor, Vector3 InScale, float InHeightOffset, System.Func<Color, Material> InMaterials)
        {
            var body = GameObject.CreatePrimitive(InType);
            body.name = BodyName;
            ObjectUtil.SafeDestroy(body.GetComponent<Collider>());
            body.transform.SetParent(InRoot, false);
            body.transform.localScale = InScale;
            body.transform.localPosition = new Vector3(0.0f, InHeightOffset, 0.0f);
            paint(body, InColor, InMaterials);
        }

        private static void paint(GameObject InGo, Color InColor, System.Func<Color, Material> InMaterials)
        {
            var renderer = InGo.GetComponent<Renderer>();
            if (InMaterials != null)
            {
                renderer.sharedMaterial = InMaterials(InColor);
            }
            else
            {
                renderer.material.color = InColor;
            }
        }

        /// <summary>
        /// 직업 모델 프리팹을 "Body" 로 붙인다 (ActorMotor 의 도약/넉백 연출이 "Body" 자식을 올린다).
        /// 루트 모션은 쓰지 않는다 — 이동은 NavMeshAgent / ActorMotor 가 하고, Animator 에는 속도만 넘긴다.
        /// </summary>
        private static void addModel(GameObject InRoot, GameObject InPrefab, float InScale = 1.0f)
        {
            GameObject body;
#if UNITY_EDITOR
            // 프리팹 굽기(에디터 정지 상태) 때는 모델 프리팹 연결을 유지한다 (모델을 고치면 월드 프리팹에도 반영).
            body = Application.isPlaying == false
                ? (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(InPrefab, InRoot.transform)
                : Object.Instantiate(InPrefab, InRoot.transform, false);
#else
            body = Object.Instantiate(InPrefab, InRoot.transform, false);
#endif
            body.name = BodyName;
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = InPrefab.transform.localScale * Mathf.Max(0.01f, InScale);
            bindAnimator(InRoot, body);
        }

        private static void addNose(Transform InParent, Vector3 InLocalPosition, Vector3 InScale, Color InColor, System.Func<Color, Material> InMaterials)
        {
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Facing";
            ObjectUtil.SafeDestroy(nose.GetComponent<Collider>());
            nose.transform.SetParent(InParent, false);
            nose.transform.localPosition = InLocalPosition;
            nose.transform.localScale = InScale;
            paint(nose, InColor, InMaterials);
        }

        /// <param name="InImmovable">true(보스/몬스터) 면 다른 에이전트에게 밀리지 않는다.
        /// 회피를 끄고 우선순위를 최상(0)으로 둬서 플레이어 에이전트가 길을 비키게 한다.
        /// 플레이어가 몸통에 겹치는 경우는 ActorMotor 가 플레이어 쪽만 밀어낸다.</param>
        private static void addAgent(GameObject InGo, float InRadius, float InHeight, bool InImmovable)
        {
            if (InGo.GetComponent<NavMeshAgent>() != null)
            {
                return;
            }

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
