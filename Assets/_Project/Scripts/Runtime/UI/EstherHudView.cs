using System.Collections.Generic;
using DemoLA.Character;
using UnityEngine;
using UnityEngine.UI;

namespace DemoLA.UI
{
    /// <summary>
    /// 에스더 패널 (HUD 오른쪽 아래, 배틀 아이템 칸 위): 파티 공용 게이지 + 현재 관문에서 쓸 수 있는 에스더 칸 (단축키 5 · 6 · 7).
    /// 게이지가 가득 차면 칸이 밝아지고 테두리가 맥동한다. 칸을 클릭해도 쓴다 (OnUse 콜백 → IPartyCommands.UseEsther).
    /// </summary>
    public class EstherHudView : MonoBehaviour
    {
        private const float CellSize = 56.0f;

        private Bar gauge;
        private RectTransform cells;
        private readonly List<Image> icons = new List<Image>();
        private readonly List<Outline> outlines = new List<Outline>();
        private string signature;

        /// <summary>칸 클릭 시 (HudView 가 플레이어 명령으로 연결).</summary>
        public System.Action<int> OnUse;

        public static EstherHudView Create(Transform InParent)
        {
            var rect = UIKit.Anchored(InParent, "Esther", new Vector2(1.0f, 0.0f), new Vector2(-20.0f, 230.0f), new Vector2(230.0f, 96.0f));
            rect.pivot = new Vector2(1.0f, 0.0f);
            UIKit.Image(rect, new Color(0.05f, 0.06f, 0.09f, 0.6f));
            var view = rect.gameObject.AddComponent<EstherHudView>();
            var titleRect = UIKit.Rect(rect, "Title", new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(8.0f, -20.0f), new Vector2(-8.0f, -2.0f));
            UIKit.Label(titleRect, "에스더", 14, TextAnchor.MiddleLeft, UIKit.AccentColor);
            view.gauge = UIKit.CreateBar(rect, "Gauge", new Color(1.0f, 0.78f, 0.3f), new Color(0.0f, 0.0f, 0.0f, 0.7f));
            view.gauge.Rect.anchorMin = new Vector2(0.0f, 1.0f);
            view.gauge.Rect.anchorMax = new Vector2(1.0f, 1.0f);
            view.gauge.Rect.offsetMin = new Vector2(60.0f, -18.0f);
            view.gauge.Rect.offsetMax = new Vector2(-8.0f, -5.0f);
            view.gauge.Label.fontSize = 11;
            view.cells = UIKit.Rect(rect, "Cells", Vector2.zero, new Vector2(1.0f, 0.0f), new Vector2(8.0f, 6.0f), new Vector2(-8.0f, 6.0f + CellSize));
            var layout = view.cells.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12.0f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            return view;
        }

        private void rebuild()
        {
            for (int i = cells.childCount - 1; i >= 0; --i)
            {
                Destroy(cells.GetChild(i).gameObject);
            }

            icons.Clear();
            outlines.Clear();
            var list = EstherSystem.Available;
            for (int i = 0; i < list.Count; ++i)
            {
                int index = i;
                var esther = list[i];
                var cell = UIKit.IconCell(cells, IconFactory.Get(EIconKind.Esther, esther.IconKey, 4), CellSize, $"{5 + i}", () => tooltip(esther, index), () => OnUse?.Invoke(index));
                outlines.Add(cell.gameObject.AddComponent<Outline>());
                outlines[i].effectDistance = new Vector2(2.0f, -2.0f);
                icons.Add(cell);
            }

            signature = EstherSystem.CurrentBossId + "|" + list.Count;
        }

        private static TooltipContent tooltip(EstherDefinition InEsther, int InIndex)
        {
            var content = new TooltipContent { Title = InEsther.DisplayName, Subtitle = $"에스더 · 단축키 {5 + InIndex}", TitleColor = InEsther.Color };
            content.Line(InEsther.Description, TooltipContent.Body);
            content.Line(EstherSystem.IsFull ? "게이지 가득 — 사용 가능" : $"게이지 {EstherSystem.Gauge:0}% (보스 피해로 찬다 · 파티 공용)", EstherSystem.IsFull ? TooltipContent.Good : TooltipContent.Dim);
            return content;
        }

        private void Update()
        {
            if (cells == null)
            {
                return;
            }

            if (signature != EstherSystem.CurrentBossId + "|" + EstherSystem.Available.Count)
            {
                rebuild();
            }

            float ratio = EstherSystem.Gauge / EstherSystem.Max;
            gauge.Fill.fillAmount = ratio;
            gauge.Label.text = EstherSystem.IsFull ? "준비 완료" : $"{EstherSystem.Gauge:0}%";
            bool full = EstherSystem.IsFull;
            float pulse = full ? 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6.0f) : 0.0f;
            for (int i = 0; i < icons.Count; ++i)
            {
                icons[i].color = full ? Color.white : new Color(0.45f, 0.45f, 0.5f, 0.9f);
                var color = i < EstherSystem.Available.Count ? EstherSystem.Available[i].Color : Color.white;
                color.a = pulse;
                outlines[i].effectColor = color;
            }
        }
    }
}
