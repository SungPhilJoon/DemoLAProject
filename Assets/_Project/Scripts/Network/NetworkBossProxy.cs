using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.World;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace DemoLA.Network
{
    /// <summary>
    /// 네트워크 보스 (프리팹 NetBoss). 서버: BossActor + StateTree 진행. 클라이언트: 같은 정의로 표시용 BossActor 를 만들고
    /// 체력 · 페이즈 · 무력화 게이지 · 상태 태그만 반영한다 (패턴 판정은 서버, 장판 연출은 NetworkRaidDirector 가 복제).
    /// </summary>
    public class NetworkBossProxy : NetworkBehaviour
    {
        public readonly NetworkVariable<FixedString32Bytes> BossId = new NetworkVariable<FixedString32Bytes>();
        public readonly NetworkVariable<int> PartySize = new NetworkVariable<int>(4);
        private readonly NetworkVariable<float> maxHealth = new NetworkVariable<float>();
        private readonly NetworkVariable<float> health = new NetworkVariable<float>();
        private readonly NetworkVariable<float> maxStagger = new NetworkVariable<float>();
        private readonly NetworkVariable<float> stagger = new NetworkVariable<float>();
        private readonly NetworkVariable<FixedString32Bytes> phase = new NetworkVariable<FixedString32Bytes>();
        private readonly NetworkVariable<int> tags = new NetworkVariable<int>();
        private readonly NetworkVariable<float> esther = new NetworkVariable<float>();

        private float replicateTimer;

        public BossActor Boss { get; private set; }

        public static event System.Action<NetworkBossProxy> OnBossReady;

        public override void OnNetworkSpawn()
        {
            if (IsServer == false)
            {
                DemoLA.Core.NetAuthority.IsReplica = true;
            }

            BossId.OnValueChanged += (a, b) => build();
            tags.OnValueChanged += handleTags;
            phase.OnValueChanged += (a, b) =>
            {
                if (IsServer == false && Boss != null)
                {
                    Boss.SetPhase(b.ToString());
                }
            };
            build();
        }

        public void ServerInit(string InBossId, int InPartySize)
        {
            PartySize.Value = InPartySize;
            BossId.Value = InBossId;
            build();
        }

        private void build()
        {
            var session = NetSession.Get();
            string id = BossId.Value.ToString();
            if (Boss != null || session == null || string.IsNullOrEmpty(id) == true)
            {
                return;
            }

            var definition = session.Database.GetBoss(id);
            if (definition == null)
            {
                return;
            }

            Boss = RaidSpawner.AttachBoss(gameObject, definition, PartySize.Value, IsServer);
            if (IsServer == false)
            {
                var agent = GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.enabled = false;
                }

                applyVitals();
                maxHealth.OnValueChanged += (a, b) => applyVitals();
                health.OnValueChanged += (a, b) => applyVitals();
                stagger.OnValueChanged += (a, b) => applyVitals();
                // 에스더 게이지는 서버가 모은다 → 클라이언트는 받기만 한다.
                DemoLA.Character.EstherSystem.IsReplica = true;
                DemoLA.Character.EstherSystem.SetGauge(esther.Value);
                esther.OnValueChanged += (a, b) => DemoLA.Character.EstherSystem.SetGauge(b);
                NetReplication.ApplyTags(Boss, 0, tags.Value);
            }

            OnBossReady?.Invoke(this);
        }

        private void handleTags(int InPrevious, int InCurrent)
        {
            if (IsServer == false && Boss != null)
            {
                NetReplication.ApplyTags(Boss, InPrevious, InCurrent);
            }
        }

        private void applyVitals()
        {
            // 서버 값이 아직 오지 않았으면(최대 체력 0) 반영하지 않는다 — 초기값 0 으로 클라이언트에서 보스가 "죽는" 문제.
            if (Boss == null || Boss.Stats == null || maxHealth.Value <= 0.0f)
            {
                return;
            }

            Boss.Stats.Set(EAttr.MaxHealth, maxHealth.Value);
            Boss.Stats.Set(EAttr.Health, health.Value);
            Boss.Stats.Set(EAttr.MaxStaggerGauge, maxStagger.Value);
            Boss.Stats.Set(EAttr.StaggerGauge, stagger.Value);
        }

        private void Update()
        {
            if (IsServer == false || Boss == null || Boss.Stats == null)
            {
                return;
            }

            replicateTimer -= Time.deltaTime;
            if (replicateTimer > 0.0f)
            {
                return;
            }

            replicateTimer = 0.1f;
            maxHealth.Value = Boss.Stats.Get(EAttr.MaxHealth);
            health.Value = Boss.Stats.Get(EAttr.Health);
            maxStagger.Value = Boss.Stats.Get(EAttr.MaxStaggerGauge);
            stagger.Value = Boss.Stats.Get(EAttr.StaggerGauge);
            phase.Value = Boss.CurrentPhaseId ?? string.Empty;
            tags.Value = NetReplication.CaptureTags(Boss);
            esther.Value = DemoLA.Character.EstherSystem.Gauge;
        }
    }
}
