using DemoLA.Character;
using UnityEngine;

namespace DemoLA.Player
{
    /// <summary>
    /// 플레이어 입력 → 캐릭터 행동. 오프라인은 LocalPartyCommands 가 바로 실행하고,
    /// 네트워크(서버 권한)에서는 DemoLA.Network 의 구현이 서버 RPC 로 보낸다 → 서버가 실행 · 결과만 복제.
    /// </summary>
    public interface IPartyCommands
    {
        void UseSkill(int InSlot, Vector3 InAim);
        void UseAwakening(Vector3 InAim);
        void UseIdentity(Vector3 InAim);
        void UseIdentityX(Vector3 InAim);
        void UseBattleItem(int InSlot, Vector3 InAim);
        void Dodge(Vector3 InAim);
        void MoveTo(Vector3 InDestination);
    }

    public class LocalPartyCommands : IPartyCommands
    {
        private readonly PartyCharacter character;

        public LocalPartyCommands(PartyCharacter InCharacter)
        {
            character = InCharacter;
        }

        public void UseSkill(int InSlot, Vector3 InAim)
        {
            character.TryUseSlot(InSlot, InAim);
        }

        public void UseAwakening(Vector3 InAim)
        {
            if (character.Awakening != null)
            {
                Framework.Ability.EActivateFailReason reason;
                character.TryUseSkill(character.Awakening, InAim, out reason);
            }
        }

        public void UseIdentity(Vector3 InAim)
        {
            character.TryUseIdentity(InAim);
        }

        public void UseIdentityX(Vector3 InAim)
        {
            character.TryUseIdentityX(InAim);
        }

        public void UseBattleItem(int InSlot, Vector3 InAim)
        {
            character.TryUseBattleItem(InSlot, InAim);
        }

        public void Dodge(Vector3 InAim)
        {
            character.TryDodge(InAim);
        }

        public void MoveTo(Vector3 InDestination)
        {
            if (character.Motor != null)
            {
                character.Motor.MoveTo(InDestination);
            }
        }
    }
}
