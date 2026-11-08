namespace DemoLA.Core
{
    /// <summary>GameplayEffectSpec.SetByCaller 키.</summary>
    public enum ESetByCaller
    {
        None = 0,
        /// <summary>스킬 계수 (공격력 × 계수).</summary>
        Coefficient = 1,
        FlatDamage = 2,
        StaggerValue = 3,
        DestructionValue = 4,
        CritRateBonus = 5,
        CritDamageBonus = 6,
        /// <summary>EPositional 값 (0 없음, 1 백어택, 2 헤드어택).</summary>
        PositionalType = 7,
        PositionalBonus = 8,
        /// <summary>보스 패턴 피해 = 대상 최대 체력 × 이 값.</summary>
        MaxHealthFraction = 9,
        /// <summary>1 이면 방어 구슬(DeathWard)을 소모하는 즉사 패턴.</summary>
        ConsumesDeathWard = 10,
        Cooldown = 11,
        Cost = 12,
        Magnitude = 13,
        IsCounter = 14,
        /// <summary>실행 결과 기록용 (DamageExecution 이 쓴다).</summary>
        ResultIsCrit = 20,
        ResultPositional = 21,
        /// <summary>스킬 보석 피해 배율 (%) — 다른 피해 증가와 곱연산.</summary>
        GemDamagePercent = 22,
        SkillId = 23,
        /// <summary>EElement (스킬 속성).</summary>
        Element = 24,
        /// <summary>ESkillFlags (각성기 / 아이덴티티 / 배틀 아이템).</summary>
        SkillFlags = 25,
        SkillCooldown = 26,
    }

    /// <summary>IGameplayEffectExecution 등록 id.</summary>
    public enum EExecution
    {
        None = 0,
        SkillDamage = 1,
        BossPatternDamage = 2,
        Heal = 3,
    }

    /// <summary>GameplayCue id. 연출 핸들러는 Cue/GameplayCueHandlers 에 있다.</summary>
    public enum ECue
    {
        None = 0,
        Hit = 1,
        CritHit = 2,
        Countered = 3,
        StaggerBroken = 4,
        Heal = 5,
        Shield = 6,
        DeathWardConsumed = 7,
        Groggy = 8,
        StatusGeneric = 9,
    }

    public enum ETeam
    {
        Neutral = 0,
        Player = 1,
        Enemy = 2,
    }

    public enum EPositional
    {
        None = 0,
        BackAttack = 1,
        HeadAttack = 2,
    }
}
