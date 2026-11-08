using System;
using System.Collections.Generic;
using System.Text;
using DemoLA.Core;
using Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DemoLA.UI
{
    /// <summary>툴팁 한 장의 내용. Lines 는 (텍스트, 색) — 색이 clear 면 기본색.</summary>
    public class TooltipContent
    {
        public Sprite Icon;
        public string Title;
        public Color TitleColor = UIKit.AccentColor;
        public string Subtitle;
        public readonly List<KeyValuePair<string, Color>> Lines = new List<KeyValuePair<string, Color>>();

        public static readonly Color Body = new Color(0.88f, 0.88f, 0.9f);
        public static readonly Color Dim = new Color(0.6f, 0.62f, 0.66f);
        public static readonly Color Good = new Color(0.55f, 0.95f, 0.5f);
        public static readonly Color Bad = new Color(1.0f, 0.45f, 0.4f);
        public static readonly Color Section = new Color(0.95f, 0.8f, 0.45f);

        public TooltipContent Line(string InText)
        {
            Lines.Add(new KeyValuePair<string, Color>(InText, Body));
            return this;
        }

        public TooltipContent Line(string InText, Color InColor)
        {
            Lines.Add(new KeyValuePair<string, Color>(InText, InColor));
            return this;
        }

        public TooltipContent Header(string InText)
        {
            Lines.Add(new KeyValuePair<string, Color>(string.Empty, Color.clear));
            Lines.Add(new KeyValuePair<string, Color>(InText, Section));
            return this;
        }
    }

    /// <summary>
    /// 마우스를 올리면 TooltipView 를 띄운다. 내용은 매번 Provider 로 다시 만든다 (값이 바뀌어도 최신 상태).
    /// </summary>
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Func<TooltipContent> Provider;

        public void OnPointerEnter(PointerEventData InEventData)
        {
            if (Provider != null)
            {
                TooltipView.Show(Provider());
            }
        }

        public void OnPointerExit(PointerEventData InEventData)
        {
            TooltipView.Hide();
        }

        private void OnDisable()
        {
            TooltipView.Hide();
        }
    }

    /// <summary>
    /// 화면에 하나뿐인 툴팁 팝업. 가장 위 캔버스에 그리고 마우스를 따라가며, 화면 밖으로 나가지 않게 반대쪽으로 넘긴다.
    /// </summary>
    public class TooltipView : BaseBehaviour
    {
        private const float Width = 380.0f;
        private static TooltipView instance;

        private Canvas canvas;
        private RectTransform panel;
        private Image iconImage;
        private Text titleText;
        private Text subtitleText;
        private Text bodyText;
        private Image titleLine;
        private bool visible;

        public static void Show(TooltipContent InContent)
        {
            if (InContent == null)
            {
                return;
            }

            ensure();
            instance.fill(InContent);
        }

        public static void Hide()
        {
            if (instance != null)
            {
                instance.visible = false;
                instance.panel.gameObject.SetActive(false);
            }
        }

        private static void ensure()
        {
            if (instance != null)
            {
                return;
            }

            var go = new GameObject("[Tooltip]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<TooltipView>();
            instance.build();
        }

        private void build()
        {
            canvas = UIKit.CreateCanvas("TooltipCanvas", 200);
            canvas.transform.SetParent(transform, false);
            // 툴팁은 클릭을 받지 않는다 (아래 아이콘의 PointerExit 가 깜빡이지 않게).
            ObjectUtil.SafeDestroy(canvas.GetComponent<GraphicRaycaster>());

            panel = UIKit.Anchored(canvas.transform, "Tooltip", Vector2.zero, Vector2.zero, new Vector2(Width, 100.0f));
            panel.pivot = new Vector2(0.0f, 1.0f);
            UIKit.Image(panel, new Color(0.04f, 0.05f, 0.07f, 0.96f));
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.8f, 0.65f, 0.35f, 0.6f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 14);
            layout.spacing = 6.0f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 머리: 아이콘 + 제목/부제
            var head = new GameObject("Head", typeof(RectTransform)).GetComponent<RectTransform>();
            head.SetParent(panel, false);
            var headLayout = head.gameObject.AddComponent<HorizontalLayoutGroup>();
            headLayout.spacing = 10.0f;
            headLayout.childControlWidth = true;
            headLayout.childControlHeight = true;
            headLayout.childForceExpandWidth = false;
            headLayout.childForceExpandHeight = false;
            headLayout.childAlignment = TextAnchor.MiddleLeft;

            var iconRect = new GameObject("Icon", typeof(RectTransform)).GetComponent<RectTransform>();
            iconRect.SetParent(head, false);
            var iconElement = iconRect.gameObject.AddComponent<LayoutElement>();
            iconElement.preferredWidth = iconElement.minWidth = 56.0f;
            iconElement.preferredHeight = iconElement.minHeight = 56.0f;
            iconImage = iconRect.gameObject.AddComponent<Image>();
            iconImage.raycastTarget = false;

            var titles = new GameObject("Titles", typeof(RectTransform)).GetComponent<RectTransform>();
            titles.SetParent(head, false);
            titles.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1.0f;
            var titleLayout = titles.gameObject.AddComponent<VerticalLayoutGroup>();
            titleLayout.childControlWidth = true;
            titleLayout.childControlHeight = true;
            titleLayout.childForceExpandWidth = true;
            titleLayout.childForceExpandHeight = false;
            titleLayout.spacing = 2.0f;
            titleText = text(titles, 21, FontStyle.Bold);
            subtitleText = text(titles, 15, FontStyle.Normal);
            subtitleText.color = TooltipContent.Dim;

            var lineRect = new GameObject("Line", typeof(RectTransform)).GetComponent<RectTransform>();
            lineRect.SetParent(panel, false);
            var lineElement = lineRect.gameObject.AddComponent<LayoutElement>();
            lineElement.preferredHeight = lineElement.minHeight = 1.0f;
            titleLine = lineRect.gameObject.AddComponent<Image>();
            titleLine.raycastTarget = false;

            bodyText = text(panel, 16, FontStyle.Normal);
            bodyText.supportRichText = true;
            bodyText.lineSpacing = 1.1f;

            panel.gameObject.SetActive(false);
        }

        private static Text text(Transform InParent, int InSize, FontStyle InStyle)
        {
            var rect = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(InParent, false);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = UIKit.Font;
            label.fontSize = InSize;
            label.fontStyle = InStyle;
            label.color = Color.white;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private void fill(TooltipContent InContent)
        {
            iconImage.gameObject.SetActive(InContent.Icon != null);
            iconImage.sprite = InContent.Icon;
            titleText.text = InContent.Title ?? string.Empty;
            titleText.color = InContent.TitleColor;
            subtitleText.text = InContent.Subtitle ?? string.Empty;
            subtitleText.gameObject.SetActive(string.IsNullOrEmpty(InContent.Subtitle) == false);
            titleLine.color = new Color(InContent.TitleColor.r, InContent.TitleColor.g, InContent.TitleColor.b, 0.5f);

            var builder = new StringBuilder();
            for (int i = 0; i < InContent.Lines.Count; ++i)
            {
                var line = InContent.Lines[i];
                if (i > 0)
                {
                    builder.Append('\n');
                }

                if (line.Value.a <= 0.0f)
                {
                    builder.Append(line.Key);
                    continue;
                }

                builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(line.Value)).Append('>').Append(line.Key).Append("</color>");
            }

            bodyText.text = builder.ToString();
            bodyText.gameObject.SetActive(InContent.Lines.Count > 0);
            panel.gameObject.SetActive(true);
            visible = true;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            follow();
        }

        private void LateUpdate()
        {
            if (visible == true)
            {
                follow();
            }
        }

        private void follow()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var canvasRect = (RectTransform)canvas.transform;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouse.position.ReadValue(), null, out local);
            // 캔버스 좌하단 기준 좌표로.
            var size = canvasRect.rect.size;
            var point = local + size * 0.5f;
            var tip = panel.rect.size;
            const float offset = 18.0f;

            float x = point.x + offset;
            if (x + tip.x > size.x - 4.0f)
            {
                x = point.x - offset - tip.x;
            }

            float y = point.y - offset;
            if (y - tip.y < 4.0f)
            {
                y = point.y + offset + tip.y;
            }

            panel.anchoredPosition = new Vector2(Mathf.Max(4.0f, x), Mathf.Min(size.y - 4.0f, y));
        }
    }
}
