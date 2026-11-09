using System;
using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Effects;
using DemoLA.World;
using Framework;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Character
{
    /// <summary>에스더 스킬 종류 (효과 묶음). 수치는 EstherDefinition 에서 바꾼다.</summary>
    public enum EEstherKind
    {
        /// <summary>보스에게 큰 피해 + 파티 피해 증가 버프.</summary>
        Strike = 0,
        /// <summary>패턴 끊기: 카운터 구간이면 카운터 성공, 무력화 체크 중이면 무력화 대량, 아니면 짧은 그로기. + 무력화 피해.</summary>
        Break = 1,
        /// <summary>파티 전원 무적 + 회복.</summary>
        Guard = 2,
    }

    /// <summary>
    /// 에스더 한 명 (GameDatabase.Esthers). 이름 · 설명 · 수치는 데이터라 자유롭게 바꿀 수 있다.
    /// Gates 에 보스 ID(Gate1 · Gate2)가 있는 관문에서만 쓸 수 있다.
    /// </summary>
    [Serializable]
    public class EstherDefinition
    {
        public string Id = "esther";
        public string DisplayName = "에스더";
        [TextArea] public string Description;
        public EEstherKind Kind;
        [Tooltip("쓸 수 있는 관문 (BossDefinition.BossId)")]
        public string[] Gates = { "Gate1", "Gate2" };
        [Tooltip("아이콘 키 (IconManifest.csv 의 Esther 행)")]
        public string IconKey;
        public Color Color = new Color(1.0f, 0.85f, 0.4f);

        [Header("Strike / Break")]
        [Tooltip("보스 체력 줄 기준 고정 피해 (방어 · 배율 적용 전)")]
        public float DamageHpLines;
        [Tooltip("무력화 수치 (스킬 무력화와 같은 단위)")]
        public float Stagger;

        [Header("Strike: 파티 버프")]
        public float BuffDamagePercent;
        public float BuffDuration;

        [Header("Break")]
        [Tooltip("카운터 · 무력화 체크가 아닐 때 보스 그로기 (초)")]
        public float GroggySeconds;

        [Header("Guard")]
        public float InvulnerableSeconds;
        [Tooltip("최대 체력 대비 회복 %")]
        public float HealPercent;

        public bool IsAvailableIn(string InBossId)
        {
            return Gates != null && Array.IndexOf(Gates, InBossId) >= 0;
        }
    }

    /// <summary>
    /// 에스더 게이지와 사용 (파티 공용 게이지 하나, 가득 차면 세 명 중 한 명을 쓴다).
    ///  - 게이지: 파티가 보스에게 준 피해. HpLinesPerGauge 줄을 깎으면 가득 (BossActor 피해 이벤트에서 AddBossDamage).
    ///  - 사용: TryUse (서버 권한 — 네트워크 클라이언트는 NetworkPartyMember.UseEstherRpc 로 보낸다). 쓰면 게이지 0.
    ///  - 보스가 생성될 때(RaidSpawner.AttachBoss) Reset — 관문마다 새로 모은다.
    /// </summary>
    public static class EstherSystem
    {
        public const float Max = 100.0f;

        private static readonly List<EstherDefinition> available = new List<EstherDefinition>();
        private static List<EstherDefinition> definitions;
        private static GameplayEffectDef strikeBuff;
        private static GameplayEffectDef fixedHit;
        private static float strikeBuffPercent = -1.0f;
        private static float strikeBuffDuration = -1.0f;
        private static bool applying;

        /// <summary>게이지 0 ~ Max.</summary>
        public static float Gauge { get; private set; }

        /// <summary>보스 체력 줄 몇 줄을 깎으면 가득 차는가.</summary>
        public static float HpLinesPerGauge = 12.0f;

        /// <summary>네트워크 클라이언트: 게이지를 직접 모으지 않고 서버 값을 받는다 (NetworkBossProxy).</summary>
        public static bool IsReplica
        {
            get { return NetAuthority.IsReplica; }
            set { NetAuthority.IsReplica = value; }
        }

        public static string CurrentBossId { get; private set; }

        public static event Action<EstherDefinition, CombatActor> OnUsed;

        public static bool IsFull
        {
            get { return Gauge >= Max - 0.001f; }
        }

        /// <summary>현재 관문에서 쓸 수 있는 에스더 (순서 = 단축키 5 · 6 · 7).</summary>
        public static IReadOnlyList<EstherDefinition> Available
        {
            get { return available; }
        }

        /// <summary>GameBootstrap 이 GameDatabase.Esthers 로 넣는다. 비어 있으면 기본 3명.</summary>
        public static void Configure(IReadOnlyList<EstherDefinition> InDefinitions, float InHpLinesPerGauge)
        {
            definitions = InDefinitions != null && InDefinitions.Count > 0 ? new List<EstherDefinition>(InDefinitions) : CreateDefaults();
            if (InHpLinesPerGauge > 0.0f)
            {
                HpLinesPerGauge = InHpLinesPerGauge;
            }

            Reset(CurrentBossId);
        }

        public static void Reset(string InBossId)
        {
            CurrentBossId = InBossId;
            Gauge = 0.0f;
            available.Clear();
            if (definitions == null)
            {
                definitions = CreateDefaults();
            }

            for (int i = 0; i < definitions.Count; ++i)
            {
                if (definitions[i] != null && (string.IsNullOrEmpty(InBossId) == true || definitions[i].IsAvailableIn(InBossId) == true))
                {
                    available.Add(definitions[i]);
                }
            }
        }

        /// <summary>네트워크 복제 · 테스트 · 디버그.</summary>
        public static void SetGauge(float InValue)
        {
            Gauge = Mathf.Clamp(InValue, 0.0f, Max);
        }

        public static void AddBossDamage(BossActor InBoss, float InHealthLost)
        {
            if (IsReplica == true || applying == true || InBoss == null || InBoss.Definition == null || InHealthLost <= 0.0f)
            {
                return;
            }

            float perGauge = Mathf.Max(1.0f, InBoss.Definition.HpPerLine * Mathf.Max(0.1f, HpLinesPerGauge));
            Gauge = Mathf.Min(Max, Gauge + InHealthLost / perGauge * Max);
        }

        /// <summary>InIndex 번째 에스더를 InUser 가 쓴다 (서버). 게이지가 차 있어야 한다.</summary>
        public static bool TryUse(CombatActor InUser, int InIndex)
        {
            if (IsFull == false || InUser == null || InUser.IsDead == true || InIndex < 0 || InIndex >= available.Count)
            {
                return false;
            }

            var esther = available[InIndex];
            var boss = findBoss();
            Gauge = 0.0f;
            applying = true;
            try
            {
                switch (esther.Kind)
                {
                    case EEstherKind.Strike:
                        strike(esther, InUser, boss);
                        break;
                    case EEstherKind.Break:
                        breakPattern(esther, InUser, boss);
                        break;
                    default:
                        guard(esther, InUser);
                        break;
                }
            }
            finally
            {
                applying = false;
            }

            DemoLA.Player.QuarterViewCamera.Shake(0.6f, 0.5f);
            Raid.Message($"에스더 — {esther.DisplayName}!", 2.5f, false);
            OnUsed?.Invoke(esther, InUser);
            return true;
        }

        private static BossActor findBoss()
        {
            var actors = CombatRegistry.All;
            for (int i = 0; i < actors.Count; ++i)
            {
                var boss = actors[i] as BossActor;
                if (boss != null && boss.IsDead == false)
                {
                    return boss;
                }
            }

            return null;
        }

        private static void strike(EstherDefinition InEsther, CombatActor InUser, BossActor InBoss)
        {
            if (InBoss != null)
            {
                hitBoss(InUser, InBoss, InBoss.Definition.HpPerLine * InEsther.DamageHpLines, InEsther.Stagger);
            }

            if (InEsther.BuffDamagePercent > 0.0f && InEsther.BuffDuration > 0.0f)
            {
                var buff = strikeBuffDef(InEsther.BuffDamagePercent, InEsther.BuffDuration);
                foreach (var member in party())
                {
                    InUser.Asc.ApplyGameplayEffectSpecToTarget(InUser.Asc.MakeOutgoingSpec(buff, 0), member.Asc);
                }
            }
        }

        private static void breakPattern(EstherDefinition InEsther, CombatActor InUser, BossActor InBoss)
        {
            if (InBoss == null)
            {
                return;
            }

            hitBoss(InUser, InBoss, InBoss.Definition.HpPerLine * InEsther.DamageHpLines, InEsther.Stagger);
            if (InBoss.HasTag((int)ETag.Boss_CounterWindow) == true)
            {
                // 카운터 구간이면 카운터 성공으로 처리 (패턴 취소 + 패턴의 카운터 그로기).
                var cue = new GameplayCueParams();
                cue.Target = InBoss.Asc;
                cue.Location = InBoss.Position;
                InBoss.Asc.SendGameplayEvent((int)ETag.Event_Countered, cue);
                return;
            }

            if (InBoss.HasTag((int)ETag.Boss_StaggerCheck) == true)
            {
                // 무력화 체크 중: 남은 게이지를 크게 깎는다 (Stagger 가 위 피해로 이미 들어갔고, 추가로 절반).
                float gauge = InBoss.Stats.Get(EAttr.StaggerGauge);
                InBoss.Stats.Set(EAttr.StaggerGauge, Mathf.Max(0.0f, gauge * 0.5f));
                return;
            }

            if (InEsther.GroggySeconds > 0.0f && InBoss.HasTag((int)ETag.State_Groggy) == false)
            {
                InBoss.ApplyGroggy(InEsther.GroggySeconds);
            }
        }

        private static void guard(EstherDefinition InEsther, CombatActor InUser)
        {
            foreach (var member in party())
            {
                if (InEsther.InvulnerableSeconds > 0.0f)
                {
                    member.ApplyStatus(ETag.State_Invulnerable, InEsther.InvulnerableSeconds, InUser.Asc);
                }

                if (InEsther.HealPercent > 0.0f)
                {
                    var heal = InUser.Asc.MakeOutgoingSpec(GameEffects.Heal, 0);
                    heal.SetByCaller((int)ESetByCaller.FlatDamage, member.Stats.Get(EAttr.MaxHealth) * InEsther.HealPercent * 0.01f);
                    InUser.Asc.ApplyGameplayEffectSpecToTarget(heal, member.Asc);
                }
            }
        }

        /// <summary>
        /// 고정 피해 · 무력화 (스킬 피해 공식 · 방어 · 플레이어 피해 배율을 거치지 않는다 — "체력 N줄" 이 그대로 들어간다).
        /// 메타 속성 IncomingDamage / IncomingStagger 로 넣으므로 피해 이벤트 · 딜 미터 · 위협은 평소처럼 처리된다.
        /// </summary>
        private static void hitBoss(CombatActor InUser, BossActor InBoss, float InFlatDamage, float InStagger)
        {
            if (fixedHit == null)
            {
                fixedHit = EffectBuilder.Instant()
                    .SetByCaller(EAttr.IncomingDamage, ESetByCaller.FlatDamage)
                    .SetByCaller(EAttr.IncomingStagger, ESetByCaller.StaggerValue)
                    .Cue(ECue.Hit).Build();
            }

            var spec = InUser.Asc.MakeOutgoingSpec(fixedHit, 0);
            spec.SetByCaller((int)ESetByCaller.FlatDamage, Mathf.Max(0.0f, InFlatDamage));
            spec.SetByCaller((int)ESetByCaller.StaggerValue, Mathf.Max(0.0f, InStagger));
            spec.HitPoint = InBoss.Position;
            InUser.Asc.ApplyGameplayEffectSpecToTarget(spec, InBoss.Asc);
        }

        private static IEnumerable<CombatActor> party()
        {
            var actors = CombatRegistry.All;
            for (int i = 0; i < actors.Count; ++i)
            {
                var actor = actors[i];
                if (actor != null && actor.Team == ETeam.Player && actor.IsDead == false)
                {
                    yield return actor;
                }
            }
        }

        private static GameplayEffectDef strikeBuffDef(float InPercent, float InDuration)
        {
            if (strikeBuff == null || Mathf.Approximately(strikeBuffPercent, InPercent) == false || Mathf.Approximately(strikeBuffDuration, InDuration) == false)
            {
                strikeBuff = new EffectBuilder(EffectBuilder.AllocateId(), EDurationPolicy.HasDuration)
                    .Duration(InDuration).Add(EAttr.DamageIncrease, InPercent).Stack(EStackPolicy.Refresh, 1).Build();
                strikeBuffPercent = InPercent;
                strikeBuffDuration = InDuration;
            }

            return strikeBuff;
        }

        /// <summary>
        /// 기본 에스더 3명 (프로젝트 자체 캐릭터 · 이름과 수치는 GameDatabase.Esthers 에서 바꾼다). 1 · 2관문 모두 사용.
        /// </summary>
        public static List<EstherDefinition> CreateDefaults()
        {
            return new List<EstherDefinition>
            {
                new EstherDefinition
                {
                    Id = "sword", DisplayName = "검의 성기사", Kind = EEstherKind.Strike, IconKey = "검의 성기사",
                    Description = "하늘에서 거대한 검을 내리꽂아 보스에게 큰 피해를 주고, 파티의 피해를 15초 동안 20% 높인다.",
                    DamageHpLines = 3.0f, Stagger = 300.0f, BuffDamagePercent = 20.0f, BuffDuration = 15.0f,
                    Color = new Color(1.0f, 0.82f, 0.35f),
                },
                new EstherDefinition
                {
                    Id = "storm", DisplayName = "폭풍의 창기사", Kind = EEstherKind.Break, IconKey = "폭풍의 창기사",
                    Description = "창을 던져 보스의 패턴을 끊는다. 카운터 구간이면 카운터 성공, 무력화 체크 중이면 무력화 게이지를 크게 깎고, 그 밖에는 보스를 3초 동안 무력화한다.",
                    DamageHpLines = 1.0f, Stagger = 1200.0f, GroggySeconds = 3.0f,
                    Color = new Color(0.45f, 0.75f, 1.0f),
                },
                new EstherDefinition
                {
                    Id = "earth", DisplayName = "대지의 수호자", Kind = EEstherKind.Guard, IconKey = "대지의 수호자",
                    Description = "대지의 방벽을 세워 파티 전원을 6초 동안 무적으로 만들고 최대 체력의 40%를 회복한다.",
                    InvulnerableSeconds = 6.0f, HealPercent = 40.0f,
                    Color = new Color(0.55f, 0.9f, 0.5f),
                },
            };
        }
    }
}
