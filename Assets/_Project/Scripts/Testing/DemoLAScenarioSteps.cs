using System;
using System.Collections;
using System.Collections.Generic;
using DemoLA.Character;
using Framework.Testing;
using UnityEngine;

namespace DemoLA.Testing
{
    /// <summary>DemoLA 단계 공통: 호스트 꺼내기.</summary>
    [Serializable]
    public abstract class DemoLAStep : ScenarioStep
    {
        protected DemoLAScenarioHost Host(ScenarioContext InContext)
        {
            return InContext.Host as DemoLAScenarioHost;
        }
    }

    /// <summary>보스를 만든다 (AI 끄기 가능 — 패턴 하나만 볼 때).</summary>
    [Serializable]
    public class SpawnBossStep : DemoLAStep
    {
        public string BossId = "Gate2";
        public Vector3 Position = new Vector3(0.0f, 0.0f, 6.0f);
        public Vector3 FaceTowards = new Vector3(0.0f, 0.0f, -10.0f);
        public bool EnableAI;
        [Tooltip("0 보다 크면 체력을 이 줄 수로 맞춘다 (페이즈 · 기믹 조건 테스트)")]
        public float HpLines = -1.0f;

        public override IEnumerator Execute(ScenarioContext InContext)
        {
            var boss = Host(InContext).SpawnBoss(BossId, Position, FaceTowards, EnableAI);
            if (boss == null)
            {
                Fail(InContext, $"보스 '{BossId}' 없음");
                yield break;
            }

            if (HpLines > 0.0f)
            {
                float perLine = boss.Stats.Get(DemoLA.Attributes.EAttr.MaxHealth) / boss.Definition.TotalHpLines;
                boss.Stats.Set(DemoLA.Attributes.EAttr.Health, perLine * HpLines);
            }

            yield return null;
        }

        public override string Describe()
        {
            return string.IsNullOrEmpty(Label) ? $"보스 {BossId} 생성 (AI {(EnableAI ? "켬" : "끔")})" : Label;
        }
    }

    [Serializable]
    public class PartyMemberSpec
    {
        public ECharacterClass Class = ECharacterClass.Slayer;
        public Vector3 Position;
        [Tooltip("켜면 파티 AI 가 싸운다. 끄면 제자리에 서 있는 더미")]
        public bool UseAI;
    }

    /// <summary>파티를 만든다. 0번이 보스의 첫 어그로 대상(패턴 대상)이다.</summary>
    [Serializable]
    public class SpawnPartyStep : DemoLAStep
    {
        public List<PartyMemberSpec> Members = new List<PartyMemberSpec>();

        public override IEnumerator Execute(ScenarioContext InContext)
        {
            var host = Host(InContext);
            for (int i = 0; i < Members.Count; ++i)
            {
                if (host.SpawnMember(Members[i].Class, Members[i].Position, Members[i].UseAI) == null)
                {
                    Fail(InContext, $"직업 {Members[i].Class} 없음");
                    yield break;
                }
            }

            yield return null;
        }

        public override string Describe()
        {
            return string.IsNullOrEmpty(Label) ? $"파티 {Members.Count}명 생성" : Label;
        }
    }

    /// <summary>보스 패턴 하나를 바로 쓰게 하고(쿨다운 무시), 끝날 때까지 기다린다.</summary>
    [Serializable]
    public class ActivatePatternStep : DemoLAStep
    {
        public string PatternId = "G2-11";
        [Tooltip("패턴 대상 파티원 번호")]
        public int TargetMember;
        public bool ClearCooldowns = true;
        public bool WaitForEnd = true;
        public float Timeout = 25.0f;

        public override IEnumerator Execute(ScenarioContext InContext)
        {
            var host = Host(InContext);
            var boss = host.Boss;
            if (boss == null)
            {
                Fail(InContext, "보스가 없음 (SpawnBossStep 먼저)");
                yield break;
            }

            if (boss.GetPattern(PatternId) == null)
            {
                Fail(InContext, $"보스에 패턴 '{PatternId}' 없음");
                yield break;
            }

            if (ClearCooldowns == true)
            {
                boss.Asc.ClearCooldowns();
            }

            Framework.Ability.EActivateFailReason reason;
            if (boss.TryActivatePattern(PatternId, host.Member(TargetMember), out reason) == false)
            {
                var tags = new List<string>();
                foreach (var tag in new[] { DemoLA.Core.ETag.State_Groggy, DemoLA.Core.ETag.Boss_PatternBusy, DemoLA.Core.ETag.Boss_Untargetable, DemoLA.Core.ETag.State_Casting, DemoLA.Core.ETag.Boss_Channeling })
                {
                    if (boss.HasTag((int)tag) == true)
                    {
                        tags.Add(tag.ToString());
                    }
                }

                Fail(InContext, $"패턴 '{PatternId}' 발동 실패 (이유 {reason}, 보스 상태 [{string.Join(",", tags)}], 활성 패턴 {(boss.ActivePattern != null ? boss.ActivePattern.Definition.PatternId : "-")})");
                yield break;
            }

            if (WaitForEnd == false)
            {
                yield break;
            }

            string endEvent = $"Pattern.Ended.{PatternId}";
            float end = InContext.Elapsed + Timeout;
            while (InContext.Recorder.Count(endEvent) == 0)
            {
                if (InContext.Elapsed >= end)
                {
                    Fail(InContext, $"패턴 '{PatternId}' 이 {Timeout:0.#}초 안에 끝나지 않음");
                    yield break;
                }

                yield return null;
            }
        }

        public override string Describe()
        {
            return string.IsNullOrEmpty(Label) ? $"패턴 {PatternId} 발동" : Label;
        }
    }

    /// <summary>보스 체력을 N 줄로 맞춘다 (페이즈 · 기믹 진입 확인).</summary>
    [Serializable]
    public class SetBossHpLinesStep : DemoLAStep
    {
        public float Lines = 130.0f;

        public override IEnumerator Execute(ScenarioContext InContext)
        {
            var boss = Host(InContext).Boss;
            if (boss == null)
            {
                Fail(InContext, "보스가 없음");
                yield break;
            }

            float perLine = boss.Stats.Get(DemoLA.Attributes.EAttr.MaxHealth) / boss.Definition.TotalHpLines;
            boss.Stats.Set(DemoLA.Attributes.EAttr.Health, perLine * Lines);
            yield return null;
        }
    }

    /// <summary>파티원을 옮긴다 (넉백 없이 — 낙사 구출 규칙 확인 등).</summary>
    [Serializable]
    public class MoveMemberStep : DemoLAStep
    {
        public int Member;
        public Vector3 Position;

        public override IEnumerator Execute(ScenarioContext InContext)
        {
            var member = Host(InContext).Member(Member);
            if (member == null)
            {
                Fail(InContext, $"파티원 {Member} 없음");
                yield break;
            }

            member.Motor.Teleport(Position);
            yield return null;
        }
    }

    /// <summary>파티원을 넉백시킨다 (몬스터 공격으로 날아간 것과 같은 처리).</summary>
    [Serializable]
    public class KnockbackMemberStep : DemoLAStep
    {
        public int Member;
        public Vector3 Direction = Vector3.forward;
        public float Distance = 8.0f;
        public float Height = 2.0f;

        public override IEnumerator Execute(ScenarioContext InContext)
        {
            var member = Host(InContext).Member(Member);
            if (member == null)
            {
                Fail(InContext, $"파티원 {Member} 없음");
                yield break;
            }

            member.Motor.Knockback(Direction, Distance, 0.45f, Height);
            yield return null;
        }
    }

    /// <summary>보스 AI 를 켜거나 끈다.</summary>
    [Serializable]
    public class SetBossAIStep : DemoLAStep
    {
        public bool Enabled = true;

        public override IEnumerator Execute(ScenarioContext InContext)
        {
            var boss = Host(InContext).Boss;
            var brain = boss != null ? boss.GetComponent<DemoLA.AI.CombatBrain>() : null;
            if (brain == null)
            {
                Fail(InContext, "보스 AI 없음");
                yield break;
            }

            if (Enabled == true)
            {
                brain.Begin();
            }
            else
            {
                brain.Stop();
            }

            yield return null;
        }
    }
}
