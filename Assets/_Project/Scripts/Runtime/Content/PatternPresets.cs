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

        /// <summary>천상 낙하(G2-25) 안전 띠 폭: 캐릭터 크기(지름 약 1m)의 1.5배.</summary>
        public const float SkyfallSafeBand = 1.5f;

        /// <summary>
        /// 천상 낙하 즉사 반경 = 다닐 수 있는 맵 가장자리(외곽 벽 링 안쪽 면) − 안전 띠.
        /// 벽 링 바깥 바닥은 벽 틈이 좁아 걸어서 갈 수 없으므로 "외곽 변두리" 는 벽 바로 안쪽 띠다. 벽이 부서진 뒤에는 바깥도 안전.
        /// </summary>
        public const float SkyfallLethalRadius = DemoLA.World.ArenaBuilder.WallRingRadius - DemoLA.World.ArenaBuilder.WallThickness * 0.5f - SkyfallSafeBand;

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
                    counter(charge, 0.6f, 1.7f);
                    steps(InPattern, charge);
                    InPattern.RandomSelectable = false;
                    break;
                case "G2-08":
                    var tripleCounter = step(EPatternTargeting.BossForward, HitArea.Circle(7.0f), 1.5f, EDamageTier.Big);
                    counter(tripleCounter, 0.7f, 1.45f);
                    steps(InPattern, tripleCounter);
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
                case "G2-24":
                    // 도끼 찍기 3연: 앞(찍은 곳 원형) → 뒤(같은 공격) → 앞 강타. 마지막 타격만 카운터 가능.
                    var frontSlam = step(EPatternTargeting.BossTowardAggro, HitArea.Circle(3.5f), 1.0f, EDamageTier.Normal);
                    frontSlam.Label = "앞 찍기";
                    frontSlam.ForwardOffset = 4.5f;
                    frontSlam.KnockbackDistance = 0.0f;
                    var backSlam = step(EPatternTargeting.BossForward, HitArea.Circle(3.5f), 0.9f, EDamageTier.Normal);
                    backSlam.Label = "뒤 찍기";
                    backSlam.StartDelay = 0.25f;
                    backSlam.ForwardOffset = -4.5f;
                    backSlam.KnockbackDistance = 0.0f;
                    var counterSlam = step(EPatternTargeting.BossForward, HitArea.Circle(4.5f), 1.6f, EDamageTier.Big, status: ETag.State_Airborne, statusTime: 1.0f);
                    counterSlam.Label = "앞 강타 (카운터)";
                    counterSlam.StartDelay = 0.35f;
                    counterSlam.ForwardOffset = 5.0f;
                    counterSlam.KnockbackDistance = 3.0f;
                    counter(counterSlam, 0.55f, 1.4f);
                    steps(InPattern, frontSlam, backSlam, counterSlam);
                    InPattern.GroggyOnCounter = 3.5f;
                    InPattern.MaxRange = 9.0f;
                    InPattern.Cooldown = 18.0f;
                    InPattern.Weight = 1.5f;
                    InPattern.RandomSelectable = true;
                    break;
                case "G2-25":
                    // 부위파괴 반격 (갑옷 1중첩이 깨질 때마다 ArmorStackGimmick 이 바로 쓴다).
                    // 0초: 경기장 중앙으로 10초 도약 시작 → 1~8초: 1초마다 모든 플레이어 발밑 폭발 → 10초: 착지와 함께
                    // 외곽 띠(캐릭터 크기 1.5배 ≈ 1.5m)를 뺀 맵 전체 즉사 + 큰 충격파. 패턴 내내 외곽 안전지대(파란 도넛) 표시.
                    var liftOff = step(EPatternTargeting.ArenaCenter, HitArea.Circle(1.0f), 0.05f, EDamageTier.None, show: false);
                    liftOff.Label = "도약 (10초)";
                    liftOff.StartLeapDuration = 10.0f;
                    liftOff.LeapHeight = 22.0f;
                    liftOff.KnockbackDistance = 0.0f;
                    var skyfall = new List<PatternStep> { liftOff };
                    for (int i = 0; i < 8; ++i)
                    {
                        var burst = step(EPatternTargeting.EachPlayer, HitArea.Circle(2.5f), 1.0f, EDamageTier.Normal);
                        burst.Label = $"추적 폭발 {i + 1}";
                        burst.KnockbackDistance = 0.0f;
                        skyfall.Add(burst);
                    }

                    var crash = step(EPatternTargeting.ArenaCenter, HitArea.Circle(SkyfallLethalRadius), 1.95f, EDamageTier.Lethal);
                    crash.Label = "천상 낙하 (즉사)";
                    crash.KnockbackDistance = 0.0f;
                    crash.BigImpact = true;
                    skyfall.Add(crash);
                    steps(InPattern, skyfall.ToArray());
                    InPattern.ShowSafeZone = true;
                    InPattern.SafeZoneInner = SkyfallLethalRadius;
                    InPattern.SafeZoneOuter = DemoLA.World.ArenaBuilder.Radius + 4.0f;
                    InPattern.UntargetableDuring = true;
                    InPattern.SuperArmor = true;
                    InPattern.ChargeNextAfter = true;
                    InPattern.IgnoreInPatternCount = true;
                    InPattern.RandomSelectable = false;
                    InPattern.Cooldown = 0.1f;
                    InPattern.Recovery = 1.0f;
                    break;
                case "G2-26":
                    // 135줄 이후 패턴이 끝날 때마다: 어그로 대상 위치로 크게 도약 (Cinemachine 시네마틱) + 착지 충격파.
                    var reLeap = step(EPatternTargeting.AggroTarget, HitArea.Circle(4.0f), 0.7f, EDamageTier.Weak, track: true, lockBefore: 0.3f);
                    reLeap.Label = "재도약";
                    reLeap.LeapDuration = 1.4f;
                    reLeap.LeapHeight = 12.0f;
                    reLeap.KnockbackDistance = 3.0f;
                    reLeap.BigImpact = true;
                    steps(InPattern, reLeap);
                    InPattern.CinematicLeap = true;
                    InPattern.IgnoreInPatternCount = true;
                    InPattern.RandomSelectable = false;
                    InPattern.Cooldown = 0.1f;
                    InPattern.Recovery = 0.5f;
                    InPattern.MaxRange = 99.0f;
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

        /// <summary>이 타격의 전조 중 카운터 가능 구간 (전조 시작 기준 초).</summary>
        private static void counter(PatternStep InStep, float InStart, float InEnd)
        {
            InStep.Counterable = true;
            InStep.CounterWindowStart = InStart;
            InStep.CounterWindowEnd = InEnd;
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
