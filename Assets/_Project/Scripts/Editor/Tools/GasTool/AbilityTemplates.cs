using DemoLA.Boss;
using DemoLA.Combat;
using DemoLA.Core;

namespace DemoLA.Editor.Gas
{
    /// <summary>
    /// 새 보스 패턴 템플릿 · 단계 템플릿 (원본 AbilityTemplates 의 DemoLA 판).
    /// 템플릿은 출발점일 뿐이며 만든 뒤 GAS Tool 에서 단계 · 카운터 · 시간을 고친다.
    /// </summary>
    public static class AbilityTemplates
    {
        public enum EKind
        {
            Empty = 0,
            Slam = 1,
            FrontBackCounterSlam = 2,
            Charge = 3,
            CounterSlam = 4,
        }

        public static readonly string[] Labels =
        {
            "빈 패턴 (단계 없음)",
            "내려찍기 1회 (정면 원형)",
            "앞 찍기 → 뒤 찍기 → 앞 강타(카운터)",
            "돌진 (직선, 경로 표시)",
            "카운터 강타 1회",
        };

        public static readonly string[] StepLabels =
        {
            "공격/앞 찍기 (정면 원형, 찍은 곳)",
            "공격/뒤 찍기 (뒤쪽 원형)",
            "공격/주변 원형 (보스 중심)",
            "공격/부채꼴 (어그로 방향)",
            "공격/대상 발밑 원형",
            "공격/돌진 (직선)",
            "카운터/앞 강타 (카운터 가능)",
        };

        public static PatternStep CreateStep(int InIndex)
        {
            var step = new PatternStep();
            step.KnockbackDistance = 0.0f;
            switch (InIndex)
            {
                case 0:
                    step.Label = "앞 찍기";
                    step.Targeting = EPatternTargeting.BossTowardAggro;
                    step.Area = HitArea.Circle(3.5f);
                    step.ForwardOffset = 4.5f;
                    step.TelegraphTime = 1.0f;
                    break;
                case 1:
                    step.Label = "뒤 찍기";
                    step.Targeting = EPatternTargeting.BossForward;
                    step.Area = HitArea.Circle(3.5f);
                    step.ForwardOffset = -4.5f;
                    step.TelegraphTime = 0.9f;
                    step.StartDelay = 0.25f;
                    break;
                case 2:
                    step.Label = "주변 원형";
                    step.Targeting = EPatternTargeting.BossForward;
                    step.Area = HitArea.Circle(6.0f);
                    step.KnockbackDistance = 4.0f;
                    break;
                case 3:
                    step.Label = "부채꼴";
                    step.Targeting = EPatternTargeting.BossTowardAggro;
                    step.Area = HitArea.Fan(9.0f, 90.0f);
                    break;
                case 4:
                    step.Label = "발밑 원형";
                    step.Targeting = EPatternTargeting.AggroTarget;
                    step.Area = HitArea.Circle(2.5f);
                    step.TrackDuringTelegraph = true;
                    break;
                case 5:
                    step.Label = "돌진";
                    step.Targeting = EPatternTargeting.BossTowardAggro;
                    step.Area = HitArea.Rect(14.0f, 3.5f);
                    step.TelegraphTime = 1.6f;
                    step.TrackDuringTelegraph = true;
                    step.TrackLockBefore = 0.3f;
                    step.BossMoveDistance = 12.0f;
                    step.BossMoveDuration = 0.5f;
                    step.KnockbackDistance = 5.0f;
                    break;
                default:
                    step.Label = "앞 강타 (카운터)";
                    step.Targeting = EPatternTargeting.BossForward;
                    step.Area = HitArea.Circle(4.5f);
                    step.ForwardOffset = 5.0f;
                    step.TelegraphTime = 1.6f;
                    step.StartDelay = 0.35f;
                    step.DamageTier = EDamageTier.Big;
                    step.KnockbackDistance = 3.0f;
                    step.Counterable = true;
                    step.CounterWindowStart = 0.55f;
                    step.CounterWindowEnd = 1.4f;
                    break;
            }

            return step;
        }

        public static void Fill(BossPatternDefinition InPattern, EKind InKind)
        {
            InPattern.Steps.Clear();
            InPattern.Category = EPatternCategory.NormalAttack;
            InPattern.Response = EPatternResponse.Dodge;
            InPattern.Implemented = true;
            InPattern.Confidence = EEvidenceConfidence.Estimated;
            switch (InKind)
            {
                case EKind.Slam:
                    InPattern.Steps.Add(CreateStep(0));
                    InPattern.MaxRange = 8.0f;
                    break;
                case EKind.FrontBackCounterSlam:
                    InPattern.Steps.Add(CreateStep(0));
                    InPattern.Steps.Add(CreateStep(1));
                    InPattern.Steps.Add(CreateStep(6));
                    InPattern.Response = EPatternResponse.Counter;
                    InPattern.GroggyOnCounter = 3.5f;
                    InPattern.MaxRange = 9.0f;
                    InPattern.Cooldown = 18.0f;
                    break;
                case EKind.Charge:
                    InPattern.Steps.Add(CreateStep(5));
                    InPattern.MaxRange = 16.0f;
                    break;
                case EKind.CounterSlam:
                    InPattern.Steps.Add(CreateStep(6));
                    InPattern.Category = EPatternCategory.Counter;
                    InPattern.Response = EPatternResponse.Counter;
                    InPattern.MaxRange = 8.0f;
                    break;
            }
        }
    }
}
