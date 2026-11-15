using System;
using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// 인원 수 밸런스. 파티는 실제 플레이어만으로 구성되므로(아군 AI 없음) 들어온 인원(1 ~ 4명)에 맞춰
    /// 보스 체력 · 무력화 게이지 · 부위파괴 필요량 · 보스가 주는 피해를 조정한다.
    ///  - 기준은 4인. "4인 배율" × "인원 곡선[인원 - 1]" 이 최종 배율이다.
    ///  - 체력 · 무력화 배율은 보스 정의 값(원본 기믹 기준 8인 설계)에 곱한다.
    ///  - 피해 배율은 즉사급(최대 체력 100% 이상)에는 적용하지 않는다 — 기믹 실패는 인원과 상관없이 즉사.
    /// GameDatabase.PartyBalance 에서 고치고, GameBootstrap 이 Settings 로 넣는다.
    /// </summary>
    [Serializable]
    public class PartyBalanceSettings
    {
        public const int MaxPartySize = 4;

        [Header("4인 기준")]
        [Tooltip("보스 체력 배율 (BossDefinition.HpPerLine 대비). 예전 값 0.5 (8인 설계의 절반)")]
        public float BossHealthScale = 0.35f;
        [Tooltip("무력화 게이지 배율 (패턴 StaggerGauge 대비). 예전 값 0.5")]
        public float BossStaggerScale = 0.4f;
        [Tooltip("부위파괴 필요량 배율 (기믹 DestructionPerStack 대비)")]
        public float BossDestructionScale = 0.8f;
        [Tooltip("보스 패턴 피해 배율 (즉사급 제외). 예전 값 1.0")]
        public float BossDamageScale = 0.6f;

        [Header("인원 곡선 (1명 · 2명 · 3명 · 4명, 4명 = 1)")]
        [Tooltip("보스 체력 · 무력화 · 부위파괴 필요량. 인원이 적을수록 서포터 · 시너지가 없어 1인당 몫보다 조금 더 깎는다")]
        public float[] ToughnessBySize = { 0.4f, 0.65f, 0.85f, 1.0f };
        [Tooltip("보스 패턴 피해. 혼자면 받아 줄 사람 · 회복이 없으므로 덜 아프게")]
        public float[] DamageBySize = { 0.7f, 0.8f, 0.9f, 1.0f };

        public float Toughness(int InPartySize)
        {
            return curve(ToughnessBySize, InPartySize);
        }

        public float Health(int InPartySize)
        {
            return BossHealthScale * Toughness(InPartySize);
        }

        public float Stagger(int InPartySize)
        {
            return BossStaggerScale * Toughness(InPartySize);
        }

        public float Destruction(int InPartySize)
        {
            return BossDestructionScale * Toughness(InPartySize);
        }

        public float Damage(int InPartySize)
        {
            return BossDamageScale * curve(DamageBySize, InPartySize);
        }

        private static float curve(float[] InCurve, int InPartySize)
        {
            if (InCurve == null || InCurve.Length == 0)
            {
                return 1.0f;
            }

            int index = Mathf.Clamp(InPartySize, 1, Mathf.Min(MaxPartySize, InCurve.Length)) - 1;
            return Mathf.Max(0.01f, InCurve[index]);
        }
    }

    /// <summary>현재 적용 중인 인원 밸런스. GameBootstrap 이 GameDatabase 의 설정으로 바꾼다.</summary>
    public static class PartyBalance
    {
        public static PartyBalanceSettings Settings = new PartyBalanceSettings();
    }
}
