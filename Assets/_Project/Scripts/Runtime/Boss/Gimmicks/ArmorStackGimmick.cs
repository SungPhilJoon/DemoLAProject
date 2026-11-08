using DemoLA.Attributes;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Effects;
using DemoLA.World;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// G2-01 갑옷 부위파괴 (돌진 유도).
    /// 시작 시 갑옷(피해 감소) 중첩. 돌진 패턴(ChargePatternId)이 기둥을 부수면(ActorMotor 충돌 → ArenaStructure.BrokenByActor) 보스 그로기,
    /// 그로기 중 부위 파괴 수치가 DestructionPerStack 쌓일 때마다 중첩 1 제거. 중첩 0 이면 성공.
    /// 이 기믹은 패시브처럼 상시 동작한다 (Begin 이후 성공할 때까지).
    /// 파괴 폭탄(배틀 아이템)은 미구현 — 구현 보류 사항.md.
    /// </summary>
    public class ArmorStackGimmick : BossGimmick
    {
        public int Stacks = 2;
        [Tooltip("중첩당 받는 피해 감소 % — 자리 표시 값")]
        public float ReductionPerStack = 40.0f;
        public float DestructionPerStack = 3.0f;
        public string ChargePatternId = "G2-11";
        public float GroggyTime = 6.0f;

        private int remaining;
        private int armorHandle;
        private bool tracking;
        private float destructionAtGroggyStart;
        private GameplayEffectDef[] stackDefs;

        public int RemainingStacks
        {
            get { return remaining; }
        }

        public override string StatusText
        {
            get { return State == EGimmickState.Running ? $"갑옷 {remaining}중첩 — 돌진을 구조물로 유도" : string.Empty; }
        }

        protected override void OnBegin()
        {
            remaining = Stacks;
            stackDefs = new GameplayEffectDef[Stacks + 1];
            applyArmor();
            Boss.Asc.OnAbilityActivated += handleAbilityActivated;
            Boss.Asc.OnAbilityEnded += handleAbilityEnded;
            ArenaStructure.BrokenByActor += handleStructureBroken;
        }

        protected override void OnTick(float InDeltaTime)
        {
            if (Boss.HasTag((int)ETag.State_Groggy) == false)
            {
                return;
            }

            float accumulated = Boss.Stats.GetBaseValue(EAttr.DestructionAccum) - destructionAtGroggyStart;
            if (accumulated >= DestructionPerStack)
            {
                destructionAtGroggyStart += DestructionPerStack;
                remaining -= 1;
                Raid.Message($"부위 파괴! 갑옷 {remaining}중첩 남음", 2.0f, false);
                applyArmor();
                if (remaining <= 0)
                {
                    Complete(true);
                }
            }
        }

        private void handleAbilityActivated(GameplayAbility InAbility)
        {
            var pattern = InAbility as BossPatternAbility;
            if (pattern != null && pattern.Definition.PatternId == ChargePatternId)
            {
                tracking = true;
            }
        }

        private void handleAbilityEnded(GameplayAbility InAbility, bool InCancelled)
        {
            var pattern = InAbility as BossPatternAbility;
            if (pattern != null && pattern.Definition.PatternId == ChargePatternId)
            {
                tracking = false;
            }
        }

        private void handleStructureBroken(ArenaStructure InStructure, CombatActor InBreaker)
        {
            if (tracking == false || InBreaker != Boss || InStructure.Kind != EStructureKind.LifePillar)
            {
                return;
            }

            tracking = false;
            Raid.Message("보스가 구조물에 충돌 — 부위 파괴하세요!", 2.5f, false);
            destructionAtGroggyStart = Boss.Stats.GetBaseValue(EAttr.DestructionAccum);
            if (Boss.Motor != null)
            {
                Boss.Motor.Stop();
            }

            Boss.ApplyGroggy(GroggyTime);
            var cue = new GameplayCueParams();
            cue.Target = Boss.Asc;
            Boss.Asc.SendGameplayEvent((int)ETag.Event_StructureCollision, cue);
        }

        private void applyArmor()
        {
            if (armorHandle != 0)
            {
                Boss.RemoveEffect(armorHandle);
                armorHandle = 0;
            }

            if (remaining <= 0)
            {
                return;
            }

            if (stackDefs[remaining] == null)
            {
                stackDefs[remaining] = EffectBuilder.Infinite()
                    .Add(EAttr.IncomingDamageReduction, Mathf.Min(95.0f, ReductionPerStack * remaining))
                    .Grant(ETag.Buff_Armor).Build();
            }

            armorHandle = Boss.ApplyEffectToSelf(stackDefs[remaining], 0);
        }

        protected override void OnFinished(bool InSucceeded)
        {
            unbind();
        }

        protected override void OnReset()
        {
            unbind();
            if (armorHandle != 0 && Boss != null)
            {
                Boss.RemoveEffect(armorHandle);
            }

            armorHandle = 0;
        }

        private void unbind()
        {
            tracking = false;
            ArenaStructure.BrokenByActor -= handleStructureBroken;
            if (Boss != null && Boss.Asc != null)
            {
                Boss.Asc.OnAbilityActivated -= handleAbilityActivated;
                Boss.Asc.OnAbilityEnded -= handleAbilityEnded;
            }
        }
    }
}
