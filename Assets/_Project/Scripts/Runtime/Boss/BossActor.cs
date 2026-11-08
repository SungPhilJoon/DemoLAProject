using System;
using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.World;
using Framework;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// 보스. 체력은 "줄" 단위로 본다 (CurrentHpLine). 패턴마다 BossPatternAbility 를 하나씩 가진다.
    /// 어그로는 받은 피해 기반 위협 수치로 정한다 (실제 규칙 미정 — 구현 보류 사항.md).
    /// </summary>
    public class BossActor : CombatActor
    {
        private const int PatternAbilityIdBase = 50000;

        private readonly Dictionary<string, BossPatternAbility> patterns = new Dictionary<string, BossPatternAbility>();
        private readonly Dictionary<CombatActor, float> threat = new Dictionary<CombatActor, float>();
        private readonly Dictionary<string, BossGimmick> gimmicks = new Dictionary<string, BossGimmick>();
        private readonly List<CombatActor> playerBuffer = new List<CombatActor>();
        private string queuedPattern;
        private int lastHpLine;
        private float hpPerLine = 1.0f;
        private CombatActor lockedTarget;
        private float retargetTimer;
        private CombatActor tauntSource;
        private float tauntRemaining;
        private readonly List<CombatActor> threatKeys = new List<CombatActor>();

        /// <summary>실제 인원 / 기준 인원 (4인 / 8인 = 0.5). 체력 · 무력화 게이지 · 기믹 개수에 곱한다.</summary>
        public float PartyScale { get; private set; } = 1.0f;

        public BossDefinition Definition { get; private set; }
        public string CurrentPhaseId { get; private set; }
        public BossPatternAbility ActivePattern { get; private set; }
        public BossGimmick ActiveGimmick { get; private set; }

        /// <summary>(이전 줄, 새 줄)</summary>
        public event Action<int, int> OnHpLineChanged;
        public event Action<string> OnPhaseChanged;
        public event Action<BossPatternAbility, bool> OnPatternEnded;

        public IReadOnlyDictionary<string, BossPatternAbility> Patterns
        {
            get { return patterns; }
        }

        public float HpPerLine
        {
            get { return hpPerLine; }
        }

        /// <summary>남은 체력 줄 (올림). 0 이면 사망.</summary>
        public int CurrentHpLine
        {
            get { return Stats == null ? 0 : Mathf.CeilToInt(Stats.Get(EAttr.Health) / hpPerLine - 0.0001f); }
        }

        /// <summary>소수점 포함 체력 줄 (HUD 용).</summary>
        public float HpLinesExact
        {
            get { return Stats == null ? 0.0f : Stats.Get(EAttr.Health) / hpPerLine; }
        }

        public bool IsBusy
        {
            get { return HasTag((int)ETag.Boss_PatternBusy) == true || HasTag((int)ETag.State_Groggy) == true; }
        }

        private float nextPatternTime;

        public void Initialize(BossDefinition InDefinition)
        {
            Initialize(InDefinition, InDefinition.DesignPartySize);
        }

        /// <summary>InPartySize 명 기준으로 초기화 (체력 · 무력화 · 기믹 개수를 인원 비율로 맞춘다).</summary>
        public void Initialize(BossDefinition InDefinition, int InPartySize)
        {
            Definition = InDefinition;
            PartyScale = Mathf.Clamp(InPartySize / (float)Mathf.Max(1, InDefinition.DesignPartySize), 0.125f, 1.0f);
            Team = ETeam.Enemy;
            Radius = InDefinition.Radius;
            var stats = new CombatAttributeSet();
            InitializeCombat(stats);
            hpPerLine = Mathf.Max(1.0f, InDefinition.HpPerLine * PartyScale);
            stats.InitializeDefaults(InDefinition.TotalHpLines * hpPerLine, 0.0f, InDefinition.AttackPower, InDefinition.Defense);
            stats.Set(EAttr.MoveSpeed, InDefinition.MoveSpeedPercent);
            lastHpLine = CurrentHpLine;
            nextPatternTime = Time.time + InDefinition.OpeningDelay;

            int index = 0;
            foreach (var pattern in InDefinition.Patterns)
            {
                if (pattern == null || patterns.ContainsKey(pattern.PatternId) == true)
                {
                    continue;
                }

                var ability = GrantAbility(new BossPatternAbility(pattern, PatternAbilityIdBase + index));
                patterns.Add(pattern.PatternId, ability);
                index += 1;
            }

            foreach (var gimmick in GetComponents<BossGimmick>())
            {
                gimmick.Bind(this);
                gimmick.ApplyPartyScale(PartyScale);
                if (string.IsNullOrEmpty(gimmick.GimmickId) == false)
                {
                    gimmicks[gimmick.GimmickId] = gimmick;
                }
            }

            stats.OnDamageTaken += handleDamageTaken;
            if (InDefinition.Phases.Count > 0)
            {
                SetPhase(InDefinition.Phases[0].PhaseId);
            }
        }

        private void Update()
        {
            if (Stats == null)
            {
                return;
            }

            tickAggro(Time.deltaTime);
            int line = CurrentHpLine;
            if (line != lastHpLine)
            {
                int previous = lastHpLine;
                lastHpLine = line;
                OnHpLineChanged?.Invoke(previous, line);
            }
        }

        // ------------------------------------------------------------------ patterns

        public BossPatternAbility GetPattern(string InPatternId)
        {
            BossPatternAbility ability;
            return InPatternId != null && patterns.TryGetValue(InPatternId, out ability) == true ? ability : null;
        }

        /// <summary>패턴 간 최소 간격(PatternGap) · 전투 시작 대기(OpeningDelay)가 지났는가.</summary>
        public bool IsPatternGapElapsed
        {
            get { return Time.time >= nextPatternTime; }
        }

        public bool IsPatternReady(string InPatternId)
        {
            var ability = GetPattern(InPatternId);
            return ability != null && IsPatternGapElapsed == true && Asc.GetCooldownRemaining(ability.AbilityId) <= 0.0f;
        }

        public bool TryActivatePattern(string InPatternId, CombatActor InTarget)
        {
            EActivateFailReason reason;
            return TryActivatePattern(InPatternId, InTarget, out reason);
        }

        /// <summary>실패 이유까지 돌려준다 (테스트 · 디버그).</summary>
        public bool TryActivatePattern(string InPatternId, CombatActor InTarget, out EActivateFailReason OutReason)
        {
            OutReason = EActivateFailReason.Custom;
            var ability = GetPattern(InPatternId);
            if (ability == null)
            {
                return false;
            }

            var info = new AbilityActivationInfo();
            var target = InTarget != null ? InTarget : AggroTarget;
            info.Target = target != null ? target.Asc : null;
            info.AimPoint = target != null ? target.Position : Position + Forward;
            if (Asc.TryActivateAbility(ability, info, out OutReason) == false)
            {
                return false;
            }

            ActivePattern = ability;
            return true;
        }

        /// <summary>다음 패턴 선택 시 가장 먼저 쓴다 (연결 패턴 / 실패 연쇄).</summary>
        public void QueuePattern(string InPatternId)
        {
            if (string.IsNullOrEmpty(InPatternId) == false && patterns.ContainsKey(InPatternId) == true)
            {
                queuedPattern = InPatternId;
            }
        }

        public string ConsumeQueuedPattern()
        {
            var pattern = queuedPattern;
            queuedPattern = null;
            return pattern;
        }

        public void NotifyPatternEnded(BossPatternAbility InAbility, bool InWasCancelled)
        {
            if (ActivePattern == InAbility)
            {
                ActivePattern = null;
            }

            nextPatternTime = Time.time + (Definition != null ? Definition.PatternGap : 0.0f);

            if (InWasCancelled == false)
            {
                QueuePattern(InAbility.Definition.LinkedPatternId);
            }

            OnPatternEnded?.Invoke(InAbility, InWasCancelled);
        }

        public void Announce(BossPatternDefinition InPattern, EPatternResponse InResponse)
        {
            if (InResponse == EPatternResponse.Counter)
            {
                Raid.Message($"{InPattern.DisplayName} — 카운터!", 1.5f, true);
            }
        }

        public void ApplyGroggy(float InDuration)
        {
            float duration = InDuration > 0.0f ? InDuration : Definition.DefaultGroggyTime;
            Asc.CancelAbilitiesWithTags(GameTags.Container(ETag.Ability_BossPattern), null);
            ApplyStatus(ETag.State_Groggy, duration, Asc);
        }

        // ------------------------------------------------------------------ phases / gimmicks

        public void SetPhase(string InPhaseId)
        {
            if (CurrentPhaseId == InPhaseId)
            {
                return;
            }

            CurrentPhaseId = InPhaseId;
            var phase = Definition.GetPhase(InPhaseId);
            if (phase != null)
            {
                if (phase.ResetHpLines > 0)
                {
                    ResetHpLines(phase.ResetHpLines);
                }

                Raid.Message(phase.DisplayName, 2.5f, false);
            }

            OnPhaseChanged?.Invoke(InPhaseId);
        }

        public BossPhaseDefinition CurrentPhase
        {
            get { return Definition != null ? Definition.GetPhase(CurrentPhaseId) : null; }
        }

        /// <summary>체력을 InLines 줄로 다시 설정 (유령 구간 등 새 체력 줄로 시작하는 페이즈).</summary>
        public void ResetHpLines(int InLines)
        {
            Stats.Set(EAttr.MaxHealth, InLines * hpPerLine);
            Stats.Set(EAttr.Health, InLines * hpPerLine);
            lastHpLine = CurrentHpLine;
        }

        public BossGimmick GetGimmick(string InGimmickId)
        {
            BossGimmick gimmick;
            return InGimmickId != null && gimmicks.TryGetValue(InGimmickId, out gimmick) == true ? gimmick : null;
        }

        public bool StartGimmick(string InGimmickId)
        {
            var gimmick = GetGimmick(InGimmickId);
            if (gimmick == null || gimmick.State == EGimmickState.Running)
            {
                return false;
            }

            Asc.CancelAbilitiesWithTags(GameTags.Container(ETag.Ability_BossPattern), null);
            ActiveGimmick = gimmick;
            gimmick.Begin();
            return true;
        }

        public void NotifyGimmickFinished(BossGimmick InGimmick, bool InSucceeded)
        {
            if (ActiveGimmick == InGimmick)
            {
                ActiveGimmick = null;
            }

            var cue = new GameplayCueParams();
            cue.Target = Asc;
            cue.Location = Position;
            Asc.SendGameplayEvent(InSucceeded == true ? (int)ETag.Event_GimmickSucceeded : (int)ETag.Event_GimmickFailed, cue);
        }

        // ------------------------------------------------------------------ aggro

        /// <summary>
        /// 현재 대상. 도발 &gt; 고정 대상(RetargetInterval 마다 NearestChance 로 가장 가까운 대상, 나머지는 무작위).
        /// </summary>
        public CombatActor AggroTarget
        {
            get
            {
                if (tauntRemaining > 0.0f && tauntSource != null && tauntSource.IsDead == false)
                {
                    return tauntSource;
                }

                if (lockedTarget == null || lockedTarget.IsDead == true || lockedTarget.IsTargetable == false)
                {
                    Retarget();
                }

                return lockedTarget;
            }
        }

        public IReadOnlyDictionary<CombatActor, float> Threat
        {
            get { return threat; }
        }

        public void Retarget()
        {
            retargetTimer = Definition != null ? Definition.Aggro.RetargetInterval : 6.0f;
            var players = AlivePlayers();
            if (players.Count == 0)
            {
                lockedTarget = null;
                return;
            }

            float nearestChance = Definition != null ? Definition.Aggro.NearestChance : 0.7f;
            lockedTarget = GameRandom.Value < nearestChance ? CombatRegistry.FindNearestHostile(this, 999.0f) : players[GameRandom.Range(0, players.Count)];
        }

        /// <summary>도발: InDuration 동안 InSource 만 노린다.</summary>
        public void Taunt(CombatActor InSource, float InDuration)
        {
            tauntSource = InSource;
            tauntRemaining = Mathf.Max(tauntRemaining, InDuration);
        }

        private void tickAggro(float InDeltaTime)
        {
            if (tauntRemaining > 0.0f)
            {
                tauntRemaining -= InDeltaTime;
            }

            retargetTimer -= InDeltaTime;
            if (retargetTimer <= 0.0f && IsBusy == false)
            {
                Retarget();
            }

            if (threat.Count > 0 && Definition != null)
            {
                float decay = Mathf.Clamp01(1.0f - Definition.Aggro.ThreatDecayPerSecond * InDeltaTime);
                threatKeys.Clear();
                threatKeys.AddRange(threat.Keys);
                for (int i = 0; i < threatKeys.Count; ++i)
                {
                    threat[threatKeys[i]] *= decay;
                }
            }
        }

        public void AddThreat(CombatActor InActor, float InAmount)
        {
            if (InActor == null)
            {
                return;
            }

            float current;
            threat.TryGetValue(InActor, out current);
            threat[InActor] = current + InAmount;
        }

        public void ClearThreat()
        {
            threat.Clear();
        }

        public IReadOnlyList<CombatActor> AlivePlayers()
        {
            CombatRegistry.FindTeam(ETeam.Player, playerBuffer);
            return playerBuffer;
        }

        protected override void OnDeath()
        {
            Raid.Message($"{Definition.DisplayName} 처치!", 4.0f, false);
            var result = new RaidResultEvent();
            result.Cleared = true;
            result.Reason = Definition.DisplayName;
            EventBus<RaidResultEvent>.Publish(result);
        }

        protected override void OnUnityDestroy()
        {
            if (Stats != null)
            {
                Stats.OnDamageTaken -= handleDamageTaken;
            }

            base.OnUnityDestroy();
        }

        private void handleDamageTaken(DamageAppliedEvent InEvent)
        {
            if (InEvent.Source == null)
            {
                return;
            }

            // 위협 수치 = 피해량 (임시 규칙). 지속 감쇠 · 도발 스킬은 미정.
            AddThreat(InEvent.Source.OwnerActor as CombatActor, InEvent.HealthLost);
        }
    }
}
