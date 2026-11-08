using System;
using System.Collections;
using System.Text;
using UnityEngine;

namespace Framework.Testing
{
    /// <summary>시나리오 실행 결과.</summary>
    public class ScenarioResult
    {
        public bool Passed;
        public bool TimedOut;
        public string Report;
        public float Elapsed;
        public ScenarioContext Context;
    }

    /// <summary>
    /// 시나리오 실행기 (코루틴). UnityTest 에서는 yield return 으로, 게임 안에서는 StartCoroutine 으로 돌린다.
    /// 흐름: 시간 배율 · 시드 → Host.SetUp → 단계 순서대로 (매 프레임 관측값 표본) → 검증 → 콜백 → Host.TearDown.
    /// 단계 하나라도 실패하거나 제한 시간을 넘거나 검증이 하나라도 틀리면 실패.
    /// </summary>
    public static class ScenarioRunner
    {
        /// <summary>단계 실행 중 매 프레임 (게임 시간 · 표본 갱신 후) 불린다 — 호스트가 공통 감시를 붙일 때.</summary>
        public static event Action<ScenarioContext> OnTick;

        public static IEnumerator Run(TestScenarioAsset InScenario, IScenarioHost InHost, ScenarioResult OutResult)
        {
            var context = new ScenarioContext(InScenario, InHost);
            OutResult.Context = context;
            float previousScale = Time.timeScale;
            Time.timeScale = Mathf.Max(0.1f, InScenario.TimeScale);
            UnityEngine.Random.InitState(InScenario.RandomSeed);

            context.Recorder.OnRecorded += e => InScenario.OnEventRecorded.Invoke(e.Name, e.Value);

            bool timedOut = false;
            bool stepFailed = false;
            float startTime = Time.time;
            yield return InHost.SetUp(context);
            context.Log($"시나리오 시작: {InScenario.Title}");
            InScenario.OnStarted.Invoke();

            context.SampleProbes();
            startTime = Time.time;
            for (int s = 0; s < InScenario.Steps.Count && timedOut == false && stepFailed == false; ++s)
            {
                var step = InScenario.Steps[s];
                if (step == null)
                {
                    continue;
                }

                step.Failed = false;
                context.Log($"단계 {s + 1}: {step.Describe()}");
                var routine = step.Execute(context);
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = routine.MoveNext();
                    }
                    catch (Exception e)
                    {
                        step.Failed = true;
                        step.FailReason = e.Message;
                        context.Log($"단계 예외: {e}");
                        break;
                    }

                    if (moved == false)
                    {
                        break;
                    }

                    yield return routine.Current;
                    context.Elapsed = Time.time - startTime;
                    context.Recorder.Now = context.Elapsed;
                    context.SampleProbes();
                    OnTick?.Invoke(context);
                    if (context.Elapsed > InScenario.Timeout)
                    {
                        timedOut = true;
                        context.Log($"제한 시간 {InScenario.Timeout:0.#}초 초과");
                        break;
                    }
                }

                stepFailed = step.Failed;
            }

            context.SampleProbes();
            var report = new StringBuilder();
            bool passed = timedOut == false && stepFailed == false;
            report.AppendLine($"=== {InScenario.Title} ({context.Elapsed:0.0}초) ===");
            for (int i = 0; i < InScenario.Expectations.Count; ++i)
            {
                var expectation = InScenario.Expectations[i];
                if (expectation == null)
                {
                    continue;
                }

                string line;
                passed &= expectation.Evaluate(context, out line);
                report.AppendLine(line);
            }

            if (stepFailed == true)
            {
                report.AppendLine("  [실패] 단계 실패 — 로그 참고");
            }

            if (timedOut == true)
            {
                report.AppendLine("  [실패] 제한 시간 초과");
            }

            report.AppendLine("--- 로그");
            report.Append(context.LogText);

            OutResult.Passed = passed;
            OutResult.TimedOut = timedOut;
            OutResult.Elapsed = context.Elapsed;
            OutResult.Report = report.ToString();
            InScenario.OnFinished.Invoke(passed, OutResult.Report);

            InHost.TearDown(context);
            Time.timeScale = previousScale;
        }
    }
}
