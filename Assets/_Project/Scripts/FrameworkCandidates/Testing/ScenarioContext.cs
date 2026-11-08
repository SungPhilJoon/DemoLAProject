using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Framework.Testing
{
    /// <summary>
    /// 프로젝트 어댑터. 시나리오 세계를 만들고(SetUp) 게임 이벤트를 Recorder 에 넣고 관측값(probe)을 등록한 뒤, 끝나면 정리한다.
    /// 코어(이 어셈블리)는 프로젝트 타입을 모른다 — 프로젝트 단계(ScenarioStep)는 Context.Host 를 자기 호스트로 캐스팅해 쓴다.
    /// </summary>
    public interface IScenarioHost
    {
        IEnumerator SetUp(ScenarioContext InContext);
        void TearDown(ScenarioContext InContext);
    }

    /// <summary>
    /// 실행 중 상태: 기록기 · 관측값 표 · 로그. 관측값(probe)은 "이름 → 현재 값" 함수이고,
    /// 실행기가 매 프레임 표본을 떠서 최소 · 최대를 기억한다 (검증이 "실행 중 최댓값" 등을 볼 수 있게).
    /// </summary>
    public class ScenarioContext
    {
        private readonly Dictionary<string, Func<float>> probes = new Dictionary<string, Func<float>>();
        private readonly Dictionary<string, float> probeMin = new Dictionary<string, float>();
        private readonly Dictionary<string, float> probeMax = new Dictionary<string, float>();
        private readonly Dictionary<string, float> probeFirst = new Dictionary<string, float>();
        private readonly StringBuilder log = new StringBuilder();

        public ScenarioContext(TestScenarioAsset InScenario, IScenarioHost InHost)
        {
            Scenario = InScenario;
            Host = InHost;
            Recorder = new ScenarioRecorder();
        }

        public TestScenarioAsset Scenario { get; private set; }
        public IScenarioHost Host { get; private set; }
        public ScenarioRecorder Recorder { get; private set; }
        /// <summary>시나리오 시작부터 지난 게임 시간 (초).</summary>
        public float Elapsed { get; internal set; }

        public void RegisterProbe(string InName, Func<float> InGetter)
        {
            probes[InName] = InGetter;
        }

        public bool HasProbe(string InName)
        {
            return probes.ContainsKey(InName);
        }

        public float ReadProbe(string InName)
        {
            Func<float> getter;
            return probes.TryGetValue(InName, out getter) == true ? getter() : float.NaN;
        }

        public float ProbeMin(string InName)
        {
            float value;
            return probeMin.TryGetValue(InName, out value) == true ? value : ReadProbe(InName);
        }

        public float ProbeMax(string InName)
        {
            float value;
            return probeMax.TryGetValue(InName, out value) == true ? value : ReadProbe(InName);
        }

        /// <summary>처음으로 유효한(NaN 이 아닌) 표본 — 변화량(Delta) 의 시작 값. 대상이 단계 도중에 생겨도 맞게 잡힌다.</summary>
        public float ProbeFirst(string InName)
        {
            float value;
            return probeFirst.TryGetValue(InName, out value) == true ? value : float.NaN;
        }

        public IEnumerable<string> ProbeNames
        {
            get { return probes.Keys; }
        }

        /// <summary>모든 관측값의 표본을 떠서 최소 · 최대를 갱신한다 (실행기가 매 프레임 부른다).</summary>
        internal void SampleProbes()
        {
            foreach (var pair in probes)
            {
                float value;
                try
                {
                    value = pair.Value();
                }
                catch (Exception)
                {
                    continue;
                }

                if (float.IsNaN(value))
                {
                    continue;
                }

                if (probeFirst.ContainsKey(pair.Key) == false)
                {
                    probeFirst[pair.Key] = value;
                }

                float current;
                probeMin[pair.Key] = probeMin.TryGetValue(pair.Key, out current) == true ? Math.Min(current, value) : value;
                probeMax[pair.Key] = probeMax.TryGetValue(pair.Key, out current) == true ? Math.Max(current, value) : value;
            }
        }

        public void Log(string InMessage)
        {
            log.Append('[').Append(Elapsed.ToString("0.00")).Append("s] ").AppendLine(InMessage);
        }

        public string LogText
        {
            get { return log.ToString(); }
        }
    }
}
