using DemoLA.Core;
using DemoLA.World;
using UnityEngine;

namespace DemoLA.Boss
{
    public enum ECollapseStage
    {
        /// <summary>G2-05: 좌/우 중 무작위 절반.</summary>
        RandomHalf = 0,
        /// <summary>G2-06: 중앙 판만 남김.</summary>
        AllExceptCenter = 1,
    }

    /// <summary>
    /// G2-05 / G2-06 지형 파괴. WarningTime 동안 무너질 구역을 표시한 뒤 ArenaFloor 타일을 무너뜨린다.
    /// 무너진 곳 위에 있으면 ArenaFloor 가 낙사 처리한다. 이 구간 보스 피해 감소(S11 단일 출처)는 적용하지 않았다.
    /// </summary>
    public class TerrainCollapseGimmick : BossGimmick
    {
        public ECollapseStage Stage;
        [Tooltip("붕괴 전 경고 시간 — 자리 표시 값")]
        public float WarningTime = 4.0f;
        public Color WarningColor = new Color(1.0f, 0.5f, 0.1f);

        private float elapsed;
        private ETileGroup chosen;
        private readonly System.Collections.Generic.List<Renderer> warned = new System.Collections.Generic.List<Renderer>();
        private readonly System.Collections.Generic.Dictionary<Renderer, Color> originalColors = new System.Collections.Generic.Dictionary<Renderer, Color>();

        public override string StatusText
        {
            get { return State == EGimmickState.Running ? $"지형 붕괴까지 {Mathf.Max(0.0f, WarningTime - elapsed):0.0}초" : string.Empty; }
        }

        protected override void OnBegin()
        {
            elapsed = 0.0f;
            var floor = ArenaFloor.Current;
            if (floor == null)
            {
                Complete(true);
                return;
            }

            chosen = GameRandom.Value < 0.5f ? ETileGroup.Left : ETileGroup.Right;
            string where = Stage == ECollapseStage.RandomHalf ? (chosen == ETileGroup.Left ? "왼쪽" : "오른쪽") : "중앙 바깥";
            Raid.Message($"지형이 무너집니다 — {where}에서 벗어나세요", WarningTime, true);
            highlight(floor, true);
        }

        protected override void OnTick(float InDeltaTime)
        {
            elapsed += InDeltaTime;
            if (elapsed < WarningTime)
            {
                return;
            }

            var floor = ArenaFloor.Current;
            highlight(floor, false);
            if (floor != null)
            {
                if (Stage == ECollapseStage.RandomHalf)
                {
                    floor.Collapse(chosen, true);
                }
                else
                {
                    floor.CollapseAllExceptCenter();
                }
            }

            Complete(true);
        }

        protected override void OnReset()
        {
            highlight(ArenaFloor.Current, false);
            if (ArenaFloor.Current != null)
            {
                ArenaFloor.Current.RestoreAll();
            }
        }

        private bool willCollapse(ArenaTile InTile)
        {
            if (InTile.IsCollapsed == true)
            {
                return false;
            }

            if (Stage == ECollapseStage.RandomHalf)
            {
                return InTile.Group == chosen && InTile.SurvivesRingCollapse == false;
            }

            return InTile.Group != ETileGroup.Center && InTile.SurvivesRingCollapse == false;
        }

        private void highlight(ArenaFloor InFloor, bool InOn)
        {
            if (InOn == false)
            {
                foreach (var pair in originalColors)
                {
                    if (pair.Key != null)
                    {
                        pair.Key.material.color = pair.Value;
                    }
                }

                originalColors.Clear();
                warned.Clear();
                return;
            }

            if (InFloor == null)
            {
                return;
            }

            for (int i = 0; i < InFloor.Tiles.Count; ++i)
            {
                var tile = InFloor.Tiles[i];
                if (willCollapse(tile) == false)
                {
                    continue;
                }

                var renderer = tile.GetComponent<Renderer>();
                if (renderer == null)
                {
                    continue;
                }

                originalColors[renderer] = renderer.material.color;
                renderer.material.color = WarningColor;
                warned.Add(renderer);
            }
        }
    }
}
