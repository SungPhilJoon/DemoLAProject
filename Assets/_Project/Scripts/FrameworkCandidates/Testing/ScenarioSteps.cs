using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Framework.Testing
{
    /// <summary>
    /// 시나리오 한 단계. [SerializeReference] 목록에 담겨 인스펙터에서 순서대로 편집된다.
    /// Execute 는 코루틴 — 여러 프레임에 걸쳐 기다릴 수 있다. 실패하면 Context.Log 에 적고 Failed 를 켠다.
    /// </summary>
    [Serializable]
    public abstract class ScenarioStep
    {
        [Tooltip("인스펙터 · 로그에 보이는 설명 (비우면 형식 이름)")]
        public string Label;

        [NonSerialized] public bool Failed;
        [NonSerialized] public string FailReason;

        public abstract IEnumerator Execute(ScenarioContext InContext);

        public virtual string Describe()
        {
            return string.IsNullOrEmpty(Label) ? GetType().Name : Label;
        }

        protected void Fail(ScenarioContext InContext, string InReason)
        {
            Failed = true;
            FailReason = InReason;
            InContext.Log($"단계 실패: {Describe()} — {InReason}");
        }
    }

    /// <summary>게임 시간으로 N초 기다린다.</summary>
    [Serializable]
    public class WaitSecondsStep : ScenarioStep
    {
        public float Seconds = 1.0f;

        public override IEnumerator Execute(ScenarioContext InContext)
        {
            float end = InContext.Elapsed + Seconds;
            while (InContext.Elapsed < end)
            {
                yield return null;
            }
        }

        public override string Describe()
        {
            return string.IsNullOrEmpty(Label) ? $"{Seconds:0.##}초 대기" : Label;
        }
    }

    /// <summary>이벤트 EventName 이 Count 번 기록될 때까지 기다린다 (시간 초과면 실패 또는 계속).</summary>
    [Serializable]
    public class WaitForEventStep : ScenarioStep
    {
        public string EventName;
        public int Count = 1;
        public float Timeout = 20.0f;
        [Tooltip("시간 초과를 실패로 볼지 (끄면 그냥 다음 단계로)")]
        public bool FailOnTimeout = true;

        public override IEnumerator Execute(ScenarioContext InContext)
        {
            float end = InContext.Elapsed + Timeout;
            while (InContext.Recorder.Count(EventName) < Count)
            {
                if (InContext.Elapsed >= end)
                {
                    if (FailOnTimeout == true)
                    {
                        Fail(InContext, $"'{EventName}' {Count}회를 {Timeout:0.#}초 안에 받지 못함 (받은 횟수 {InContext.Recorder.Count(EventName)})");
                    }

                    yield break;
                }

                yield return null;
            }
        }

        public override string Describe()
        {
            return string.IsNullOrEmpty(Label) ? $"'{EventName}' {Count}회 대기" : Label;
        }
    }

    /// <summary>관측값 Probe 가 조건을 만족할 때까지 기다린다.</summary>
    [Serializable]
    public class WaitForProbeStep : ScenarioStep
    {
        public string Probe;
        public EScenarioCompare Compare = EScenarioCompare.GreaterOrEqual;
        public float Value;
        public float Value2;
        public float Timeout = 20.0f;
        public bool FailOnTimeout = true;

        public override IEnumerator Execute(ScenarioContext InContext)
        {
            float end = InContext.Elapsed + Timeout;
            while (ScenarioCompare.Test(InContext.ReadProbe(Probe), Compare, Value, Value2) == false)
            {
                if (InContext.Elapsed >= end)
                {
                    if (FailOnTimeout == true)
                    {
                        Fail(InContext, $"관측값 '{Probe}' 이 {ScenarioCompare.Text(Compare, Value, Value2)} 가 되지 않음 (현재 {InContext.ReadProbe(Probe):0.###})");
                    }

                    yield break;
                }

                yield return null;
            }
        }
    }

    /// <summary>인스펙터에서 연결한 UnityEvent 를 부른다 (에셋의 함수 · 커스텀 검증 · 로그 등).</summary>
    [Serializable]
    public class InvokeUnityEventStep : ScenarioStep
    {
        public UnityEvent Event = new UnityEvent();

        public override IEnumerator Execute(ScenarioContext InContext)
        {
            Event.Invoke();
            yield break;
        }
    }

    /// <summary>로그에 글을 남긴다 (리포트 구분용).</summary>
    [Serializable]
    public class LogStep : ScenarioStep
    {
        public string Message;

        public override IEnumerator Execute(ScenarioContext InContext)
        {
            InContext.Log(Message);
            yield break;
        }
    }
}
