using System.Collections.Generic;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.World;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// G1-01 구슬 획득 후 무력화.
    /// 보스 기준 8방향에 붉은/푸른 구슬 생성 → 구슬은 보스 쪽으로 다가온다 → 색을 번갈아 획득 →
    /// 전부 모으면 무력화 패턴(StaggerPatternId) 시작 → 무력화 성공 = 기믹 성공.
    /// 같은 색 연속 = 전멸, 구슬이 보스에 닿음 = 실패(OrbReachOutcome).
    /// </summary>
    public class OrbAlternationGimmick : BossGimmick
    {
        public int OrbCount = 8;
        public float SpawnRadius = 11.0f;
        [Tooltip("구슬이 보스 쪽으로 다가오는 속도 (m/s) — 자리 표시 값")]
        public float OrbSpeed = 0.8f;
        public float CurseDuration = 8.0f;
        [Tooltip("모두 모은 뒤 쓰는 무력화 패턴 (BossPatternDefinition.StaggerCheck = true)")]
        public string StaggerPatternId = "G1-01";
        [Tooltip("구슬이 보스에 닿았을 때 쓰는 패턴 (비우면 기믹 실패만)")]
        public string OrbReachChainPatternId = "G1-22";
        public Color RedColor = new Color(1.0f, 0.2f, 0.2f);
        public Color BlueColor = new Color(0.2f, 0.4f, 1.0f);

        private readonly List<RaidPickup> orbs = new List<RaidPickup>();
        private OrbSequenceRule rule;
        private bool waitingStagger;
        private bool staggerBroken;

        public override void ApplyPartyScale(float InScale)
        {
            // 8인 8개 → 4인 4개 (색 교대가 가능하도록 짝수 유지).
            OrbCount = Mathf.Max(2, Mathf.RoundToInt(OrbCount * InScale / 2.0f) * 2);
        }

        public OrbSequenceRule Rule
        {
            get { return rule; }
        }

        public IReadOnlyList<RaidPickup> Orbs
        {
            get { return orbs; }
        }

        public override string StatusText
        {
            get
            {
                if (rule == null)
                {
                    return string.Empty;
                }

                if (waitingStagger == true)
                {
                    return "구슬 완료 — 무력화!";
                }

                string last = rule.LastColor.HasValue == false ? "없음" : (rule.LastColor.Value == EOrbColor.Red ? "붉은색" : "푸른색");
                return $"구슬 {rule.Collected}/{rule.Required} · 직전 색: {last}";
            }
        }

        protected override void OnBegin()
        {
            clearOrbs();
            rule = new OrbSequenceRule(OrbCount);
            waitingStagger = false;
            staggerBroken = false;
            Boss.AddTag((int)ETag.Boss_Channeling);
            Raid.Message("구슬을 번갈아 획득하세요 (같은 색 연속 금지)", 3.0f, true);

            var colors = new List<EOrbColor>();
            for (int i = 0; i < OrbCount; ++i)
            {
                colors.Add(i % 2 == 0 ? EOrbColor.Red : EOrbColor.Blue);
            }

            for (int i = colors.Count - 1; i > 0; --i)
            {
                int j = GameRandom.Range(0, i + 1);
                var swap = colors[i];
                colors[i] = colors[j];
                colors[j] = swap;
            }

            var center = Boss.Position;
            for (int i = 0; i < OrbCount; ++i)
            {
                float angle = 360.0f / OrbCount * i * Mathf.Deg2Rad;
                var position = center + new Vector3(Mathf.Sin(angle), 0.0f, Mathf.Cos(angle)) * SpawnRadius;
                var color = colors[i];
                var orb = RaidPickup.Create($"Orb_{color}_{i}", position, color == EOrbColor.Red ? RedColor : BlueColor, 0.9f);
                orb.Payload = (int)color;
                orb.MoveSpeed = OrbSpeed;
                orb.MoveTarget = center;
                orb.HasMoveTarget = true;
                orb.PickRadius = 0.8f;
                orb.OnPicked += handlePicked;
                orb.OnReachedTarget += handleReachedBoss;
                orbs.Add(orb);
            }

            Boss.Asc.OnGameplayEvent += handleBossEvent;
            Boss.OnPatternEnded += handlePatternEnded;
        }

        private void handlePicked(RaidPickup InOrb, CombatActor InActor)
        {
            var result = rule.Pick((EOrbColor)InOrb.Payload);
            orbs.Remove(InOrb);
            InOrb.Consume();
            InActor.ApplyStatus(ETag.Debuff_OrbCurse, CurseDuration, Boss.Asc);

            if (result == EOrbPickResult.SameColorTwice)
            {
                Raid.Message("같은 색 구슬을 연속으로 획득 — 전멸", 3.0f, true);
                Raid.WipeParty(Boss, PatternId);
                Complete(false);
                return;
            }

            if (rule.IsComplete == true)
            {
                waitingStagger = true;
                Boss.RemoveTag((int)ETag.Boss_Channeling);
                if (Boss.TryActivatePattern(StaggerPatternId, null) == false)
                {
                    // 무력화 패턴이 없으면 구슬 수집만으로 성공 처리.
                    Complete(true);
                }
            }
        }

        private void handleReachedBoss(RaidPickup InOrb)
        {
            rule.OrbReachedBoss();
            Raid.Message("구슬이 보스에 닿았습니다 — 실패", 2.5f, true);
            Boss.QueuePattern(OrbReachChainPatternId);
            Complete(false);
        }

        private void handleBossEvent(int InEventTag, GameplayCueParams InPayload)
        {
            if (InEventTag == (int)ETag.Event_StaggerBroken && waitingStagger == true)
            {
                staggerBroken = true;
            }
        }

        private void handlePatternEnded(BossPatternAbility InAbility, bool InCancelled)
        {
            if (waitingStagger == false || InAbility.Definition.PatternId != StaggerPatternId)
            {
                return;
            }

            Complete(staggerBroken);
        }

        protected override void OnFinished(bool InSucceeded)
        {
            cleanup();
        }

        protected override void OnReset()
        {
            cleanup();
        }

        protected override void OnUnityDestroy()
        {
            cleanup();
        }

        private void cleanup()
        {
            clearOrbs();
            waitingStagger = false;
            if (Boss != null)
            {
                Boss.RemoveTag((int)ETag.Boss_Channeling);
                Boss.Asc.OnGameplayEvent -= handleBossEvent;
                Boss.OnPatternEnded -= handlePatternEnded;
            }
        }

        private void clearOrbs()
        {
            for (int i = 0; i < orbs.Count; ++i)
            {
                if (orbs[i] != null)
                {
                    orbs[i].Consume();
                }
            }

            orbs.Clear();
        }
    }
}
