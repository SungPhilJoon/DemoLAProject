using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Boss.Telegraph;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Effects;
using DemoLA.World;
using Framework;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// BossPatternDefinition 하나를 실행하는 GameplayAbility.
    /// 흐름: (반복 회차마다) 단계별 [대기 → 전조 장판 → 판정] → 후딜.
    ///  - Counterable: 회차마다 카운터 구간 동안 Boss_CounterWindow 태그. Event_Countered 를 받으면 패턴 취소 + 무력화.
    ///  - StaggerCheck: 시작 시 무력화 게이지를 채우고 StaggerTime 안에 0 이 되면 성공(무력화), 아니면 실패 결과.
    /// </summary>
    public class BossPatternAbility : GameplayAbility
    {
        public readonly BossPatternDefinition Definition;

        /// <summary>(판정, 원점, 정면, 전조 시간) 장판 하나가 시작될 때. 네트워크 서버가 클라이언트에 장판을 복제하는 데 쓴다.</summary>
        public static event System.Action<HitArea, Vector3, Vector3, float> TelegraphStarted;

        private readonly List<TelegraphView> views = new List<TelegraphView>();
        private readonly List<CombatActor> trackedTargets = new List<CombatActor>();
        private readonly List<CombatActor> buffer = new List<CombatActor>();
        private readonly List<CombatActor> players = new List<CombatActor>();
        private CombatActor primaryTarget;
        private int stepIndex;
        private int repeatIndex;
        private int failCount;
        private bool counteredThisRepeat;
        private bool counterWindowOpen;
        private bool staggerActive;

        // 십자 / X: views 와 같은 순서로 (주인 대상 번호, 회전각 — 주 장판은 NaN)
        private readonly List<int> viewOwners = new List<int>();
        private readonly List<float> viewAngles = new List<float>();

        // 추적 휠윈드
        private CombatActor chaseTarget;
        private TelegraphView chaseMarker;

        // 돌진
        private readonly HashSet<CombatActor> chargeHits = new HashSet<CombatActor>();
        private PatternStep chargeStep;
        private Vector3 chargeDirection;
        private float chargeFraction;
        private bool charging;

        public BossPatternAbility(BossPatternDefinition InDefinition, int InAbilityId)
        {
            Definition = InDefinition;
            AbilityId = InAbilityId;
            AbilityTags.Add((int)ETag.Ability_BossPattern);
            ActivationOwnedTags.Add((int)ETag.Boss_PatternBusy);
            if (InDefinition.SuperArmor == true)
            {
                ActivationOwnedTags.Add((int)ETag.State_SuperArmor);
            }

            if (InDefinition.UntargetableDuring == true)
            {
                ActivationOwnedTags.Add((int)ETag.Boss_Untargetable);
            }

            BlockAbilitiesWithTags.Add((int)ETag.Ability_BossPattern);
            var blocked = GameTags.IncapacitatedTags;
            for (int i = 0; i < blocked.Length; ++i)
            {
                ActivationBlockedTags.Add((int)blocked[i]);
            }

            CooldownEffect = GameEffects.Cooldown;
            CooldownDuration = InDefinition.Cooldown;
        }

        public BossActor Boss
        {
            get { return Owner != null ? Owner.OwnerActor as BossActor : null; }
        }

        public bool IsCounterWindowOpen
        {
            get { return counterWindowOpen; }
        }

        protected override void OnActivate(AbilityActivationInfo InInfo)
        {
            var boss = Boss;
            if (boss == null || CommitAbility() == false)
            {
                EndAbility(true);
                return;
            }

            primaryTarget = InInfo.Target != null ? InInfo.Target.OwnerActor as CombatActor : null;
            if (primaryTarget == null || primaryTarget.IsDead == true)
            {
                primaryTarget = boss.AggroTarget;
            }

            if (boss.Motor != null)
            {
                boss.Motor.Stop();
            }

            if (primaryTarget != null)
            {
                boss.FaceTowards(primaryTarget.Position);
            }

            repeatIndex = 0;
            failCount = 0;
            Owner.OnGameplayEvent += handleGameplayEvent;
            boss.Announce(Definition, Definition.Response);

            if (Definition.StaggerCheck == true)
            {
                beginStaggerCheck();
            }

            runRepeat();
        }

        protected override void OnEnd(bool InWasCancelled)
        {
            Owner.OnGameplayEvent -= handleGameplayEvent;
            endCharge(true);
            endChase();
            closeCounterWindow();
            if (staggerActive == true)
            {
                staggerActive = false;
                Owner.Tags.RemoveTag((int)ETag.Boss_StaggerCheck);
            }

            despawnViews();
            var boss = Boss;
            if (boss != null)
            {
                boss.NotifyPatternEnded(this, InWasCancelled);
            }
        }

        // ------------------------------------------------------------------ steps

        private void runRepeat()
        {
            stepIndex = 0;
            counteredThisRepeat = false;
            if (Definition.Counterable == true)
            {
                AddTask(new WaitDelayTask(Definition.CounterWindowStart, openCounterWindow));
                AddTask(new WaitDelayTask(Definition.CounterWindowEnd, () =>
                {
                    if (counterWindowOpen == true && counteredThisRepeat == false)
                    {
                        failCount += 1;
                    }

                    closeCounterWindow();
                }));
            }

            runStep();
        }

        private void runStep()
        {
            if (IsActive == false)
            {
                return;
            }

            if (stepIndex >= Definition.Steps.Count)
            {
                onRepeatFinished();
                return;
            }

            var step = Definition.Steps[stepIndex];
            if (step.ChaseTarget == false)
            {
                endChase();
            }

            if (step.StartDelay > 0.0f)
            {
                AddTask(new WaitDelayTask(step.StartDelay, () => beginTelegraph(step)));
            }
            else
            {
                beginTelegraph(step);
            }
        }

        private void beginTelegraph(PatternStep InStep)
        {
            var boss = Boss;
            if (boss == null || IsActive == false)
            {
                return;
            }

            despawnViews();
            trackedTargets.Clear();
            resolveTargets(InStep, boss);
            if (InStep.ChaseTarget == true && chaseTarget == null)
            {
                beginChase(boss);
            }

            float armOffset = InStep.RandomCrossOrX == true ? (GameRandom.Value < 0.5f ? 0.0f : 45.0f) : InStep.ArmAngleOffset;
            var armArea = HitArea.Rect(InStep.ArmLength, InStep.ArmWidth);
            for (int i = 0; i < trackedTargets.Count; ++i)
            {
                Vector3 origin;
                Vector3 forward;
                placement(InStep, boss, trackedTargets[i], out origin, out forward);
                spawnView(InStep, boss, InStep.Area, origin, forward, i, float.NaN);
                for (int a = 0; a < InStep.ArmCount; ++a)
                {
                    float angle = armOffset + 360.0f / InStep.ArmCount * a;
                    spawnView(InStep, boss, armArea, origin, Quaternion.AngleAxis(angle, Vector3.up) * forward, i, angle);
                }
            }

            float duration = Mathf.Max(0.01f, InStep.TelegraphTime);
            AddTask(new WaitDurationTask(duration, (InRatio, InDelta) =>
            {
                if (InStep.BossSpinSpeed != 0.0f)
                {
                    boss.transform.Rotate(0.0f, InStep.BossSpinSpeed * InDelta, 0.0f, Space.World);
                }

                if (InStep.ChaseTarget == true)
                {
                    tickChase(InStep, boss, InDelta);
                }

                float remaining = duration * (1.0f - InRatio);
                bool tracking = InStep.TrackDuringTelegraph == true && remaining > InStep.TrackLockBefore;
                for (int i = 0; i < views.Count; ++i)
                {
                    int owner = i < viewOwners.Count ? viewOwners[i] : i;
                    if (tracking == true && owner < trackedTargets.Count)
                    {
                        Vector3 origin;
                        Vector3 forward;
                        placement(InStep, boss, trackedTargets[owner], out origin, out forward);
                        float angle = i < viewAngles.Count ? viewAngles[i] : float.NaN;
                        views[i].Place(origin, float.IsNaN(angle) ? forward : Quaternion.AngleAxis(angle, Vector3.up) * forward);
                        if (float.IsNaN(angle) == true)
                        {
                            clipChargeTelegraph(InStep, boss, views[i]);
                        }
                    }

                    views[i].SetProgress(InRatio);
                }
            }, () => resolveHit(InStep)));
        }

        private void spawnView(PatternStep InStep, BossActor InBoss, in HitArea InArea, Vector3 InOrigin, Vector3 InForward, int InOwner, float InAngle)
        {
            var view = TelegraphView.Spawn(InArea, InOrigin, InForward);
            if (float.IsNaN(InAngle) == true)
            {
                clipChargeTelegraph(InStep, InBoss, view);
            }

            if (InStep.ShowTelegraph == true)
            {
                TelegraphStarted?.Invoke(InArea, InOrigin, InForward, InStep.TelegraphTime);
            }
            else
            {
                view.gameObject.SetActive(false);
            }

            views.Add(view);
            viewOwners.Add(InOwner);
            viewAngles.Add(InAngle);
        }

        // ------------------------------------------------------------------ chase (추적 휠윈드)

        /// <summary>추적할 대상: 이 패턴의 첫 대상(도약 대상)이 아닌 다른 플레이어 중 무작위.</summary>
        private void beginChase(BossActor InBoss)
        {
            CombatRegistry.FindTeam(ETeam.Player, players);
            var candidates = players.FindAll(p => p != primaryTarget);
            chaseTarget = candidates.Count > 0 ? candidates[GameRandom.Range(0, candidates.Count)] : primaryTarget;
            if (chaseTarget == null)
            {
                return;
            }

            chaseMarker = TelegraphView.Spawn(HitArea.Circle(1.4f), chaseTarget.Position, Vector3.forward, true);
            Raid.Message($"{chaseTarget.GetName()} 추적!", 2.0f, true);
        }

        private void tickChase(PatternStep InStep, BossActor InBoss, float InDelta)
        {
            if (chaseTarget == null || chaseTarget.IsDead == true || InBoss.Motor == null)
            {
                return;
            }

            var delta = HitGeometry.Flat(chaseTarget.Position - InBoss.Position);
            float gap = delta.magnitude - (InBoss.Radius + chaseTarget.Radius) * 0.6f;
            if (gap > 0.0f)
            {
                InBoss.Motor.Nudge(delta.normalized * Mathf.Min(gap, InStep.ChaseSpeed * InDelta));
            }

            if (chaseMarker != null)
            {
                chaseMarker.Place(chaseTarget.Position, Vector3.forward);
            }
        }

        private void endChase()
        {
            chaseTarget = null;
            if (chaseMarker != null)
            {
                chaseMarker.Despawn();
                chaseMarker = null;
            }
        }

        private void resolveHit(PatternStep InStep)
        {
            var boss = Boss;
            if (boss == null || IsActive == false)
            {
                return;
            }

            if (InStep.LeapDuration > 0.0f && boss.Motor != null && views.Count > 0)
            {
                // 장판 중심으로 뛰어올라 착지하는 순간 판정한다 (장판은 착지까지 남겨 둔다).
                boss.Motor.Leap(views[0].Origin, InStep.LeapDuration, InStep.LeapHeight);
                AddTask(new WaitDelayTask(InStep.LeapDuration, () =>
                {
                    DemoLA.Player.QuarterViewCamera.Shake(0.7f, 0.5f);
                    applyStepHit(InStep);
                }));
                return;
            }

            applyStepHit(InStep);
        }

        private void applyStepHit(PatternStep InStep)
        {
            var boss = Boss;
            if (boss == null || IsActive == false)
            {
                return;
            }

            float fraction = InStep.DamageFraction + Definition.FailEscalationFraction * failCount * (Definition.Counterable == true ? 1.0f : 0.0f);
            if (InStep.BossMoveDistance > 0.0f && boss.Motor != null)
            {
                if (InStep.ChargeSweepHit == true)
                {
                    beginCharge(InStep, fraction);
                    return;
                }

                boss.Motor.Dash(boss.Forward, InStep.BossMoveDistance, InStep.BossMoveDuration);
            }

            var hitOnce = new HashSet<CombatActor>();
            for (int i = 0; i < views.Count; ++i)
            {
                var view = views[i];
                CombatRegistry.FindHostiles(boss, view.Area, view.Origin, view.ForwardDirection, buffer);
                for (int t = 0; t < buffer.Count; ++t)
                {
                    if (hitOnce.Add(buffer[t]) == true)
                    {
                        applyHit(buffer[t], InStep, fraction);
                        knockback(buffer[t], InStep, view.Origin);
                    }
                }
            }

            if (InStep.BreaksWalls == true)
            {
                breakWalls(InStep, boss);
            }

            if (InStep.BreaksAllStructures == true)
            {
                breakAllStructures();
            }

            flashViews();
            stepIndex += 1;
            runStep();
        }

        /// <summary>맵의 남은 기둥 · 벽을 전부 무너뜨린다 (구슬 없음).</summary>
        private static void breakAllStructures()
        {
            var structures = ArenaStructure.Structures;
            bool any = false;
            for (int i = structures.Count - 1; i >= 0; --i)
            {
                if (structures[i].IsDestroyed == false)
                {
                    structures[i].Break();
                    any = true;
                }
            }

            if (any == true)
            {
                DemoLA.Player.QuarterViewCamera.Shake(0.8f, 0.7f);
            }
        }

        /// <summary>이 단계의 장판(판정 범위)에 닿은 벽을 무너뜨린다.</summary>
        private void breakWalls(PatternStep InStep, BossActor InBoss)
        {
            var structures = ArenaStructure.Structures;
            for (int v = 0; v < views.Count; ++v)
            {
                var view = views[v];
                for (int i = structures.Count - 1; i >= 0; --i)
                {
                    var structure = structures[i];
                    if (structure.Kind != EStructureKind.Wall || structure.IsDestroyed == true)
                    {
                        continue;
                    }

                    if (structure.Overlaps(InStep.Area, view.Origin, view.ForwardDirection) == true)
                    {
                        structure.Break(InBoss);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ charge

        /// <summary>
        /// 돌진: 장판 방향 고정 → (움찔) → 보스가 실제로 달려가며 몸에 닿는 대상을 판정한다.
        /// 맞은 대상은 경로 바깥쪽으로 밀려난다. 기둥에 부딪히면 ActorMotor 가 멈추고 기둥을 부순다.
        /// </summary>
        private void beginCharge(PatternStep InStep, float InFraction)
        {
            var boss = Boss;
            chargeStep = InStep;
            chargeFraction = InFraction;
            chargeDirection = HitGeometry.Flat(views.Count > 0 ? views[0].ForwardDirection : boss.Forward).normalized;
            boss.FaceTowards(boss.Position + chargeDirection);
            flashViews();

            if (InStep.ChargeWindup > 0.0f)
            {
                AddTask(new WaitDelayTask(InStep.ChargeWindup, startDash));
            }
            else
            {
                startDash();
            }
        }

        private void startDash()
        {
            var boss = Boss;
            if (boss == null || IsActive == false)
            {
                return;
            }

            chargeHits.Clear();
            charging = true;
            boss.Motor.OnDashCrashed += handleDashCrashed;
            boss.Motor.Dash(chargeDirection, chargeStep.BossMoveDistance, chargeStep.BossMoveDuration);
            AddTask(new WaitDurationTask(chargeStep.BossMoveDuration + 0.02f, (InRatio, InDelta) => sweepCharge(), () =>
            {
                if (charging == true)
                {
                    sweepCharge();
                    endCharge(false);
                }

                if (IsActive == false)
                {
                    return;
                }

                stepIndex += 1;
                runStep();
            }));
        }

        private void sweepCharge()
        {
            var boss = Boss;
            if (boss == null || charging == false)
            {
                return;
            }

            float radius = Mathf.Max(boss.Radius, chargeStep.Area.Width * 0.5f);
            CombatRegistry.FindHostiles(boss, HitArea.Circle(radius), boss.Position, chargeDirection, buffer);
            for (int i = 0; i < buffer.Count; ++i)
            {
                var target = buffer[i];
                if (chargeHits.Add(target) == false)
                {
                    continue;
                }

                applyHit(target, chargeStep, chargeFraction);
                if (chargeStep.KnockbackDistance > 0.0f && target.IsDead == false && target.Motor != null)
                {
                    // 돌진선 기준 대상이 있는 쪽으로, 약간 앞으로 밀어낸다.
                    var toTarget = HitGeometry.Flat(target.Position - boss.Position);
                    var right = new Vector3(chargeDirection.z, 0.0f, -chargeDirection.x);
                    float sign = Vector3.Dot(toTarget, right) >= 0.0f ? 1.0f : -1.0f;
                    var push = (right * sign + chargeDirection * 0.5f).normalized;
                    target.Motor.Knockback(push, chargeStep.KnockbackDistance, chargeStep.KnockbackDuration, chargeStep.KnockbackHeight);
                }
            }
        }

        private void handleDashCrashed(ArenaStructure InStructure)
        {
            var boss = Boss;
            sweepCharge();
            endCharge(false);
            if (boss == null || IsActive == false)
            {
                return;
            }

            // 벽에 막힌 돌진은 그냥 멈춘다 (그로기는 생명 구슬 기둥에 부딪혔을 때만).
            if (InStructure != null && InStructure.Kind == EStructureKind.Wall)
            {
                return;
            }

            // 기믹(갑옷 등)이 이미 그로기를 걸었으면 패턴은 그쪽에서 취소된다.
            if (boss.HasTag((int)ETag.State_Groggy) == true || chargeStep.CrashGroggyTime <= 0.0f)
            {
                return;
            }

            Raid.Message("보스가 기둥에 충돌!", 2.0f, false);
            boss.ApplyGroggy(chargeStep.CrashGroggyTime);
        }

        private void endCharge(bool InCancelDash)
        {
            if (charging == false)
            {
                return;
            }

            charging = false;
            var boss = Boss;
            if (boss != null && boss.Motor != null)
            {
                boss.Motor.OnDashCrashed -= handleDashCrashed;
                if (InCancelDash == true)
                {
                    boss.Motor.CancelDash();
                }
            }
        }

        /// <summary>돌진 장판을 처음 만나는 기둥까지 잘라서 "여기서 부딪힌다" 를 보여 준다.</summary>
        private static void clipChargeTelegraph(PatternStep InStep, BossActor InBoss, TelegraphView InView)
        {
            if (InStep.BossMoveDistance <= 0.0f || InStep.ClipTelegraphToStructure == false || InStep.Area.Shape != EHitShape.Rect)
            {
                return;
            }

            float length = InStep.Area.Length;
            var origin = HitGeometry.Flat(InView.Origin);
            var forward = HitGeometry.Flat(InView.ForwardDirection).normalized;
            var structures = ArenaStructure.Structures;
            for (int i = 0; i < structures.Count; ++i)
            {
                var structure = structures[i];
                if (structure.IsDestroyed == true)
                {
                    continue;
                }

                var delta = HitGeometry.Flat(structure.transform.position) - origin;
                float along = Vector3.Dot(delta, forward);
                float side = (delta - forward * along).magnitude;
                if (along > 0.0f && side <= structure.Radius + InBoss.Radius)
                {
                    length = Mathf.Min(length, along);
                }
            }

            InView.SetLengthScale(length / Mathf.Max(0.01f, InStep.Area.Length));
        }

        private void applyHit(CombatActor InTarget, PatternStep InStep, float InFraction)
        {
            if (InFraction > 0.0f)
            {
                var spec = Owner.MakeOutgoingSpec(GameEffects.BossPatternDamage, AbilityId);
                spec.SetByCaller((int)ESetByCaller.MaxHealthFraction, InFraction);
                spec.SetByCaller((int)ESetByCaller.ConsumesDeathWard, InStep.ConsumesDeathWard == true ? 1.0f : 0.0f);
                spec.HitPoint = InTarget.Position;
                Owner.ApplyGameplayEffectSpecToTarget(spec, InTarget.Asc);
            }

            if (InStep.ApplyStatus != ETag.None && InTarget.IsDead == false)
            {
                InTarget.ApplyStatus(InStep.ApplyStatus, InStep.StatusDuration, Owner);
            }
        }

        /// <summary>일반 판정 넉백: 판정 중심에서 바깥으로 날린다 (돌진은 sweepCharge 가 따로 한다).</summary>
        private static void knockback(CombatActor InTarget, PatternStep InStep, Vector3 InCenter)
        {
            if (InStep.KnockbackDistance <= 0.0f || InStep.BossMoveDistance > 0.0f || InTarget.IsDead == true || InTarget.Motor == null)
            {
                return;
            }

            if (InTarget.HasTag((int)ETag.State_SuperArmor) == true || InTarget.HasTag((int)ETag.State_Invulnerable) == true)
            {
                return;
            }

            var away = HitGeometry.Flat(InTarget.Position - InCenter);
            InTarget.Motor.Knockback(away.sqrMagnitude > 0.01f ? away : -InTarget.Forward, InStep.KnockbackDistance, 0.45f, InStep.KnockbackHeight);
        }

        private void onRepeatFinished()
        {
            repeatIndex += 1;
            if (Definition.Counterable == true && repeatIndex < Mathf.Max(1, Definition.CounterRepeat))
            {
                runRepeat();
                return;
            }

            if (staggerActive == true)
            {
                // 무력화 체크가 끝날 때까지 기다린다 (beginStaggerCheck 의 태스크가 종료시킨다).
                return;
            }

            AddTask(new WaitDelayTask(Definition.Recovery, () => EndAbility(false)));
        }

        // ------------------------------------------------------------------ counter

        private void openCounterWindow()
        {
            if (counterWindowOpen == true || IsActive == false)
            {
                return;
            }

            counterWindowOpen = true;
            Owner.Tags.AddTag((int)ETag.Boss_CounterWindow);
        }

        private void closeCounterWindow()
        {
            if (counterWindowOpen == false)
            {
                return;
            }

            counterWindowOpen = false;
            Owner.Tags.RemoveTag((int)ETag.Boss_CounterWindow);
        }

        private void handleGameplayEvent(int InEventTag, GameplayCueParams InPayload)
        {
            if (InEventTag != (int)ETag.Event_Countered || counterWindowOpen == false)
            {
                return;
            }

            counteredThisRepeat = true;
            closeCounterWindow();
            var boss = Boss;
            Raid.Message($"카운터 성공! ({Definition.DisplayName})", 2.0f, false);
            EndAbility(true);
            if (boss != null)
            {
                boss.ApplyGroggy(Definition.GroggyOnCounter);
            }
        }

        // ------------------------------------------------------------------ stagger

        private void beginStaggerCheck()
        {
            staggerActive = true;
            var stats = Boss.Stats;
            float gauge = Definition.StaggerGauge * Boss.PartyScale;
            stats.Set(EAttr.MaxStaggerGauge, gauge);
            stats.Set(EAttr.StaggerGauge, gauge);
            Owner.Tags.AddTag((int)ETag.Boss_StaggerCheck);
            Raid.Message($"무력화하세요! ({Definition.DisplayName})", 2.5f, true);

            AddTask(new WaitDurationTask(Definition.StaggerTime, (InRatio, InDelta) =>
            {
                if (staggerActive == true && stats.Get(EAttr.StaggerGauge) <= 0.0f)
                {
                    onStaggerSucceeded();
                }
            }, () =>
            {
                if (staggerActive == true)
                {
                    onStaggerFailed();
                }
            }));
        }

        private void onStaggerSucceeded()
        {
            staggerActive = false;
            Owner.Tags.RemoveTag((int)ETag.Boss_StaggerCheck);
            var boss = Boss;
            var cue = new GameplayCueParams();
            cue.Target = Owner;
            cue.Location = boss.Position;
            Owner.SendGameplayEvent((int)ETag.Event_StaggerBroken, cue);
            GameplayCueManager.Execute((int)ECue.StaggerBroken, cue);
            Raid.Message("무력화 성공!", 2.0f, false);
            EndAbility(true);
            boss.ApplyGroggy(Definition.GroggyOnStaggerSuccess);
        }

        private void onStaggerFailed()
        {
            staggerActive = false;
            Owner.Tags.RemoveTag((int)ETag.Boss_StaggerCheck);
            var boss = Boss;
            switch (Definition.StaggerFailOutcome)
            {
                case EStaggerFailOutcome.Wipe:
                    Raid.Message("무력화 실패 — 전멸", 3.0f, true);
                    Raid.WipeParty(boss, Definition.DisplayName);
                    break;
                case EStaggerFailOutcome.HeavyDamage:
                    Raid.Message("무력화 실패", 2.0f, true);
                    CombatRegistry.FindTeam(ETeam.Player, players);
                    var step = new PatternStep();
                    for (int i = 0; i < players.Count; ++i)
                    {
                        applyHit(players[i], step, DamageTierUtil.Fraction(Definition.StaggerFailDamage));
                    }

                    break;
                case EStaggerFailOutcome.ChainPattern:
                    Raid.Message("무력화 실패 — 연쇄 공격", 2.0f, true);
                    boss.QueuePattern(Definition.StaggerFailChainPatternId);
                    break;
            }

            EndAbility(false);
        }

        // ------------------------------------------------------------------ targeting

        private void resolveTargets(PatternStep InStep, BossActor InBoss)
        {
            switch (InStep.Targeting)
            {
                case EPatternTargeting.EachPlayer:
                    CombatRegistry.FindTeam(ETeam.Player, players);
                    trackedTargets.AddRange(players);
                    if (trackedTargets.Count == 0)
                    {
                        trackedTargets.Add(null);
                    }

                    break;
                case EPatternTargeting.RandomPlayer:
                    CombatRegistry.FindTeam(ETeam.Player, players);
                    trackedTargets.Add(players.Count > 0 ? players[GameRandom.Range(0, players.Count)] : null);
                    break;
                default:
                    trackedTargets.Add(primaryTarget);
                    break;
            }
        }

        private static void placement(PatternStep InStep, BossActor InBoss, CombatActor InTarget, out Vector3 OutOrigin, out Vector3 OutForward)
        {
            var bossPosition = InBoss.Position;
            var targetPosition = InTarget != null ? InTarget.Position : bossPosition + InBoss.Forward * 3.0f;
            var toTarget = HitGeometry.Flat(targetPosition - bossPosition);
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                toTarget = InBoss.Forward;
            }

            switch (InStep.Targeting)
            {
                case EPatternTargeting.BossForward:
                    OutOrigin = bossPosition;
                    OutForward = InBoss.Forward;
                    return;
                case EPatternTargeting.BossTowardAggro:
                    InBoss.FaceTowards(targetPosition);
                    OutOrigin = bossPosition;
                    OutForward = toTarget;
                    return;
                case EPatternTargeting.ArenaCenter:
                    OutOrigin = ArenaFloor.Current != null ? ArenaFloor.Current.Center : Vector3.zero;
                    OutForward = Vector3.forward;
                    return;
                default:
                    OutOrigin = targetPosition;
                    OutForward = toTarget;
                    return;
            }
        }

        /// <summary>판정 순간 장판을 번쩍이고 사라지게 한다.</summary>
        private void flashViews()
        {
            viewOwners.Clear();
            viewAngles.Clear();
            for (int i = 0; i < views.Count; ++i)
            {
                if (views[i] == null)
                {
                    continue;
                }

                if (views[i].gameObject.activeSelf == true)
                {
                    views[i].FlashAndDespawn();
                }
                else
                {
                    views[i].Despawn();
                }
            }

            views.Clear();
        }

        private void despawnViews()
        {
            viewOwners.Clear();
            viewAngles.Clear();
            for (int i = 0; i < views.Count; ++i)
            {
                if (views[i] != null)
                {
                    views[i].Despawn();
                }
            }

            views.Clear();
        }
    }
}
