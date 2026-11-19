using System.Collections.Generic;
using DemoLA.Combat;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Skills
{
    /// <summary>
    /// 스킬 시전 한 번의 GameplayCue 타임라인 (캐릭터에 자동으로 붙는다). SkillCueTiming 의 구간대로 프레임워크 GameplayCueManager 를 부른다.
    ///  - 시전 전 · 시전 중: 구간 시작에 Add, 끝에 Remove
    ///  - 시전 후: 구간 시작(마지막 타격)에 Execute
    /// 큐 id = GameplayCueNotify.Register() (큐 태그 또는 CueId). 파라미터 Anchor = 캐릭터 (무기 소켓을 찾는 기준).
    /// 서버/로컬은 SkillAbility, 네트워크 클라이언트는 시전 애니메이션 RPC 가 같은 SkillAbility.PlayCastAnimation 으로 부른다.
    /// 시전이 끝나거나 취소되면(Stop) 아직 시작하지 않은 큐는 버리고, 켜져 있는 시전 전 · 중 큐는 끈다. 이미 터진 시전 후 큐는 끝까지 재생된다.
    /// </summary>
    public class SkillCuePlayer : MonoBehaviour
    {
        private struct Entry
        {
            public ESkillCuePhase Phase;
            public int CueId;
            public float Start;
            public float End;
            public bool Started;
            public bool Done;
        }

        private readonly List<Entry> entries = new List<Entry>(3);
        private CombatActor actor;
        private float elapsed;

        /// <summary>InDefinition 의 큐 슬롯을 재생한다. InTimeScale = 시전 시간 배율 (공격 속도 · 트라이포드, 1 = 기본).</summary>
        public static void Play(CombatActor InActor, SkillDefinition InDefinition, float InTimeScale)
        {
            if (InActor == null || InDefinition == null || InDefinition.HasCastCues == false)
            {
                return;
            }

            var player = InActor.GetComponent<SkillCuePlayer>();
            if (player == null)
            {
                player = InActor.gameObject.AddComponent<SkillCuePlayer>();
            }

            player.begin(InActor, InDefinition, Mathf.Max(0.05f, InTimeScale));
        }

        public static void Stop(CombatActor InActor)
        {
            var player = InActor != null ? InActor.GetComponent<SkillCuePlayer>() : null;
            if (player != null)
            {
                player.stopAll();
            }
        }

        private void begin(CombatActor InActor, SkillDefinition InDefinition, float InTimeScale)
        {
            stopAll();
            actor = InActor;
            elapsed = 0.0f;
            foreach (var phase in SkillCueTiming.Phases)
            {
                var slot = SkillCueTiming.Slot(InDefinition, phase);
                if (slot == null || slot.IsSet == false)
                {
                    continue;
                }

                float start;
                float end;
                SkillCueTiming.Window(InDefinition, phase, out start, out end);
                entries.Add(new Entry
                {
                    Phase = phase,
                    CueId = slot.Cue.Register(),
                    Start = start * InTimeScale,
                    End = end * InTimeScale,
                });
            }

            enabled = entries.Count > 0;
            tick(0.0f);
        }

        private void Update()
        {
            tick(Time.deltaTime);
        }

        private void tick(float InDeltaTime)
        {
            elapsed += InDeltaTime;
            bool pending = false;
            for (int i = 0; i < entries.Count; ++i)
            {
                var entry = entries[i];
                if (entry.Started == false && elapsed >= entry.Start)
                {
                    entry.Started = true;
                    if (entry.Phase == ESkillCuePhase.PostCast)
                    {
                        GameplayCueManager.Execute(entry.CueId, makeParams(entry.End - entry.Start));
                        entry.Done = true;
                    }
                    else
                    {
                        GameplayCueManager.Add(entry.CueId, makeParams(entry.End - entry.Start));
                    }
                }

                if (entry.Started == true && entry.Done == false && elapsed >= entry.End)
                {
                    GameplayCueManager.Remove(entry.CueId, makeParams(0.0f));
                    entry.Done = true;
                }

                entries[i] = entry;
                pending |= entry.Done == false;
            }

            if (pending == false)
            {
                entries.Clear();
                enabled = false;
            }
        }

        private void stopAll()
        {
            for (int i = 0; i < entries.Count; ++i)
            {
                if (entries[i].Started == true && entries[i].Done == false)
                {
                    GameplayCueManager.Remove(entries[i].CueId, makeParams(0.0f));
                }
            }

            entries.Clear();
            enabled = false;
        }

        private GameplayCueParams makeParams(float InMagnitude)
        {
            var owner = actor != null ? actor.transform : transform;
            var p = new GameplayCueParams();
            p.Source = actor != null ? actor.Asc : null;
            p.Target = p.Source;
            p.Location = owner.position;
            p.Direction = owner.forward;
            p.Magnitude = InMagnitude;
            p.Anchor = owner;
            return p;
        }

        private void OnDisable()
        {
            // 캐릭터가 꺼지면 켜 둔 큐도 끈다 (enabled = false 로 쉬는 경우는 entries 가 이미 비어 있다).
            if (entries.Count > 0)
            {
                stopAll();
            }
        }
    }
}
