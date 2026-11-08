using System.Collections.Generic;
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

        [Header("실행")]
        public List<PatternStep> Steps = new List<PatternStep>();
        public float Recovery = 1.0f;
        public bool SuperArmor = true;
        public bool UntargetableDuring;

        [Header("카운터")]
        public bool Counterable;
        [Tooltip("첫 단계 전조 시작 기준 카운터 가능 구간")]
        public float CounterWindowStart = 0.3f;
        public float CounterWindowEnd = 1.0f;
        public float GroggyOnCounter = 3.0f;
        [Tooltip("연속 카운터 횟수 (3연 카운터 = 3). 각 회차가 Steps 전체를 반복한다")]
        public int CounterRepeat = 1;
        [Tooltip("카운터 실패 회차마다 피해 비율 증가")]
        public float FailEscalationFraction = 0.2f;

        [Header("무력화 체크")]
        public bool StaggerCheck;
        public float StaggerGauge = 1000.0f;
        public float StaggerTime = 10.0f;
        public EStaggerFailOutcome StaggerFailOutcome = EStaggerFailOutcome.Wipe;
        public EDamageTier StaggerFailDamage = EDamageTier.Lethal;
        public string StaggerFailChainPatternId;
        public float GroggyOnStaggerSuccess = 5.0f;
    }
}
