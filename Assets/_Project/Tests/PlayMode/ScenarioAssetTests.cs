using System.Collections;
using System.Collections.Generic;
using DemoLA.Testing;
using Framework.Testing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DemoLA.Tests
{
    /// <summary>
    /// 데이터 기반 시나리오 테스트. Assets/_Project/Tests/Scenarios 아래의 모든 TestScenarioAsset 을 찾아
    /// 하나씩 실행한다 — 테스트를 늘릴 때 코드를 쓰지 않고 에셋을 추가/편집한다.
    /// (보스 패턴 시나리오는 메뉴 DemoLA/Tests/보스 패턴 시나리오 생성 으로 만든다.)
    /// </summary>
    public class ScenarioAssetTests
    {
        public const string ScenarioFolder = "Assets/_Project/Tests/Scenarios";

        /// <summary>테스트 케이스 = 시나리오 에셋 경로. 이름에 분류 · 제목이 보이도록 TestCaseData 로 만든다.</summary>
        public static IEnumerable<TestCaseData> Scenarios()
        {
#if UNITY_EDITOR
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:TestScenarioAsset", new[] { ScenarioFolder }))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TestScenarioAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                yield return new TestCaseData(path).SetName($"{asset.Category}.{asset.Title}").SetCategory(asset.Category).Returns(null);
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        [TestCaseSource(nameof(Scenarios))]
        public IEnumerator Run(string InPath)
        {
#if UNITY_EDITOR
            var scenario = UnityEditor.AssetDatabase.LoadAssetAtPath<TestScenarioAsset>(InPath);
            Assert.IsNotNull(scenario, InPath);
            if (scenario.Enabled == false)
            {
                Assert.Ignore($"꺼진 시나리오: {scenario.Title}");
            }

            var result = new ScenarioResult();
            yield return ScenarioRunner.Run(scenario, new DemoLAScenarioHost(), result);
            Debug.Log(result.Report);
            Assert.IsTrue(result.Passed, result.Report);
#else
            yield break;
#endif
        }
    }
}
