using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Core;
using DemoLA.Effects;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Build
{
    /// <summary>
    /// 각인별 "발동형" 효과 (CSV 의 상시 수치 · 조건부 피해 위에 더해진다). 각인 번호(EngravingRow.Index) × 레벨(1~3).
    /// 수치는 이 프로젝트의 자리 표시 값이다.
    /// </summary>
    public static class EngravingProcs
    {
        public static List<ProcDefinition> Get(int InEngravingIndex, int InLevel)
        {
            var list = new List<ProcDefinition>();
            int l = Mathf.Clamp(InLevel, 1, 3) - 1;
            switch (InEngravingIndex)
            {
                case 1: // 정밀 타격
                    list.Add(ProcDefinition.Buff("정밀 조준", EProcTrigger.OnCrit, 6.0f, 5,
                        mod(EAttr.CritDamage, pick(l, 2.0f, 3.0f, 4.0f))));
                    break;
                case 2: // 약점 공략
                    list.Add(debuff("약점 노출", EProcTrigger.OnHit, 6.0f, 3, mod(EAttr.IncomingDamageAmplify, pick(l, 1.5f, 2.5f, 3.5f)))
                        .WithChance(0.3f, 2.0f));
                    break;
                case 3: // 그림자 일격
                    list.Add(ProcDefinition.Buff("그림자", EProcTrigger.OnBackAttack, 8.0f, 5, mod(EAttr.CritRate, pick(l, 1.0f, 1.5f, 2.0f)))
                        .WithBurst(5, "그림자 습격", EProcAction.Damage, pick(l, 3.0f, 5.0f, 8.0f)));
                    break;
                case 4: // 치명 강화
                    list.Add(ProcDefinition.Buff("예리함", EProcTrigger.OnCrit, 4.0f, 1, mod(EAttr.AttackSpeed, pick(l, 5.0f, 8.0f, 12.0f)))
                        .WithChance(0.25f, 6.0f));
                    break;
                case 5: // 가속
                    list.Add(ProcDefinition.Buff("질풍", EProcTrigger.OnDodge, 3.0f, 1,
                        mod(EAttr.MoveSpeed, pick(l, 10.0f, 15.0f, 20.0f)), mod(EAttr.AttackSpeed, pick(l, 4.0f, 6.0f, 8.0f))));
                    break;
                case 6: // 강철 피부
                    list.Add(action("강철 장막", EProcTrigger.OnDamaged, EProcAction.Shield, pick(l, 5.0f, 8.0f, 12.0f)).WithChance(1.0f, 15.0f));
                    break;
                case 7: // 흐르는 마나
                    list.Add(action("마나 순환", EProcTrigger.OnSkillUse, EProcAction.ReduceCooldowns, pick(l, 1.0f, 1.5f, 2.0f)).WithChance(0.15f, 4.0f));
                    break;
                case 8: // 축복의 손길
                    list.Add(action("축복", EProcTrigger.Periodic, EProcAction.PartyHeal, pick(l, 1.0f, 2.0f, 3.0f)).WithParam(5.0f));
                    break;
                case 9: // 단단한 의지
                    list.Add(ProcDefinition.Buff("불굴", EProcTrigger.OnDamaged, 6.0f, 3, mod(EAttr.DamageIncrease, pick(l, 2.0f, 3.0f, 5.0f)))
                        .WithChance(1.0f, 1.0f));
                    break;
                case 10: // 부위 파괴 전문가
                    list.Add(debuff("균열", EProcTrigger.OnHeadAttack, 6.0f, 1, mod(EAttr.IncomingDamageAmplify, pick(l, 2.0f, 3.0f, 5.0f))));
                    break;
                case 11: // 결의
                    list.Add(action("최후의 결의", EProcTrigger.OnHealthBelow, EProcAction.Heal, pick(l, 10.0f, 15.0f, 20.0f)).WithParam(50.0f).WithChance(1.0f, 30.0f));
                    break;
                case 12: // 역경
                    list.Add(ProcDefinition.Buff("역경 극복", EProcTrigger.OnHealthBelow, 8.0f, 1, mod(EAttr.DamageIncrease, pick(l, 10.0f, 15.0f, 20.0f)))
                        .WithParam(30.0f).WithChance(1.0f, 40.0f));
                    break;
                case 13: // 방패 연계
                    list.Add(action("방패 연계", EProcTrigger.OnLongCooldownSkill, EProcAction.Shield, pick(l, 2.0f, 3.0f, 5.0f)).WithParam(10.0f));
                    break;
                case 14: // 질주 본능
                    list.Add(action("질주 본능", EProcTrigger.OnDodge, EProcAction.IdentityGauge, pick(l, 5.0f, 10.0f, 15.0f)).WithChance(1.0f, 3.0f));
                    break;
                case 15: // 근접 집중
                    list.Add(ProcDefinition.Buff("밀착", EProcTrigger.OnHit, 6.0f, 5, mod(EAttr.DamageIncrease, pick(l, 1.0f, 1.5f, 2.0f)))
                        .WithChance(1.0f, 0.5f));
                    break;
                case 16: // 원거리 집중
                    list.Add(ProcDefinition.Buff("집중", EProcTrigger.OnLongCooldownSkill, 4.0f, 1, mod(EAttr.CritRate, pick(l, 4.0f, 6.0f, 8.0f)))
                        .WithParam(8.0f));
                    break;
                case 17: // 일격 집중
                    list.Add(ProcDefinition.Buff("일격", EProcTrigger.OnLongCooldownSkill, 3.0f, 1, mod(EAttr.DamageIncrease, pick(l, 6.0f, 10.0f, 15.0f)))
                        .WithParam(10.0f));
                    break;
                case 18: // 무력화 응징
                    list.Add(debuff("응징", EProcTrigger.OnHit, 6.0f, 3, mod(EAttr.IncomingDamageAmplify, pick(l, 1.0f, 2.0f, 3.0f)))
                        .WithChance(0.1f, 1.0f));
                    break;
                case 101: // 정령 공명
                case 102: // 정령 집중
                    list.Add(action("정령 교감", EProcTrigger.OnIdentity, EProcAction.ReduceCooldowns, pick(l, 1.0f, 2.0f, 3.0f)));
                    break;
                case 111: // 충격 수련
                case 112: // 투지 해방
                    list.Add(ProcDefinition.Buff("투지", EProcTrigger.OnIdentity, 8.0f, 1, mod(EAttr.CritRate, pick(l, 5.0f, 8.0f, 10.0f))));
                    break;
                case 121: // 광기
                case 122: // 처단
                    list.Add(ProcDefinition.Buff("피의 갈망", EProcTrigger.OnHit, 5.0f, 10, mod(EAttr.AttackSpeed, pick(l, 0.5f, 0.8f, 1.2f)))
                        .WithChance(1.0f, 0.3f).WithBurst(10, "핏빛 폭발", EProcAction.Damage, pick(l, 2.0f, 3.5f, 5.0f)));
                    break;
                case 131: // 만개
                case 132: // 여백
                    var bloom = ProcDefinition.Buff("화폭", EProcTrigger.OnIdentity, 8.0f, 1, mod(EAttr.DamageIncrease, pick(l, 3.0f, 5.0f, 7.0f)));
                    bloom.Action = EProcAction.PartyBuff;
                    list.Add(bloom);
                    break;
            }

            return list;
        }

        private static float pick(int InLevel, float InA, float InB, float InC)
        {
            return InLevel <= 0 ? InA : (InLevel == 1 ? InB : InC);
        }

        private static EffectModifierEntry mod(EAttr InAttr, float InValue)
        {
            return EffectModifierEntry.Char(InAttr, EModifierOp.Add, InValue);
        }

        private static ProcDefinition debuff(string InName, EProcTrigger InTrigger, float InDuration, int InMaxStacks, params EffectModifierEntry[] InModifiers)
        {
            var proc = ProcDefinition.Buff(InName, InTrigger, InDuration, InMaxStacks, InModifiers);
            proc.Action = EProcAction.TargetDebuff;
            return proc;
        }

        private static ProcDefinition action(string InName, EProcTrigger InTrigger, EProcAction InAction, float InValue)
        {
            return new ProcDefinition { Name = InName, Trigger = InTrigger, Action = InAction, Value = InValue };
        }
    }
}
