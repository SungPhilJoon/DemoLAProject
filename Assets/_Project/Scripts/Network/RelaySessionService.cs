using System;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace DemoLA.Network
{
    /// <summary>
    /// Relay 로비 (구현 보류 사항 답변 9: "Delay 로비" = Relay 로비).
    /// Unity Gaming Services 의 Multiplayer Services 세션 + Relay 로 서로 다른 LAN 의 플레이어가 참가 코드 하나로 모인다.
    ///  - 포트 포워딩 · 전용 서버 없이 호스트가 Relay 할당을 받고, 참가자는 코드로 붙는다 (Netcode for GameObjects 가 자동 연결).
    ///  - Relay/Lobby 는 UGS 무료 사용량 안에서 쓸 수 있다 (프로젝트를 UGS 클라우드 프로젝트에 연결해야 함).
    ///  - 인증은 익명 로그인. 이후 PlayFab 로그인으로 바꿀 자리.
    /// </summary>
    public static class RelaySessionService
    {
        public const int MaxPlayers = 4;

        public static ISession Current { get; private set; }

        public static string JoinCode
        {
            get { return Current != null ? Current.Code : null; }
        }

        public static bool IsHost
        {
            get { return Current != null && Current.IsHost; }
        }

        public static async UniTask EnsureSignedInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (AuthenticationService.Instance.IsSignedIn == false)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        /// <summary>세션을 만들고 Relay 할당을 받아 호스트로 시작한다. 반환값 = 참가 코드.</summary>
        public static async UniTask<string> HostAsync()
        {
            await EnsureSignedInAsync();
            var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = false }.WithRelayNetwork();
            Current = await MultiplayerService.Instance.CreateSessionAsync(options);
            Debug.Log($"[Relay] 세션 생성: 코드 {Current.Code}");
            return Current.Code;
        }

        /// <summary>참가 코드로 세션에 들어가 클라이언트로 접속한다.</summary>
        public static async UniTask JoinAsync(string InCode)
        {
            if (string.IsNullOrWhiteSpace(InCode) == true)
            {
                throw new ArgumentException("참가 코드가 비어 있습니다.");
            }

            await EnsureSignedInAsync();
            Current = await MultiplayerService.Instance.JoinSessionByCodeAsync(InCode.Trim().ToUpperInvariant());
            Debug.Log($"[Relay] 세션 참가: {Current.Code} ({Current.PlayerCount}/{Current.MaxPlayers})");
        }

        public static async UniTask LeaveAsync()
        {
            if (Current == null)
            {
                return;
            }

            var session = Current;
            Current = null;
            try
            {
                await session.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Relay] 나가기 실패: {e.Message}");
            }
        }
    }
}
