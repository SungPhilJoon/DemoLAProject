using System;
using System.Collections.Generic;
using DemoLA.Character;
using DemoLA.Core;
using Framework;
using UnityEngine;

namespace DemoLA.World
{
    /// <summary>
    /// 레이드 입장 준비 확인. 파티원 전원이 "준비 완료" 여야 카운트다운(EnterDelay) 후 입장한다.
    ///  - 한 명이라도 준비를 풀면 카운트다운이 취소된다.
    ///  - 관문을 바꾸면 모두의 준비가 풀린다. 내 설정을 적용(저장)하면 내 준비가 풀린다.
    ///  - AI 파티원은 잠시 뒤 스스로 준비한다 (설정을 마친 다른 파티원 역할).
    /// </summary>
    public class RaidLobby : BaseBehaviour
    {
        public class Member
        {
            public PartyCharacter Character;
            public bool IsPlayer;
            public bool Ready;
            public float AutoReadyAt;
        }

        public const float EnterDelay = 5.0f;

        private readonly List<Member> members = new List<Member>();
        private float countdown = -1.0f;

        /// <summary>카운트다운이 끝나 입장할 때.</summary>
        public event Action OnEnter;
        /// <summary>준비 상태가 바뀌었을 때 (UI 갱신).</summary>
        public event Action OnChanged;

        public bool IsActive { get; private set; }

        public IReadOnlyList<Member> Members
        {
            get { return members; }
        }

        /// <summary>남은 입장 카운트다운 (초). 카운트다운 중이 아니면 -1.</summary>
        public float Countdown
        {
            get { return countdown; }
        }

        public int ReadyCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < members.Count; ++i)
                {
                    count += members[i].Ready ? 1 : 0;
                }

                return count;
            }
        }

        public bool IsPlayerReady
        {
            get
            {
                var member = members.Find(m => m.IsPlayer);
                return member != null && member.Ready;
            }
        }

        public void Begin(IReadOnlyList<PartyCharacter> InParty, PartyCharacter InPlayer)
        {
            members.Clear();
            for (int i = 0; i < InParty.Count; ++i)
            {
                var member = new Member();
                member.Character = InParty[i];
                member.IsPlayer = InParty[i] == InPlayer;
                member.AutoReadyAt = member.IsPlayer ? -1.0f : Time.time + GameRandom.Range(1.5f, 4.0f);
                members.Add(member);
            }

            countdown = -1.0f;
            IsActive = true;
            GateVeil.Charged = false;
            OnChanged?.Invoke();
        }

        public void End()
        {
            IsActive = false;
            members.Clear();
            countdown = -1.0f;
            GateVeil.Charged = false;
            OnChanged?.Invoke();
        }

        public void SetPlayerReady(bool InReady)
        {
            var member = members.Find(m => m.IsPlayer);
            if (member == null || member.Ready == InReady)
            {
                return;
            }

            member.Ready = InReady;
            Raid.Message(InReady ? "준비 완료" : "준비 취소", 1.5f, false);
            OnChanged?.Invoke();
        }

        /// <summary>모두의 준비를 푼다 (관문 변경 등). AI 는 잠시 뒤 다시 준비한다.</summary>
        public void ResetAll(string InReason)
        {
            for (int i = 0; i < members.Count; ++i)
            {
                members[i].Ready = false;
                members[i].AutoReadyAt = members[i].IsPlayer ? -1.0f : Time.time + GameRandom.Range(1.0f, 3.0f);
            }

            if (string.IsNullOrEmpty(InReason) == false)
            {
                Raid.Message(InReason, 2.0f, false);
            }

            OnChanged?.Invoke();
        }

        private void Update()
        {
            if (IsActive == false)
            {
                return;
            }

            bool changed = false;
            for (int i = 0; i < members.Count; ++i)
            {
                var member = members[i];
                if (member.IsPlayer == false && member.Ready == false && member.AutoReadyAt >= 0.0f && Time.time >= member.AutoReadyAt)
                {
                    member.Ready = true;
                    changed = true;
                }
            }

            bool allReady = members.Count > 0 && ReadyCount == members.Count;
            if (allReady == false)
            {
                if (countdown >= 0.0f)
                {
                    countdown = -1.0f;
                    Raid.Message("입장 취소 — 준비하지 않은 파티원이 있습니다", 2.0f, true);
                    changed = true;
                }
            }
            else if (countdown < 0.0f)
            {
                countdown = EnterDelay;
                changed = true;
            }
            else
            {
                countdown -= Time.deltaTime;
                if (countdown <= 0.0f)
                {
                    countdown = -1.0f;
                    IsActive = false;
                    OnEnter?.Invoke();
                    return;
                }
            }

            GateVeil.Charged = allReady;
            if (changed == true)
            {
                OnChanged?.Invoke();
            }
        }
    }
}
