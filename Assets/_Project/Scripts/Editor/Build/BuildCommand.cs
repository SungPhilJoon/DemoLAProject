using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DemoLA.Editor.Build
{
    /// <summary>
    /// 빌드 자동화 진입점 (CI/CD · 로컬 CLI · 에디터 메뉴 공용).
    ///
    /// 배치 모드 (CI):
    ///   Unity -batchmode -quit -projectPath . -buildTarget StandaloneWindows64 \
    ///         -executeMethod DemoLA.Editor.Build.BuildCommand.Build \
    ///         -demolaTarget Win64 -outputPath Builds/Win64 [-development] [-buildNumber 42] [-versionName 0.3.0] [-skipSetup] [-skipAddressables] [-aab]
    ///
    /// 단계: (필요 시) 데이터/씬 셋업 → 버전 기록 → Addressables 콘텐츠 빌드 → 플레이어 빌드 → Builds/build-report.json.
    /// 실패하면 배치 모드에서 종료 코드 1 (CI 가 실패로 본다).
    /// Android 서명: 환경 변수 DEMOLA_KEYSTORE_PATH / DEMOLA_KEYSTORE_PASS / DEMOLA_KEY_ALIAS / DEMOLA_KEY_PASS (없으면 디버그 서명).
    /// </summary>
    public static class BuildCommand
    {
        public const string ReportPath = "Builds/build-report.json";

        public enum ETarget
        {
            Win64 = 0,
            Android = 1,
            Linux64 = 2,
            OSX = 3,
        }

        [Serializable]
        public class Options
        {
            public ETarget Target = ETarget.Win64;
            public string OutputPath;
            public bool Development;
            public bool SkipSetup;
            public bool SkipAddressables;
            public bool AppBundle;
            public int BuildNumber;
            public string VersionName;
        }

        [Serializable]
        public class Report
        {
            public string Target;
            public string Result;
            public string OutputPath;
            public long TotalSizeBytes;
            public double BuildSeconds;
            public int Errors;
            public int Warnings;
            public string Version;
            public int BuildNumber;
            public bool Development;
            public string Addressables;
            public string FinishedAt;
            public List<string> Messages = new List<string>();
        }

        // ------------------------------------------------------------------ entry points

        /// <summary>배치 모드 진입점 (-executeMethod). 명령줄 인자를 읽는다.</summary>
        public static void Build()
        {
            var options = ParseArgs(Environment.GetCommandLineArgs());
            var report = Run(options);
            if (Application.isBatchMode == true)
            {
                EditorApplication.Exit(report.Result == BuildResult.Succeeded.ToString() ? 0 : 1);
            }
        }

        [MenuItem("DemoLA/Build/Windows", priority = 100)]
        public static void BuildWindowsFromEditor()
        {
            Run(new Options { Target = ETarget.Win64 });
        }

        [MenuItem("DemoLA/Build/Windows (Development)", priority = 101)]
        public static void BuildWindowsFromEditorDev()
        {
            Run(new Options { Target = ETarget.Win64, Development = true });
        }

        [MenuItem("DemoLA/Build/Android", priority = 110)]
        public static void BuildAndroidFromEditor()
        {
            Run(new Options { Target = ETarget.Android });
        }

        [MenuItem("DemoLA/Build/Android (Development)", priority = 111)]
        public static void BuildAndroidFromEditorDev()
        {
            Run(new Options { Target = ETarget.Android, Development = true });
        }

        [MenuItem("DemoLA/Build/Addressables 콘텐츠만 빌드", priority = 120)]
        public static void BuildAddressablesOnly()
        {
            Debug.Log(BuildAddressables());
        }

        // ------------------------------------------------------------------ pipeline

        public static Report Run(Options InOptions)
        {
            var report = new Report();
            report.Target = InOptions.Target.ToString();
            report.Development = InOptions.Development;
            var started = DateTime.Now;
            try
            {
                if (InOptions.SkipSetup == false)
                {
                    ensureSetup(report);
                }

                applyVersion(InOptions, report);
                var target = toBuildTarget(InOptions.Target);
                var group = BuildPipeline.GetBuildTargetGroup(target);
                if (EditorUserBuildSettings.activeBuildTarget != target)
                {
                    report.Messages.Add($"빌드 타깃 전환: {EditorUserBuildSettings.activeBuildTarget} → {target}");
                    EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
                }

                if (InOptions.Target == ETarget.Android)
                {
                    configureAndroid(InOptions, report);
                }

                if (InOptions.SkipAddressables == false)
                {
                    report.Addressables = BuildAddressables();
                    if (report.Addressables.StartsWith("error") == true)
                    {
                        throw new Exception(report.Addressables);
                    }
                }

                string output = resolveOutput(InOptions);
                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (scenes.Length == 0)
                {
                    throw new Exception("Build Settings 에 씬이 없습니다 (DemoLA/Setup/0. 전체 셋업).");
                }

                var playerOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = target,
                    targetGroup = group,
                    options = InOptions.Development == true ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None,
                };

                var buildReport = BuildPipeline.BuildPlayer(playerOptions);
                var summary = buildReport.summary;
                report.Result = summary.result.ToString();
                report.OutputPath = summary.outputPath;
                report.TotalSizeBytes = (long)summary.totalSize;
                report.Errors = summary.totalErrors;
                report.Warnings = summary.totalWarnings;
                foreach (var step in buildReport.steps)
                {
                    foreach (var message in step.messages)
                    {
                        if (message.type == LogType.Error || message.type == LogType.Exception)
                        {
                            report.Messages.Add($"[{step.name}] {message.content}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                report.Result = BuildResult.Failed.ToString();
                report.Messages.Add(e.Message);
                Debug.LogException(e);
            }

            report.BuildSeconds = (DateTime.Now - started).TotalSeconds;
            report.FinishedAt = DateTime.Now.ToString("o");
            writeReport(report);
            Debug.Log($"[DemoLA Build] {report.Target} {report.Result} ({report.BuildSeconds:0}s) → {report.OutputPath}");
            return report;
        }

        /// <summary>Addressables 콘텐츠 빌드 (로컬 번들, 기본 카탈로그). 결과 문자열이 "error" 로 시작하면 실패.</summary>
        public static string BuildAddressables()
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return "skipped (Addressables 설정 없음)";
            }

            suppressBuildReportPrompt();
            AddressableAssetSettings.CleanPlayerContent();
            AddressablesPlayerBuildResult result;
            AddressableAssetSettings.BuildPlayerContent(out result);
            if (string.IsNullOrEmpty(result.Error) == false)
            {
                return "error: " + result.Error;
            }

            return $"ok ({result.Duration:0.0}s, {result.FileRegistry?.GetFilePaths().Count() ?? 0} files)";
        }

        /// <summary>
        /// Addressables 2.x 는 에디터(배치 모드 아님)에서 처음 콘텐츠를 빌드할 때 "Build Report" 예/아니오 모달을 띄워
        /// 자동 빌드(CLI)가 멈춘다. 이미 안내했다는 내부 플래그를 켜서 묻지 않게 한다 (Debug Build Layout 설정은 그대로).
        /// </summary>
        private static void suppressBuildReportPrompt()
        {
            var property = typeof(UnityEditor.AddressableAssets.Settings.ProjectConfigData).GetProperty("UserHasBeenInformedAboutBuildReportSettingPreBuild",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (property != null && property.CanWrite == true)
            {
                property.SetValue(null, true);
            }
        }

        public static Options ParseArgs(string[] InArgs)
        {
            var options = new Options();
            for (int i = 0; i < InArgs.Length; ++i)
            {
                string arg = InArgs[i];
                string next = i + 1 < InArgs.Length ? InArgs[i + 1] : null;
                switch (arg)
                {
                    case "-demolaTarget":
                        ETarget target;
                        if (Enum.TryParse(next, true, out target) == true)
                        {
                            options.Target = target;
                        }

                        break;
                    case "-outputPath":
                        options.OutputPath = next;
                        break;
                    case "-development":
                        options.Development = true;
                        break;
                    case "-skipSetup":
                        options.SkipSetup = true;
                        break;
                    case "-skipAddressables":
                        options.SkipAddressables = true;
                        break;
                    case "-aab":
                        options.AppBundle = true;
                        break;
                    case "-buildNumber":
                        int.TryParse(next, out options.BuildNumber);
                        break;
                    case "-versionName":
                        options.VersionName = next;
                        break;
                }
            }

            return options;
        }

        // ------------------------------------------------------------------ helpers

        private static void ensureSetup(Report InReport)
        {
            bool hasData = AssetDatabase.LoadAssetAtPath<DemoLA.Data.GameDatabase>(DemoLAProjectSetup.DatabasePath) != null;
            bool hasScene = File.Exists(DemoLAProjectSetup.ScenePath);
            if (hasData == true && hasScene == true)
            {
                // 데이터는 유지하고 CSV 테이블 · Addressables 등록만 최신으로 맞춘다.
                InReport.Messages.Add(DemoLAProjectSetup.RunFullSetup(false, false));
                return;
            }

            InReport.Messages.Add("셋업 실행: " + DemoLAProjectSetup.RunFullSetup(hasData == false, true));
        }

        private static void applyVersion(Options InOptions, Report InReport)
        {
            if (string.IsNullOrEmpty(InOptions.VersionName) == false)
            {
                PlayerSettings.bundleVersion = InOptions.VersionName;
            }

            if (InOptions.BuildNumber > 0)
            {
                PlayerSettings.Android.bundleVersionCode = InOptions.BuildNumber;
                PlayerSettings.macOS.buildNumber = InOptions.BuildNumber.ToString();
            }

            InReport.Version = PlayerSettings.bundleVersion;
            InReport.BuildNumber = InOptions.BuildNumber;
        }

        private static void configureAndroid(Options InOptions, Report InReport)
        {
            EditorUserBuildSettings.buildAppBundle = InOptions.AppBundle;
            string keystore = Environment.GetEnvironmentVariable("DEMOLA_KEYSTORE_PATH");
            if (string.IsNullOrEmpty(keystore) == false && File.Exists(keystore) == true)
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("DEMOLA_KEYSTORE_PASS");
                PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("DEMOLA_KEY_ALIAS");
                PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("DEMOLA_KEY_PASS");
                InReport.Messages.Add("Android: 환경 변수 키스토어로 서명");
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = false;
                InReport.Messages.Add("Android: 디버그 서명 (DEMOLA_KEYSTORE_PATH 없음)");
            }
        }

        private static string resolveOutput(Options InOptions)
        {
            string folder = string.IsNullOrEmpty(InOptions.OutputPath) == false ? InOptions.OutputPath : $"Builds/{InOptions.Target}{(InOptions.Development ? "-Dev" : string.Empty)}";
            string product = string.IsNullOrEmpty(PlayerSettings.productName) == false ? PlayerSettings.productName : "DemoLA";
            switch (InOptions.Target)
            {
                case ETarget.Win64:
                    return Path.Combine(folder, product + ".exe");
                case ETarget.Android:
                    return Path.Combine(folder, product + (InOptions.AppBundle ? ".aab" : ".apk"));
                case ETarget.OSX:
                    return Path.Combine(folder, product + ".app");
                default:
                    return Path.Combine(folder, product);
            }
        }

        private static BuildTarget toBuildTarget(ETarget InTarget)
        {
            switch (InTarget)
            {
                case ETarget.Android: return BuildTarget.Android;
                case ETarget.Linux64: return BuildTarget.StandaloneLinux64;
                case ETarget.OSX: return BuildTarget.StandaloneOSX;
                default: return BuildTarget.StandaloneWindows64;
            }
        }

        private static void writeReport(Report InReport)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(InReport, true));
        }

        /// <summary>비동기 빌드 시작 표시 (build_status 가 "running" 을 돌려준다).</summary>
        public static void WriteRunning(Options InOptions)
        {
            var report = new Report();
            report.Target = InOptions.Target.ToString();
            report.Result = "Running";
            report.Development = InOptions.Development;
            report.FinishedAt = string.Empty;
            writeReport(report);
        }

        public static Report ReadLastReport()
        {
            return File.Exists(ReportPath) == true ? JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath)) : null;
        }
    }
}
