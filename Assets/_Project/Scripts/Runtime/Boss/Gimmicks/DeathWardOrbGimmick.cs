using System.Collections.Generic;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Effects;
using DemoLA.World;
using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// G2-02 방어 구슬 획득. 구조물(첨탑) 파괴 후 구조물당 OrbsPerStructure 개 생성.
    /// 1인 1개 획득하면 Buff_DeathWard — 이후 ConsumesDeathWard 패턴(G2-03) 1회를 막아 준다.
    /// 구조물을 무엇이 파괴하는지(보스 돌진 / 플레이어)는 미정이라 기믹 시작 시 모두 파괴한다.
    /// </summary>
    public class DeathWardOrbGimmick : BossGimmick
    {
        public int OrbsPerStructure = 2;
        public float OrbOffset = 1.8f;
        [Tooltip("이 시간 안에 모두 줍지 않아도 기믹은 성공으로 끝난다 (구슬은 남는다)")]
        public float Duration = 12.0f;
        public Color OrbColor = new Color(1.0f, 0.85f, 0.3f);

        private readonly List<RaidPickup> orbs = new List<RaidPickup>();
        private float elapsed;
        private int picked;

        public override void ApplyPartyScale(float InScale)
        {
            // 구조물 4개 × 2개 = 8인분 → 4인이면 구조물당 1개.
            OrbsPerStructure = Mathf.Max(1, Mathf.RoundToInt(OrbsPerStructure * InScale));
        }

        public override string StatusText
        {
            get { return State == EGimmickState.Running ? $"방어 구슬 {picked}/{picked + orbs.Count} · 1인 1개" : string.Empty; }
        }

        protected override void OnBegin()
        {
            elapsed = 0.0f;
            picked = 0;
            clear();
            Raid.Message("방어 구슬을 1인 1개씩 획득하세요", 3.0f, false);

            var structures = new List<ArenaStructure>(ArenaStructure.Structures);
            for (int s = 0; s < structures.Count; ++s)
            {
                var structure = structures[s];
                // 이미 부서진 기둥(보스 돌진 등)에서는 구슬도 나오지 않는다. 외곽 벽은 대상이 아니다.
                if (structure.IsDestroyed == true || structure.Kind != EStructureKind.LifePillar)
                {
                    continue;
                }

                var position = structure.transform.position;
                structure.Break();
                for (int i = 0; i < OrbsPerStructure; ++i)
                {
                    float angle = (360.0f / OrbsPerStructure * i + s * 37.0f) * Mathf.Deg2Rad;
                    var orb = RaidPickup.Create("WardOrb", position + new Vector3(Mathf.Sin(angle), 0.0f, Mathf.Cos(angle)) * OrbOffset, OrbColor, 0.7f);
                    orb.CanPick = InActor => InActor.HasTag((int)ETag.Buff_DeathWard) == false;
                    orb.OnPicked += handlePicked;
                    orbs.Add(orb);
                }
            }
        }

        protected override void OnTick(float InDeltaTime)
        {
            elapsed += InDeltaTime;
            if (elapsed >= Duration || orbs.Count == 0)
            {
                Complete(true);
            }
        }

        private void handlePicked(RaidPickup InOrb, CombatActor InActor)
        {
            orbs.Remove(InOrb);
            InOrb.Consume();
            picked += 1;
            InActor.ApplyEffect(GameEffects.DeathWard, Boss.Asc);
        }

        protected override void OnFinished(bool InSucceeded)
        {
            // 남은 구슬은 다음 즉사기 전까지 남겨 둔다 (주울 수 있음).
        }

        protected override void OnReset()
        {
            clear();
            for (int i = 0; i < ArenaStructure.Structures.Count; ++i)
            {
                ArenaStructure.Structures[i].Restore();
            }
        }

        protected override void OnUnityDestroy()
        {
            clear();
        }

        private void clear()
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
