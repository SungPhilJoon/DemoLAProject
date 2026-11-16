using DemoLA.Combat;
using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// 보스 패턴 시작 · 끝 · 도약을 Console 에 남긴다 (어떤 공격이 왜 시작됐는지 추적용).
    /// 에디터와 개발 빌드에서만 기본으로 켜진다. 끄려면 BossPatternLog.Enabled = false.
    /// </summary>
    public static class BossPatternLog
    {
        public static bool Enabled = Debug.isDebugBuild;

        public static void Started(BossActor InBoss, BossPatternDefinition InDefinition, CombatActor InTarget)
        {
            if (Enabled == false)
            {
                return;
            }

            string target = InTarget != null ? $"{InTarget.GetName()} {fmt(InTarget.Position)} 거리 {Vector3.Distance(Flat(InBoss.Position), Flat(InTarget.Position)):0.0}m" : "없음";
            Debug.Log($"[BossPattern] ▶ {InDefinition.TagName} '{InDefinition.DisplayName}' ({InDefinition.Category}) t={Time.time:0.0}s · 보스 {fmt(InBoss.Position)} · 대상 {target}");
        }

        public static void Ended(BossActor InBoss, BossPatternDefinition InDefinition, bool InCancelled)
        {
            if (Enabled == false)
            {
                return;
            }

            Debug.Log($"[BossPattern] ■ {InDefinition.TagName} '{InDefinition.DisplayName}' {(InCancelled ? "취소됨" : "종료")} t={Time.time:0.0}s · 보스 {fmt(InBoss.Position)}");
        }

        public static void Leap(BossActor InBoss, BossPatternDefinition InDefinition, int InStep, EPatternTargeting InTargeting, Vector3 InDestination)
        {
            if (Enabled == false)
            {
                return;
            }

            Debug.Log($"[BossPattern]   ↗ {InDefinition.TagName} 단계 {InStep} 도약 ({InTargeting}) {fmt(InBoss.Position)} → {fmt(InDestination)}");
        }

        public static void Message(string InText)
        {
            if (Enabled == true)
            {
                Debug.Log($"[BossPattern] {InText}");
            }
        }

        private static Vector3 Flat(Vector3 InV)
        {
            return new Vector3(InV.x, 0.0f, InV.z);
        }

        private static string fmt(Vector3 InV)
        {
            return $"({InV.x:0.0}, {InV.z:0.0})";
        }
    }
}
