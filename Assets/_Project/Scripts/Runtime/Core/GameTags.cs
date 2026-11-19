using System;
using System.Collections.Generic;
using Framework.Ability;

namespace DemoLA.Core
{
    /// <summary>
    /// 게임 태그 (GameplayTagManager 에 int id 로 등록).
    /// 이름의 '_' 가 계층 구분자다. "State_Dead" 의 부모는 "State" 이며 RegisterAll 이 이름으로 부모를 찾아 연결한다.
    /// 새 태그는 부모 태그 뒤에 추가하기만 하면 된다 (값은 저장 데이터에 쓰이므로 기존 값은 바꾸지 않는다).
    /// [GameplayTagEnum] → 프레임워크 GAS Tool 의 GameplayTag 탭 · [GameplayTag] 드롭다운에 나타난다.
    /// </summary>
    [GameplayTagEnum('_')]
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
        /// <summary>즉사 패턴 1회 방어 (방어 구슬, Pattern.G2.WardOrb).</summary>
        Buff_DeathWard = 31,
        Buff_DamageReduction = 32,
        /// <summary>보스 갑옷 중첩 (Pattern.G2.ArmorBreak).</summary>
        Buff_Armor = 33,
        Buff_SpeedUp = 34,
        Buff_Shield = 35,

        Debuff = 50,
        Debuff_Bleed = 51,
        /// <summary>구슬 획득 디버프 (Pattern.G1.OrbStagger). 정화로 해제.</summary>
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

        // ------------------------------------------------------------------ 보스 패턴 (패턴 ID = 태그)
        // 표기: Pattern.G1.OrbStagger (GameTags.ToTagString). 패턴 · 기믹 · StateTree · 대기열 모두 이 값(int)으로 비교한다.
        // 새 패턴: 관문 블록 끝에 이어 붙인다 (G1 = 211~249, G2 = 251~299). 값은 에셋에 저장되므로 기존 값은 바꾸지 않는다.

        Pattern = 200,
        Pattern_G1 = 201,
        Pattern_G2 = 202,

        /// <summary>구슬 획득 후 무력화 (기믹).</summary>
        Pattern_G1_OrbStagger = 211,
        /// <summary>반대 색 보스 난입.</summary>
        Pattern_G1_RivalIntrusion = 212,
        /// <summary>추적 회오리.</summary>
        Pattern_G1_ChasingWhirlwind = 213,
        /// <summary>공포 후 후방 처형 (스태거 체크).</summary>
        Pattern_G1_FearExecution = 214,
        /// <summary>보스 분열 (페이즈 전환).</summary>
        Pattern_G1_Split = 215,
        /// <summary>점멸 후 2연 할퀴기.</summary>
        Pattern_G1_BlinkDoubleClaw = 216,
        /// <summary>3단 할퀴기.</summary>
        Pattern_G1_TripleClaw = 217,
        /// <summary>부채꼴 마구 할퀴기.</summary>
        Pattern_G1_FanFrenzyClaw = 218,
        /// <summary>출혈 스택과 추가 할퀴기.</summary>
        Pattern_G1_BleedClaw = 219,
        /// <summary>도약 후 2연 할퀴기.</summary>
        Pattern_G1_LeapDoubleClaw = 220,
        /// <summary>회전차기.</summary>
        Pattern_G1_SpinKick = 221,
        /// <summary>웅크린 뒤 바닥 폭발.</summary>
        Pattern_G1_CrouchBurst = 222,
        /// <summary>경로 예고 돌진 (소닉).</summary>
        Pattern_G1_SonicCharge = 223,
        /// <summary>직선 붙잡기.</summary>
        Pattern_G1_LineGrab = 224,
        /// <summary>칼날 날리기.</summary>
        Pattern_G1_BladeThrow = 225,
        /// <summary>회전 베기와 기운 색 징조.</summary>
        Pattern_G1_SpinSlashOmen = 226,
        /// <summary>중력지대 (대상 디버프 후 어둠 장판).</summary>
        Pattern_G1_GravityZone = 227,
        /// <summary>동결 덫.</summary>
        Pattern_G1_FreezeTrap = 228,
        /// <summary>폭발 덫.</summary>
        Pattern_G1_ExplosiveTrap = 229,
        /// <summary>기폭 투사체.</summary>
        Pattern_G1_DetonateProjectile = 230,
        /// <summary>무작위 띄우기 장판.</summary>
        Pattern_G1_RandomLaunchField = 231,
        /// <summary>무력화 실패 시 연쇄 공격.</summary>
        Pattern_G1_StaggerFailChain = 232,
        /// <summary>안개 패턴과 감금 잡기.</summary>
        Pattern_G1_FogPrisonGrab = 233,

        /// <summary>갑옷 부위파괴 (돌진 유도).</summary>
        Pattern_G2_ArmorBreak = 251,
        /// <summary>방어 구슬 획득.</summary>
        Pattern_G2_WardOrb = 252,
        /// <summary>130줄 즉사 장판 2연속.</summary>
        Pattern_G2_LethalFieldDouble = 253,
        /// <summary>무작위 대상 추적 즉사 장판.</summary>
        Pattern_G2_ChasingLethalField = 254,
        /// <summary>지형 파괴 1차 (한쪽 절반).</summary>
        Pattern_G2_TerrainBreakHalf = 255,
        /// <summary>지형 파괴 2차 (중앙만 남김).</summary>
        Pattern_G2_TerrainBreakCenter = 256,
        /// <summary>카운터 필수 돌진.</summary>
        Pattern_G2_CounterCharge = 257,
        /// <summary>3연 카운터.</summary>
        Pattern_G2_TripleCounter = 258,
        /// <summary>발밑 장판 연속 타격.</summary>
        Pattern_G2_FootFieldBarrage = 259,
        /// <summary>분신 카운터 (버프 해제).</summary>
        Pattern_G2_CloneCounter = 260,
        /// <summary>돌진 (경로 표시).</summary>
        Pattern_G2_Charge = 261,
        /// <summary>점프 후 T자 쐐기와 소용돌이.</summary>
        Pattern_G2_LeapWedgeVortex = 262,
        /// <summary>2회 휘두르기 후 내려치기 · 폭발.</summary>
        Pattern_G2_SwingSlamBurst = 263,
        /// <summary>점프 후 도끼 연속 낙하.</summary>
        Pattern_G2_LeapAxeRain = 264,
        /// <summary>안/밖 범위 선택.</summary>
        Pattern_G2_InOutField = 265,
        /// <summary>대상 추적 레이저 (부채꼴).</summary>
        Pattern_G2_TrackingLaser = 266,
        /// <summary>빔 추적과 돌 연쇄 폭발.</summary>
        Pattern_G2_BeamRockChain = 267,
        /// <summary>추적 장판 이후 강력한 일격.</summary>
        Pattern_G2_ChaseFieldSmash = 268,
        /// <summary>구체 생성 후 무력화 (스태거 체크).</summary>
        Pattern_G2_SphereStagger = 269,
        /// <summary>유령 구간 잡기.</summary>
        Pattern_G2_GhostGrab = 270,
        /// <summary>휠윈드 (외곽 벽 파괴).</summary>
        Pattern_G2_Whirlwind = 271,
        /// <summary>3연 베기 (외곽 벽 파괴).</summary>
        Pattern_G2_TripleSlash = 272,
        /// <summary>도약 강타 → 추적 휠윈드.</summary>
        Pattern_G2_LeapSmashWhirlwind = 273,
        /// <summary>도끼 앞뒤 찍기 → 카운터 강타.</summary>
        Pattern_G2_AxeChopCounter = 274,
        /// <summary>부위파괴 반격 — 천상 낙하.</summary>
        Pattern_G2_Skyfall = 275,
        /// <summary>재도약 (135줄 이후).</summary>
        Pattern_G2_ReLeap = 276,
        /// <summary>기둥 엄폐 낙하 (135줄 이후).</summary>
        Pattern_G2_PillarCoverDrop = 277,
    }

    public static class GameTags
    {
        /// <summary>
        /// 모든 ETag 를 이름 기반 계층으로 프레임워크 GameplayTagRegistry 에 등록한다 (부모 = 마지막 '_' 앞 이름, 표시 이름 "State.Dead").
        /// 이미 등록돼 있으면 아무것도 하지 않고, GameplayTagManager.Clear 뒤에는 다시 등록한다.
        /// </summary>
        public static void RegisterAll()
        {
            GameplayTagRegistry.RegisterEnum<ETag>('_');
        }

        /// <summary>테스트에서 GameplayTagManager.Clear 를 부른 뒤 다시 등록할 때 (Clear 가 이름 표도 비우므로 RegisterAll 과 같다).</summary>
        public static void ForceReregister()
        {
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

        /// <summary>InTag 가 InQuery 이거나 그 하위 태그인가 (예: Pattern_G1_Split 은 Pattern_G1 에 일치). 정수 비교만 한다.</summary>
        public static bool Matches(ETag InTag, ETag InQuery)
        {
            RegisterAll();
            return InTag != ETag.None && GameplayTagManager.MatchesTag((int)InTag, (int)InQuery);
        }

        /// <summary>보스 패턴 태그인가 (Pattern 하위의 실제 패턴 — 관문 묶음 태그 Pattern_G1 같은 것은 제외).</summary>
        public static bool IsPattern(ETag InTag)
        {
            return InTag != ETag.Pattern && InTag != ETag.Pattern_G1 && InTag != ETag.Pattern_G2 && Matches(InTag, ETag.Pattern) == true;
        }

        /// <summary>패턴의 관문 태그 (Pattern_G1 / Pattern_G2). 패턴이 아니면 None.</summary>
        public static ETag PatternGate(ETag InTag)
        {
            if (Matches(InTag, ETag.Pattern_G1) == true)
            {
                return ETag.Pattern_G1;
            }

            return Matches(InTag, ETag.Pattern_G2) == true ? ETag.Pattern_G2 : ETag.None;
        }

        /// <summary>패턴 에셋 이름 꼬리 ("G1_OrbStagger" → 에셋 PT_G1_OrbStagger).</summary>
        public static string PatternAssetName(ETag InTag)
        {
            string text = ToTagString(InTag);
            return text.StartsWith("Pattern.", StringComparison.Ordinal) == true ? text.Substring("Pattern.".Length).Replace('.', '_') : text;
        }

        /// <summary>패턴 태그가 속한 보스 ID ("Gate1" / "Gate2"). 패턴이 아니면 null.</summary>
        public static string PatternBossId(ETag InTag)
        {
            switch (PatternGate(InTag))
            {
                case ETag.Pattern_G1: return "Gate1";
                case ETag.Pattern_G2: return "Gate2";
            }

            return null;
        }

        private static Dictionary<ETag, string> tagStrings;
        private static Dictionary<string, ETag> tagsByString;

        /// <summary>GameplayTag 표기 ("Pattern.G1.OrbStagger"). 로그 · UI · 에디터 표시용 — 비교에는 쓰지 않는다.</summary>
        public static string ToTagString(ETag InTag)
        {
            buildStringTables();
            string text;
            return tagStrings.TryGetValue(InTag, out text) == true ? text : ((int)InTag).ToString();
        }

        /// <summary>
        /// "Pattern.G1.OrbStagger" 또는 "Pattern_G1_OrbStagger" → ETag. 데이터 가져오기(CSV · 에디터 입력) 때만 쓴다.
        /// 런타임 비교는 항상 ETag 값으로 한다.
        /// </summary>
        public static bool TryParse(string InText, out ETag OutTag)
        {
            OutTag = ETag.None;
            if (string.IsNullOrEmpty(InText) == true)
            {
                return false;
            }

            buildStringTables();
            return tagsByString.TryGetValue(InText.Trim().Replace('_', '.'), out OutTag);
        }

        /// <summary>'|' 로 구분된 태그 목록을 읽는다. 읽지 못한 이름은 OutUnknown 에 담는다.</summary>
        public static List<ETag> ParseList(string[] InTexts, List<string> OutUnknown = null)
        {
            var list = new List<ETag>();
            if (InTexts == null)
            {
                return list;
            }

            for (int i = 0; i < InTexts.Length; ++i)
            {
                ETag tag;
                if (TryParse(InTexts[i], out tag) == true)
                {
                    list.Add(tag);
                }
                else if (string.IsNullOrWhiteSpace(InTexts[i]) == false && OutUnknown != null)
                {
                    OutUnknown.Add(InTexts[i]);
                }
            }

            return list;
        }

        private static void buildStringTables()
        {
            if (tagStrings != null)
            {
                return;
            }

            var strings = new Dictionary<ETag, string>();
            var lookup = new Dictionary<string, ETag>();
            foreach (ETag tag in Enum.GetValues(typeof(ETag)))
            {
                string text = tag.ToString().Replace('_', '.');
                strings[tag] = text;
                lookup[text] = tag;
            }

            tagsByString = lookup;
            tagStrings = strings;
        }

        /// <summary>행동 불가 상태 (스킬/이동 차단에 공통으로 쓴다).</summary>
        public static readonly ETag[] IncapacitatedTags =
        {
            ETag.State_Dead, ETag.State_Stunned, ETag.State_Airborne, ETag.State_Frozen,
            ETag.State_Feared, ETag.State_Grabbed, ETag.State_Groggy, ETag.State_Falling,
        };
    }
}
