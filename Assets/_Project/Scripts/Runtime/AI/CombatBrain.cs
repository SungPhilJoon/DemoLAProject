using DemoLA.Boss;
using DemoLA.Combat;
using DemoLA.Core;
using Framework;
using Framework.Ability;
using Framework.StateTree;
using UnityEngine;

namespace DemoLA.AI
{
    /// <summary>
    /// StateTree 실행기 (언리얼 StateTreeComponent 대응). 보스와 아군 AI 가 같이 쓴다.
    /// ASC 게임플레이 이벤트 / 보스 체력 줄 변화를 트리 이벤트(EAIEvent)로 바꿔 보낸다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class CombatBrain : BaseBehaviour
    {
        [SerializeField] private StateTreeAsset tree;
        [SerializeField] private bool autoStart = true;

        private StateTreeInstance instance;
        private CombatActor actor;
        private BossActor boss;

        public StateTreeInstance Instance
        {
            get { return instance; }
        }

        public CombatActor Actor
        {
            get { return actor; }
        }

        public StateTreeAsset Tree
        {
            get { return tree; }
        }

        public void SetTree(StateTreeAsset InTree, bool InStart)
        {
            Stop();
            tree = InTree;
            if (InStart == true)
            {
                Begin();
            }
        }

        public void Begin()
        {
            if (tree == null)
            {
                return;
            }

            actor = GetComponent<CombatActor>();
            if (actor == null || actor.IsInitialized == false)
            {
                return;
            }

            Stop();
            boss = actor as BossActor;
            instance = new StateTreeInstance(tree, this, new StateTreeBlackboard());
            StateTreeDebugger.Register(actor.GetName(), this, instance);
            bind(true);
            instance.Start();
        }

        public void Stop()
        {
            if (instance == null)
            {
                return;
            }

            bind(false);
            instance.Stop();
            StateTreeDebugger.Unregister(instance);
            instance = null;
        }

        public void SendEvent(EAIEvent InEvent)
        {
            if (instance != null)
            {
                instance.SendEvent((int)InEvent);
            }
        }

        private void Start()
        {
            if (autoStart == true && instance == null)
            {
                Begin();
            }
        }

        private void Update()
        {
            if (instance == null)
            {
                return;
            }

            if (actor == null || actor.IsDead == true)
            {
                return;
            }

            instance.Tick(Time.deltaTime);
        }

        protected override void OnUnityDestroy()
        {
            Stop();
        }

        private void bind(bool InBind)
        {
            if (actor == null || actor.Asc == null)
            {
                return;
            }

            if (InBind == true)
            {
                actor.Asc.OnGameplayEvent += handleGameplayEvent;
                if (boss != null)
                {
                    boss.OnHpLineChanged += handleHpLineChanged;
                    boss.OnPatternEnded += handlePatternEnded;
                    boss.OnPhaseChanged += handlePhaseChanged;
                }
            }
            else
            {
                actor.Asc.OnGameplayEvent -= handleGameplayEvent;
                if (boss != null)
                {
                    boss.OnHpLineChanged -= handleHpLineChanged;
                    boss.OnPatternEnded -= handlePatternEnded;
                    boss.OnPhaseChanged -= handlePhaseChanged;
                }
            }
        }

        private void handleGameplayEvent(int InTag, GameplayCueParams InPayload)
        {
            switch ((ETag)InTag)
            {
                case ETag.Event_Countered:
                    SendEvent(EAIEvent.Countered);
                    break;
                case ETag.Event_StaggerBroken:
                    SendEvent(EAIEvent.StaggerBroken);
                    break;
                case ETag.Event_GimmickSucceeded:
                    SendEvent(EAIEvent.GimmickSucceeded);
                    break;
                case ETag.Event_GimmickFailed:
                    SendEvent(EAIEvent.GimmickFailed);
                    break;
                case ETag.Event_StructureCollision:
                    SendEvent(EAIEvent.StructureCollision);
                    break;
            }
        }

        private void handleHpLineChanged(int InOld, int InNew)
        {
            SendEvent(EAIEvent.HpLineChanged);
        }

        private void handlePatternEnded(BossPatternAbility InAbility, bool InCancelled)
        {
            SendEvent(EAIEvent.PatternEnded);
        }

        private void handlePhaseChanged(string InPhaseId)
        {
            SendEvent(EAIEvent.PhaseChanged);
        }
    }

    /// <summary>노드에서 컨텍스트 → 액터를 꺼내는 헬퍼.</summary>
    public static class AIContext
    {
        public static CombatActor Actor(StateTreeContext InContext)
        {
            var brain = InContext.Owner as CombatBrain;
            return brain != null ? brain.Actor : null;
        }

        public static BossActor Boss(StateTreeContext InContext)
        {
            return Actor(InContext) as BossActor;
        }

        public static DemoLA.Character.PartyCharacter Party(StateTreeContext InContext)
        {
            return Actor(InContext) as DemoLA.Character.PartyCharacter;
        }

        public static CombatActor Target(StateTreeContext InContext)
        {
            return InContext.Blackboard.GetObject<CombatActor>((int)EAIBlackboard.Target);
        }

        /// <summary>SelectTargetTask 가 다음 패턴용으로 지정한 대상 (없거나 죽었으면 null).</summary>
        public static CombatActor PatternTarget(StateTreeContext InContext)
        {
            var target = InContext.Blackboard.GetObject<CombatActor>((int)EAIBlackboard.PatternTarget);
            return target != null && target.IsDead == false ? target : null;
        }

        /// <summary>패턴을 실제로 시작한 뒤 호출 — 지정 대상은 패턴 1회에만 쓴다.</summary>
        public static void ClearPatternTarget(StateTreeContext InContext)
        {
            InContext.Blackboard.SetObject((int)EAIBlackboard.PatternTarget, null);
        }
    }
}
