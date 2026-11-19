using System.Collections.Generic;
using System.IO;
using DemoLA.Boss;
using DemoLA.Character;
using DemoLA.Content;
using DemoLA.Core;
using DemoLA.Data;
using DemoLA.Testing;
using Framework.Testing;
using UnityEditor;
using UnityEngine;

namespace DemoLA.Editor
{
    /// <summary>
    /// 시나리오 에셋(TestScenarioAsset) 생성기.
    ///  - 보스 패턴: 구현된 모든 패턴마다 "보스(AI 끔) + 더미 파티 4명 → 패턴 발동 → 끝날 때까지" 시나리오 1개.
    ///    기본 검증 = 시작 1회 · 끝남 1회 · (장판이 있으면) 장판 1개 이상. 받은 피해 · 넉백 수는 측정(ReportOnly).
    ///  - 기믹: 도약 십자/X 추적, 돌진 → 기둥 충돌, 첫 즉사기 구조물 전부 파괴, 넉백 낙사 / 넉백 없는 구출.
    ///  - 밸런스: 4인 파티(테스트 딜 측정기) 60초 → 보스 체력 줄 · 피해 · 치명타 비율 (범위 검증 + 측정).
    /// 이미 있는 에셋은 건너뛴다 (직접 고친 값을 지키기 위해). 덮어쓰려면 "다시 생성" 메뉴.
    /// </summary>
    public static class ScenarioAssetFactory
    {
        private const string Root = "Assets/_Project/Tests/Scenarios";

        [MenuItem("DemoLA/Tests/시나리오 생성 (없는 것만)", priority = 40)]
        public static void CreateMissingMenu()
        {
            Debug.Log($"[Scenario] {CreateAll(false)}");
        }

        [MenuItem("DemoLA/Tests/시나리오 다시 생성 (덮어쓰기)", priority = 41)]
        public static void RecreateMenu()
        {
            if (EditorUtility.DisplayDialog("시나리오 다시 생성", "생성기가 만드는 시나리오 에셋을 모두 덮어씁니다. 직접 고친 값은 사라집니다.", "덮어쓰기", "취소") == true)
            {
                Debug.Log($"[Scenario] {CreateAll(true)}");
            }
        }

        public static string CreateAll(bool InOverwrite)
        {
            string errors;
            var database = DefaultContent.CreateDatabase(DemoLATables.FromCsv(TablePaths.ReadCsv, out errors));
            int created = 0;
            int skipped = 0;
            foreach (var boss in database.Bosses)
            {
                foreach (var pattern in boss.Patterns)
                {
                    if (pattern == null || pattern.Implemented == false)
                    {
                        continue;
                    }

                    save(patternScenario(boss, pattern), $"{Root}/Patterns/{boss.BossId}", $"TS_{GameTags.PatternAssetName(pattern.PatternTag)}", InOverwrite, ref created, ref skipped);
                }
            }

            save(leapCrossChase(), $"{Root}/Mechanics", "TS_G2_LeapSmashWhirlwind_CrossChase", InOverwrite, ref created, ref skipped);
            save(chargeIntoPillar(), $"{Root}/Mechanics", "TS_G2_Charge_PillarCrash", InOverwrite, ref created, ref skipped);
            save(firstWipeBreaksAll(), $"{Root}/Mechanics", "TS_G2_LethalFieldDouble_BreakAllStructures", InOverwrite, ref created, ref skipped);
            save(knockbackFall(), $"{Root}/Mechanics", "TS_Fall_KnockbackAfterCollapse", InOverwrite, ref created, ref skipped);
            save(knockbackRescuedBeforeCollapse(), $"{Root}/Mechanics", "TS_Fall_KnockbackRescuedAtWall", InOverwrite, ref created, ref skipped);
            save(rescueWithoutKnockback(), $"{Root}/Mechanics", "TS_Fall_RescueWithoutKnockback", InOverwrite, ref created, ref skipped);
            save(edgeExitOnlyOnce(), $"{Root}/Mechanics", "TS_EdgeExitLeap_Removed", InOverwrite, ref created, ref skipped);
            save(balanceGate("Gate1", 15.0f), $"{Root}/Balance", "TS_Balance_Gate1_15s", InOverwrite, ref created, ref skipped);
            save(skillCuePhases(), $"{Root}/SkillCue", "TS_SkillCue_Slayer_CastPhases", InOverwrite, ref created, ref skipped);
            AssetDatabase.SaveAssets();
            return $"시나리오 생성 {created}개, 건너뜀 {skipped}개 → {Root}";
        }

        // ------------------------------------------------------------------ 패턴

        private static TestScenarioAsset patternScenario(BossDefinition InBoss, BossPatternDefinition InPattern)
        {
            var scenario = make($"{InPattern.TagName} {InPattern.DisplayName}", $"보스 패턴 {InPattern.TagName} 단독 확인 (자동 생성)", "Pattern." + InBoss.BossId);
            float timeout = estimateDuration(InPattern) + 10.0f;
            scenario.Timeout = timeout + 10.0f;
            scenario.Steps.Add(new SpawnBossStep { BossId = InBoss.BossId, Position = new Vector3(0.0f, 0.0f, 6.0f), FaceTowards = new Vector3(0.0f, 0.0f, -6.0f) });
            scenario.Steps.Add(dummyParty());
            scenario.Steps.Add(new ActivatePatternStep { PatternTag = InPattern.PatternTag, Timeout = timeout });

            scenario.Expectations.Add(new EventExpectation { Label = "패턴 시작", EventName = $"Pattern.Started.{InPattern.TagName}", Compare = EScenarioCompare.GreaterOrEqual, Value = 1 });
            scenario.Expectations.Add(new EventExpectation { Label = "패턴 끝남", EventName = $"Pattern.Ended.{InPattern.TagName}", Compare = EScenarioCompare.GreaterOrEqual, Value = 1 });
            bool anyTelegraph = false;
            for (int i = 0; i < InPattern.Steps.Count; ++i)
            {
                anyTelegraph |= InPattern.Steps[i].ShowTelegraph;
            }

            if (anyTelegraph == true)
            {
                scenario.Expectations.Add(new EventExpectation { Label = "장판 표시", EventName = "Telegraph", Compare = EScenarioCompare.GreaterOrEqual, Value = 1 });
            }

            scenario.Expectations.Add(new EventExpectation { Label = "파티가 받은 피해", EventName = "Damage.ToParty", Aggregate = EScenarioAggregate.Sum, ReportOnly = true });
            scenario.Expectations.Add(new EventExpectation { Label = "넉백 횟수", EventName = "Knockback", ReportOnly = true });
            scenario.Expectations.Add(new EventExpectation { Label = "취소됨(1)", EventName = $"Pattern.Ended.{InPattern.TagName}", Aggregate = EScenarioAggregate.Last, ReportOnly = true });
            return scenario;
        }

        /// <summary>패턴이 끝나기까지 걸릴 시간 대략 (전조 · 지연 · 도약 · 돌진 · 반복 · 무력화 체크 · 후딜).</summary>
        private static float estimateDuration(BossPatternDefinition InPattern)
        {
            float steps = 0.0f;
            for (int i = 0; i < InPattern.Steps.Count; ++i)
            {
                var step = InPattern.Steps[i];
                steps += step.StartDelay + step.TelegraphTime + step.LeapDuration + step.BossMoveDuration + step.ChargeWindup;
            }

            int repeats = InPattern.IsCounterable ? Mathf.Max(1, InPattern.CounterRepeat) : 1;
            float total = steps * repeats + InPattern.Recovery;
            if (InPattern.StaggerCheck == true)
            {
                total = Mathf.Max(total, InPattern.StaggerTime) + InPattern.Recovery;
            }

            return total;
        }

        private static SpawnPartyStep dummyParty()
        {
            var party = new SpawnPartyStep { Label = "더미 파티 4명 (보스 앞)" };
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Slayer, Position = new Vector3(0.0f, 0.0f, 1.5f) });
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Infighter, Position = new Vector3(-3.0f, 0.0f, 3.0f) });
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Summoner, Position = new Vector3(3.0f, 0.0f, 3.0f) });
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Artist, Position = new Vector3(0.0f, 0.0f, -2.0f) });
            return party;
        }

        // ------------------------------------------------------------------ 기믹

        private static TestScenarioAsset leapCrossChase()
        {
            var s = make("도약 십자/X → 추적 휠윈드", "원 + 십자/X 장판 5개가 동시에 보이고, 보스가 도약해 이동하고, 넉백이 일어나는지", "Mechanics");
            s.Timeout = 40.0f;
            s.Steps.Add(new SpawnBossStep { BossId = "Gate2", Position = new Vector3(0.0f, 0.0f, 8.0f), FaceTowards = Vector3.zero });
            s.Steps.Add(dummyParty());
            s.Steps.Add(new ActivatePatternStep { PatternTag = ETag.Pattern_G2_LeapSmashWhirlwind, Timeout = 25.0f });
            s.Expectations.Add(new ProbeExpectation { Label = "동시 장판 수", Probe = "Telegraph.Visible", Moment = EProbeMoment.MaxDuring, Compare = EScenarioCompare.GreaterOrEqual, Value = 5 });
            s.Expectations.Add(new ProbeExpectation { Label = "보스 이동 거리", Probe = "Boss.Moved", Moment = EProbeMoment.MaxDuring, Compare = EScenarioCompare.GreaterOrEqual, Value = 3 });
            s.Expectations.Add(new EventExpectation { Label = "넉백", EventName = "Knockback", Compare = EScenarioCompare.GreaterOrEqual, Value = 1 });
            s.Expectations.Add(new EventExpectation { Label = "추적 안내", EventName = "Message", ReportOnly = true });
            return s;
        }

        private static TestScenarioAsset chargeIntoPillar()
        {
            // 기둥(45°, 반지름 14) 너머에 대상을 세워 돌진 경로가 기둥을 지나게 한다.
            var s = make("돌진 → 기둥 충돌", "돌진이 기둥에 막혀 기둥이 부서지고, 생명 구슬 2개가 나오고, 보스가 그로기에 빠지는지", "Mechanics");
            s.Timeout = 30.0f;
            s.Steps.Add(new SpawnBossStep { BossId = "Gate2", Position = new Vector3(2.0f, 0.0f, 2.0f), FaceTowards = new Vector3(14.0f, 0.0f, 14.0f) });
            var party = new SpawnPartyStep { Label = "기둥 너머 대상 1명" };
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Slayer, Position = new Vector3(14.5f, 0.0f, 14.5f) });
            s.Steps.Add(party);
            s.Steps.Add(new ActivatePatternStep { PatternTag = ETag.Pattern_G2_Charge, Timeout = 10.0f });
            s.Steps.Add(new WaitSecondsStep { Seconds = 1.0f });
            s.Expectations.Add(new EventExpectation { Label = "기둥 파괴", EventName = "Structure.Broken.Pillar", Compare = EScenarioCompare.Equal, Value = 1 });
            s.Expectations.Add(new ProbeExpectation { Label = "생명 구슬", Probe = "Pickups", Moment = EProbeMoment.MaxDuring, Compare = EScenarioCompare.GreaterOrEqual, Value = 2 });
            s.Expectations.Add(new ProbeExpectation { Label = "보스 그로기", Probe = "Boss.Groggy", Moment = EProbeMoment.MaxDuring, Compare = EScenarioCompare.Equal, Value = 1 });
            return s;
        }

        private static TestScenarioAsset firstWipeBreaksAll()
        {
            var s = make("첫 즉사기 → 기둥 · 벽 전부 파괴", "Pattern.G2.LethalFieldDouble 첫 타격에 남은 기둥 4 + 벽 10 이 모두 무너지는지", "Mechanics");
            s.Timeout = 30.0f;
            s.Steps.Add(new SpawnBossStep { BossId = "Gate2" });
            s.Steps.Add(dummyParty());
            s.Steps.Add(new ActivatePatternStep { PatternTag = ETag.Pattern_G2_LethalFieldDouble, Timeout = 15.0f });
            s.Expectations.Add(new ProbeExpectation { Label = "무너진 구조물", Probe = "Structures.Destroyed", Compare = EScenarioCompare.GreaterOrEqual, Value = 14 });
            s.Expectations.Add(new EventExpectation { Label = "파티 사망 (즉사기)", EventName = "Died.Party", ReportOnly = true });
            return s;
        }

        private static TestScenarioAsset knockbackFall()
        {
            var s = make("지형 파괴 후 넉백으로 맵 밖 → 낙사", "지형 파괴 이후, 외곽 벽 바깥 가장자리에서 바깥으로 넉백되면 떨어져 죽는지", "Mechanics");
            s.Timeout = 15.0f;
            s.Steps.Add(new SpawnBossStep { BossId = "Gate2", Position = Vector3.zero });
            var party = new SpawnPartyStep();
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Slayer, Position = new Vector3(0.0f, 0.0f, 23.0f) });
            s.Steps.Add(party);
            s.Steps.Add(new EnableFallDeathStep { Label = "지형 파괴 이후 상태" });
            s.Steps.Add(new KnockbackMemberStep { Member = 0, Direction = Vector3.forward, Distance = 8.0f });
            s.Steps.Add(new WaitForEventStep { EventName = "Died.Party", Timeout = 6.0f });
            s.Expectations.Add(new EventExpectation { Label = "낙사", EventName = "Died.Party", Compare = EScenarioCompare.Equal, Value = 1 });
            return s;
        }

        private static TestScenarioAsset knockbackRescuedBeforeCollapse()
        {
            var s = make("지형 파괴 전 넉백으로 맵 밖 → 구출", "외곽 Ring 벽이 있는 초반에는 넉백으로 바닥 밖에 떨어져도 죽지 않고 가장 가까운 바닥으로 옮겨지는지", "Mechanics");
            s.Timeout = 15.0f;
            s.Steps.Add(new SpawnBossStep { BossId = "Gate2", Position = Vector3.zero });
            var party = new SpawnPartyStep();
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Slayer, Position = new Vector3(0.0f, 0.0f, 23.0f) });
            s.Steps.Add(party);
            s.Steps.Add(new KnockbackMemberStep { Member = 0, Direction = Vector3.forward, Distance = 8.0f });
            s.Steps.Add(new WaitSecondsStep { Seconds = 4.0f });
            s.Expectations.Add(new EventExpectation { Label = "사망 없음", EventName = "Died.Party", Compare = EScenarioCompare.Equal, Value = 0 });
            s.Expectations.Add(new ProbeExpectation { Label = "바닥 안으로 돌아옴", Probe = "Party0.DistanceFromCenter", Compare = EScenarioCompare.LessOrEqual, Value = 27 });
            return s;
        }

        /// <summary>2관문 보스 AI 를 2분 돌린다: 돌진은 여러 번, 외곽 이탈 도약(G2-28, 삭제)은 한 번도 없다.</summary>
        private static TestScenarioAsset edgeExitOnlyOnce()
        {
            var s = make("G2-28 외곽 이탈 없음", "보스 AI 로 2분: 돌진(Pattern.G2.Charge)이 여러 번 나와도 외곽 이탈 도약(G2-28)은 나오지 않는다 (삭제 — 보스가 외곽 구석에 머물러 공격이 끊겼다)", "Mechanics");
            s.Timeout = 140.0f;
            s.TimeScale = 3.0f;
            s.Steps.Add(new SpawnBossStep { BossId = "Gate2", Position = new Vector3(0.0f, 0.0f, 6.0f), FaceTowards = new Vector3(0.0f, 0.0f, -6.0f), EnableAI = true });
            var party = new SpawnPartyStep { Label = "무적 더미 2명" };
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Slayer, Position = new Vector3(-2.0f, 0.0f, -4.0f), Invulnerable = true });
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Artist, Position = new Vector3(2.0f, 0.0f, -6.0f), Invulnerable = true });
            s.Steps.Add(party);
            s.Steps.Add(new WaitSecondsStep { Seconds = 120.0f });
            s.Expectations.Add(new EventExpectation { Label = "돌진 여러 번", EventName = "Pattern.Started.Pattern.G2.Charge", Compare = EScenarioCompare.GreaterOrEqual, Value = 2 });
            s.Expectations.Add(new EventExpectation { Label = "외곽 이탈 도약 없음", EventName = "Pattern.Started.G2-28", Compare = EScenarioCompare.Equal, Value = 0 });
            s.Expectations.Add(new EventExpectation { Label = "패턴 수", EventName = "Pattern.Started", ReportOnly = true });
            return s;
        }

        private static TestScenarioAsset rescueWithoutKnockback()
        {
            var s = make("넉백 없이 바닥 밖 → 구출", "넉백 없이 바닥 밖에 놓이면 죽지 않고 가장 가까운 바닥으로 옮겨지는지", "Mechanics");
            s.Timeout = 15.0f;
            s.Steps.Add(new SpawnBossStep { BossId = "Gate2", Position = Vector3.zero });
            var party = new SpawnPartyStep();
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Slayer, Position = new Vector3(0.0f, 0.0f, -5.0f) });
            s.Steps.Add(party);
            s.Steps.Add(new MoveMemberStep { Member = 0, Position = new Vector3(0.0f, 0.0f, 31.0f) });
            s.Steps.Add(new WaitSecondsStep { Seconds = 3.0f });
            s.Expectations.Add(new EventExpectation { Label = "사망 없음", EventName = "Died.Party", Compare = EScenarioCompare.Equal, Value = 0 });
            s.Expectations.Add(new ProbeExpectation { Label = "바닥 안으로 돌아옴", Probe = "Party0.DistanceFromCenter", Compare = EScenarioCompare.LessOrEqual, Value = 27 });
            return s;
        }

        // ------------------------------------------------------------------ 밸런스

        /// <summary>4인 파티가 InSeconds 동안 깎는 체력 줄. 1관문(60줄)은 딜 측정기 4명이 약 30초에 잡으므로 15초 구간으로 잰다 (실제 플레이는 이동 · 회피로 약 1분).</summary>
        private static TestScenarioAsset balanceGate(string InBossId, float InSeconds)
        {
            var s = make($"밸런스 {InBossId} {InSeconds:0}초", $"보스 AI 를 끄고 4인 파티(테스트 딜 측정기)가 {InSeconds:0}초 동안 때린다. 체력 줄 · 피해 · 치명타 비율을 측정하고 범위를 벗어나면 실패", "Balance");
            s.Timeout = InSeconds + 20.0f;
            s.TimeScale = 3.0f;
            s.Steps.Add(new SpawnBossStep { BossId = InBossId, Position = new Vector3(0.0f, 0.0f, 6.0f), FaceTowards = new Vector3(0.0f, 0.0f, -6.0f) });
            var party = new SpawnPartyStep { Label = "4인 파티 (딜 측정기)" };
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Slayer, Position = new Vector3(-2.0f, 0.0f, -4.0f), AutoAttack = true });
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Infighter, Position = new Vector3(0.0f, 0.0f, -4.0f), AutoAttack = true });
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Summoner, Position = new Vector3(2.0f, 0.0f, -4.0f), AutoAttack = true });
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Artist, Position = new Vector3(0.0f, 0.0f, -6.0f), AutoAttack = true });
            s.Steps.Add(party);
            s.Steps.Add(new WaitSecondsStep { Seconds = InSeconds });
            // 범위는 현재 밸런스(PlayerDamageScale 0.25, 4인 보스 체력 0.35 — PartyBalance) 측정값(15초 −48줄) 기준. 밸런스를 바꾸면 이 값도 갱신한다.
            s.Expectations.Add(new ProbeExpectation { Label = $"{InSeconds:0}초 동안 깎은 체력 줄 (줄어든 양, 음수)", Probe = "Boss.HpLines", Moment = EProbeMoment.Delta, Compare = EScenarioCompare.Between, Value = -58, Value2 = -25 });
            s.Expectations.Add(new ProbeExpectation { Label = "파티 치명타 비율 %", Probe = "Party.CritRate", Compare = EScenarioCompare.Between, Value = 20, Value2 = 70 });
            s.Expectations.Add(new EventExpectation { Label = "보스에게 준 피해 합", EventName = "Damage.ToBoss", Aggregate = EScenarioAggregate.Sum, ReportOnly = true });
            s.Expectations.Add(new EventExpectation { Label = "타격 수", EventName = "Hit.Party", ReportOnly = true });
            return s;
        }

        // ------------------------------------------------------------------ util

        // ------------------------------------------------------------------ 스킬 GameplayCue

        /// <summary>
        /// 슬레이어 첫 스킬에 시전 전(마법진) · 중(천상 궤적) · 후(방사 광선) 셰이더 큐를 끼우고 시전한다.
        /// 프레임워크 GameplayCueManager → ShaderGameplayCueNotify 경로로 Active/Removed/Executed 가 순서대로 한 번씩 오고, 뷰가 겹쳐 그려졌다가 모두 정리되는지.
        /// </summary>
        private static TestScenarioAsset skillCuePhases()
        {
            var s = make("스킬 큐 시전 전 · 중 · 후", "슬레이어 Q 스킬: 시전 전 아우라 → 시전 중 무기 궤적 → 마지막 타격 무기 분출 큐가 순서대로 재생되고 끝나면 정리되는지", "SkillCue");
            s.TimeScale = 1.0f;
            s.Timeout = 20.0f;
            s.Steps.Add(new SpawnPartyStep { Members = new List<PartyMemberSpec> { new PartyMemberSpec { Class = ECharacterClass.Slayer, Position = Vector3.zero, Invulnerable = true } } });
            s.Steps.Add(new AssignSkillCueStep { Phase = DemoLA.Skills.ESkillCuePhase.PreCast, ShaderName = "DemoLA/SkillCue/PreCast/AuraRuneCircle" });
            s.Steps.Add(new AssignSkillCueStep { Phase = DemoLA.Skills.ESkillCuePhase.Casting, ShaderName = "DemoLA/SkillCue/Casting/TrailCelestial" });
            s.Steps.Add(new AssignSkillCueStep { Phase = DemoLA.Skills.ESkillCuePhase.PostCast, ShaderName = "DemoLA/SkillCue/PostCast/BurstRadiantRays" });
            s.Steps.Add(new WaitSecondsStep { Seconds = 0.3f });
            s.Steps.Add(new CastSkillStep { SkillSlot = 0 });
            // 시전 후 큐(약 0.7초) · 꺼짐 페이드가 끝날 때까지.
            s.Steps.Add(new WaitSecondsStep { Seconds = 1.5f });

            const string pre = "Cue.PreCast.";
            const string casting = "Cue.Casting.";
            const string post = "Cue.PostCast.";
            s.Expectations.Add(new EventExpectation { Label = "시전 1회", EventName = "Skill.Cast", Compare = EScenarioCompare.Equal, Value = 1 });
            s.Expectations.Add(new EventExpectation { Label = "시전 전 켜짐", EventName = pre + "Active", Compare = EScenarioCompare.Equal, Value = 1 });
            s.Expectations.Add(new EventExpectation { Label = "시전 전 꺼짐", EventName = pre + "Removed", Compare = EScenarioCompare.Equal, Value = 1 });
            s.Expectations.Add(new EventExpectation { Label = "시전 중 켜짐", EventName = casting + "Active", Compare = EScenarioCompare.Equal, Value = 1 });
            s.Expectations.Add(new EventExpectation { Label = "시전 중 꺼짐", EventName = casting + "Removed", Compare = EScenarioCompare.Equal, Value = 1 });
            s.Expectations.Add(new EventExpectation { Label = "시전 후 분출", EventName = post + "Executed", Compare = EScenarioCompare.Equal, Value = 1 });
            s.Expectations.Add(new EventIntervalExpectation { Label = "아우라 → 궤적 순서", FromEvent = pre + "Active", ToEvent = casting + "Active", Compare = EScenarioCompare.GreaterOrEqual, Value = 0 });
            s.Expectations.Add(new EventIntervalExpectation { Label = "궤적 → 분출 순서", FromEvent = casting + "Active", ToEvent = post + "Executed", Compare = EScenarioCompare.Greater, Value = 0 });
            s.Expectations.Add(new EventIntervalExpectation { Label = "분출 → 스킬 끝 (분출이 시전 안에)", FromEvent = post + "Executed", ToEvent = "Skill.Ended", Compare = EScenarioCompare.GreaterOrEqual, Value = 0 });
            s.Expectations.Add(new ProbeExpectation { Label = "동시에 그려진 큐", Probe = "Cue.Views", Moment = EProbeMoment.MaxDuring, Compare = EScenarioCompare.GreaterOrEqual, Value = 1 });
            s.Expectations.Add(new ProbeExpectation { Label = "끝나면 정리됨", Probe = "Cue.Views", Moment = EProbeMoment.AtEnd, Compare = EScenarioCompare.Equal, Value = 0 });
            s.Expectations.Add(new EventIntervalExpectation { Label = "시전 → 분출 시간", FromEvent = "Skill.Cast", ToEvent = post + "Executed", ReportOnly = true });
            s.Expectations.Add(new EventExpectation { Label = "큐 호출 수", EventName = "Cue", ReportOnly = true });
            return s;
        }

        private static TestScenarioAsset make(string InTitle, string InDescription, string InCategory)
        {
            var scenario = ScriptableObject.CreateInstance<TestScenarioAsset>();
            scenario.DisplayName = InTitle;
            scenario.Description = InDescription;
            scenario.Category = InCategory;
            return scenario;
        }

        private static void save(TestScenarioAsset InScenario, string InFolder, string InName, bool InOverwrite, ref int OutCreated, ref int OutSkipped)
        {
            ensureFolder(InFolder);
            string path = $"{InFolder}/{sanitize(InName)}.asset";
            if (AssetDatabase.LoadAssetAtPath<TestScenarioAsset>(path) != null)
            {
                if (InOverwrite == false)
                {
                    OutSkipped += 1;
                    Object.DestroyImmediate(InScenario);
                    return;
                }

                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(InScenario, path);
            OutCreated += 1;
        }

        private static string sanitize(string InName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                InName = InName.Replace(c, '_');
            }

            return InName.Replace(' ', '_');
        }

        private static void ensureFolder(string InPath)
        {
            if (AssetDatabase.IsValidFolder(InPath) == true)
            {
                return;
            }

            string parent = Path.GetDirectoryName(InPath).Replace('\\', '/');
            ensureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(InPath));
        }
    }
}
