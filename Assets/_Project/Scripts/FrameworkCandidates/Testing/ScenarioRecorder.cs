using System;
using System.Collections.Generic;

namespace Framework.Testing
{
    /// <summary>기록된 이벤트 하나 (이름 · 값 · 발생 시각 · 부가 문자열).</summary>
    public struct ScenarioEvent
    {
        public string Name;
        public float Value;
        public float Time;
        public string Detail;
    }

    /// <summary>
    /// 시나리오 동안 일어난 이벤트를 이름별로 모은다. 프로젝트 어댑터(IScenarioHost)가 게임 이벤트를 여기에 넣고,
    /// 검증(ScenarioExpectation)은 이름으로 개수 · 합 · 최대 · 최소 · 평균 · 마지막 값을 읽는다.
    /// 이름은 "Pattern.Started", "Pattern.Started.G2-23" 처럼 점으로 세분해 둘 다 기록하는 것을 권장한다.
    /// </summary>
    public class ScenarioRecorder
    {
        private readonly List<ScenarioEvent> events = new List<ScenarioEvent>();

        /// <summary>이벤트가 기록될 때마다 (UnityEvent 콜백 · 대기 단계가 듣는다).</summary>
        public event Action<ScenarioEvent> OnRecorded;

        public float Now { get; set; }

        public IReadOnlyList<ScenarioEvent> Events
        {
            get { return events; }
        }

        public void Record(string InName, float InValue = 0.0f, string InDetail = null)
        {
            var e = new ScenarioEvent { Name = InName, Value = InValue, Time = Now, Detail = InDetail };
            events.Add(e);
            OnRecorded?.Invoke(e);
        }

        public int Count(string InName)
        {
            int count = 0;
            for (int i = 0; i < events.Count; ++i)
            {
                count += events[i].Name == InName ? 1 : 0;
            }

            return count;
        }

        /// <summary>집계값. 이벤트가 없으면 (Count/Sum 은 0) 나머지는 NaN.</summary>
        public float Aggregate(string InName, EScenarioAggregate InAggregate)
        {
            int count = 0;
            float sum = 0.0f;
            float max = float.NegativeInfinity;
            float min = float.PositiveInfinity;
            float last = float.NaN;
            for (int i = 0; i < events.Count; ++i)
            {
                if (events[i].Name != InName)
                {
                    continue;
                }

                float v = events[i].Value;
                count += 1;
                sum += v;
                max = Math.Max(max, v);
                min = Math.Min(min, v);
                last = v;
            }

            switch (InAggregate)
            {
                case EScenarioAggregate.Count: return count;
                case EScenarioAggregate.Sum: return sum;
                case EScenarioAggregate.Max: return count > 0 ? max : float.NaN;
                case EScenarioAggregate.Min: return count > 0 ? min : float.NaN;
                case EScenarioAggregate.Average: return count > 0 ? sum / count : float.NaN;
                case EScenarioAggregate.Last: return last;
                case EScenarioAggregate.FirstTime:
                    for (int i = 0; i < events.Count; ++i)
                    {
                        if (events[i].Name == InName)
                        {
                            return events[i].Time;
                        }
                    }

                    return float.NaN;
                default: return float.NaN;
            }
        }
    }

    public enum EScenarioAggregate
    {
        Count = 0,
        Sum = 1,
        Max = 2,
        Min = 3,
        Average = 4,
        Last = 5,
        /// <summary>처음 기록된 시각 (시나리오 시작 기준 초).</summary>
        FirstTime = 6,
    }
}
