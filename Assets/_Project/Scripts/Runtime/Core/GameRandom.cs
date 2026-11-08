namespace DemoLA.Core
{
    /// <summary>
    /// 게임 로직 전용 난수. 테스트(딜량 시나리오, 강화 확률)를 재현 가능하게 하려고 UnityEngine.Random 대신 쓴다.
    /// 멀티플레이 단계에서는 서버가 시드를 정하고 클라이언트에 나눠 주는 방식으로 확장한다.
    /// </summary>
    public static class GameRandom
    {
        private static System.Random Rng = new System.Random(12345);

        public static void SetSeed(int InSeed)
        {
            Rng = new System.Random(InSeed);
        }

        /// <summary>[0, 1)</summary>
        public static float Value
        {
            get { return (float)Rng.NextDouble(); }
        }

        public static float Range(float InMin, float InMax)
        {
            return InMin + (InMax - InMin) * Value;
        }

        /// <summary>[InMin, InMax)</summary>
        public static int Range(int InMin, int InMax)
        {
            return Rng.Next(InMin, InMax);
        }
    }
}
