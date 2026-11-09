using DemoLA.Boss;
using DemoLA.Character;
using DemoLA.Player;
using UnityEngine;

namespace DemoLA.Network.Testing
{
    /// <summary>
    /// 자기 캐릭터 자동 조작 (멀티플레이 테스트). PlayerController.Commands 를 그대로 쓰므로
    /// 클라이언트에서는 NetworkPartyCommands → 서버 RPC 경로를, 호스트에서는 로컬 경로를 탄다.
    /// 보스 주변 원을 돌며 이동 · 스킬 슬롯을 차례로 사용 · 가끔 회피 · 에스더 게이지가 차면 사용.
    /// 시드는 clientId 로 정해 실행마다 같은 순서로 행동한다.
    /// </summary>
    public class NetAutopilot : MonoBehaviour
    {
        private float timer;
        private int slot;
        private int step;
        private System.Random random;

        public int Commands { get; private set; }

        private void Start()
        {
            ulong id = Unity.Netcode.NetworkManager.Singleton != null ? Unity.Netcode.NetworkManager.Singleton.LocalClientId : 0;
            random = new System.Random(1000 + (int)id);
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0.0f)
            {
                return;
            }

            timer = 0.45f + (float)random.NextDouble() * 0.3f;
            var controller = FindAnyObjectByType<PlayerController>();
            var boss = FindAnyObjectByType<BossActor>();
            if (controller == null || controller.Commands == null || controller.Character == null || boss == null || controller.Character.IsDead == true)
            {
                return;
            }

            var commands = controller.Commands;
            var me = controller.Character;
            step += 1;
            Commands += 1;
            if (EstherSystem.IsFull == true && EstherSystem.Available.Count > 0)
            {
                commands.UseEsther(random.Next(EstherSystem.Available.Count));
                return;
            }

            if (step % 4 == 0)
            {
                // 보스 주변 원을 돈다 (반지름 5~8m, 각도는 실행마다 같은 순서).
                float angle = (float)(random.NextDouble() * Mathf.PI * 2.0);
                float radius = 5.0f + (float)random.NextDouble() * 3.0f;
                commands.MoveTo(boss.Position + new Vector3(Mathf.Sin(angle), 0.0f, Mathf.Cos(angle)) * radius);
                return;
            }

            if (step % 11 == 0)
            {
                commands.Dodge(me.Position + (me.Position - boss.Position));
                return;
            }

            commands.UseSkill(slot, boss.Position);
            slot = (slot + 1) % PartyCharacter.SkillSlotCount;
        }
    }
}
