namespace DemoLA.Core
{
    /// <summary>
    /// 이 프로세스가 네트워크 클라이언트(서버 아님)인가. 런타임 어셈블리는 Netcode 를 모르므로 네트워크 층이 알려 준다
    /// (NetworkRaidDirector.OnNetworkSpawn). 클라이언트는 같은 액터를 만들되 게임 규칙은 서버만 진행한다:
    /// GAS · AI · 낙사 판정 · 구슬 획득 · 에스더 게이지. 클라이언트는 서버가 보낸 결과만 반영한다.
    /// 오프라인 · 호스트 = false.
    /// </summary>
    public static class NetAuthority
    {
        public static bool IsReplica;
    }
}
