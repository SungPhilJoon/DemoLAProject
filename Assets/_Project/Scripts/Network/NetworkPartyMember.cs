using System;
using DemoLA.Attributes;
using DemoLA.AI;
using DemoLA.Build;
using DemoLA.Character;
using DemoLA.Core;
using DemoLA.Player;
using DemoLA.World;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace DemoLA.Network
{
    /// <summary>
    /// 네트워크 파티원 (PlayerPrefab · AI 아군 공용 프리팹 NetPartyMember).
    ///  - 플레이어 접속 시 NGO 가 스폰 → 소유 클라이언트가 직업과 빌드(JSON)를 서버로 보낸다 → 서버가 캐릭터를 만든다.
    ///  - AI 아군: 플레이어가 고르지 않은 직업을 서버가 같은 프리팹으로 스폰하고 StateTree 를 붙인다.
    ///  - 모든 피어가 같은 PartyCharacter 를 만들되, 서버만 GAS/AI 를 진행하고 클라이언트는 복제 값만 반영한다.
    /// </summary>
    public class NetworkPartyMember : NetworkBehaviour
    {
        public readonly NetworkVariable<int> ClassId = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<bool> IsAI = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<FixedString64Bytes> DisplayName = new NetworkVariable<FixedString64Bytes>();
        private readonly NetworkVariable<float> maxHealth = new NetworkVariable<float>();
        private readonly NetworkVariable<float> health = new NetworkVariable<float>();
        private readonly NetworkVariable<float> maxMana = new NetworkVariable<float>();
        private readonly NetworkVariable<float> mana = new NetworkVariable<float>();
        private readonly NetworkVariable<float> shield = new NetworkVariable<float>();
        private readonly NetworkVariable<float> identity = new NetworkVariable<float>();
        private readonly NetworkVariable<int> tags = new NetworkVariable<int>();

        private float replicateTimer;
        private string pendingBuildJson;

        public PartyCharacter Character { get; private set; }

        public static event Action<NetworkPartyMember> OnCharacterReady;

        public override void OnNetworkSpawn()
        {
            if (IsServer == false)
            {
                DemoLA.Core.NetAuthority.IsReplica = true;
            }

            ClassId.OnValueChanged += handleClassChanged;
            tags.OnValueChanged += handleTagsChanged;
            if (ClassId.Value >= 0)
            {
                build();
            }

            if (IsOwner == true && IsAI.Value == false)
            {
                // 소유자: 저장된 직업 · 빌드를 서버에 알린다.
                var session = NetSession.Get();
                var cls = session != null ? session.Save.PlayerClass : ECharacterClass.Slayer;
                var buildData = session != null ? session.GetBuild(cls) : new CharacterBuildData { Class = cls };
                SubmitLoadoutRpc((int)cls, JsonUtility.ToJson(buildData), session != null ? "플레이어" : "Player");
            }
        }

        public override void OnNetworkDespawn()
        {
            ClassId.OnValueChanged -= handleClassChanged;
            tags.OnValueChanged -= handleTagsChanged;
        }

        /// <summary>서버: AI 아군으로 초기화 (스폰 직후 호출).</summary>
        public void ServerInitAI(ECharacterClass InClass)
        {
            IsAI.Value = true;
            DisplayName.Value = $"AI {InClass}";
            ClassId.Value = (int)InClass;
        }

        [Rpc(SendTo.Server)]
        private void SubmitLoadoutRpc(int InClass, string InBuildJson, string InName)
        {
            pendingBuildJson = InBuildJson;
            DisplayName.Value = $"{InName} {OwnerClientId}";
            ClassId.Value = InClass;
            NetworkRaidDirector.Instance?.ServerOnLoadoutChanged();
        }

        // ------------------------------------------------------------------ commands (server)

        [Rpc(SendTo.Server)]
        public void UseSkillRpc(int InSlot, Vector3 InAim)
        {
            if (Character != null && Character.TryUseSlot(InSlot, InAim) == true)
            {
                notifyCooldown(Character.GetSkillInSlot(InSlot));
            }
        }

        [Rpc(SendTo.Server)]
        public void UseAwakeningRpc(Vector3 InAim)
        {
            Framework.Ability.EActivateFailReason reason;
            if (Character != null && Character.Awakening != null && Character.TryUseSkill(Character.Awakening, InAim, out reason) == true)
            {
                notifyCooldown(Character.Awakening);
            }
        }

        [Rpc(SendTo.Server)]
        public void UseIdentityRpc(Vector3 InAim)
        {
            Character?.TryUseIdentity(InAim);
        }

        [Rpc(SendTo.Server)]
        public void UseIdentityXRpc(Vector3 InAim)
        {
            Character?.TryUseIdentityX(InAim);
        }

        [Rpc(SendTo.Server)]
        public void UseEstherRpc(int InIndex)
        {
            Character?.TryUseEsther(InIndex);
        }

        [Rpc(SendTo.Server)]
        public void UseBattleItemRpc(int InSlot, Vector3 InAim)
        {
            if (Character != null && Character.TryUseBattleItem(InSlot, InAim) == true)
            {
                notifyCooldown(Character.BattleItems[InSlot]);
            }
        }

        [Rpc(SendTo.Server)]
        public void DodgeRpc(Vector3 InAim)
        {
            if (Character != null && Character.TryDodge(InAim) == true)
            {
                notifyCooldown(Character.Dodge);
            }
        }

        [Rpc(SendTo.Server)]
        public void MoveToRpc(Vector3 InDestination)
        {
            if (Character != null && Character.Motor != null)
            {
                Character.Motor.MoveTo(InDestination);
            }
        }

        /// <summary>소유자 HUD 의 쿨다운 표시용: 서버에서 실제로 쓴 쿨다운을 소유자 ASC 에 똑같이 건다.</summary>
        private void notifyCooldown(Framework.Ability.GameplayAbility InAbility)
        {
            if (InAbility == null || IsServer == false)
            {
                return;
            }

            float duration = Character.Asc.GetCooldownDuration(InAbility.AbilityId);
            if (duration > 0.0f && IsOwnedByServer == false)
            {
                CooldownRpc(InAbility.AbilityId, duration);
            }
        }

        [Rpc(SendTo.Owner)]
        private void CooldownRpc(int InAbilityId, float InDuration)
        {
            if (Character == null)
            {
                return;
            }

            var spec = Character.Asc.MakeOutgoingSpec(DemoLA.Effects.GameEffects.Cooldown, InAbilityId);
            spec.DurationOverride = InDuration;
            Character.Asc.ApplyGameplayEffectSpecToSelf(spec);
        }

        // ------------------------------------------------------------------ build / replicate

        private void handleClassChanged(int InPrevious, int InCurrent)
        {
            if (InCurrent >= 0 && Character == null)
            {
                build();
            }
        }

        private void build()
        {
            var session = NetSession.Get();
            var bootstrap = GameBootstrap.Current;
            if (session == null || Character != null)
            {
                return;
            }

            var db = session.Database;
            var cls = db.GetClass((ECharacterClass)ClassId.Value);
            CharacterBuildData buildData = null;
            if (IsServer == true && string.IsNullOrEmpty(pendingBuildJson) == false)
            {
                buildData = JsonUtility.FromJson<CharacterBuildData>(pendingBuildJson);
            }

            if (buildData == null || buildData.Class != cls.Class)
            {
                buildData = session.GetBuild(cls.Class).Clone();
            }

            // AI 의 StateTree 는 서버에만.
            var tree = IsServer == true && IsAI.Value == true && bootstrap != null ? bootstrap.PartyTree : null;
            gameObject.name = DisplayName.Value.ToString();
            Character = RaidSpawner.AttachCharacter(gameObject, cls, buildData, db, tree);
            Character.SetName(DisplayName.Value.ToString());
            if (IsServer == false)
            {
                // 클라이언트: 위치는 NetworkTransform, 판단은 서버.
                var agent = GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.enabled = false;
                }

                NetReplication.ApplyTags(Character, 0, tags.Value);
                applyVitals();
                maxHealth.OnValueChanged += (a, b) => applyVitals();
                health.OnValueChanged += (a, b) => applyVitals();
                mana.OnValueChanged += (a, b) => applyVitals();
                shield.OnValueChanged += (a, b) => applyVitals();
                identity.OnValueChanged += (a, b) => applyVitals();
            }

            OnCharacterReady?.Invoke(this);
        }

        private void handleTagsChanged(int InPrevious, int InCurrent)
        {
            if (IsServer == false && Character != null)
            {
                NetReplication.ApplyTags(Character, InPrevious, InCurrent);
            }
        }

        private void applyVitals()
        {
            // 서버 값이 아직 오지 않았으면(최대 체력 0) 반영하지 않는다 — 초기값 0 으로 클라이언트에서 "죽는" 문제.
            if (Character == null || Character.Stats == null || maxHealth.Value <= 0.0f)
            {
                return;
            }

            NetReplication.ApplyVitals(Character.Stats, maxHealth.Value, health.Value, maxMana.Value, mana.Value, shield.Value, identity.Value);
        }

        private void Update()
        {
            if (IsServer == false || Character == null || Character.Stats == null)
            {
                return;
            }

            // 서버 → 클라이언트 복제 (10Hz). 값이 바뀔 때만 NetworkVariable 이 전송한다.
            replicateTimer -= Time.deltaTime;
            if (replicateTimer > 0.0f)
            {
                return;
            }

            replicateTimer = 0.1f;
            var stats = Character.Stats;
            maxHealth.Value = stats.Get(EAttr.MaxHealth);
            health.Value = stats.Get(EAttr.Health);
            maxMana.Value = stats.Get(EAttr.MaxMana);
            mana.Value = Mathf.Round(stats.Get(EAttr.Mana));
            shield.Value = stats.Get(EAttr.Shield);
            identity.Value = Mathf.Round(stats.Get(EAttr.IdentityGauge));
            tags.Value = NetReplication.CaptureTags(Character);
        }
    }

    internal static class NetSession
    {
        public static GameSession Get()
        {
            return Framework.Managers.Get<GameSession>();
        }
    }
}
