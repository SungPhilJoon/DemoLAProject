namespace DemoLA.Attributes
{
    /// <summary>
    /// 캐릭터/몬스터 공용 속성 인덱스 (CombatAttributeSet).
    /// 퍼센트 속성은 "퍼센트 단위" 로 저장한다 (CritRate 25 = 25%).
    /// 저장 데이터에는 이름이 아니라 값이 들어가므로 중간 삽입하지 말고 Count 앞에 추가한다.
    /// </summary>
    public enum EAttr
    {
        Health = 0,
        MaxHealth = 1,
        Mana = 2,
        MaxMana = 3,
        Shield = 4,

        /// <summary>최종 공격력. 장비/주스탯에서 StatFormula.AttackPower 로 계산해 Base 에 넣는다.</summary>
        AttackPower = 5,
        Defense = 6,

        /// <summary>파생: Base + 치명 스탯 × 계수.</summary>
        CritRate = 7,
        /// <summary>치명타 피해 % (기본 200).</summary>
        CritDamage = 8,

        // 전투 특성 포인트 (치명/특화/신속/제압/인내/숙련)
        StatCrit = 9,
        StatSpecialization = 10,
        StatSwiftness = 11,
        StatDomination = 12,
        StatEndurance = 13,
        StatExpertise = 14,

        /// <summary>파생: 기본 100 + 신속 × 계수.</summary>
        AttackSpeed = 15,
        /// <summary>파생: 기본 100 + 신속 × 계수.</summary>
        MoveSpeed = 16,
        /// <summary>파생: 신속 × 계수 (%). 쿨다운 배율 = 1 - CDR/100.</summary>
        CooldownReduction = 17,

        /// <summary>피해 증가 (합연산 그룹, %).</summary>
        DamageIncrease = 18,
        /// <summary>
        /// 독립 곱연산 피해 증가. 이 속성을 건드리는 "이펙트마다" 따로 곱한다 (DamageExecution 참고).
        /// 각인처럼 서로 곱연산되는 출처에 쓴다.
        /// </summary>
        IndependentDamage = 19,
        BackAttackDamage = 20,
        HeadAttackDamage = 21,
        /// <summary>받는 피해 감소 (%).</summary>
        IncomingDamageReduction = 22,
        /// <summary>받는 피해 증가 (%) — 대상에 거는 디버프용.</summary>
        IncomingDamageAmplify = 23,
        StaggerPower = 24,
        DestructionPower = 25,
        OutgoingHealing = 26,

        IdentityGauge = 27,
        MaxIdentityGauge = 28,

        /// <summary>보스 무력화 게이지 (스태거 체크 중에만 의미가 있다).</summary>
        StaggerGauge = 29,
        MaxStaggerGauge = 30,
        /// <summary>누적 부위 파괴 수치 (갑옷 기믹).</summary>
        DestructionAccum = 31,

        /// <summary>주 스탯 (힘/민/지 통합).</summary>
        MainStat = 32,
        WeaponPower = 33,
        /// <summary>공격력 % 증가 (주스탯 공식 이후 곱).</summary>
        AttackPowerPercent = 34,

        // ---- 메타 속성: 이펙트가 Base 에 쌓으면 PostGameplayEffectExecute 가 실제 속성으로 옮기고 0 으로 되돌린다.
        IncomingDamage = 40,
        IncomingHeal = 41,
        IncomingStagger = 42,
        IncomingDestruction = 43,

        Count = 44,
    }

    /// <summary>스킬 한 개가 가지는 파라미터 (SkillAttributeSet). 트라이포드/보석이 GameplayEffect 로 바꾼다.</summary>
    public enum ESkillAttr
    {
        /// <summary>피해 % (기본 100).</summary>
        DamagePercent = 0,
        /// <summary>쿨다운 % (기본 100).</summary>
        CooldownPercent = 1,
        RangePercent = 2,
        /// <summary>시전 속도 % (기본 100, 높을수록 빠름).</summary>
        CastSpeedPercent = 3,
        StaggerPercent = 4,
        DestructionAdd = 5,
        CritRateAdd = 6,
        CritDamageAdd = 7,
        ManaCostPercent = 8,
        BackAttackAdd = 9,
        HeadAttackAdd = 10,
        /// <summary>추가 타격 횟수 (마지막 타격을 반복).</summary>
        ExtraHits = 11,
        /// <summary>보석(멸화 대응) 피해 % — 다른 피해 증가와 곱연산 (기본 0).</summary>
        GemDamagePercent = 12,
        /// <summary>보석(홍염 대응) 쿨다운 감소 % (기본 0).</summary>
        GemCooldownPercent = 13,
        /// <summary>1 이면 슈퍼아머.</summary>
        SuperArmor = 14,
        /// <summary>1 이면 카운터 판정.</summary>
        Counter = 15,

        Count = 16,
    }
}
