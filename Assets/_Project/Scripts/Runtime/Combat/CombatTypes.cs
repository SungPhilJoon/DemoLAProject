namespace DemoLA.Combat
{
    /// <summary>공격 속성. 보스는 속성별 받는 피해 배율을 가진다 (BossDefinition.ElementWeakness).</summary>
    public enum EElement
    {
        None = 0,
        Fire = 1,
        Ice = 2,
        Lightning = 3,
        Earth = 4,
        Water = 5,
        Dark = 6,
        Holy = 7,
    }

    /// <summary>조건부 피해 모디파이어가 붙는 계산 그룹.</summary>
    public enum EDamageGroup
    {
        /// <summary>독립 곱연산 피해 %.</summary>
        Independent = 0,
        /// <summary>합연산 피해 증가 %.</summary>
        Additive = 1,
        CritRate = 2,
        CritDamage = 3,
        /// <summary>받는 피해 감소 % (피격자 쪽).</summary>
        IncomingReduction = 4,
    }

    /// <summary>
    /// 조건 종류. 실제 판정은 DamageCondition 하위 클래스가 [Kind] 로 맡는다 (Combat/DamageConditions.cs).
    /// 값을 바꾸면 CSV 의 이름이 깨지므로 끝에만 추가한다.
    /// </summary>
    public enum EDamageCondition
    {
        Always = 0,
        TargetIsBoss = 1,
        BackAttack = 2,
        HeadAttack = 3,
        /// <summary>시전자 체력 % ≥ Param.</summary>
        SelfHealthAbove = 4,
        /// <summary>시전자 체력 % ≤ Param.</summary>
        SelfHealthBelow = 5,
        /// <summary>대상 체력 % ≤ Param.</summary>
        TargetHealthBelow = 6,
        /// <summary>보호막이 있을 때.</summary>
        SelfShielded = 7,
        /// <summary>대상이 무력화(그로기) 상태.</summary>
        TargetGroggy = 8,
        /// <summary>시전자 이동속도 초과분(%) × Param 비례 (값 = 비율).</summary>
        MoveSpeedScaled = 9,
        /// <summary>대상과의 거리 ≤ Param (근거리).</summary>
        TargetWithin = 10,
        /// <summary>대상과의 거리 ≥ Param (원거리).</summary>
        TargetBeyond = 11,
        /// <summary>각성기만.</summary>
        AwakeningSkill = 12,
        /// <summary>아이덴티티 스킬만.</summary>
        IdentitySkill = 13,
        /// <summary>쿨다운 Param 초 이상 스킬만.</summary>
        CooldownAtLeast = 14,
    }

    public enum EBattleItemType
    {
        /// <summary>최대 체력 Value% 즉시 회복.</summary>
        HealPotion = 0,
        /// <summary>Duration 동안 무적 + 행동 불가 (시간 정지).</summary>
        TimeStop = 1,
        /// <summary>디버프 해제 (정화).</summary>
        Panacea = 2,
        /// <summary>조준 지점 Radius 범위 부위 파괴 Value + 무력화.</summary>
        DestructionBomb = 3,
        /// <summary>조준 지점 Radius 범위 기절 Duration.</summary>
        Flashbang = 4,
    }
}
