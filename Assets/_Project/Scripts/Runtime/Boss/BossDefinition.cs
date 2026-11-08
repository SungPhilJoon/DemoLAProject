using System;
using System.Collections.Generic;
using Framework.StateTree;
using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>xlsx 페이즈 시트 한 행.</summary>
    [Serializable]
    public class BossPhaseDefinition
    {
        public string PhaseId = "G1-P0";
        public string DisplayName = "Phase";
        [TextArea] public string EnterConditionText;
        [Tooltip("이 체력 줄 이하가 되면 진입 (-1 = 전투 시작 / 기믹 결과로만 진입)")]
        public float EnterAtHpLine = -1.0f;
        public List<string> PatternIds = new List<string>();
        [Tooltip("진입 시 체력 줄을 다시 채운다 (유령 구간: 40줄) — 0 이면 유지")]
        public int ResetHpLines;
        public float TransitionTime;
        public bool InvulnerableDuringTransition;
    }

    public enum EGimmickKind
    {
        OrbAlternation = 0,
        DeathWardOrbs = 1,
        TerrainCollapseHalf = 2,
        TerrainCollapseCenter = 3,
        ArmorStack = 4,
    }

    /// <summary>스폰 시 보스에 붙일 기믹 컴포넌트 (프리팹을 쓰면 직접 붙여도 된다).</summary>
    [Serializable]
    public class GimmickSpec
    {
        public string GimmickId = "G1-01";
        public EGimmickKind Kind;
    }

    [Serializable]
    public class ElementWeakness
    {
        public DemoLA.Combat.EElement Element = DemoLA.Combat.EElement.Holy;
        [Tooltip("받는 피해 증가 % (군단장 레이드: 성속성에 약함)")]
        public float DamagePercent = 15.0f;
    }

    /// <summary>
    /// 어그로 규칙. RetargetInterval 마다 대상을 다시 고른다: NearestChance 확률로 가장 가까운 대상, 나머지는 무작위.
    /// 도발(Taunt)은 이 규칙보다 우선. 위협 수치는 초당 ThreatDecayPerSecond 비율로 줄어든다.
    /// </summary>
    [Serializable]
    public class AggroSettings
    {
        public float RetargetInterval = 6.0f;
        [Range(0.0f, 1.0f)] public float NearestChance = 0.7f;
        [Range(0.0f, 1.0f)] public float ThreatDecayPerSecond = 0.05f;
    }

    /// <summary>
    /// 보스 1마리 (관문 단위). AI 흐름은 Brain(StateTree) 에서, 패턴 내용은 Patterns 에서 편집한다.
    /// 체력 줄 수 · 줄당 체력은 문서에 없어서 자리 표시 값이다.
    /// </summary>
    [CreateAssetMenu(menuName = "DemoLA/Boss/Boss Definition", fileName = "BOSS_New")]
    public class BossDefinition : ScriptableObject
    {
        public string BossId = "Gate1";
        public string DisplayName = "Boss";
        public int TotalHpLines = 160;
        public float HpPerLine = 2000000.0f;
        public float AttackPower = 50000.0f;
        public float Defense = 30000.0f;
        public float Radius = 1.6f;
        public float MoveSpeedPercent = 90.0f;
        public Color BodyColor = new Color(0.45f, 0.1f, 0.12f);
        public float DefaultGroggyTime = 3.0f;
        [Tooltip("패턴이 끝난 뒤 다음 패턴을 시작하기까지 최소 간격 (초). 그동안 보스는 대상을 쫓아 걷는다")]
        public float PatternGap = 2.5f;
        [Tooltip("전투 시작 후 첫 패턴까지 대기 (초)")]
        public float OpeningDelay = 3.0f;
        public StateTreeAsset Brain;
        public List<BossPhaseDefinition> Phases = new List<BossPhaseDefinition>();
        public List<BossPatternDefinition> Patterns = new List<BossPatternDefinition>();
        public List<GimmickSpec> Gimmicks = new List<GimmickSpec>();
        public List<ElementWeakness> Weaknesses = new List<ElementWeakness>();
        public AggroSettings Aggro = new AggroSettings();
        [Tooltip("원본 기믹 기준 인원 (xlsx = 8인). 실제 인원과의 비율로 체력/무력화/구슬 수를 맞춘다")]
        public int DesignPartySize = 8;

        public float ElementMultiplier(DemoLA.Combat.EElement InElement)
        {
            for (int i = 0; i < Weaknesses.Count; ++i)
            {
                if (Weaknesses[i].Element == InElement && InElement != DemoLA.Combat.EElement.None)
                {
                    return 1.0f + Weaknesses[i].DamagePercent * 0.01f;
                }
            }

            return 1.0f;
        }

        public BossPatternDefinition GetPattern(string InPatternId)
        {
            for (int i = 0; i < Patterns.Count; ++i)
            {
                if (Patterns[i] != null && Patterns[i].PatternId == InPatternId)
                {
                    return Patterns[i];
                }
            }

            return null;
        }

        public BossPhaseDefinition GetPhase(string InPhaseId)
        {
            for (int i = 0; i < Phases.Count; ++i)
            {
                if (Phases[i].PhaseId == InPhaseId)
                {
                    return Phases[i];
                }
            }

            return null;
        }
    }
}
