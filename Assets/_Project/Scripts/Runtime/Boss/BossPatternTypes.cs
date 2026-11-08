using System;
using DemoLA.Combat;
using DemoLA.Core;
using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>xlsx 목록 시트 '분류'.</summary>
    public enum EPatternCategory
    {
        NormalAttack = 0,
        Gimmick = 1,
        StaggerCheck = 2,
        Counter = 3,
        PartyGimmick = 4,
        PhaseTransition = 5,
        FieldHazard = 6,
    }

    /// <summary>xlsx 목록 시트 '필요 대응'.</summary>
    public enum EPatternResponse
    {
        None = 0,
        Dodge = 1,
        Stagger = 2,
        Counter = 3,
        PartyGimmick = 4,
        Tank = 5,
        Move = 6,
    }

    /// <summary>xlsx 목록 시트 '피해 등급(가정)' — 최대 체력 대비 비율은 DamageTierUtil.</summary>
    public enum EDamageTier
    {
        None = 0,
        Weak = 1,
        Normal = 2,
        Big = 3,
        VeryBig = 4,
        Lethal = 5,
    }

    /// <summary>xlsx 목록 시트 '신뢰도'.</summary>
    public enum EEvidenceConfidence
    {
        VideoMeasured = 0,
        PublicGuide = 1,
        Estimated = 2,
    }

    public enum EPatternTargeting
    {
        /// <summary>보스 위치 · 보스 정면.</summary>
        BossForward = 0,
        /// <summary>어그로 대상 위치.</summary>
        AggroTarget = 1,
        /// <summary>무작위 플레이어 1명.</summary>
        RandomPlayer = 2,
        /// <summary>살아 있는 플레이어 전원 (각자 발밑).</summary>
        EachPlayer = 3,
        /// <summary>맵 중앙.</summary>
        ArenaCenter = 4,
        /// <summary>보스 위치, 어그로 대상 방향.</summary>
        BossTowardAggro = 5,
    }

    public enum EStaggerFailOutcome
    {
        /// <summary>파티 전멸.</summary>
        Wipe = 0,
        /// <summary>전원에게 FailDamageTier 피해.</summary>
        HeavyDamage = 1,
        /// <summary>FailChainPatternId 패턴을 이어서 쓴다 (G1-22).</summary>
        ChainPattern = 2,
    }

    public static class DamageTierUtil
    {
        /// <summary>xlsx 목록 시트 J열 (가정값, 실제 수치 아님).</summary>
        public static float Fraction(EDamageTier InTier)
        {
            switch (InTier)
            {
                case EDamageTier.Weak: return 0.05f;
                case EDamageTier.Normal: return 0.15f;
                case EDamageTier.Big: return 0.35f;
                case EDamageTier.VeryBig: return 0.6f;
                case EDamageTier.Lethal: return 1.0f;
            }

            return 0.0f;
        }

        public static EDamageTier FromKorean(string InText)
        {
            switch ((InText ?? string.Empty).Trim())
            {
                case "약함": return EDamageTier.Weak;
                case "보통": return EDamageTier.Normal;
                case "큰 피해": return EDamageTier.Big;
                case "매우 큰 피해": return EDamageTier.VeryBig;
                case "즉사급": return EDamageTier.Lethal;
            }

            return EDamageTier.None;
        }
    }

    /// <summary>패턴 한 단계: 전조(장판 표시) → 판정 1회.</summary>
    [Serializable]
    public class PatternStep
    {
        public string Label = "Hit";
        [Tooltip("이전 단계 종료 후 대기 (초)")]
        public float StartDelay;
        public EPatternTargeting Targeting = EPatternTargeting.BossTowardAggro;
        public HitArea Area = HitArea.Circle(4.0f);
        [Tooltip("전조 시간 (초). xlsx 에서 미측정 — 자리 표시 값")]
        public float TelegraphTime = 1.2f;
        [Tooltip("전조 중 대상을 따라간다 (추적 장판)")]
        public bool TrackDuringTelegraph;
        [Tooltip("추적 장판이 판정 몇 초 전에 고정되는가")]
        public float TrackLockBefore = 0.5f;
        public bool ShowTelegraph = true;
        public EDamageTier DamageTier = EDamageTier.Normal;
        [Tooltip("0 이상이면 DamageTier 대신 이 비율 사용")]
        public float DamageFractionOverride = -1.0f;
        public ETag ApplyStatus = ETag.None;
        public float StatusDuration = 1.0f;
        [Tooltip("방어 구슬(DeathWard)이 막아 주는 즉사 패턴인가")]
        public bool ConsumesDeathWard;
        [Tooltip("판정 직전에 보스가 정면으로 이동 (돌진)")]
        public float BossMoveDistance;
        public float BossMoveDuration = 0.3f;

        [Tooltip("판정 범위에 닿은 외곽 벽(EStructureKind.Wall)을 무너뜨린다 (휠윈드 · 3연 베기)")]
        public bool BreaksWalls;
        [Tooltip("판정 순간 맵의 모든 기둥 · 벽을 무너뜨린다 (첫 즉사 패턴의 첫 타격). 생명 구슬은 나오지 않는다")]
        public bool BreaksAllStructures;
        [Tooltip("이 단계의 대기(전조) 동안 보스가 제자리에서 도는 속도 (도/초). 휠윈드")]
        public float BossSpinSpeed;

        [Header("도약 (LeapDuration > 0): 장판 중심으로 뛰어올라 착지하며 판정")]
        public float LeapDuration;
        public float LeapHeight = 4.0f;

        [Header("십자 / X 범위 (ArmCount > 0): 중심에서 뻗는 직선 범위")]
        public int ArmCount;
        [Tooltip("중심에서 한쪽 끝까지 길이 (m). 4방향이면 전체 크기 = 2 × 이 값")]
        public float ArmLength = 20.0f;
        public float ArmWidth = 4.0f;
        [Tooltip("0 = 십자(+), 45 = X")]
        public float ArmAngleOffset;
        [Tooltip("켜면 매번 십자 / X 중 무작위")]
        public bool RandomCrossOrX;

        [Header("넉백 (일반 판정): 판정 중심에서 바깥으로 날린다")]
        [Tooltip("일반 판정의 넉백 높이 (m). 돌진 넉백에도 쓴다")]
        public float KnockbackHeight = 1.5f;

        [Header("추적 (ChaseTarget): 다른 대상을 정해 따라가며 판정")]
        public bool ChaseTarget;
        public float ChaseSpeed = 5.0f;

        [Header("돌진 (BossMoveDistance > 0)")]
        [Tooltip("돌진 중 보스 몸에 닿은 대상을 판정한다 (끄면 판정 시점에 장판 전체 즉시 판정)")]
        public bool ChargeSweepHit = true;
        [Tooltip("돌진에 맞은 대상을 돌진 경로 옆으로 밀어내는 거리 (m)")]
        public float KnockbackDistance = 4.0f;
        public float KnockbackDuration = 0.25f;
        [Tooltip("기둥에 부딪히면 보스 그로기 시간 (초). 기믹이 따로 그로기를 걸면 그쪽이 우선")]
        public float CrashGroggyTime = 4.0f;
        [Tooltip("장판 길이를 처음 만나는 기둥까지 잘라서 보여 준다")]
        public bool ClipTelegraphToStructure = true;
        [Tooltip("돌진 직전 보스가 뒤로 움찔하는 시간 (초). 0 이면 없음")]
        public float ChargeWindup = 0.25f;

        public float DamageFraction
        {
            get { return DamageFractionOverride >= 0.0f ? DamageFractionOverride : DamageTierUtil.Fraction(DamageTier); }
        }
    }
}
