using System.Collections.Generic;
using System.IO;
using DemoLA.Boss;
using DemoLA.Character;
using DemoLA.Content;
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
    ///  - 밸런스: AI 파티 60초 → 보스 체력 줄 · 피해 · 치명타 비율 (범위 검증 + 측정).
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

                    save(patternScenario(boss, pattern), $"{Root}/Patterns/{boss.BossId}", $"TS_{pattern.PatternId}", InOverwrite, ref created, ref skipped);
                }
            }

            save(leapCrossChase(), $"{Root}/Mechanics", "TS_G2-23_도약십자추적", InOverwrite, ref created, ref skipped);
            save(chargeIntoPillar(), $"{Root}/Mechanics", "TS_G2-11_돌진기둥충돌", InOverwrite, ref created, ref skipped);
            save(firstWipeBreaksAll(), $"{Root}/Mechanics", "TS_G2-03_구조물전부파괴", InOverwrite, ref created, ref skipped);
            save(knockbackFall(), $"{Root}/Mechanics", "TS_낙사_넉백_지형파괴후", InOverwrite, ref created, ref skipped);
            save(knockbackRescuedBeforeCollapse(), $"{Root}/Mechanics", "TS_낙사_넉백_벽구간구출", InOverwrite, ref created, ref skipped);
            save(rescueWithoutKnockback(), $"{Root}/Mechanics", "TS_낙사_넉백없으면구출", InOverwrite, ref created, ref skipped);
            save(balanceGate("Gate1"), $"{Root}/Balance", "TS_밸런스_Gate1_60초", InOverwrite, ref created, ref skipped);
            AssetDatabase.SaveAssets();
            return $"시나리오 생성 {created}개, 건너뜀 {skipped}개 → {Root}";
        }

        // ------------------------------------------------------------------ 패턴

        private static TestScenarioAsset patternScenario(BossDefinition InBoss, BossPatternDefinition InPattern)
        {
            var scenario = make($"{InPattern.PatternId} {InPattern.DisplayName}", $"보스 패턴 {InPattern.PatternId} 단독 확인 (자동 생성)", "Pattern." + InBoss.BossId);
            float timeout = estimateDuration(InPattern) + 10.0f;
            scenario.Timeout = timeout + 10.0f;
            scenario.Steps.Add(new SpawnBossStep { BossId = InBoss.BossId, Position = new Vector3(0.0f, 0.0f, 6.0f), FaceTowards = new Vector3(0.0f, 0.0f, -6.0f) });
            scenario.Steps.Add(dummyParty());
            scenario.Steps.Add(new ActivatePatternStep { PatternId = InPattern.PatternId, Timeout = timeout });

            scenario.Expectations.Add(new EventExpectation { Label = "패턴 시작", EventName = $"Pattern.Started.{InPattern.PatternId}", Compare = EScenarioCompare.GreaterOrEqual, Value = 1 });
            scenario.Expectations.Add(new EventExpectation { Label = "패턴 끝남", EventName = $"Pattern.Ended.{InPattern.PatternId}", Compare = EScenarioCompare.GreaterOrEqual, Value = 1 });
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
            scenario.Expectations.Add(new EventExpectation { Label = "취소됨(1)", EventName = $"Pattern.Ended.{InPattern.PatternId}", Aggregate = EScenarioAggregate.Last, ReportOnly = true });
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
            s.Steps.Add(new ActivatePatternStep { PatternId = "G2-23", Timeout = 25.0f });
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
            s.Steps.Add(new ActivatePatternStep { PatternId = "G2-11", Timeout = 10.0f });
            s.Steps.Add(new WaitSecondsStep { Seconds = 1.0f });
            s.Expectations.Add(new EventExpectation { Label = "기둥 파괴", EventName = "Structure.Broken.Pillar", Compare = EScenarioCompare.Equal, Value = 1 });
            s.Expectations.Add(new ProbeExpectation { Label = "생명 구슬", Probe = "Pickups", Moment = EProbeMoment.MaxDuring, Compare = EScenarioCompare.GreaterOrEqual, Value = 2 });
            s.Expectations.Add(new ProbeExpectation { Label = "보스 그로기", Probe = "Boss.Groggy", Moment = EProbeMoment.MaxDuring, Compare = EScenarioCompare.Equal, Value = 1 });
            return s;
        }

        private static TestScenarioAsset firstWipeBreaksAll()
        {
            var s = make("첫 즉사기 → 기둥 · 벽 전부 파괴", "G2-03 첫 타격에 남은 기둥 4 + 벽 10 이 모두 무너지는지", "Mechanics");
            s.Timeout = 30.0f;
            s.Steps.Add(new SpawnBossStep { BossId = "Gate2" });
            s.Steps.Add(dummyParty());
            s.Steps.Add(new ActivatePatternStep { PatternId = "G2-03", Timeout = 15.0f });
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

        private static TestScenarioAsset balanceGate(string InBossId)
        {
            var s = make($"밸런스 {InBossId} 60초", "보스 AI 를 끄고 AI 파티 4명이 60초 동안 때린다. 체력 줄 · 피해 · 치명타 비율을 측정하고 범위를 벗어나면 실패", "Balance");
            s.Timeout = 80.0f;
            s.TimeScale = 3.0f;
            s.Steps.Add(new SpawnBossStep { BossId = InBossId, Position = new Vector3(0.0f, 0.0f, 6.0f), FaceTowards = new Vector3(0.0f, 0.0f, -6.0f) });
            var party = new SpawnPartyStep { Label = "AI 파티 4명" };
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Slayer, Position = new Vector3(-2.0f, 0.0f, -4.0f), UseAI = true });
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Infighter, Position = new Vector3(0.0f, 0.0f, -4.0f), UseAI = true });
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Summoner, Position = new Vector3(2.0f, 0.0f, -4.0f), UseAI = true });
            party.Members.Add(new PartyMemberSpec { Class = ECharacterClass.Artist, Position = new Vector3(0.0f, 0.0f, -6.0f), UseAI = true });
            s.Steps.Add(party);
            s.Steps.Add(new WaitSecondsStep { Seconds = 60.0f });
            // 범위는 현재 밸런스(PlayerDamageScale 0.18, 치명타 조정 후) 측정값 기준. 밸런스를 바꾸면 이 값도 갱신한다.
            s.Expectations.Add(new ProbeExpectation { Label = "60초 동안 깎은 체력 줄 (줄어든 양, 음수)", Probe = "Boss.HpLines", Moment = EProbeMoment.Delta, Compare = EScenarioCompare.Between, Value = -45, Value2 = -10 });
            s.Expectations.Add(new ProbeExpectation { Label = "파티 치명타 비율 %", Probe = "Party.CritRate", Compare = EScenarioCompare.Between, Value = 20, Value2 = 70 });
            s.Expectations.Add(new EventExpectation { Label = "보스에게 준 피해 합", EventName = "Damage.ToBoss", Aggregate = EScenarioAggregate.Sum, ReportOnly = true });
            s.Expectations.Add(new EventExpectation { Label = "타격 수", EventName = "Hit.Party", ReportOnly = true });
            return s;
        }

        // ------------------------------------------------------------------ util

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
