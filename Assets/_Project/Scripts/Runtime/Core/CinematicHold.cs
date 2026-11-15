using UnityEngine;

namespace DemoLA.Core
{
    /// <summary>
    /// 시네마틱 동안 게임 진행을 멈춘다: AI(CombatBrain)는 판단하지 않고 플레이어 입력은 무시된다.
    /// 보스 패턴 시작 대기는 BossActor.DelayFirstPattern 이 따로 늘린다. Until 이 지나면 자동으로 풀린다.
    /// </summary>
    public static class CinematicHold
    {
        public static float Until;

        public static bool Active
        {
            get { return Time.time < Until; }
        }

        public static void Hold(float InSeconds)
        {
            Until = Mathf.Max(Until, Time.time + InSeconds);
        }

        public static void Release()
        {
            Until = 0.0f;
        }
    }
}
