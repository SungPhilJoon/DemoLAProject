using DemoLA.Character;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Skills;
using UnityEngine;

namespace DemoLA.Testing
{
    /// <summary>
    /// 시나리오 테스트 전용 딜 측정기 (게임에는 없다 — 게임의 파티는 실제 플레이어만).
    /// 밸런스 시나리오가 "4명이 60초 동안 때리면 몇 줄?" 을 재현할 수 있게, 회피 · 기믹 없이
    /// 가장 가까운 적 옆에 서서 장착 스킬을 순서대로 쓰고 아이덴티티가 차면 쓴다.
    /// </summary>
    public class ScenarioAutoAttacker : MonoBehaviour
    {
        public float SearchRadius = 60.0f;

        private PartyCharacter character;

        private void Awake()
        {
            character = GetComponent<PartyCharacter>();
        }

        private void Update()
        {
            if (character == null || character.Stats == null || character.IsDead == true || character.IsCasting == true)
            {
                return;
            }

            var target = CombatRegistry.FindNearestHostile(character, SearchRadius);
            if (target == null)
            {
                return;
            }

            float distance = Vector3.Distance(HitGeometry.Flat(target.Position), HitGeometry.Flat(character.Position)) - target.Radius;
            if (distance <= 6.0f && tryIdentity(target.Position) == true)
            {
                return;
            }

            var skill = pickSkill(distance);
            if (skill != null)
            {
                Framework.Ability.EActivateFailReason reason;
                character.FaceTowards(target.Position);
                character.TryUseSkill(skill, target.Position, out reason);
                return;
            }

            var desired = desiredPosition(target);
            if (character.Motor != null && HitGeometry.Flat(desired - character.Position).magnitude > 1.0f)
            {
                character.Motor.MoveTo(desired);
            }
        }

        private bool tryIdentity(Vector3 InAim)
        {
            var x = character.IdentityX;
            if (x != null && x.IsReady == true && character.TryUseIdentityX(InAim) == true)
            {
                return true;
            }

            var z = character.Identity;
            return z != null && z.HasResource == true && character.TryUseIdentity(InAim) == true;
        }

        private SkillAbility pickSkill(float InDistance)
        {
            var skills = character.EquippedSkills;
            for (int i = 0; i < skills.Count; ++i)
            {
                var skill = skills[i];
                if (skill.Definition.IsAwakening == false && character.IsSkillReady(skill) == true && InDistance <= skill.Definition.EstimatedRange)
                {
                    return skill;
                }
            }

            return null;
        }

        private Vector3 desiredPosition(CombatActor InTarget)
        {
            var cls = character.ClassDefinition;
            float range = cls != null ? cls.PreferredRange : 3.0f;
            var direction = HitGeometry.Flat(character.Position - InTarget.Position);
            if (cls != null && cls.PrefersBack == true)
            {
                direction = -HitGeometry.Flat(InTarget.Forward);
            }

            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector3.back;
            }

            return InTarget.Position + direction.normalized * (range + InTarget.Radius);
        }
    }
}
