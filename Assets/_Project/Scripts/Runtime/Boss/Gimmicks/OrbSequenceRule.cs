namespace DemoLA.Boss
{
    public enum EOrbColor
    {
        Red = 0,
        Blue = 1,
    }

    public enum EOrbPickResult
    {
        Accepted = 0,
        /// <summary>같은 색을 연속으로 획득 → 전멸 (xlsx G1-01).</summary>
        SameColorTwice = 1,
        /// <summary>이미 끝난 기믹.</summary>
        Ignored = 2,
    }

    /// <summary>
    /// 구슬 기믹(G1-01) 규칙 — 순수 로직 (테스트 대상).
    /// "서로 다른 색을 번갈아 획득해야 하며 같은 색을 연속으로 먹으면 전멸."
    /// 파티 전체에서 직전에 먹은 색과 비교한다 (개인 단위인지 파티 단위인지 원문 불명 → 구현 보류 사항.md).
    /// </summary>
    public class OrbSequenceRule
    {
        private bool hasLast;
        private EOrbColor last;

        public int Collected { get; private set; }
        public int Required { get; private set; }
        public bool IsFailed { get; private set; }

        public bool IsComplete
        {
            get { return IsFailed == false && Collected >= Required; }
        }

        public EOrbColor? LastColor
        {
            get { return hasLast == true ? last : (EOrbColor?)null; }
        }

        public OrbSequenceRule(int InRequired)
        {
            Required = InRequired;
        }

        public EOrbPickResult Pick(EOrbColor InColor)
        {
            if (IsFailed == true || IsComplete == true)
            {
                return EOrbPickResult.Ignored;
            }

            if (hasLast == true && last == InColor)
            {
                IsFailed = true;
                return EOrbPickResult.SameColorTwice;
            }

            hasLast = true;
            last = InColor;
            Collected += 1;
            return EOrbPickResult.Accepted;
        }

        /// <summary>구슬이 보스에 닿았다 (xlsx: 실패).</summary>
        public void OrbReachedBoss()
        {
            IsFailed = true;
        }
    }
}
