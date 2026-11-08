using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Framework.Testing
{
    /// <summary>이벤트 이름 · 값 · 부가 문자열을 받는 UnityEvent.</summary>
    [Serializable]
    public class ScenarioRecordedEvent : UnityEvent<string, float>
    {
    }

    /// <summary>판정 결과(통과 여부, 리포트)를 받는 UnityEvent.</summary>
    [Serializable]
    public class ScenarioFinishedEvent : UnityEvent<bool, string>
    {
    }

    /// <summary>
    /// 데이터 기반 테스트 · 밸런스 시나리오 한 개.
    ///  - Steps: 순서대로 실행할 단계 (대기 · 이벤트 대기 · 프로젝트 단계 등) — [SerializeReference] 로 형식을 섞어 담는다.
    ///  - Expectations: 끝에 판정할 조건 (이벤트 집계 · 관측값 · 이벤트 사이 시간). ReportOnly 는 측정만.
    ///  - 콜백: OnStarted / OnEventRecorded(이름, 값) / OnFinished(통과, 리포트) 에 함수를 연결해 동적으로 확인 · 기록한다.
    /// 같은 에셋을 UnityTest(자동 검증)와 플레이 중 실행(밸런스 측정) 양쪽에서 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "Framework/Testing/Test Scenario", fileName = "TS_New")]
    public class TestScenarioAsset : ScriptableObject
    {
        public string DisplayName;
        [TextArea(2, 6)] public string Description;
        [Tooltip("끄면 UnityTest 에서 건너뛴다")]
        public bool Enabled = true;
        [Tooltip("분류 (UnityTest 이름 앞에 붙고, 필터로 쓴다)")]
        public string Category = "Default";
        [Tooltip("실행 중 Time.timeScale (빠르게 돌리기)")]
        public float TimeScale = 3.0f;
        [Tooltip("전체 제한 시간 (게임 시간, 초). 넘으면 실패")]
        public float Timeout = 60.0f;
        public int RandomSeed = 7;
        [Tooltip("프로젝트 어댑터가 읽는 자유 설정 (예: 관문 ID). 키=값 형식")]
        public List<string> HostSettings = new List<string>();

        [SerializeReference, SubclassSelector] public List<ScenarioStep> Steps = new List<ScenarioStep>();
        [SerializeReference, SubclassSelector] public List<ScenarioExpectation> Expectations = new List<ScenarioExpectation>();

        [Header("콜백")]
        public UnityEvent OnStarted = new UnityEvent();
        public ScenarioRecordedEvent OnEventRecorded = new ScenarioRecordedEvent();
        public ScenarioFinishedEvent OnFinished = new ScenarioFinishedEvent();

        public string Title
        {
            get { return string.IsNullOrEmpty(DisplayName) ? name : DisplayName; }
        }

        /// <summary>HostSettings 에서 "키=값" 을 찾는다.</summary>
        public string GetSetting(string InKey, string InDefault = null)
        {
            for (int i = 0; i < HostSettings.Count; ++i)
            {
                var entry = HostSettings[i];
                int at = entry != null ? entry.IndexOf('=') : -1;
                if (at > 0 && entry.Substring(0, at).Trim() == InKey)
                {
                    return entry.Substring(at + 1).Trim();
                }
            }

            return InDefault;
        }
    }
}
