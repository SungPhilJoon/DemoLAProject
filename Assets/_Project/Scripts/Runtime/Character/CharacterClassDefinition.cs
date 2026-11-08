using System.Collections.Generic;
using DemoLA.Skills;
using UnityEngine;

namespace DemoLA.Character
{
    /// <summary>문서(프로젝트 개발 사항.md)의 4개 직업.</summary>
    public enum ECharacterClass
    {
        Summoner = 0,
        Infighter = 1,
        Slayer = 2,
        Artist = 3,
    }

    public enum ECombatRole
    {
        Dealer = 0,
        Support = 1,
    }

    /// <summary>
    /// 직업 정의. 스킬 목록과 기본 능력치만 가진다.
    /// 직업 고유 시스템(아이덴티티)의 세부 규칙은 미정이라 공용 게이지(IdentityGauge)만 쓴다 (구현 보류 사항.md).
    /// </summary>
    [CreateAssetMenu(menuName = "DemoLA/Character/Class Definition", fileName = "CLS_New")]
    public class CharacterClassDefinition : ScriptableObject
    {
        public ECharacterClass Class;
        public string DisplayName = "Class";
        public ECombatRole Role;
        public Color BodyColor = Color.white;

        [Header("Base Stats (장비 이전)")]
        public float BaseMaxHealth = 150000.0f;
        public float BaseMaxMana = 1000.0f;
        public float BaseDefense = 20000.0f;
        public float BaseMainStat = 50000.0f;
        public float BaseWeaponPower = 30000.0f;
        public float BaseCritDamage = 200.0f;

        [Header("Skills")]
        [Tooltip("스킬 바 순서 (Q W E R A S D F) + 각성기는 V")]
        public List<SkillDefinition> Skills = new List<SkillDefinition>();
        [Tooltip("각성기 2종 — 빌드에서 하나를 고른다 (CharacterBuildData.SelectedAwakening)")]
        public List<SkillDefinition> Awakenings = new List<SkillDefinition>();
        [Tooltip("회피기 재사용 대기시간 (직업마다 다름)")]
        public float DodgeCooldown = 8.0f;
        public float DodgeDistance = 5.0f;
        [Tooltip("회피 중 무적 시간")]
        public float DodgeInvulnerableTime = 0.3f;

        public SkillDefinition GetAwakening(int InIndex)
        {
            if (Awakenings.Count == 0)
            {
                return null;
            }

            return Awakenings[Mathf.Clamp(InIndex, 0, Awakenings.Count - 1)];
        }

        [Header("AI")]
        [Tooltip("파티 AI 가 유지하려는 보스와의 거리")]
        public float AiPreferredRange = 3.0f;
        [Tooltip("파티 AI 가 백어택 위치를 노리는가")]
        public bool AiPrefersBack;
    }
}
