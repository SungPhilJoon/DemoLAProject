using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Boss.Telegraph;
using DemoLA.Character;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.World;
using Unity.Netcode;
using UnityEngine;

namespace DemoLA.Network.Testing
{
    /// <summary>
    /// 동기화 측정: 모든 피어가 같은 기준 시각(NetworkManager.ServerTime — 클라이언트는 추정 서버 시각)으로
    /// 0.2초마다 상태 스냅샷을, 장판 · 카운터 구간이 시작될 때 이벤트를 JSON 한 줄씩 남긴다.
    /// 분석: python Tools/data/net_sync_check.py Builds/NetTest  (호스트와 클라이언트를 비교해 어긋난 항목 보고)
    ///
    /// 스냅샷 항목: 보스(체력 · 무력화 · 페이즈 · 복제 태그 · 위치 · 방향) · 파티원별(체력 · 태그 · 위치 · 사망) ·
    /// 에스더 게이지 · 무너진 구조물 · 무너진 바닥 타일 · 보이는 장판 수 · 바닥 구슬 수
    /// </summary>
    public class NetSyncProbe : MonoBehaviour
    {
        private const float Interval = 0.2f;

        private StreamWriter writer;
        private readonly StringBuilder line = new StringBuilder(1024);
        private float timer;
        private string role;
        private bool counterOpen;

        public string FilePath { get; private set; }

        public void Begin(string InDirectory, string InRole)
        {
            role = InRole;
            Directory.CreateDirectory(InDirectory);
            ulong id = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
            FilePath = Path.Combine(InDirectory, $"{InRole}-{id}-{System.Diagnostics.Process.GetCurrentProcess().Id}.jsonl");
            writer = new StreamWriter(FilePath, false, new UTF8Encoding(false));
            BossPatternAbility.TelegraphStarted += serverTelegraph;
            NetworkRaidDirector.ClientTelegraph += clientTelegraph;
            Debug.Log($"[NetTest] probe → {FilePath}");
        }

        private void OnDestroy()
        {
            BossPatternAbility.TelegraphStarted -= serverTelegraph;
            NetworkRaidDirector.ClientTelegraph -= clientTelegraph;
            Flush();
            if (writer != null)
            {
                writer.Dispose();
                writer = null;
            }
        }

        public void Flush()
        {
            if (writer != null)
            {
                writer.Flush();
            }
        }

        private static double serverTime()
        {
            var manager = NetworkManager.Singleton;
            return manager != null && manager.IsListening == true ? manager.ServerTime.Time : Time.realtimeSinceStartupAsDouble;
        }

        private void serverTelegraph(HitArea InArea, Vector3 InOrigin, Vector3 InForward, float InDuration)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer == true)
            {
                writeEvent("tele", InOrigin, InDuration);
            }
        }

        private void clientTelegraph(Vector3 InOrigin, float InDuration)
        {
            writeEvent("tele", InOrigin, InDuration);
        }

        private void writeEvent(string InType, Vector3 InOrigin, float InValue)
        {
            if (writer == null)
            {
                return;
            }

            line.Clear();
            line.Append("{\"type\":\"").Append(InType).Append("\",\"role\":\"").Append(role).Append("\",\"t\":").Append(f(serverTime()))
                .Append(",\"x\":").Append(f(InOrigin.x)).Append(",\"z\":").Append(f(InOrigin.z)).Append(",\"v\":").Append(f(InValue)).Append('}');
            writer.WriteLine(line.ToString());
        }

        private void Update()
        {
            if (writer == null || NetworkManager.Singleton == null || NetworkManager.Singleton.IsListening == false)
            {
                return;
            }

            var boss = FindAnyObjectByType<BossActor>();
            if (boss != null && boss.Stats != null)
            {
                bool open = boss.HasTag((int)ETag.Boss_CounterWindow);
                if (open != counterOpen)
                {
                    counterOpen = open;
                    writeEvent(open ? "counter_open" : "counter_close", boss.Position, 0.0f);
                }
            }

            timer -= Time.unscaledDeltaTime;
            if (timer > 0.0f)
            {
                return;
            }

            timer = Interval;
            writeSnapshot(boss);
        }

        private void writeSnapshot(BossActor InBoss)
        {
            line.Clear();
            line.Append("{\"type\":\"snap\",\"role\":\"").Append(role).Append("\",\"t\":").Append(f(serverTime()));
            if (InBoss != null && InBoss.Stats != null)
            {
                var p = InBoss.Position;
                line.Append(",\"boss\":{\"hp\":").Append(f(InBoss.Stats.Get(EAttr.Health)))
                    .Append(",\"st\":").Append(f(InBoss.Stats.Get(EAttr.StaggerGauge)))
                    .Append(",\"ph\":\"").Append(InBoss.CurrentPhaseId ?? string.Empty).Append('"')
                    .Append(",\"tags\":").Append(NetReplication.CaptureTags(InBoss))
                    .Append(",\"x\":").Append(f(p.x)).Append(",\"z\":").Append(f(p.z))
                    .Append(",\"yaw\":").Append(f(InBoss.transform.eulerAngles.y)).Append('}');
            }

            line.Append(",\"m\":[");
            var members = FindObjectsByType<NetworkPartyMember>(FindObjectsSortMode.None);
            bool first = true;
            foreach (var member in members)
            {
                var c = member.Character;
                if (c == null || c.Stats == null)
                {
                    continue;
                }

                if (first == false)
                {
                    line.Append(',');
                }

                first = false;
                var p = c.Position;
                line.Append("{\"id\":").Append(member.NetworkObjectId)
                    .Append(",\"hp\":").Append(f(c.Stats.Get(EAttr.Health)))
                    .Append(",\"tags\":").Append(NetReplication.CaptureTags(c))
                    .Append(",\"dead\":").Append(c.IsDead ? "true" : "false")
                    .Append(",\"x\":").Append(f(p.x)).Append(",\"z\":").Append(f(p.z)).Append('}');
            }

            line.Append(']');
            line.Append(",\"es\":").Append(f(EstherSystem.Gauge));

            var destroyed = new List<string>();
            var structures = ArenaStructure.Structures;
            for (int i = 0; i < structures.Count; ++i)
            {
                if (structures[i] != null && structures[i].IsDestroyed == true)
                {
                    destroyed.Add(structures[i].name);
                }
            }

            destroyed.Sort(System.StringComparer.Ordinal);
            line.Append(",\"sd\":\"").Append(string.Join("|", destroyed)).Append('"');

            int collapsed = 0;
            if (ArenaFloor.Current != null)
            {
                var tiles = ArenaFloor.Current.Tiles;
                for (int i = 0; i < tiles.Count; ++i)
                {
                    if (tiles[i] != null && tiles[i].IsCollapsed == true)
                    {
                        collapsed += 1;
                    }
                }
            }

            line.Append(",\"fl\":").Append(collapsed);
            int telegraphs = 0;
            var views = TelegraphView.ActiveViews;
            var positions = new StringBuilder();
            for (int i = 0; i < views.Count; ++i)
            {
                if (views[i] != null && views[i].gameObject.activeSelf == true)
                {
                    telegraphs += 1;
                    var o = views[i].Origin;
                    positions.Append(positions.Length > 0 ? "," : string.Empty).Append('[').Append(f(o.x)).Append(',').Append(f(o.z)).Append(']');
                }
            }

            line.Append(",\"tv\":").Append(telegraphs);
            line.Append(",\"tp\":[").Append(positions).Append(']');
            line.Append(",\"pk\":").Append(FindObjectsByType<RaidPickup>(FindObjectsSortMode.None).Length);
            line.Append('}');
            writer.WriteLine(line.ToString());
        }

        private static string f(double InValue)
        {
            return InValue.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
