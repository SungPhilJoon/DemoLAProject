using System.Collections.Generic;
using DemoLA.Boss;
using DemoLA.Combat;
using DemoLA.Data;
using UnityEngine;

namespace DemoLA.Content
{
    /// <summary>
    /// 보스 패턴/페이즈 테이블 행 → 정의 객체.
    /// xlsx 의 한국어 드롭다운 값을 enum 으로 바꾸고, 판정 모양에 맞는 기본 단계를 만든다.
    /// 실제 동작은 PatternPresets 가 덮어쓰고, 행에 측정값(TelegraphTime/Recovery/AreaScale)이 있으면 마지막에 그 값을 쓴다.
    /// </summary>
    public static class BossPatternCsv
    {
        public static BossPatternDefinition CreatePattern(BossPatternRow InRow)
        {
            var pattern = ScriptableObject.CreateInstance<BossPatternDefinition>();
            pattern.name = "PT_" + InRow.PatternId;
            pattern.PatternId = InRow.PatternId;
            pattern.PhaseId = InRow.PhaseId;
            pattern.DisplayName = InRow.Name;
            pattern.Category = ParseCategory(InRow.Category);
            pattern.Response = ParseResponse(InRow.Response);
            pattern.ShapeText = InRow.Shape;
            pattern.LinkedPatternId = InRow.LinkedPatternId;
            pattern.Confidence = ParseConfidence(InRow.Confidence);
            pattern.DamageTier = DamageTierUtil.FromKorean(InRow.DamageTier);
            pattern.Notes = string.IsNullOrEmpty(InRow.DamageText) == true ? InRow.Notes : $"[피해] {InRow.DamageText}\n{InRow.Notes}";
            pattern.TimingsMeasured = false;

            var step = new PatternStep();
            step.Label = InRow.Name;
            step.Area = DefaultArea(InRow.Shape);
            // 피해 표현이 없는 일반 공격은 '보통' 으로 가정한다 (구현 보류 사항.md).
            step.DamageTier = pattern.DamageTier != EDamageTier.None ? pattern.DamageTier : EDamageTier.Normal;
            step.Targeting = InRow.Shape == "대상 지정" ? EPatternTargeting.RandomPlayer : EPatternTargeting.BossTowardAggro;
            pattern.Steps.Add(step);
            pattern.MaxRange = Mathf.Max(6.0f, step.Area.Reach);
            pattern.RandomSelectable = pattern.Category == EPatternCategory.NormalAttack || pattern.Category == EPatternCategory.FieldHazard;
            return pattern;
        }

        /// <summary>프리셋 적용 후 호출. 행에 적힌 측정값으로 전조/후딜/판정 크기를 덮어쓴다.</summary>
        public static void ApplyMeasured(BossPatternDefinition InPattern, BossPatternRow InRow)
        {
            if (InRow.TelegraphTime >= 0.0f && InPattern.Steps.Count > 0)
            {
                // 첫 단계 전조 시간을 기준으로 나머지 단계도 같은 비율로 맞춘다.
                float scale = InRow.TelegraphTime / Mathf.Max(0.01f, InPattern.Steps[0].TelegraphTime);
                for (int i = 0; i < InPattern.Steps.Count; ++i)
                {
                    InPattern.Steps[i].TelegraphTime *= scale;
                }
            }

            if (InRow.Recovery >= 0.0f)
            {
                InPattern.Recovery = InRow.Recovery;
            }

            if (InRow.AreaScale > 0.0f)
            {
                for (int i = 0; i < InPattern.Steps.Count; ++i)
                {
                    InPattern.Steps[i].Area = InPattern.Steps[i].Area.Scaled(InRow.AreaScale);
                }
            }

            InPattern.TimingsMeasured = InRow.Measured;
        }

        public static BossPhaseDefinition CreatePhase(BossPhaseRow InRow)
        {
            var phase = new BossPhaseDefinition();
            phase.PhaseId = InRow.PhaseId;
            // 원작 고유 명칭을 피하려고 화면 표시명은 페이즈 ID 기반으로 쓴다 (xlsx 이름은 조건 메모에 보관).
            phase.DisplayName = PhaseDisplayName(InRow.PhaseId);
            phase.EnterConditionText = $"{InRow.Name} / 진입: {InRow.EnterCondition} / 종료: {InRow.EndCondition}";
            if (InRow.PatternIds != null)
            {
                for (int i = 0; i < InRow.PatternIds.Length; ++i)
                {
                    var id = InRow.PatternIds[i].Trim();
                    if (id.Length > 0)
                    {
                        phase.PatternIds.Add(id);
                    }
                }
            }

            return phase;
        }

        public static string PhaseDisplayName(string InPhaseId)
        {
            switch (InPhaseId)
            {
                case "G1-P0": return "1관문 — 조우";
                case "G1-P1": return "1관문 — 분열 구간";
                case "G1-P2": return "1관문 — 두 번째 구간";
                case "G1-P3": return "1관문 — 합체 구간";
                case "G2-P1": return "2관문 — 갑옷 구간";
                case "G2-P2": return "2관문 — 지형 파괴 구간";
                case "G2-P3": return "2관문 — 망령 구간";
            }

            return InPhaseId;
        }

        public static HitArea DefaultArea(string InShape)
        {
            switch (InShape)
            {
                case "원": return HitArea.Circle(4.0f);
                case "부채꼴": return HitArea.Fan(7.0f, 90.0f);
                case "직선(사각형)": return HitArea.Rect(12.0f, 3.0f);
                case "도넛": return HitArea.Donut(3.0f, 9.0f);
                case "전체 범위": return HitArea.Global();
                case "대상 지정": return HitArea.Circle(3.0f);
            }

            return HitArea.Circle(4.5f, 1.5f);
        }

        public static EPatternCategory ParseCategory(string InText)
        {
            switch (InText)
            {
                case "기믹": return EPatternCategory.Gimmick;
                case "스태거 체크": return EPatternCategory.StaggerCheck;
                case "카운터": return EPatternCategory.Counter;
                case "파티 기믹": return EPatternCategory.PartyGimmick;
                case "페이즈 전환": return EPatternCategory.PhaseTransition;
                case "필드 장판": return EPatternCategory.FieldHazard;
            }

            return EPatternCategory.NormalAttack;
        }

        public static EPatternResponse ParseResponse(string InText)
        {
            switch (InText)
            {
                case "회피": return EPatternResponse.Dodge;
                case "스태거": return EPatternResponse.Stagger;
                case "카운터": return EPatternResponse.Counter;
                case "파티 기믹 수행": return EPatternResponse.PartyGimmick;
                case "탱킹/버티기": return EPatternResponse.Tank;
                case "이동": return EPatternResponse.Move;
            }

            return EPatternResponse.None;
        }

        public static EEvidenceConfidence ParseConfidence(string InText)
        {
            switch (InText)
            {
                case "영상 직접 측정": return EEvidenceConfidence.VideoMeasured;
                case "추정": return EEvidenceConfidence.Estimated;
            }

            return EEvidenceConfidence.PublicGuide;
        }
    }
}
