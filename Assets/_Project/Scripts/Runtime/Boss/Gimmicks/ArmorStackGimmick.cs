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
    /// G2-01 갑옷 부위파괴 (돌진 유도) — 135줄 전에 갑옷 2중첩을 부위파괴로 벗긴다.
    ///  - 갑옷: 중첩당 받는 피해 감소.
    ///  - 부위파괴 수치(DestructionAccum)는 평소에도 쌓이지만, 돌진(G2-11 · G2-07)이 벽이나 기둥에 부딪혀 걸린 그로기 동안에는
    ///    CrashWeaknessMultiplier 배로 쌓인다 (= 부위파괴 계수가 낮아짐 → 그로기 때 노려야 쉽다).
    ///  - 누적이 DestructionPerStack 에 이르면 갑옷 1중첩 파괴 → 그로기를 풀고 즉시 반격 패턴(SkyfallPatternId: 10초 도약 →
    ///    1초마다 플레이어 위치 폭발 → 외곽 띠를 뺀 전체 즉사 낙하)을 쓴다. 반격이 끝나면(착지) 다음 패턴은 돌진 (ChargeNextAfter).
    ///  - 보스 체력이 LockHpLine 줄 이하가 되면 더 이상 부위파괴를 할 수 없다 (남은 갑옷은 그대로).
    /// 이 기믹은 전투 시작부터 패시브처럼 동작한다 (Begin 이후 끝날 때까지).
    /// </summary>
    public class ArmorStackGimmick : BossGimmick
    {
        public int Stacks = 2;
        [Tooltip("중첩당 받는 피해 감소 % — 자리 표시 값")]
        public float ReductionPerStack = 40.0f;
        [Tooltip("갑옷 1중첩을 벗기는 데 필요한 부위파괴 수치 (평소 기준)")]
        public float DestructionPerStack = 15.0f;
        [Tooltip("돌진 충돌 그로기 동안 부위파괴 수치 배율 (부위파괴 계수 감소)")]
        public float CrashWeaknessMultiplier = 2.5f;
        [Tooltip("돌진이 벽 · 기둥에 부딪혔을 때 그로기 (초)")]
        public float GroggyTime = 6.0f;
        public string SkyfallPatternId = "G2-25";
        [Tooltip("이 체력 줄 이하에서는 부위파괴 불가")]
        public float LockHpLine = 135.0f;

        private int remaining;
        private int armorHandle;
        private bool charging;
        private bool crashGroggy;
        private float progress;
        private float lastDestruction;
        private GameplayEffectDef[] stackDefs;

        public int RemainingStacks
        {
            get { return remaining; }
        }

        /// <summary>현재 중첩의 부위파괴 진행도 0~1.</summary>
        public float Progress
        {
            get { return DestructionPerStack > 0.0f ? Mathf.Clamp01(progress / DestructionPerStack) : 0.0f; }
        }

        /// <summary>돌진 충돌 그로기 중 (부위파괴 계수 감소 상태).</summary>
        public bool IsWeakened
        {
            get { return crashGroggy; }
        }

        public override string StatusText
        {
            get
            {
                if (State != EGimmickState.Running)
                {
                    return string.Empty;
                }

                string weak = crashGroggy ? $" · 충돌 그로기: 부위파괴 ×{CrashWeaknessMultiplier:0.#}" : " — 돌진을 벽 · 기둥으로 유도";
                return $"갑옷 {remaining}중첩 · 부위파괴 {Progress * 100.0f:0}%{weak}";
            }
        }

        protected override void OnBegin()
        {
            remaining = Stacks;
            progress = 0.0f;
            crashGroggy = false;
            lastDestruction = Boss.Stats.GetBaseValue(EAttr.DestructionAccum);
            stackDefs = new GameplayEffectDef[Stacks + 1];
            applyArmor();
            Boss.Asc.OnAbilityActivated += handleAbilityActivated;
            Boss.Asc.OnAbilityEnded += handleAbilityEnded;
            if (Boss.Motor != null)
            {
                Boss.Motor.OnDashCrashed += handleDashCrashed;
            }
        }

        protected override void OnTick(float InDeltaTime)
        {
            if (Boss.HpLinesExact <= LockHpLine)
            {
                Raid.Message($"{LockHpLine:0}줄 — 더 이상 부위파괴를 할 수 없습니다", 2.5f, true);
                Complete(remaining <= 0);
                return;
            }

            if (crashGroggy == true && Boss.HasTag((int)ETag.State_Groggy) == false)
            {
                crashGroggy = false;
            }

            float destruction = Boss.Stats.GetBaseValue(EAttr.DestructionAccum);
            float gained = destruction - lastDestruction;
            lastDestruction = destruction;
            if (gained <= 0.0f)
            {
                return;
            }

            progress += gained * (crashGroggy == true ? CrashWeaknessMultiplier : 1.0f);
            if (progress >= DestructionPerStack)
            {
                breakStack();
            }
        }

        /// <summary>갑옷 1중첩 파괴 → 그로기 해제 → 반격 패턴 (10초 도약 · 전체 낙하).</summary>
        private void breakStack()
        {
            progress = 0.0f;
            crashGroggy = false;
            remaining -= 1;
            applyArmor();
            Raid.Message(remaining > 0 ? $"부위 파괴! 갑옷 {remaining}중첩 남음 — 보스가 도약합니다" : "부위 파괴! 갑옷이 모두 벗겨졌습니다 — 보스가 도약합니다", 2.5f, false);
            Boss.Asc.RemoveEffectsWithGrantedTags(GameTags.Container(ETag.State_Groggy));
            // 진행 중인 패턴을 끊고 바로 반격한다.
            Boss.Asc.CancelAbilitiesWithTags(GameTags.Container(ETag.Ability_BossPattern), null);
            if (Boss.TryActivatePattern(SkyfallPatternId, null) == false)
            {
                Boss.QueuePattern(SkyfallPatternId);
            }

            if (remaining <= 0)
            {
                Complete(true);
            }
        }

        private void handleAbilityActivated(GameplayAbility InAbility)
        {
            var pattern = InAbility as BossPatternAbility;
            if (pattern != null && pattern.Definition.IsCharge == true)
            {
                charging = true;
            }
        }

        private void handleAbilityEnded(GameplayAbility InAbility, bool InCancelled)
        {
            var pattern = InAbility as BossPatternAbility;
            if (pattern != null && pattern.Definition.IsCharge == true)
            {
                charging = false;
            }
        }

        private void handleDashCrashed(ArenaStructure InStructure)
        {
            NotifyChargeCrashed(InStructure);
        }

        /// <summary>돌진이 벽이나 기둥에 부딪힘 → 그로기 + 부위파괴 계수 감소 (ActorMotor.OnDashCrashed · 테스트).</summary>
        public void NotifyChargeCrashed(ArenaStructure InStructure)
        {
            if (charging == false || InStructure == null)
            {
                return;
            }

            charging = false;
            crashGroggy = true;
            Raid.Message($"보스가 {(InStructure.Kind == EStructureKind.Wall ? "벽" : "기둥")}에 충돌 — 지금 부위파괴하세요! (×{CrashWeaknessMultiplier:0.#})", 2.5f, false);
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
            charging = false;
            crashGroggy = false;
            if (Boss != null && Boss.Motor != null)
            {
                Boss.Motor.OnDashCrashed -= handleDashCrashed;
            }

            if (Boss != null && Boss.Asc != null)
            {
                Boss.Asc.OnAbilityActivated -= handleAbilityActivated;
                Boss.Asc.OnAbilityEnded -= handleAbilityEnded;
            }
        }
    }
}
