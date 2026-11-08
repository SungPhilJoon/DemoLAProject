using System;
using UnityEngine;

namespace DemoLA.Attributes
{
    /// <summary>
    /// 능력치 환산 계수. 실제 게임 수치는 확인되지 않았으므로 전부 에디터에서 바꿀 수 있는 자리 표시 값이다.
    /// (Documentation/구현 보류 사항.md 의 "능력치 공식" 항목 참고)
    /// </summary>
    [Serializable]
    public class StatFormulaSettings
    {
        [Tooltip("치명 스탯 1 당 치명타 적중률 %")]
        public float CritRatePerPoint = 0.0357f;
        [Tooltip("신속 스탯 1 당 공격 속도 %")]
        public float AttackSpeedPerSwiftness = 0.0171f;
        [Tooltip("신속 스탯 1 당 이동 속도 %")]
        public float MoveSpeedPerSwiftness = 0.0171f;
        [Tooltip("신속 스탯 1 당 쿨다운 감소 %")]
        public float CooldownReductionPerSwiftness = 0.0215f;
        [Tooltip("특화 스탯 1 당 아이덴티티 게이지 획득량 % (직업별 효과는 미정)")]
        public float IdentityGainPerSpecialization = 0.03f;
        [Tooltip("공격력 = sqrt(주스탯 × 무기공격력 / Divisor)")]
        public float AttackPowerDivisor = 6.0f;
        [Tooltip("방어력 감소율 = Def / (Def + K)")]
        public float DefenseConstant = 6500.0f;
        [Tooltip("백어택 기본 피해 증가 %")]
        public float BaseBackAttackBonus = 5.0f;
        [Tooltip("헤드어택 기본 피해 증가 %")]
        public float BaseHeadAttackBonus = 5.0f;
        [Tooltip("백어택 추가 치명타 적중률 % (커뮤니티 통설 기준 10)")]
        public float BackAttackCritBonus = 10.0f;
        [Tooltip("백어택 판정 각도 (대상 정면 기준, 이 각도 이상 뒤쪽)")]
        public float BackAttackAngle = 120.0f;
        [Tooltip("헤드어택 판정 각도 (대상 정면 기준, 이 각도 이하 앞쪽)")]
        public float HeadAttackAngle = 60.0f;
        [Tooltip("이동 속도 100% 일 때 m/s")]
        public float BaseMoveSpeedMeters = 5.5f;
        [Tooltip("플레이어가 주는 모든 피해 배율 (밸런스). 0.18 = 4인 파티 기준 2관문 순수 딜 시간 약 6~7분 (갑옷 · 기믹 시간 별도)")]
        public float PlayerDamageScale = 0.18f;
        [Tooltip("이동/공격 속도 상한 %")]
        public float SpeedCap = 140.0f;
    }

    /// <summary>현재 적용 중인 공식. GameBootstrap 이 GameDatabase 의 설정으로 바꾼다.</summary>
    public static class StatFormula
    {
        public static StatFormulaSettings Settings = new StatFormulaSettings();

        public static float AttackPower(float InMainStat, float InWeaponPower, float InPercent)
        {
            if (InMainStat <= 0.0f || InWeaponPower <= 0.0f)
            {
                return 0.0f;
            }

            float divisor = Mathf.Max(0.0001f, Settings.AttackPowerDivisor);
            return Mathf.Sqrt(InMainStat * InWeaponPower / divisor) * (1.0f + InPercent * 0.01f);
        }

        /// <summary>방어력 → 받는 피해 감소 비율 (0~1). 방어 관통은 InPenetration(0~1) 만큼 방어력을 무시한다.</summary>
        public static float DefenseReduction(float InDefense, float InPenetration)
        {
            float defense = Mathf.Max(0.0f, InDefense * (1.0f - Mathf.Clamp01(InPenetration)));
            return defense / (defense + Mathf.Max(1.0f, Settings.DefenseConstant));
        }
    }
}
