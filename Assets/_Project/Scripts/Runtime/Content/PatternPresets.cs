using System.Collections.Generic;
using DemoLA.Boss;
using DemoLA.Combat;
using DemoLA.Core;

namespace DemoLA.Content
{
    /// <summary>
    /// xlsx '비고' 칸의 설명을 실행 단계로 옮긴 기본값. 시간/크기는 전부 자리 표시 값이다 (TimingsMeasured = false).
    /// 비고만으로 동작을 정할 수 없는 패턴은 Implemented = false 로 남기고 구현 보류 사항.md 에 적었다.
    /// </summary>
    public static class PatternPresets
    {
        /// <summary>구현하지 않은 패턴 ID (구현 보류 사항.md 와 일치시킨다).</summary>
        public static readonly HashSet<string> Deferred = new HashSet<string>
        {
            "G1-02", "G1-09", "G1-23", "G2-10", "G2-17",
        };

        public static void Apply(BossPatternDefinition InPattern)
        {
            InPattern.Implemented = Deferred.Contains(InPattern.PatternId) == false;
            switch (InPattern.PatternId)
            {
                // ------------------------------------------------------------ Gate 1
                case "G1-01":
                    // 구슬은 OrbAlternationGimmick 이 처리. 이 패턴은 구슬을 다 모은 뒤의 무력화 체크.
                    noSteps(InPattern);
                    InPattern.StaggerCheck = true;
                    InPattern.StaggerGauge = 1500.0f;
                    InPattern.StaggerTime = 12.0f;
                    InPattern.StaggerFailOutcome = EStaggerFailOutcome.ChainPattern;
                    InPattern.StaggerFailChainPatternId = "G1-22";
                    InPattern.GroggyOnStaggerSuccess = 5.0f;
                    break;
                case "G1-03":
                    steps(InPattern, step(EPatternTargeting.AggroTarget, HitArea.Circle(2.5f), 6.0f, EDamageTier.Normal, track: true, lockBefore: 0.6f));
                    InPattern.RandomSelectable = true;
                    InPattern.Cooldown = 20.0f;
                    break;
                case "G1-04":
                    steps(InPattern, step(EPatternTargeting.RandomPlayer, HitArea.Circle(1.5f), 0.6f, EDamageTier.None, status: ETag.State_Feared, statusTime: 3.0f));
                    InPattern.StaggerCheck = true;
                    InPattern.StaggerGauge = 800.0f;
                    InPattern.StaggerTime = 4.0f;
                    InPattern.StaggerFailOutcome = EStaggerFailOutcome.HeavyDamage;
                    InPattern.StaggerFailDamage = EDamageTier.Lethal;
                    InPattern.RandomSelectable = true;
                    InPattern.Cooldown = 45.0f;
                    InPattern.Weight = 0.5f;
                    break;
                case "G1-05":
                    steps(InPattern, step(EPatternTargeting.BossForward, HitArea.Circle(1.0f), 4.0f, EDamageTier.None, show: false));
                    InPattern.UntargetableDuring = true;
                    InPattern.RandomSelectable = false;
                    break;
                case "G1-06":
                    steps(InPattern,
                        step(EPatternTargeting.AggroTarget, HitArea.Circle(2.5f), 0.8f, EDamageTier.Normal),
                        step(EPatternTargeting.BossForward, HitArea.Circle(4.0f), 0.5f, EDamageTier.Normal, status: ETag.State_Airborne, statusTime: 1.0f));
                    break;
                case "G1-07":
                    steps(InPattern,
                        step(EPatternTargeting.BossTowardAggro, HitArea.Fan(5.0f, 120.0f), 0.6f, EDamageTier.Normal),
                        step(EPatternTargeting.BossTowardAggro, HitArea.Fan(5.0f, 120.0f), 0.45f, EDamageTier.Normal),
                        step(EPatternTargeting.BossTowardAggro, HitArea.Fan(6.0f, 140.0f), 0.6f, EDamageTier.Normal));
                    break;
                case "G1-08":
                    steps(InPattern, step(EPatternTargeting.BossTowardAggro, HitArea.Fan(6.0f, 100.0f), 1.0f, EDamageTier.Big));
                    break;
                case "G1-10":
                    steps(InPattern,
                        step(EPatternTargeting.AggroTarget, HitArea.Circle(3.0f), 1.0f, EDamageTier.Normal),
                        step(EPatternTargeting.BossForward, HitArea.Circle(5.0f), 0.5f, EDamageTier.Normal, status: ETag.State_Airborne, statusTime: 1.0f));
                    break;
                case "G1-11":
                    steps(InPattern, step(EPatternTargeting.BossForward, HitArea.Circle(4.5f), 0.9f, EDamageTier.Normal, status: ETag.State_Airborne, statusTime: 1.0f));
                    break;
                case "G1-12":
                    steps(InPattern, step(EPatternTargeting.EachPlayer, HitArea.Circle(3.0f), 1.5f, EDamageTier.Normal, status: ETag.State_Airborne, statusTime: 1.0f));
                    break;
                case "G1-13":
                    var dash = step(EPatternTargeting.BossTowardAggro, HitArea.Rect(14.0f, 3.0f), 1.2f, EDamageTier.Normal, status: ETag.State_Airborne, statusTime: 1.0f);
                    dash.BossMoveDistance = 12.0f;
                    steps(InPattern, dash);
                    InPattern.MaxRange = 14.0f;
                    break;
                case "G1-14":
                    steps(InPattern, step(EPatternTargeting.BossTowardAggro, HitArea.Rect(12.0f, 2.5f), 1.2f, EDamageTier.Big, status: ETag.State_Grabbed, statusTime: 2.0f));
                    InPattern.MaxRange = 12.0f;
                    break;
                case "G1-15":
                    steps(InPattern,
                        step(EPatternTargeting.BossTowardAggro, HitArea.Fan(10.0f, 30.0f), 1.0f, EDamageTier.Normal),
                        step(EPatternTargeting.BossForward, HitArea.Donut(3.0f, 10.0f), 0.8f, EDamageTier.Normal));
                    InPattern.MaxRange = 10.0f;
                    break;
                case "G1-16":
                    // 기운 색에 따른 4가지 변형(결빙/단타/다단/충격파)은 무작위 변형 기능이 없어 '붉은 단타' 하나만 둔다.
                    steps(InPattern,
                        step(EPatternTargeting.BossForward, HitArea.Circle(4.0f), 0.8f, EDamageTier.Normal),
                        step(EPatternTargeting.EachPlayer, HitArea.Circle(3.0f), 1.2f, EDamageTier.Big));
                    break;
                case "G1-17":
                    steps(InPattern, step(EPatternTargeting.RandomPlayer, HitArea.Circle(5.0f), 3.0f, EDamageTier.Normal, track: true, lockBefore: 0.4f, status: ETag.Debuff_Slow, statusTime: 3.0f));
                    InPattern.MaxRange = 30.0f;
                    break;
                case "G1-18":
                    steps(InPattern, step(EPatternTargeting.EachPlayer, HitArea.Circle(2.5f), 2.0f, EDamageTier.Weak, status: ETag.State_Frozen, statusTime: 2.0f));
                    InPattern.MaxRange = 30.0f;
                    break;
                case "G1-19":
                    steps(InPattern, step(EPatternTargeting.BossForward, HitArea.Circle(8.0f), 3.0f, EDamageTier.Big));
                    break;
                case "G1-20":
                    steps(InPattern,
                        step(EPatternTargeting.RandomPlayer, HitArea.Circle(2.5f), 0.7f, EDamageTier.Normal),
                        step(EPatternTargeting.RandomPlayer, HitArea.Circle(2.5f), 0.5f, EDamageTier.Normal),
                        step(EPatternTargeting.RandomPlayer, HitArea.Circle(2.5f), 0.5f, EDamageTier.Normal));
                    InPattern.MaxRange = 30.0f;
                    break;
                case "G1-21":
                    steps(InPattern, step(EPatternTargeting.RandomPlayer, HitArea.Circle(3.0f), 1.2f, EDamageTier.Normal, status: ETag.State_Airborne, statusTime: 1.0f));
                    InPattern.MaxRange = 30.0f;
                    break;
                case "G1-22":
                    steps(InPattern,
                        step(EPatternTargeting.BossForward, HitArea.Circle(6.0f), 1.0f, EDamageTier.Big),
                        step(EPatternTargeting.EachPlayer, HitArea.Circle(3.0f), 1.0f, EDamageTier.Big),
                        step(EPatternTargeting.BossForward, HitArea.Donut(5.0f, 14.0f), 1.0f, EDamageTier.Big),
                        step(EPatternTargeting.BossTowardAggro, HitArea.Rect(20.0f, 3.0f), 1.0f, EDamageTier.VeryBig));
                    InPattern.RandomSelectable = false;
                    break;

                // ------------------------------------------------------------ Gate 2
                case "G2-01":
                case "G2-02":
                case "G2-05":
                case "G2-06":
                    // 컴포넌트 기믹 (ArmorStack / DeathWardOrb / TerrainCollapse). 패턴 정의는 기록용.
                    noSteps(InPattern);
                    break;
                case "G2-03":
                    var first = step(EPatternTargeting.ArenaCenter, HitArea.Global(), 3.0f, EDamageTier.Lethal);
                    first.ConsumesDeathWard = true;
                    // 첫 타격 순간 맵의 기둥 · 벽이 모두 무너진다.
                    first.BreaksAllStructures = true;
                    var second = step(EPatternTargeting.ArenaCenter, HitArea.Global(), 3.0f, EDamageTier.Lethal);
                    second.ConsumesDeathWard = true;
                    second.StartDelay = 1.0f;
                    steps(InPattern, first, second);
                    InPattern.RandomSelectable = false;
                    break;
                case "G2-04":
                    steps(InPattern, step(EPatternTargeting.RandomPlayer, HitArea.Fan(12.0f, 40.0f), 3.0f, EDamageTier.Lethal, track: true, lockBefore: 0.8f));
                    InPattern.MaxRange = 40.0f;
                    InPattern.Cooldown = 30.0f;
                    InPattern.Weight = 0.5f;
                    break;
                case "G2-07":
                    var charge = step(EPatternTargeting.BossTowardAggro, HitArea.Rect(14.0f, 4.0f), 1.8f, EDamageTier.Lethal, status: ETag.State_Grabbed, statusTime: 2.0f);
                    charge.BossMoveDistance = 12.0f;
                    steps(InPattern, charge);
                    InPattern.Counterable = true;
                    InPattern.CounterWindowStart = 0.6f;
                    InPattern.CounterWindowEnd = 1.7f;
                    InPattern.RandomSelectable = false;
                    break;
                case "G2-08":
                    steps(InPattern, step(EPatternTargeting.BossForward, HitArea.Circle(7.0f), 1.5f, EDamageTier.Big));
                    InPattern.Counterable = true;
                    InPattern.CounterWindowStart = 0.7f;
                    InPattern.CounterWindowEnd = 1.45f;
                    InPattern.CounterRepeat = 3;
                    InPattern.FailEscalationFraction = 0.35f;
                    InPattern.RandomSelectable = true;
                    InPattern.Cooldown = 40.0f;
                    InPattern.MaxRange = 7.0f;
                    break;
                case "G2-09":
                    var list = new List<PatternStep>();
                    list.Add(step(EPatternTargeting.BossForward, HitArea.Circle(6.0f), 1.2f, EDamageTier.Big));
                    for (int i = 0; i < 5; ++i)
                    {
                        list.Add(step(EPatternTargeting.EachPlayer, HitArea.Circle(2.5f), 0.7f, EDamageTier.Normal));
                    }

                    steps(InPattern, list.ToArray());
                    InPattern.RandomSelectable = false;
                    break;
                case "G2-11":
                    // 전조 2.0초: 1.7초 동안 어그로 대상을 따라 방향을 틀고, 마지막 0.3초는 고정 → 바로 0.55초 동안 14m 돌진.
                    var bossCharge = step(EPatternTargeting.BossTowardAggro, HitArea.Rect(16.0f, 3.5f), 2.0f, EDamageTier.Normal,
                        track: true, lockBefore: 0.3f, status: ETag.State_Airborne, statusTime: 1.0f);
                    bossCharge.BossMoveDistance = 14.0f;
                    bossCharge.BossMoveDuration = 0.55f;
                    bossCharge.ChargeWindup = 0.0f;
                    bossCharge.KnockbackDistance = 6.0f;
                    bossCharge.KnockbackHeight = 2.0f;
                    steps(InPattern, bossCharge);
                    InPattern.MaxRange = 16.0f;
                    InPattern.Weight = 2.0f;
                    break;
                case "G2-21":
                    // 휠윈드: 장판은 처음 1.2초 예고 한 번만 보여 준다. 이후 보스가 계속 돌면서 0.45초마다 4회 더 판정(장판 없음).
                    // 범위에 닿은 외곽 벽이 무너진다.
                    var spinList = new List<PatternStep>();
                    var spinStart = step(EPatternTargeting.BossForward, HitArea.Circle(5.5f), 1.2f, EDamageTier.Normal);
                    spinStart.BreaksWalls = true;
                    spinList.Add(spinStart);
                    for (int i = 0; i < 4; ++i)
                    {
                        var spin = step(EPatternTargeting.BossForward, HitArea.Circle(5.5f), 0.45f, EDamageTier.Weak, show: false);
                        spin.BreaksWalls = true;
                        spin.BossSpinSpeed = 800.0f;
                        spinList.Add(spin);
                    }

                    steps(InPattern, spinList.ToArray());
                    InPattern.Cooldown = 18.0f;
                    InPattern.Recovery = 1.5f;
                    break;
                case "G2-22":
                    // 3연 베기: 대상을 향한 부채꼴 2회 → 정면 직선 내려베기. 장판 없음 (보스 동작을 보고 피한다).
                    // 범위에 닿은 외곽 벽이 무너진다.
                    var slash1 = step(EPatternTargeting.BossTowardAggro, HitArea.Fan(7.0f, 120.0f), 1.0f, EDamageTier.Normal, track: true, lockBefore: 0.4f, show: false);
                    var slash2 = step(EPatternTargeting.BossTowardAggro, HitArea.Fan(7.0f, 120.0f), 0.6f, EDamageTier.Normal, track: true, lockBefore: 0.3f, show: false);
                    var slash3 = step(EPatternTargeting.BossTowardAggro, HitArea.Rect(10.0f, 3.0f), 0.8f, EDamageTier.Big, status: ETag.State_Airborne, statusTime: 1.0f, show: false);
                    slash1.BreaksWalls = true;
                    slash2.BreaksWalls = true;
                    slash3.BreaksWalls = true;
                    steps(InPattern, slash1, slash2, slash3);
                    InPattern.Cooldown = 14.0f;
                    InPattern.Recovery = 1.5f;
                    break;
                case "G2-23":
                    // 도약 강타 → 추적 휠윈드.
                    // 1) 어그로 대상 위치에 원(5m) + 십자 또는 X(중심에서 20m씩, 전체 40m) 장판. 1.6초 중 마지막 0.5초는 고정.
                    //    보스가 그 중심으로 0.6초 동안 뛰어올라 착지하며 판정 → 맞으면 바깥으로 날아간다.
                    var leap = step(EPatternTargeting.AggroTarget, HitArea.Circle(5.0f), 1.6f, EDamageTier.Big, track: true, lockBefore: 0.5f,
                        status: ETag.State_Airborne, statusTime: 0.8f);
                    leap.LeapDuration = 0.6f;
                    leap.LeapHeight = 5.0f;
                    leap.ArmCount = 4;
                    leap.ArmLength = 20.0f;
                    leap.ArmWidth = 4.0f;
                    leap.RandomCrossOrX = true;
                    leap.KnockbackDistance = 7.0f;
                    leap.KnockbackHeight = 2.5f;
                    leap.BreaksWalls = true;
                    var chaseList = new List<PatternStep> { leap };
                    // 2) 다른 대상을 정해(발밑 파란 원) 3초 동안 휠윈드로 따라간다. 0.5초마다 판정 (장판 없음).
                    for (int i = 0; i < 5; ++i)
                    {
                        var spin = step(EPatternTargeting.BossForward, HitArea.Circle(3.5f), 0.5f, EDamageTier.Weak, track: true, lockBefore: 0.0f, show: false);
                        spin.ChaseTarget = true;
                        spin.ChaseSpeed = 5.5f;
                        spin.BossSpinSpeed = 900.0f;
                        spin.BreaksWalls = true;
                        if (i == 0)
                        {
                            spin.StartDelay = 0.5f;
                        }

                        chaseList.Add(spin);
                    }

                    // 3) 마지막 한 바퀴: 넓은 원 판정 (짧게 장판 표시) → 맞으면 크게 날아간다.
                    var finale = step(EPatternTargeting.BossForward, HitArea.Circle(5.0f), 0.5f, EDamageTier.Big, track: true, lockBefore: 0.0f,
                        status: ETag.State_Airborne, statusTime: 1.0f);
                    finale.ChaseTarget = true;
                    finale.ChaseSpeed = 5.5f;
                    finale.BossSpinSpeed = 1200.0f;
                    finale.KnockbackDistance = 9.0f;
                    finale.KnockbackHeight = 3.0f;
                    finale.BreaksWalls = true;
                    chaseList.Add(finale);
                    steps(InPattern, chaseList.ToArray());
                    InPattern.Cooldown = 30.0f;
                    InPattern.Recovery = 1.5f;
                    InPattern.MaxRange = 40.0f;
                    InPattern.Weight = 1.5f;
                    break;
                case "G2-12":
                    steps(InPattern,
                        step(EPatternTargeting.BossTowardAggro, HitArea.Rect(16.0f, 2.0f), 1.0f, EDamageTier.Normal),
                        step(EPatternTargeting.AggroTarget, HitArea.Circle(2.5f), 4.0f, EDamageTier.Normal, track: true, lockBefore: 0.5f));
                    break;
                case "G2-13":
                    steps(InPattern,
                        step(EPatternTargeting.BossTowardAggro, HitArea.Fan(6.0f, 150.0f), 0.7f, EDamageTier.Normal),
                        step(EPatternTargeting.BossTowardAggro, HitArea.Fan(6.0f, 150.0f), 0.5f, EDamageTier.Normal),
                        step(EPatternTargeting.BossTowardAggro, HitArea.Rect(7.0f, 2.5f), 0.7f, EDamageTier.Normal, status: ETag.State_Airborne, statusTime: 1.0f),
                        step(EPatternTargeting.BossForward, HitArea.Circle(7.0f), 0.6f, EDamageTier.Normal),
                        step(EPatternTargeting.BossForward, HitArea.Circle(7.0f), 0.6f, EDamageTier.Normal));
                    break;
                case "G2-14":
                    var drops = new List<PatternStep>();
                    for (int i = 0; i < 8; ++i)
                    {
                        drops.Add(step(EPatternTargeting.RandomPlayer, HitArea.Circle(3.0f), 0.6f, EDamageTier.Normal));
                    }

                    drops.Add(step(EPatternTargeting.BossForward, HitArea.Circle(10.0f), 1.5f, EDamageTier.VeryBig));
                    steps(InPattern, drops.ToArray());
                    InPattern.Cooldown = 35.0f;
                    InPattern.MaxRange = 30.0f;
                    break;
                case "G2-15":
                    // 안/밖 무작위 변형 미구현 — '안쪽 안전(바깥 판정)' 변형만.
                    steps(InPattern, step(EPatternTargeting.BossForward, HitArea.Donut(3.0f, 12.0f), 1.5f, EDamageTier.Big));
                    break;
                case "G2-16":
                    steps(InPattern, step(EPatternTargeting.RandomPlayer, HitArea.Fan(14.0f, 35.0f), 2.0f, EDamageTier.Normal, track: true, lockBefore: 0.6f));
                    InPattern.MaxRange = 30.0f;
                    break;
                case "G2-18":
                    steps(InPattern,
                        step(EPatternTargeting.RandomPlayer, HitArea.Fan(12.0f, 40.0f), 2.0f, EDamageTier.Normal, track: true, lockBefore: 0.6f),
                        step(EPatternTargeting.AggroTarget, HitArea.Circle(4.0f), 0.8f, EDamageTier.Big));
                    InPattern.RandomSelectable = false;
                    break;
                case "G2-19":
                    steps(InPattern, step(EPatternTargeting.BossForward, HitArea.Circle(8.0f), 8.0f, EDamageTier.None));
                    InPattern.StaggerCheck = true;
                    InPattern.StaggerGauge = 1200.0f;
                    InPattern.StaggerTime = 8.0f;
                    InPattern.StaggerFailOutcome = EStaggerFailOutcome.HeavyDamage;
                    InPattern.StaggerFailDamage = EDamageTier.VeryBig;
                    InPattern.RandomSelectable = true;
                    InPattern.Cooldown = 50.0f;
                    InPattern.Weight = 0.4f;
                    break;
                case "G2-20":
                    steps(InPattern, step(EPatternTargeting.BossTowardAggro, HitArea.Fan(8.0f, 120.0f), 1.5f, EDamageTier.Lethal, status: ETag.State_Grabbed, statusTime: 2.0f));
                    InPattern.Cooldown = 25.0f;
                    break;
            }

            if (InPattern.Implemented == false)
            {
                InPattern.RandomSelectable = false;
            }
        }

        private static PatternStep step(EPatternTargeting InTargeting, HitArea InArea, float InTelegraph, EDamageTier InTier,
            bool track = false, float lockBefore = 0.5f, ETag status = ETag.None, float statusTime = 1.0f, bool show = true)
        {
            var step = new PatternStep();
            step.Targeting = InTargeting;
            step.Area = InArea;
            step.TelegraphTime = InTelegraph;
            step.DamageTier = InTier;
            step.TrackDuringTelegraph = track;
            step.TrackLockBefore = lockBefore;
            step.ApplyStatus = status;
            step.StatusDuration = statusTime;
            step.ShowTelegraph = show;
            return step;
        }

        private static void steps(BossPatternDefinition InPattern, params PatternStep[] InSteps)
        {
            InPattern.Steps.Clear();
            InPattern.Steps.AddRange(InSteps);
            float reach = 0.0f;
            for (int i = 0; i < InSteps.Length; ++i)
            {
                if (InSteps[i].Targeting == EPatternTargeting.BossForward || InSteps[i].Targeting == EPatternTargeting.BossTowardAggro)
                {
                    reach = UnityEngine.Mathf.Max(reach, InSteps[i].Area.Reach);
                }
            }

            if (reach > 0.0f && reach < 900.0f)
            {
                InPattern.MaxRange = UnityEngine.Mathf.Max(InPattern.MaxRange, reach);
            }
        }

        private static void noSteps(BossPatternDefinition InPattern)
        {
            InPattern.Steps.Clear();
            InPattern.RandomSelectable = false;
        }
    }
}
