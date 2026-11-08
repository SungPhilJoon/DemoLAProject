using Framework;
using UnityEngine;

namespace DemoLA.World
{
    /// <summary>지형 파괴 기믹(G2-05/06)에서 함께 무너지는 바닥 묶음.</summary>
    public enum ETileGroup
    {
        /// <summary>끝까지 남는 중앙 판.</summary>
        Center = 0,
        /// <summary>1차 파괴 후보 (좌).</summary>
        Left = 1,
        /// <summary>1차 파괴 후보 (우).</summary>
        Right = 2,
        /// <summary>2차 파괴 대상 (중앙을 제외한 나머지).</summary>
        Ring = 3,
    }

    /// <summary>바닥 타일 한 칸. 무너지면 비활성화되고 그 위는 낙사 구역이 된다.</summary>
    public class ArenaTile : BaseBehaviour
    {
        public ETileGroup Group;
        [Tooltip("Left/Right 타일이 2차 파괴 때도 남는가 (중앙 근처)")]
        public bool SurvivesRingCollapse;

        public bool IsCollapsed { get; private set; }

        public void Collapse()
        {
            if (IsCollapsed == true)
            {
                return;
            }

            IsCollapsed = true;
            gameObject.SetActive(false);
        }

        public void Restore()
        {
            IsCollapsed = false;
            gameObject.SetActive(true);
        }
    }
}
