using System.Collections.Generic;
using DemoLA.Core;
using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// 보스 패턴 1개 (pattern_analysis_valtan.xlsx 의 패턴목록 한 행 + 실행 단계).
    /// 에디터에서 언제든 바꿀 수 있는 모듈 단위이며, BossPatternAbility 가 실행하고 StateTree 의 UsePatternTask 가 고른다.
    /// 시간(전조/판정/후딜)과 판정 크기는 xlsx 에서 비어 있어 자리 표시 값이다 → TimingsMeasured = false.
    /// </summary>
    [CreateAssetMenu(menuName = "DemoLA/Boss/Pattern Definition", fileName = "PT_New")]
    public class BossPatternDefinition : ScriptableObject
    {
        [Header("xlsx 패턴목록")]
        public string PatternId = "G1-01";
        public string PhaseId = "G1-P1";
        public string DisplayName = "Pattern";
        public EPatternCategory Category;
        public EPatternResponse Response;
        public string ShapeText;
        public string LinkedPatternId;
        public EEvidenceConfidence Confidence = EEvidenceConfidence.PublicGuide;
        public EDamageTier DamageTier;
        [TextArea(2, 6)] public string Notes;
        [Tooltip("전조/판정/후딜 시간을 영상에서 측정해 넣었는가")]
        public bool TimingsMeasured;
        [Tooltip("구현 완료 여부 (xlsx 구현 상태 칼럼과 맞춘다)")]
        public bool Implemented;

        [Header("선택 (StateTree UseRandomPatternTask)")]
        public float Cooldown = 10.0f;
        public float Weight = 1.0f;
        public float MinRange;
        public float MaxRange = 12.0f;
        [Tooltip("false 면 랜덤 선택에서 제외 (기믹/전환 전용)")]
        public bool RandomSelectable = true;

        [Tooltip("(대상 선택) 이 패턴이 노릴 대상을 정하는 규칙. 비어 있으면 현재 어그로 대상. 도발 중에는 무시된다.\n" +
                 "단계(Steps)의 Targeting 이 AggroTarget / BossTowardAggro 인 단계가 이 대상을 쓴다.\n" +
                 "툴: DemoLA/Tools/GAS Tool (Ability 탭 → 대상)")]
        [SerializeReference, SubclassPicker] public PatternTargetSelector TargetSelector;

        [Header("실행")]
        public List<PatternStep> Steps = new List<PatternStep>();
        public float Recovery = 1.0f;
        public bool SuperArmor = true;
        public bool UntargetableDuring;
        [Tooltip("패턴 동안 경기장 중앙 기준 도넛 안전지대(파란색)를 보여 준다 (SafeZoneInner ~ SafeZoneOuter m)")]
        public bool ShowSafeZone;
        public float SafeZoneInner = 24.5f;
        public float SafeZoneOuter = 30.0f;
        [Tooltip("도약할 때 Cinemachine 카메라가 보스를 비춘다 (재도약 · 천상 낙하)")]
        public bool CinematicLeap;
        [Tooltip("이 패턴이 끝나면 다음 패턴은 돌진 (StateTree ChargeDueCondition 이 바로 참) — 부위파괴 반격 착지 후 돌진")]
        public bool ChargeNextAfter;
        [Tooltip("\"돌진 뒤 다른 패턴 N개\" 세기에서 뺀다 (재도약 · 반격처럼 규칙이 강제로 끼워 넣는 패턴)")]
        public bool IgnoreInPatternCount;

        [Header("카운터 (패턴 공통) — 구간은 단계(Steps)마다 Counterable 로 정한다")]
        [Tooltip("(이전 방식) 단계에 카운터가 하나도 없을 때만 쓰는 패턴 단위 구간. 새 패턴은 단계의 Counterable 을 쓴다")]
        public bool Counterable;
        [Tooltip("회차 시작(첫 단계의 StartDelay 포함) 기준 카운터 가능 구간 (초). 이 동안 보스에 Boss_CounterWindow 태그")]
        public float CounterWindowStart = 0.3f;
        public float CounterWindowEnd = 1.0f;
        public float GroggyOnCounter = 3.0f;
        [Tooltip("연속 카운터 횟수 (3연 카운터 = 3). 각 회차가 Steps 전체를 반복한다")]
        public int CounterRepeat = 1;
        [Tooltip("카운터 실패 회차마다 피해 비율 증가")]
        public float FailEscalationFraction = 0.2f;
        [Tooltip("GAS Tool 에서 편집했다 → 실행 데이터(단계 · 카운터 · 대상 · 쿨다운 등)는 이 에셋이 원본. CSV 재가져오기 · 전체 셋업은 CSV 메타데이터(이름 · 분류 · 메모)만 갱신한다")]
        [UnityEngine.Serialization.FormerlySerializedAs("CounterEdited")]
        public bool ToolEdited;

        /// <summary>돌진 패턴인가 (보스가 실제로 달려가며 몸으로 판정하는 단계가 있다).</summary>
        public bool IsCharge
        {
            get
            {
                for (int i = 0; i < Steps.Count; ++i)
                {
                    if (Steps[i] != null && Steps[i].BossMoveDistance > 0.0f && Steps[i].ChargeSweepHit == true)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>카운터가 가능한 패턴인가 (단계 중 하나라도, 또는 이전 방식 패턴 단위).</summary>
        public bool IsCounterable
        {
            get { return Counterable == true || HasStepCounter == true; }
        }

        /// <summary>단계 단위 카운터가 하나라도 있으면 패턴 단위 구간(Counterable/CounterWindow*)은 쓰지 않는다.</summary>
        public bool HasStepCounter
        {
            get
            {
                for (int i = 0; i < Steps.Count; ++i)
                {
                    if (Steps[i] != null && Steps[i].Counterable == true)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        [Header("무력화 체크")]
        public bool StaggerCheck;
        public float StaggerGauge = 1000.0f;
        public float StaggerTime = 10.0f;
        public EStaggerFailOutcome StaggerFailOutcome = EStaggerFailOutcome.Wipe;
        public EDamageTier StaggerFailDamage = EDamageTier.Lethal;
        public string StaggerFailChainPatternId;
        public float GroggyOnStaggerSuccess = 5.0f;

        /// <summary>회차 1번의 길이 (초): 단계마다 StartDelay + 전조 + 도약 + 돌진. 카운터 구간 · 타임라인 표시에 쓴다.</summary>
        public float RepeatDuration()
        {
            float t = 0.0f;
            for (int i = 0; i < Steps.Count; ++i)
            {
                float hit;
                float end;
                StepTiming(i, ref t, out hit, out end);
            }

            return t;
        }

        /// <summary>InStepIndex 단계: InOutCursor(이전 단계 끝)에서 시작해 판정 시각 · 끝 시각을 계산하고 커서를 끝으로 옮긴다. 반환 = 전조 시작.</summary>
        public float StepTiming(int InStepIndex, ref float InOutCursor, out float OutHit, out float OutEnd)
        {
            var step = Steps[InStepIndex];
            float telegraphStart = InOutCursor + Mathf.Max(0.0f, step.StartDelay);
            OutHit = telegraphStart + Mathf.Max(0.01f, step.TelegraphTime) + Mathf.Max(0.0f, step.LeapDuration);
            OutEnd = OutHit;
            if (step.BossMoveDistance > 0.0f && step.ChargeSweepHit == true)
            {
                OutHit += Mathf.Max(0.0f, step.ChargeWindup);
                OutEnd = OutHit + Mathf.Max(0.0f, step.BossMoveDuration);
            }

            InOutCursor = OutEnd;
            return telegraphStart;
        }
    }
}
