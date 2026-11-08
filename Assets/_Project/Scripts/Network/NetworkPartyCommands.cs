using DemoLA.Player;
using UnityEngine;

namespace DemoLA.Network
{
    /// <summary>네트워크 모드의 입력 경로: 모든 행동을 서버 RPC 로 보낸다 (서버 권한, 예측 없음).</summary>
    public class NetworkPartyCommands : IPartyCommands
    {
        private readonly NetworkPartyMember member;
        private float moveTimer;

        public NetworkPartyCommands(NetworkPartyMember InMember)
        {
            member = InMember;
        }

        public void UseSkill(int InSlot, Vector3 InAim)
        {
            member.UseSkillRpc(InSlot, InAim);
        }

        public void UseAwakening(Vector3 InAim)
        {
            member.UseAwakeningRpc(InAim);
        }

        public void UseIdentity(Vector3 InAim)
        {
            member.UseIdentityRpc(InAim);
        }

        public void UseIdentityX(Vector3 InAim)
        {
            member.UseIdentityXRpc(InAim);
        }

        public void UseBattleItem(int InSlot, Vector3 InAim)
        {
            member.UseBattleItemRpc(InSlot, InAim);
        }

        public void Dodge(Vector3 InAim)
        {
            member.DodgeRpc(InAim);
        }

        public void MoveTo(Vector3 InDestination)
        {
            // 이동 입력은 매 프레임 들어오므로 0.1초 간격으로 줄여 보낸다.
            if (Time.unscaledTime < moveTimer)
            {
                return;
            }

            moveTimer = Time.unscaledTime + 0.1f;
            member.MoveToRpc(InDestination);
        }
    }
}
