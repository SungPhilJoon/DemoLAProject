using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Framework.Editor;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor
{
    /// <summary>
    /// Addressables 에셋 의존성 리포트 (프레임워크 AddressablesDependencyAnalyzer 위에 게임 규칙을 더한다).
    ///  - 문제: 프레임워크 정적 검사 (누락 주소 · 깨진 참조 · Resources 사용 등)
    ///  - 그룹 제안: 주소 없는 에셋이 여러 그룹의 번들에 끌려 들어가면(암묵 중복) "Shared" 그룹 후보,
    ///    한 그룹만 쓰면 그 그룹에 명시 등록 후보.
    /// 그룹 자동 설정은 다음 단계(문서) — 지금은 제안만 리포트로 남긴다.
    /// 출력: DependencyReports/dependency-report.md / .json
    /// </summary>
    public static class DependencyReport
    {
        public const string OutputFolder = "DependencyReports";

        [Serializable]
        public class GroupSuggestion
        {
            public string Asset;
            public string AssetType;
            public long SizeBytes;
            public string SuggestedGroup;
            public string Reason;
            public List<string> PulledInByGroups = new List<string>();
        }

        [Serializable]
        public class ReportData
        {
            public string GeneratedAt;
            public double AnalyzeSeconds;
            public int AddressableCount;
            public int NodeCount;
            public List<DependencyIssue> Issues = new List<DependencyIssue>();
            public List<GroupSuggestion> Suggestions = new List<GroupSuggestion>();
        }

        [MenuItem("DemoLA/Tools/의존성 리포트 생성", priority = 200)]
        public static void RunMenu()
        {
            Debug.Log(Run());
            DependencyReportWindow.Open();
        }

        public static string Run()
        {
            var data = Analyze(true);
            Directory.CreateDirectory(OutputFolder);
            File.WriteAllText(Path.Combine(OutputFolder, "dependency-report.json"), JsonUtility.ToJson(data, true));
            File.WriteAllText(Path.Combine(OutputFolder, "dependency-report.md"), toMarkdown(data));
            return $"주소 {data.AddressableCount} · 노드 {data.NodeCount} · 문제 {data.Issues.Count} · 그룹 제안 {data.Suggestions.Count} → {OutputFolder}/dependency-report.md";
        }

        public static ReportData Analyze(bool InScanMissing)
        {
            var graph = AddressablesDependencyAnalyzer.Build(InScanMissing);
            var data = new ReportData();
            data.GeneratedAt = DateTime.Now.ToString("o");
            data.AnalyzeSeconds = graph.BuildSeconds;
            data.NodeCount = graph.Nodes.Count;
            data.AddressableCount = graph.Nodes.Values.Count(n => n.IsAddressable);
            data.Issues.AddRange(graph.Issues);

            foreach (var node in graph.Nodes.Values)
            {
                if (node.IsAddressable == true || node.Path.EndsWith(".cs") == true || node.PulledInBy.Count == 0)
                {
                    continue;
                }

                var groups = node.PulledInBy
                    .Select(root => graph.Nodes.TryGetValue(root, out var rootNode) ? rootNode.Group : null)
                    .Where(g => string.IsNullOrEmpty(g) == false)
                    .Distinct()
                    .ToList();
                if (groups.Count == 0)
                {
                    continue;
                }

                var suggestion = new GroupSuggestion();
                suggestion.Asset = node.Path;
                suggestion.AssetType = node.AssetType;
                suggestion.SizeBytes = node.SizeBytes;
                suggestion.PulledInByGroups = groups;
                if (groups.Count > 1)
                {
                    suggestion.SuggestedGroup = "Shared " + typeFolder(node.AssetType);
                    suggestion.Reason = $"{groups.Count}개 그룹 번들에 중복 포함 (암묵 의존)";
                }
                else
                {
                    suggestion.SuggestedGroup = groups[0];
                    suggestion.Reason = "한 그룹만 사용 — 명시 등록하면 번들 경계가 분명해진다";
                }

                data.Suggestions.Add(suggestion);
            }

            data.Suggestions = data.Suggestions.OrderByDescending(s => s.PulledInByGroups.Count).ThenByDescending(s => s.SizeBytes).ToList();
            return data;
        }

        public static ReportData ReadLast()
        {
            string path = Path.Combine(OutputFolder, "dependency-report.json");
            return File.Exists(path) == true ? JsonUtility.FromJson<ReportData>(File.ReadAllText(path)) : null;
        }

        private static string typeFolder(string InType)
        {
            switch (InType)
            {
                case "Texture2D": return "Textures";
                case "Material": return "Materials";
                case "Shader": return "Shaders";
                case "Mesh": return "Meshes";
                case "AnimationClip": return "Animations";
            }

            return "Assets";
        }

        private static string toMarkdown(ReportData InData)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Addressables 의존성 리포트");
            builder.AppendLine();
            builder.AppendLine($"- 생성: {InData.GeneratedAt} ({InData.AnalyzeSeconds:0.0}s)");
            builder.AppendLine($"- 주소 등록 에셋 {InData.AddressableCount} · 분석 노드 {InData.NodeCount}");
            builder.AppendLine();
            builder.AppendLine("## 문제");
            builder.AppendLine();
            if (InData.Issues.Count == 0)
            {
                builder.AppendLine("없음");
            }

            foreach (var issue in InData.Issues.OrderBy(i => i.Severity))
            {
                builder.AppendLine($"- **[{issue.Severity}] {issue.Code}** {issue.Title} — {issue.Why} → {issue.HowToFix}");
                foreach (var asset in issue.Assets.Take(10))
                {
                    builder.AppendLine($"  - `{asset}`");
                }
            }

            builder.AppendLine();
            builder.AppendLine("## 그룹 제안");
            builder.AppendLine();
            builder.AppendLine("| 에셋 | 종류 | 크기 | 제안 그룹 | 이유 |");
            builder.AppendLine("|---|---|---|---|---|");
            foreach (var suggestion in InData.Suggestions.Take(200))
            {
                builder.AppendLine($"| `{suggestion.Asset}` | {suggestion.AssetType} | {suggestion.SizeBytes / 1024}KB | {suggestion.SuggestedGroup} | {suggestion.Reason} ({string.Join(", ", suggestion.PulledInByGroups)}) |");
            }

            return builder.ToString();
        }
    }

    /// <summary>리포트 보기 창 (DemoLA/Tools/의존성 리포트).</summary>
    public class DependencyReportWindow : EditorWindow
    {
        private DependencyReport.ReportData data;
        private Vector2 scroll;

        [MenuItem("DemoLA/Tools/의존성 리포트 보기", priority = 201)]
        public static void Open()
        {
            var window = GetWindow<DependencyReportWindow>("Dependency Report");
            window.data = DependencyReport.ReadLast();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("분석", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    DependencyReport.Run();
                    data = DependencyReport.ReadLast();
                }

                if (GUILayout.Button("리포트 폴더", EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    EditorUtility.RevealInFinder(DependencyReport.OutputFolder);
                }
            }

            if (data == null)
            {
                EditorGUILayout.HelpBox("리포트가 없습니다. '분석' 을 누르세요.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"{data.GeneratedAt} · 주소 {data.AddressableCount} · 문제 {data.Issues.Count} · 제안 {data.Suggestions.Count}");
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("문제", EditorStyles.boldLabel);
            foreach (var issue in data.Issues)
            {
                var type = issue.Severity == EDependencySeverity.Error ? MessageType.Error : MessageType.Warning;
                EditorGUILayout.HelpBox($"[{issue.Code}] {issue.Title}\n{issue.Why}\n→ {issue.HowToFix}", type);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("그룹 제안", EditorStyles.boldLabel);
            foreach (var suggestion in data.Suggestions)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("선택", GUILayout.Width(44)))
                    {
                        Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(suggestion.Asset);
                    }

                    EditorGUILayout.LabelField(suggestion.Asset, GUILayout.MinWidth(260));
                    EditorGUILayout.LabelField(suggestion.SuggestedGroup, GUILayout.Width(150));
                    EditorGUILayout.LabelField(suggestion.Reason, GUILayout.MinWidth(200));
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
