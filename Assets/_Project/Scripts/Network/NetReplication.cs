using DemoLA.Attributes;
using DemoLA.Combat;
using DemoLA.Core;

namespace DemoLA.Network
{
    /// <summary>
    /// 서버 권한 복제 규칙 (현재 프레임워크 GAS 0.2 와 함께 쓰는 게임 쪽 구조).
    ///  - 서버만 GAS 를 진행한다 (어빌리티 실행 · 이펙트 적용 · 피해 계산 · AI).
    ///  - 클라이언트는 같은 액터를 만들되 어빌리티를 실행하지 않고, 서버가 보낸 속성 값과 상태 태그만 반영한다.
    ///  - 클라이언트 입력은 서버 RPC (NetworkPartyCommands) 로 보낸다.
    /// 프레임워크 0.3 의 IAbilitySystemNetwork 훅으로 옮기면 이 클래스의 역할(태그/속성 반영)을 ASC 가 직접 한다.
    /// </summary>
    public static class NetReplication
    {
        /// <summary>복제하는 상태 태그 (비트 순서 = 배열 순서).</summary>
        public static readonly ETag[] ReplicatedTags =
        {
            ETag.State_Dead, ETag.State_Groggy, ETag.Boss_CounterWindow, ETag.Boss_StaggerCheck, ETag.Buff_Armor,
            ETag.State_Invulnerable, ETag.State_Casting, ETag.Buff_DeathWard, ETag.Boss_Untargetable, ETag.Boss_PatternBusy,
            ETag.Boss_Channeling, ETag.State_Airborne, ETag.State_Frozen, ETag.State_Stunned, ETag.State_SuperArmor,
        };

        public static int CaptureTags(CombatActor InActor)
        {
            int flags = 0;
            for (int i = 0; i < ReplicatedTags.Length; ++i)
            {
                if (InActor.Asc.Tags.HasTagExact((int)ReplicatedTags[i]) == true || InActor.HasTag((int)ReplicatedTags[i]) == true)
                {
                    flags |= 1 << i;
                }
            }

            return flags;
        }

        /// <summary>클라이언트: 복제된 비트와 현재 태그를 비교해 추가/제거한다.</summary>
        public static void ApplyTags(CombatActor InActor, int InPrevious, int InCurrent)
        {
            for (int i = 0; i < ReplicatedTags.Length; ++i)
            {
                bool before = (InPrevious & (1 << i)) != 0;
                bool after = (InCurrent & (1 << i)) != 0;
                if (before == after)
                {
                    continue;
                }

                if (after == true)
                {
                    InActor.AddTag((int)ReplicatedTags[i]);
                }
                else
                {
                    InActor.RemoveTag((int)ReplicatedTags[i]);
                }
            }
        }

        /// <summary>클라이언트: 서버 값으로 Base 를 덮는다 (최대값을 먼저).</summary>
        public static void ApplyVitals(CombatAttributeSet InStats, float InMaxHealth, float InHealth, float InMaxMana, float InMana, float InShield, float InIdentity)
        {
            InStats.Set(EAttr.MaxHealth, InMaxHealth);
            InStats.Set(EAttr.Health, InHealth);
            InStats.Set(EAttr.MaxMana, InMaxMana);
            InStats.Set(EAttr.Mana, InMana);
            InStats.Set(EAttr.Shield, InShield);
            InStats.Set(EAttr.IdentityGauge, InIdentity);
        }
    }
}
