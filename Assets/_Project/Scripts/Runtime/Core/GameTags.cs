using System;
using System.Collections.Generic;
using Framework.Ability;

namespace DemoLA.Core
{
    /// <summary>
    /// 게임 태그 (GameplayTagManager 에 int id 로 등록).
    /// 이름의 '_' 가 계층 구분자다. "State_Dead" 의 부모는 "State" 이며 RegisterAll 이 이름으로 부모를 찾아 연결한다.
    /// 새 태그는 부모 태그 뒤에 추가하기만 하면 된다 (값은 저장 데이터에 쓰이므로 기존 값은 바꾸지 않는다).
    /// </summary>
    public enum ETag
    {
        None = 0,

        State = 1,
        State_Dead = 2,
        State_Stunned = 3,
        State_Airborne = 4,
        State_Casting = 5,
        State_Invulnerable = 6,
        State_SuperArmor = 7,
        /// <summary>무력화(그로기). 보스가 스태거/카운터/구조물 충돌로 행동 불가인 상태.</summary>
        State_Groggy = 8,
        State_Silenced = 9,
        State_Feared = 10,
        State_Frozen = 11,
        State_Grabbed = 12,
        State_Dashing = 13,
        State_Falling = 14,
        /// <summary>넉백 면역 (경직 면역): 밀려나지 않는다. 띄우기 · 기절은 받는다.</summary>
        State_PushImmune = 15,

        Buff = 30,
        /// <summary>즉사 패턴 1회 방어 (방어 구슬, G2-02).</summary>
        Buff_DeathWard = 31,
        Buff_DamageReduction = 32,
        /// <summary>보스 갑옷 중첩 (G2-01).</summary>
        Buff_Armor = 33,
        Buff_SpeedUp = 34,
        Buff_Shield = 35,

        Debuff = 50,
        Debuff_Bleed = 51,
        /// <summary>구슬 획득 디버프 (G1-01). 정화로 해제.</summary>
        Debuff_OrbCurse = 52,
        Debuff_Darkness = 53,
        Debuff_Slow = 54,

        Boss = 70,
        /// <summary>카운터 가능 구간. 카운터 스킬이 적중하면 패턴이 끊긴다.</summary>
        Boss_CounterWindow = 71,
        /// <summary>무력화(스태거) 체크 진행 중.</summary>
        Boss_StaggerCheck = 72,
        Boss_Untargetable = 73,
        Boss_Channeling = 74,
        Boss_PatternBusy = 75,

        Ability = 90,
        Ability_Skill = 91,
        Ability_Skill_Counter = 92,
        Ability_Dodge = 93,
        Ability_BossPattern = 94,
        Ability_Awakening = 95,
        Ability_Identity = 96,
        Ability_BattleItem = 97,

        Team = 110,
        Team_Player = 111,
        Team_Enemy = 112,

        Event = 130,
        Event_Hit = 131,
        Event_Countered = 132,
        Event_StaggerBroken = 133,
        Event_Died = 134,
        Event_PatternEnded = 135,
        Event_GimmickSucceeded = 136,
        Event_GimmickFailed = 137,
        Event_StructureCollision = 138,
        Event_ArmorStackRemoved = 139,
    }

    public static class GameTags
    {
        private static bool Registered;

        /// <summary>모든 ETag 를 이름 기반 계층으로 등록한다. 여러 번 불러도 한 번만 등록한다.</summary>
        public static void RegisterAll()
        {
            if (Registered == true)
            {
                return;
            }

            Registered = true;
            var byName = new Dictionary<string, int>();
            foreach (ETag tag in Enum.GetValues(typeof(ETag)))
            {
                byName[tag.ToString()] = (int)tag;
            }

            foreach (var pair in byName)
            {
                if (pair.Value == (int)ETag.None)
                {
                    continue;
                }

                int parent = GameplayTagManager.InvalidTag;
                int split = pair.Key.LastIndexOf('_');
                if (split > 0)
                {
                    int found;
                    if (byName.TryGetValue(pair.Key.Substring(0, split), out found) == true)
                    {
                        parent = found;
                    }
                }

                GameplayTagManager.SetParent(pair.Value, parent);
            }
        }

        /// <summary>테스트에서 GameplayTagManager.Clear 를 부른 뒤 다시 등록할 때.</summary>
        public static void ForceReregister()
        {
            Registered = false;
            RegisterAll();
        }

        public static GameplayTagContainer Container(params ETag[] InTags)
        {
            var container = new GameplayTagContainer();
            if (InTags == null)
            {
                return container;
            }

            for (int i = 0; i < InTags.Length; ++i)
            {
                if (InTags[i] != ETag.None)
                {
                    container.Add((int)InTags[i]);
                }
            }

            return container;
        }

        /// <summary>행동 불가 상태 (스킬/이동 차단에 공통으로 쓴다).</summary>
        public static readonly ETag[] IncapacitatedTags =
        {
            ETag.State_Dead, ETag.State_Stunned, ETag.State_Airborne, ETag.State_Frozen,
            ETag.State_Feared, ETag.State_Grabbed, ETag.State_Groggy, ETag.State_Falling,
        };
    }
}
