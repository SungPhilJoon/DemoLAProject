using System;
using System.Collections;
using DemoLA.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace DemoLA.Network.Testing
{
    /// <summary>
    /// 멀티플레이 테스트용 명령줄 인자. 인자가 없으면 아무것도 하지 않는다 (일반 실행 · Relay 로비 그대로).
    ///   -demolaNet host|client      역할 (Relay 없이 UnityTransport 직접 접속)
    ///   -demolaAddress 127.0.0.1    클라이언트가 붙을 주소 (기본 127.0.0.1)
    ///   -demolaPort 7777            포트
    ///   -demolaGate Gate2           호스트가 시작할 관문
    ///   -demolaStartWhen 2          호스트: 이 인원(호스트 포함)이 접속하면 레이드 시작 (기본 1 = 바로)
    ///   -demolaAutopilot            자기 캐릭터를 자동 조작 (이동 · 스킬 · 회피 · 에스더 — 클라이언트는 실제 RPC 경로)
    ///   -demolaGodMode              (호스트) 플레이어 팀 무적 — 오래 돌리는 동기화 측정용
    ///   -demolaProbe Builds/NetTest 동기화 스냅샷을 {역할}-{clientId}.jsonl 로 기록 (NetSyncProbe)
    ///   -demolaQuitAfter 60         레이드 시작 후 N 초 뒤 종료 (0 = 종료 안 함)
    ///   -demolaScript "8:gimmick:G2-05;15:pattern:G2-24"
    ///                               (호스트) 레이드 시작 후 N 초에 기믹 / 패턴을 강제로 실행 — 카운터 · 지형 붕괴 · 구조물 동기화 확인용
    /// 실행: Tools/dev.sh net [클라이언트 수] [초] [관문]  (빌드한 플레이어를 호스트 1 + 클라이언트 N 개로 띄우고 분석)
    /// </summary>
    public class NetTestArgs
    {
        public string Mode;
        public string Address = "127.0.0.1";
        public ushort Port = 7777;
        public string Gate = "Gate2";
        public int StartWhen = 1;
        public bool Autopilot;
        public bool GodMode;
        public string ProbeDirectory;
        public float QuitAfter;
        public string Script;

        public bool IsHost
        {
            get { return Mode == "host"; }
        }

        public static NetTestArgs Parse(string[] InArgs)
        {
            var args = new NetTestArgs();
            for (int i = 0; i < InArgs.Length; ++i)
            {
                string next = i + 1 < InArgs.Length ? InArgs[i + 1] : string.Empty;
                switch (InArgs[i])
                {
                    case "-demolaNet": args.Mode = next.ToLowerInvariant(); break;
                    case "-demolaAddress": args.Address = next; break;
                    case "-demolaPort": ushort.TryParse(next, out args.Port); break;
                    case "-demolaGate": args.Gate = next; break;
                    case "-demolaStartWhen": int.TryParse(next, out args.StartWhen); break;
                    case "-demolaAutopilot": args.Autopilot = true; break;
                    case "-demolaGodMode": args.GodMode = true; break;
                    case "-demolaProbe": args.ProbeDirectory = next; break;
                    case "-demolaQuitAfter": float.TryParse(next, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out args.QuitAfter); break;
                    case "-demolaScript": args.Script = next; break;
                }
            }

            return args;
        }
    }

    /// <summary>Relay 없이 같은 PC / LAN 에서 바로 접속 (테스트 · 로컬 개발용). 로비 창의 LAN 버튼도 이것을 쓴다.</summary>
    public static class NetDirectConnect
    {
        public static bool StartHost(ushort InPort)
        {
            var manager = NetworkManager.Singleton;
            var transport = manager != null ? manager.GetComponent<UnityTransport>() : null;
            if (transport == null)
            {
                Debug.LogError("[NetTest] NetworkManager / UnityTransport 없음 (레이드 씬에서 실행)");
                return false;
            }

            transport.SetConnectionData("127.0.0.1", InPort, "0.0.0.0");
            return manager.StartHost();
        }

        public static bool StartClient(string InAddress, ushort InPort)
        {
            var manager = NetworkManager.Singleton;
            var transport = manager != null ? manager.GetComponent<UnityTransport>() : null;
            if (transport == null)
            {
                Debug.LogError("[NetTest] NetworkManager / UnityTransport 없음 (레이드 씬에서 실행)");
                return false;
            }

            transport.SetConnectionData(InAddress, InPort);
            return manager.StartClient();
        }
    }

    /// <summary>명령줄 인자로 호스트/클라이언트를 자동 시작하고, 자동 조작 · 스냅샷 기록 · 자동 종료를 붙인다.</summary>
    public class NetTestLauncher : MonoBehaviour
    {
        private NetTestArgs args;
        private float raidStartedAt = -1.0f;

        public static NetTestArgs Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void boot()
        {
            var parsed = NetTestArgs.Parse(Environment.GetCommandLineArgs());
            if (parsed.Mode != "host" && parsed.Mode != "client")
            {
                return;
            }

            Current = parsed;
            var go = new GameObject("NetTestLauncher");
            DontDestroyOnLoad(go);
            go.AddComponent<NetTestLauncher>().args = parsed;
            if (Application.isBatchMode == true)
            {
                Application.targetFrameRate = 60;
            }
        }

        private IEnumerator Start()
        {
            Debug.Log($"[NetTest] {args.Mode} 시작 대기 (port {args.Port}, gate {args.Gate})");
            while (GameBootstrap.Current == null || Framework.Managers.Get<GameSession>() == null || NetworkManager.Singleton == null)
            {
                yield return null;
            }

            // 대기실/레이드 준비가 끝나도록 한 프레임 더.
            yield return new WaitForSeconds(0.5f);
            bool started = args.IsHost == true ? NetDirectConnect.StartHost(args.Port) : NetDirectConnect.StartClient(args.Address, args.Port);
            Debug.Log($"[NetTest] {args.Mode} 시작 {(started ? "성공" : "실패")}");
            if (started == false)
            {
                Application.Quit(2);
                yield break;
            }

            if (string.IsNullOrEmpty(args.ProbeDirectory) == false)
            {
                gameObject.AddComponent<NetSyncProbe>().Begin(args.ProbeDirectory, args.IsHost ? "host" : "client");
            }

            if (args.Autopilot == true)
            {
                gameObject.AddComponent<NetAutopilot>();
            }

            if (args.IsHost == true)
            {
                yield return hostFlow();
            }
            else
            {
                yield return clientFlow();
            }
        }

        private IEnumerator hostFlow()
        {
            var manager = NetworkManager.Singleton;
            float timeout = Time.realtimeSinceStartup + 60.0f;
            while (manager.ConnectedClientsList.Count < Mathf.Max(1, args.StartWhen) && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Debug.Log($"[NetTest] 접속 {manager.ConnectedClientsList.Count}명 → 레이드 시작 ({args.Gate})");
            // 클라이언트의 직업 · 빌드(SubmitLoadoutRpc)가 도착할 시간.
            yield return new WaitForSeconds(1.5f);
            while (NetworkRaidDirector.Instance == null)
            {
                yield return null;
            }

            NetworkRaidDirector.Instance.ServerStartRaid(args.Gate);
            raidStartedAt = Time.realtimeSinceStartup;
            if (args.GodMode == true)
            {
                StartCoroutine(godMode());
            }

            if (string.IsNullOrEmpty(args.Script) == false)
            {
                StartCoroutine(runScript(args.Script));
            }

            yield return quitLater();
        }

        private IEnumerator clientFlow()
        {
            var manager = NetworkManager.Singleton;
            float timeout = Time.realtimeSinceStartup + 30.0f;
            while (manager.IsConnectedClient == false && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            if (manager.IsConnectedClient == false)
            {
                Debug.LogError("[NetTest] 접속 실패 (시간 초과)");
                Application.Quit(3);
                yield break;
            }

            Debug.Log($"[NetTest] 접속 완료 (clientId {manager.LocalClientId})");
            raidStartedAt = Time.realtimeSinceStartup;
            yield return quitLater();
        }

        /// <summary>"초:gimmick|pattern:ID;..." 를 레이드 시작 기준 시각에 실행한다. 보스가 바쁘거나 쿨다운이면 12초까지 다시 시도.</summary>
        private IEnumerator runScript(string InScript)
        {
            var entries = new System.Collections.Generic.List<(float Time, string Kind, string Id)>();
            foreach (var part in InScript.Split(';'))
            {
                var cells = part.Trim().Split(':');
                float time;
                if (cells.Length == 3 && float.TryParse(cells[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out time) == true)
                {
                    entries.Add((time, cells[1].Trim().ToLowerInvariant(), cells[2].Trim()));
                }
            }

            entries.Sort((a, b) => a.Time.CompareTo(b.Time));
            foreach (var entry in entries)
            {
                while (Time.realtimeSinceStartup - raidStartedAt < entry.Time)
                {
                    yield return null;
                }

                float giveUp = Time.realtimeSinceStartup + 12.0f;
                bool done = false;
                while (done == false && Time.realtimeSinceStartup < giveUp)
                {
                    var boss = FindAnyObjectByType<DemoLA.Boss.BossActor>();
                    if (boss != null && boss.IsDead == false)
                    {
                        if (entry.Kind == "gimmick")
                        {
                            var running = boss.ActiveGimmick;
                            if (running != null && running.GimmickId != entry.Id)
                            {
                                running.ResetGimmick();
                            }

                            done = boss.StartGimmick(entry.Id);
                        }
                        else
                        {
                            done = boss.IsBusy == false && boss.TryActivatePattern(entry.Id, null);
                        }
                    }

                    if (done == false)
                    {
                        yield return new WaitForSeconds(0.25f);
                    }
                }

                Debug.Log($"[NetTest] script {entry.Time}s {entry.Kind} {entry.Id} → {(done ? "실행" : "실패")}");
            }
        }

        /// <summary>플레이어 팀이 죽지 않게 체력을 채운다 (서버). 동기화 측정이 전멸로 끊기지 않게.</summary>
        private IEnumerator godMode()
        {
            while (true)
            {
                foreach (var actor in DemoLA.Combat.CombatRegistry.All)
                {
                    if (actor != null && actor.Team == ETeam.Player && actor.IsDead == false)
                    {
                        float max = actor.Stats.Get(DemoLA.Attributes.EAttr.MaxHealth);
                        if (actor.Stats.Get(DemoLA.Attributes.EAttr.Health) < max * 0.5f)
                        {
                            actor.Stats.Set(DemoLA.Attributes.EAttr.Health, max);
                        }
                    }
                }

                yield return new WaitForSeconds(0.5f);
            }
        }

        private IEnumerator quitLater()
        {
            if (args.QuitAfter <= 0.0f)
            {
                yield break;
            }

            while (Time.realtimeSinceStartup - raidStartedAt < args.QuitAfter)
            {
                yield return null;
            }

            Debug.Log("[NetTest] 시간 종료");
            var probe = GetComponent<NetSyncProbe>();
            if (probe != null)
            {
                probe.Flush();
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            yield return new WaitForSeconds(0.3f);
            Application.Quit(0);
        }
    }
}
