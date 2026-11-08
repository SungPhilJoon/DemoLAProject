using System.Collections.Generic;
using DemoLA.Combat;
using DemoLA.Core;
using Framework;

namespace DemoLA.World
{
    /// <summary>레이드 공용 동작 (안내 문구, 전멸 처리).</summary>
    public static class Raid
    {
        private static readonly List<CombatActor> Buffer = new List<CombatActor>();

        public static void Message(string InMessage, float InDuration, bool InWarning)
        {
            var evt = new RaidMessageEvent();
            evt.Message = InMessage;
            evt.Duration = InDuration;
            evt.IsWarning = InWarning;
            EventBus<RaidMessageEvent>.Publish(evt);
        }

        /// <summary>플레이어 팀 전원 사망 (전멸기). 무적은 무시한다.</summary>
        public static void WipeParty(CombatActor InSource, string InReason)
        {
            CombatRegistry.FindTeam(ETeam.Player, Buffer);
            for (int i = 0; i < Buffer.Count; ++i)
            {
                Buffer[i].RemoveTag((int)ETag.State_Invulnerable);
                Buffer[i].Kill();
            }

            var result = new RaidResultEvent();
            result.Cleared = false;
            result.Reason = InReason;
            EventBus<RaidResultEvent>.Publish(result);
        }
    }
}
