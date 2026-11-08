using DemoLA.Attributes;
using DemoLA.Combat;
using Framework.Ability;

namespace DemoLA.Build
{
    /// <summary>툴팁 · 요약에 쓰는 한글 이름 (enum 이름이 그대로 노출되지 않게 한다).</summary>
    public static class KoreanText
    {
        public static string Attr(EAttr InAttr)
        {
            switch (InAttr)
            {
                case EAttr.Health: return "생명력";
                case EAttr.MaxHealth: return "최대 생명력";
                case EAttr.Mana: return "마나";
                case EAttr.MaxMana: return "최대 마나";
                case EAttr.Shield: return "보호막";
                case EAttr.AttackPower: return "공격력";
                case EAttr.Defense: return "방어력";
                case EAttr.CritRate: return "치명타 적중률";
                case EAttr.CritDamage: return "치명타 피해";
                case EAttr.StatCrit: return "치명";
                case EAttr.StatSpecialization: return "특화";
                case EAttr.StatSwiftness: return "신속";
                case EAttr.StatDomination: return "제압";
                case EAttr.StatEndurance: return "인내";
                case EAttr.StatExpertise: return "숙련";
                case EAttr.AttackSpeed: return "공격 속도";
                case EAttr.MoveSpeed: return "이동 속도";
                case EAttr.CooldownReduction: return "재사용 대기시간 감소";
                case EAttr.DamageIncrease: return "주는 피해";
                case EAttr.IndependentDamage: return "주는 피해 (별도 곱연산)";
                case EAttr.BackAttackDamage: return "백어택 피해";
                case EAttr.HeadAttackDamage: return "헤드어택 피해";
                case EAttr.IncomingDamageReduction: return "받는 피해 감소";
                case EAttr.IncomingDamageAmplify: return "받는 피해 증가";
                case EAttr.StaggerPower: return "무력화";
                case EAttr.DestructionPower: return "부위 파괴";
                case EAttr.OutgoingHealing: return "치유량";
                case EAttr.IdentityGauge: return "아이덴티티 게이지";
                case EAttr.MaxIdentityGauge: return "최대 아이덴티티 게이지";
                case EAttr.MainStat: return "주 능력치";
                case EAttr.WeaponPower: return "무기 공격력";
                case EAttr.AttackPowerPercent: return "공격력 %";
                default: return "능력치";
            }
        }

        /// <summary>퍼센트 단위로 읽는 속성인가 (수치 뒤에 % 를 붙인다).</summary>
        public static bool IsPercent(EAttr InAttr)
        {
            switch (InAttr)
            {
                case EAttr.CritRate:
                case EAttr.CritDamage:
                case EAttr.AttackSpeed:
                case EAttr.MoveSpeed:
                case EAttr.CooldownReduction:
                case EAttr.DamageIncrease:
                case EAttr.IndependentDamage:
                case EAttr.BackAttackDamage:
                case EAttr.HeadAttackDamage:
                case EAttr.IncomingDamageReduction:
                case EAttr.IncomingDamageAmplify:
                case EAttr.StaggerPower:
                case EAttr.OutgoingHealing:
                case EAttr.AttackPowerPercent:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>"치명타 적중률 +15%" 같은 한 줄.</summary>
        public static string Modifier(EAttr InAttr, EModifierOp InOp, float InValue)
        {
            string name = Attr(InAttr);
            string sign = InValue >= 0.0f ? "+" : string.Empty;
            switch (InOp)
            {
                case EModifierOp.MultiplyPercent:
                    return $"{name} {sign}{InValue:0.##}%";
                case EModifierOp.Override:
                    return $"{name} {InValue:0.##} 으로 고정";
                default:
                    return $"{name} {sign}{InValue:0.##}{(IsPercent(InAttr) ? "%" : string.Empty)}";
            }
        }

        public static string Condition(EDamageCondition InCondition, float InParam)
        {
            switch (InCondition)
            {
                case EDamageCondition.Always: return "항상";
                case EDamageCondition.TargetIsBoss: return "보스에게";
                case EDamageCondition.BackAttack: return "백어택 적중 시";
                case EDamageCondition.HeadAttack: return "헤드어택 적중 시";
                case EDamageCondition.SelfHealthAbove: return $"내 생명력이 {InParam:0}% 이상일 때";
                case EDamageCondition.SelfHealthBelow: return $"내 생명력이 {InParam:0}% 이하일 때";
                case EDamageCondition.TargetHealthBelow: return $"대상 생명력이 {InParam:0}% 이하일 때";
                case EDamageCondition.SelfShielded: return "보호막이 있을 때";
                case EDamageCondition.TargetGroggy: return "무력화 상태 대상에게";
                case EDamageCondition.MoveSpeedScaled: return "이동 속도 100% 초과분에 비례해";
                case EDamageCondition.TargetWithin: return $"{InParam:0.#}m 이내 대상에게";
                case EDamageCondition.TargetBeyond: return $"{InParam:0.#}m 밖 대상에게";
                case EDamageCondition.AwakeningSkill: return "각성기로";
                case EDamageCondition.IdentitySkill: return "아이덴티티 기술로";
                default: return "특정 조건에서";
            }
        }

        public static string Group(EDamageGroup InGroup)
        {
            switch (InGroup)
            {
                case EDamageGroup.Independent: return "주는 피해 (별도 곱연산)";
                case EDamageGroup.Additive: return "주는 피해";
                case EDamageGroup.CritRate: return "치명타 적중률";
                case EDamageGroup.CritDamage: return "치명타 피해";
                case EDamageGroup.IncomingReduction: return "받는 피해 감소";
                default: return "피해";
            }
        }

        public static string Element(EElement InElement)
        {
            switch (InElement)
            {
                case EElement.Fire: return "화속성";
                case EElement.Ice: return "수속성(얼음)";
                case EElement.Lightning: return "뇌속성";
                case EElement.Earth: return "토속성";
                case EElement.Water: return "수속성";
                case EElement.Dark: return "암속성";
                case EElement.None: return "무속성";
                default: return "성속성";
            }
        }

        public static string Stat(ECombatStat InStat)
        {
            switch (InStat)
            {
                case ECombatStat.Crit: return "치명";
                case ECombatStat.Specialization: return "특화";
                case ECombatStat.Swiftness: return "신속";
                case ECombatStat.Domination: return "제압";
                case ECombatStat.Endurance: return "인내";
                case ECombatStat.Expertise: return "숙련";
                default: return "없음";
            }
        }
    }
}
