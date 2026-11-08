using System;
using DemoLA.Character;
using DemoLA.Core;
using DemoLA.Data;
using DemoLA.Editor.Build;
using DemoLA.Tools;
using Framework;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor.Pipeline
{
    /// <summary>
    /// Unity CLI 용 프로젝트 명령 — `unity command demola &lt;name&gt;`.
    /// 터미널(Tools/dev.sh · dev.ps1) · CI · 에이전트가 열린 에디터를 직접 조작해 셋업 → 플레이 확인 → 빌드까지 돌린다.
    /// 로직은 DemoLA.Editor / DemoLA.Runtime 에 두고 여기서는 얇게 감싼다 (패키지가 없으면 이 어셈블리는 컴파일되지 않는다).
    /// </summary>
    public static class DemoLAPipelineCommands
    {
        private const string Group = "demola";

        [CliGroupCommand(Group, "Run the full DemoLA setup: CSV tables, data assets, input asset, Addressables group and the raid scene. Not undoable via Ctrl+Z.")]
        public static object Setup(
            [CliArg("overwrite", "Recreate the generated data assets (editor edits to classes/skills/patterns are lost).", DefaultValue = false)] bool overwrite = false,
            [CliArg("scene", "Also (re)create Assets/_Project/Scenes/Raid.unity.", DefaultValue = true)] bool scene = true)
        {
            bool hasData = AssetDatabase.LoadAssetAtPath<GameDatabase>(DemoLAProjectSetup.DatabasePath) != null;
            return new { success = true, summary = DemoLAProjectSetup.RunFullSetup(overwrite == true || hasData == false, scene) };
        }

        [CliGroupCommand(Group, "Re-import CSV tables (Assets/_Project/Data/CSV) and refresh boss pattern assets from BossPatternTable.")]
        public static object ImportTables()
        {
            DemoLAProjectSetup.ImportTables();
            return new { success = true, patterns = BossPatternImporter.Reimport() };
        }

        [CliGroupCommand(Group, "Select the raid gate (Gate1 / Gate2) used by the next play session.")]
        public static object SelectGate(
            [CliArg("gate", "Boss id, e.g. Gate1 or Gate2.", Required = true)] string gate)
        {
            // 메인 스레드를 막고 비동기 저장을 기다리면 교착되므로 동기로 읽고 쓴다.
            string path = new DemoLA.Save.LocalJsonSaveService().FilePath;
            var data = System.IO.File.Exists(path) ? JsonUtility.FromJson<DemoLA.Save.SaveData>(System.IO.File.ReadAllText(path)) : new DemoLA.Save.SaveData();
            data.SelectedBoss = gate;
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(data, true));
            return new { success = true, gate, path };
        }

        [CliGroupCommand(Group, "In the raid lobby, mark the local player ready (the raid starts after every member is ready and the countdown ends).")]
        public static object Ready()
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (EditorApplication.isPlaying == false || bootstrap == null)
            {
                return new { success = false, message = "플레이 중이 아님" };
            }

            if (bootstrap.InLobby == false)
            {
                return new { success = true, message = "이미 레이드 중" };
            }

            var lobby = bootstrap.GetComponent<DemoLA.World.RaidLobby>();
            lobby.SetPlayerReady(true);
            return new { success = true, message = $"준비 완료 ({lobby.ReadyCount}/{lobby.Members.Count}) — 전원 준비 후 {DemoLA.World.RaidLobby.EnterDelay:0}초 뒤 입장" };
        }

        [CliGroupCommand(Group, "Damage meter report of the running play session (per party member and per skill).")]
        public static object Dps()
        {
            return new { playing = EditorApplication.isPlaying, report = DpsMeter.Report() };
        }

        [CliGroupCommand(Group, "Raid state of the running play session: boss hp lines, phase, active pattern/gimmick, party health.")]
        public static object RaidStatus()
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (EditorApplication.isPlaying == false || bootstrap == null || bootstrap.Boss == null)
            {
                return new { playing = EditorApplication.isPlaying, message = bootstrap != null && bootstrap.InLobby ? "레이드 입장 대기실" : "레이드 진행 중이 아님" };
            }

            var boss = bootstrap.Boss;
            var party = new System.Collections.Generic.List<object>();
            foreach (var actor in DemoLA.Combat.CombatRegistry.All)
            {
                if (actor is PartyCharacter member)
                {
                    party.Add(new { name = member.GetName(), hp = member.Stats.HealthRatio, dead = member.IsDead });
                }
            }

            return new
            {
                playing = true,
                boss = boss.Definition.DisplayName,
                hpLines = boss.HpLinesExact,
                phase = boss.CurrentPhaseId,
                pattern = boss.ActivePattern != null ? boss.ActivePattern.Definition.PatternId : null,
                gimmick = boss.ActiveGimmick != null ? boss.ActiveGimmick.GimmickId : null,
                party,
            };
        }

        [CliGroupCommand(Group, "Deterministic DPS simulation (balance tool) for one class against a training dummy.")]
        public static object Balance(
            [CliArg("class", "Summoner | Infighter | Slayer | Artist", DefaultValue = "Slayer")] string className = "Slayer",
            [CliArg("seconds", "Simulated combat seconds.", DefaultValue = 60.0f)] float seconds = 60.0f,
            [CliArg("trials", "Number of seeded trials to average.", DefaultValue = 5)] int trials = 5,
            [CliArg("behind", "Attack from behind (back attack).", DefaultValue = false)] bool behind = false,
            [CliArg("bare", "Use an empty build instead of the default full build.", DefaultValue = false)] bool bare = false)
        {
            var database = loadDatabase();
            var cls = (ECharacterClass)Enum.Parse(typeof(ECharacterClass), className, true);
            var config = new SimulationConfig
            {
                Label = $"{cls}{(bare ? " 맨몸" : string.Empty)}{(behind ? " 백" : string.Empty)}",
                Class = cls,
                Duration = seconds,
                Trials = trials,
                FromBehind = behind,
                Build = bare ? new DemoLA.Build.CharacterBuildData { Class = cls } : null,
            };
            var result = CombatSimulator.Run(database, config);
            return new { dps = result.MeanDps, std = result.StdDps, crit = result.CritRate, back = result.BackRate, report = CombatSimulator.Describe(result) };
        }

        [CliGroupCommand(Group, "Addressables dependency report (duplicates, missing references, Resources usage). Writes DependencyReports/*.md and *.json.")]
        public static object Deps()
        {
            var summary = DependencyReport.Run();
            return new { success = true, summary };
        }

        [CliGroupCommand(Group, "Queue a player build with DemoLA.Editor.Build.BuildCommand (setup, Addressables, player). Returns immediately; poll 'demola build_status'. Not undoable via Ctrl+Z.")]
        public static object Build(
            [CliArg("target", "Win64 | Android | Linux64 | OSX", DefaultValue = "Win64")] string target = "Win64",
            [CliArg("development", "Development build.", DefaultValue = false)] bool development = false,
            [CliArg("confirm", "Required to actually build.", DefaultValue = false)] bool confirm = false)
        {
            if (confirm == false)
            {
                return new { success = false, message = "confirm=true 가 있어야 빌드합니다." };
            }

            var options = new BuildCommand.Options
            {
                Target = (BuildCommand.ETarget)Enum.Parse(typeof(BuildCommand.ETarget), target, true),
                Development = development,
            };
            // 빌드는 메인 스레드를 오래 잡으므로 다음 에디터 틱에 시작하고 바로 응답한다.
            BuildCommand.WriteRunning(options);
            EditorApplication.delayCall += () => BuildCommand.Run(options);
            return new { success = true, status = "queued", target = options.Target.ToString(), poll = "unity command demola build_status" };
        }

        [CliGroupCommand(Group, "Last build report written by BuildCommand (Builds/build-report.json).", MainThreadRequired = false)]
        public static object BuildStatus()
        {
            return (object)BuildCommand.ReadLastReport() ?? new { message = "빌드 기록 없음" };
        }

        private static GameDatabase loadDatabase()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DemoLAProjectSetup.DatabasePath);
            if (database != null)
            {
                return database;
            }

            string errors;
            return DemoLA.Content.DefaultContent.CreateDatabase(DemoLATables.FromCsv(TablePaths.ReadCsv, out errors));
        }
    }
}
