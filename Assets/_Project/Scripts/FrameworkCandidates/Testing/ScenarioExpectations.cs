using System;
using UnityEngine;
using UnityEngine.Events;

namespace Framework.Testing
{
    public enum EScenarioCompare
    {
        Equal = 0,
        NotEqual = 1,
        Greater = 2,
        GreaterOrEqual = 3,
        Less = 4,
        LessOrEqual = 5,
        /// <summary>Value ≤ x ≤ Value2.</summary>
        Between = 6,
    }

    public static class ScenarioCompare
    {
        public const float Epsilon = 0.0001f;

        public static bool Test(float InActual, EScenarioCompare InCompare, float InValue, float InValue2)
        {
            if (float.IsNaN(InActual))
            {
                return false;
            }

            switch (InCompare)
            {
                case EScenarioCompare.Equal: return Math.Abs(InActual - InValue) <= Epsilon;
                case EScenarioCompare.NotEqual: return Math.Abs(InActual - InValue) > Epsilon;
                case EScenarioCompare.Greater: return InActual > InValue;
                case EScenarioCompare.GreaterOrEqual: return InActual >= InValue - Epsilon;
                case EScenarioCompare.Less: return InActual < InValue;
                case EScenarioCompare.LessOrEqual: return InActual <= InValue + Epsilon;
                case EScenarioCompare.Between: return InActual >= InValue - Epsilon && InActual <= InValue2 + Epsilon;
                default: return false;
            }
        }

        public static string Text(EScenarioCompare InCompare, float InValue, float InValue2)
        {
            switch (InCompare)
            {
                case EScenarioCompare.Equal: return $"= {InValue:0.###}";
                case EScenarioCompare.NotEqual: return $"≠ {InValue:0.###}";
                case EScenarioCompare.Greater: return $"> {InValue:0.###}";
                case EScenarioCompare.GreaterOrEqual: return $"≥ {InValue:0.###}";
                case EScenarioCompare.Less: return $"< {InValue:0.###}";
                case EScenarioCompare.LessOrEqual: return $"≤ {InValue:0.###}";
                default: return $"{InValue:0.###} ~ {InValue2:0.###}";
            }
        }
    }

    /// <summary>실제 값을 받는 UnityEvent (커스텀 검증 · 기록 연결용).</summary>
    [Serializable]
    public class ScenarioValueEvent : UnityEvent<float>
    {
    }

    /// <summary>
    /// 시나리오 끝에 확인하는 조건 하나. Evaluate 가 실제 값을 내고, Compare 로 판정한다.
    /// OnEvaluated 에 함수를 연결하면 실제 값을 받아 추가 확인 · 기록을 할 수 있다.
    /// ReportOnly 를 켜면 판정하지 않고 리포트에만 남긴다 (밸런스 측정용).
    /// </summary>
    [Serializable]
    public abstract class ScenarioExpectation
    {
        public string Label;
        public EScenarioCompare Compare = EScenarioCompare.GreaterOrEqual;
        public float Value;
        [Tooltip("Between 일 때 상한")]
        public float Value2;
        [Tooltip("판정하지 않고 값만 리포트에 남긴다 (밸런스 측정)")]
        public bool ReportOnly;
        public ScenarioValueEvent OnEvaluated = new ScenarioValueEvent();

        protected abstract float Actual(ScenarioContext InContext);
        protected abstract string Subject { get; }

        public bool Evaluate(ScenarioContext InContext, out string OutLine)
        {
            float actual = Actual(InContext);
            OnEvaluated.Invoke(actual);
            string name = string.IsNullOrEmpty(Label) ? Subject : $"{Label} ({Subject})";
            if (ReportOnly == true)
            {
                OutLine = $"  [측정] {name} = {actual:0.###}";
                return true;
            }

            bool passed = ScenarioCompare.Test(actual, Compare, Value, Value2);
            OutLine = $"  [{(passed ? "통과" : "실패")}] {name} = {actual:0.###} (기대 {ScenarioCompare.Text(Compare, Value, Value2)})";
            return passed;
        }
    }

    /// <summary>이벤트 집계 (개수 · 합 · 최대 · 최소 · 평균 · 마지막 · 처음 시각).</summary>
    [Serializable]
    public class EventExpectation : ScenarioExpectation
    {
        public string EventName;
        public EScenarioAggregate Aggregate = EScenarioAggregate.Count;

        protected override string Subject
        {
            get { return $"이벤트 '{EventName}' {Aggregate}"; }
        }

        protected override float Actual(ScenarioContext InContext)
        {
            return InContext.Recorder.Aggregate(EventName, Aggregate);
        }
    }

    public enum EProbeMoment
    {
        /// <summary>시나리오가 끝난 순간의 값.</summary>
        AtEnd = 0,
        /// <summary>실행 중 가장 작았던 값.</summary>
        MinDuring = 1,
        /// <summary>실행 중 가장 컸던 값.</summary>
        MaxDuring = 2,
        /// <summary>끝 값 − 처음 유효한 값 (변화량).</summary>
        Delta = 3,
    }

    /// <summary>관측값(probe) 조건.</summary>
    [Serializable]
    public class ProbeExpectation : ScenarioExpectation
    {
        public string Probe;
        public EProbeMoment Moment = EProbeMoment.AtEnd;

        protected override string Subject
        {
            get { return $"관측값 '{Probe}' {Moment}"; }
        }

        protected override float Actual(ScenarioContext InContext)
        {
            switch (Moment)
            {
                case EProbeMoment.MinDuring: return InContext.ProbeMin(Probe);
                case EProbeMoment.MaxDuring: return InContext.ProbeMax(Probe);
                case EProbeMoment.Delta: return InContext.ReadProbe(Probe) - InContext.ProbeFirst(Probe);
                default: return InContext.ReadProbe(Probe);
            }
        }
    }

    /// <summary>두 이벤트 사이의 시간 (처음 A → 처음 B, 초).</summary>
    [Serializable]
    public class EventIntervalExpectation : ScenarioExpectation
    {
        public string FromEvent;
        public string ToEvent;

        protected override string Subject
        {
            get { return $"'{FromEvent}' → '{ToEvent}' 시간"; }
        }

        protected override float Actual(ScenarioContext InContext)
        {
            return InContext.Recorder.Aggregate(ToEvent, EScenarioAggregate.FirstTime) - InContext.Recorder.Aggregate(FromEvent, EScenarioAggregate.FirstTime);
        }
    }
}
