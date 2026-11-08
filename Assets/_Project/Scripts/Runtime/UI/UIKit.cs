using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DemoLA.UI
{
    /// <summary>
    /// 코드로 uGUI 를 조립하는 도우미 (프리팹/아트 미정 단계). 프리팹으로 옮길 때 이 레이아웃을 기준으로 만든다.
    /// 글꼴은 내장 LegacyRuntime (한글은 OS 글꼴 대체로 표시) — TMP 한글 폰트 에셋은 구현 보류 사항.md.
    /// </summary>
    public static class UIKit
    {
        private static Font font;

        public static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.09f, 0.88f);
        public static readonly Color RowColor = new Color(1.0f, 1.0f, 1.0f, 0.05f);
        public static readonly Color AccentColor = new Color(0.95f, 0.75f, 0.35f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.25f, 0.32f, 1.0f);

        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return font;
            }
        }

        public static Canvas CreateCanvas(string InName, int InSortingOrder)
        {
            EnsureEventSystem();
            var go = new GameObject(InName, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = InSortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920.0f, 1080.0f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        public static RectTransform Rect(Transform InParent, string InName, Vector2 InAnchorMin, Vector2 InAnchorMax, Vector2 InOffsetMin, Vector2 InOffsetMax)
        {
            var go = new GameObject(InName, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(InParent, false);
            rect.anchorMin = InAnchorMin;
            rect.anchorMax = InAnchorMax;
            rect.offsetMin = InOffsetMin;
            rect.offsetMax = InOffsetMax;
            return rect;
        }

        /// <summary>앵커 한 점 기준 (pivot = anchor) 고정 크기.</summary>
        public static RectTransform Anchored(Transform InParent, string InName, Vector2 InAnchor, Vector2 InPosition, Vector2 InSize)
        {
            var rect = Rect(InParent, InName, InAnchor, InAnchor, Vector2.zero, Vector2.zero);
            rect.pivot = InAnchor;
            rect.anchoredPosition = InPosition;
            rect.sizeDelta = InSize;
            return rect;
        }

        public static RectTransform Stretch(Transform InParent, string InName, float InPadding = 0.0f)
        {
            return Rect(InParent, InName, Vector2.zero, Vector2.one, new Vector2(InPadding, InPadding), new Vector2(-InPadding, -InPadding));
        }

        public static Image Image(RectTransform InRect, Color InColor, bool InRaycast = false)
        {
            var image = InRect.gameObject.AddComponent<Image>();
            image.color = InColor;
            image.raycastTarget = InRaycast;
            return image;
        }

        public static Text Label(Transform InParent, string InText, int InSize, TextAnchor InAlign, Color InColor)
        {
            var rect = Stretch(InParent, "Label");
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = InText;
            text.fontSize = InSize;
            text.alignment = InAlign;
            text.color = InColor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button Button(Transform InParent, string InText, int InSize, Action InOnClick)
        {
            var rect = (RectTransform)new GameObject("Button", typeof(RectTransform)).transform;
            rect.SetParent(InParent, false);
            var image = Image(rect, ButtonColor, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            Label(rect, InText, InSize, TextAnchor.MiddleCenter, Color.white);
            if (InOnClick != null)
            {
                button.onClick.AddListener(() => InOnClick());
            }

            return button;
        }

        public static void SetButtonText(Button InButton, string InText)
        {
            var text = InButton.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = InText;
            }
        }

        /// <summary>가로 막대 (배경 + 채움). 채움 비율은 Bar.Set.</summary>
        public static Bar CreateBar(Transform InParent, string InName, Color InFill, Color InBack)
        {
            var rect = (RectTransform)new GameObject(InName, typeof(RectTransform)).transform;
            rect.SetParent(InParent, false);
            Image(rect, InBack);
            var fillRect = Stretch(rect, "Fill", 2.0f);
            var fill = Image(fillRect, InFill);
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.sprite = WhiteSprite;
            var label = Label(rect, string.Empty, 18, TextAnchor.MiddleCenter, Color.white);
            return new Bar(rect, fill, label);
        }

        private static Sprite whiteSprite;

        /// <summary>Filled 이미지에는 스프라이트가 필요하다.</summary>
        public static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite == null)
                {
                    var texture = Texture2D.whiteTexture;
                    whiteSprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                }

                return whiteSprite;
            }
        }

        public static VerticalLayoutGroup VerticalList(RectTransform InRect, float InSpacing, int InPadding)
        {
            var layout = InRect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = InSpacing;
            layout.padding = new RectOffset(InPadding, InPadding, InPadding, InPadding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = InRect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return layout;
        }

        public static HorizontalLayoutGroup HorizontalRow(Transform InParent, float InHeight, float InSpacing)
        {
            var rect = (RectTransform)new GameObject("Row", typeof(RectTransform)).transform;
            rect.SetParent(InParent, false);
            Image(rect, RowColor);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = InHeight;
            element.preferredHeight = InHeight;
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = InSpacing;
            layout.padding = new RectOffset(8, 8, 2, 2);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return layout;
        }

        public static Text RowLabel(Transform InRow, string InText, float InWidth, int InSize = 18)
        {
            var rect = (RectTransform)new GameObject("Cell", typeof(RectTransform)).transform;
            rect.SetParent(InRow, false);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = InWidth;
            element.flexibleWidth = InWidth <= 0.0f ? 1.0f : 0.0f;
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = InText;
            text.fontSize = InSize;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        public static Button RowButton(Transform InRow, string InText, float InWidth, Action InOnClick)
        {
            var button = Button(InRow, InText, 17, InOnClick);
            var element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = InWidth;
            element.minWidth = InWidth;
            return button;
        }

        /// <summary>"◀ 값 ▶" 순환 선택기. InStep(+1/-1) 후 InGetText 로 값을 다시 그린다.</summary>
        public static void Selector(Transform InRow, float InWidth, Func<string> InGetText, Action<int> InStep)
        {
            Text value = null;
            RowButton(InRow, "◀", 34, () =>
            {
                InStep(-1);
                value.text = InGetText();
            });
            value = RowLabel(InRow, InGetText(), InWidth);
            value.alignment = TextAnchor.MiddleCenter;
            value.color = AccentColor;
            RowButton(InRow, "▶", 34, () =>
            {
                InStep(1);
                value.text = InGetText();
            });
        }

        /// <summary>
        /// 아이콘 칸 (고정 크기). 마우스를 올리면 InTooltip 내용으로 툴팁이 뜬다.
        /// InBadge 는 오른쪽 아래 작은 글자 (+강화 · 레벨 · 각성 등). InOnClick 이 있으면 버튼처럼 눌린다.
        /// </summary>
        public static Image IconCell(Transform InParent, Sprite InSprite, float InSize, string InBadge, Func<TooltipContent> InTooltip, Action InOnClick = null)
        {
            var rect = (RectTransform)new GameObject("Icon", typeof(RectTransform)).transform;
            rect.SetParent(InParent, false);
            rect.sizeDelta = new Vector2(InSize, InSize);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = InSize;
            element.preferredHeight = element.minHeight = InSize;
            element.flexibleWidth = 0.0f;
            element.flexibleHeight = 0.0f;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = InSprite;
            image.preserveAspect = true;
            image.raycastTarget = true;

            if (string.IsNullOrEmpty(InBadge) == false)
            {
                var badgeRect = Rect(rect, "Badge", Vector2.zero, Vector2.one, new Vector2(3.0f, 2.0f), new Vector2(-4.0f, -2.0f));
                var badge = badgeRect.gameObject.AddComponent<Text>();
                badge.font = Font;
                badge.text = InBadge;
                badge.fontSize = Mathf.Max(11, Mathf.RoundToInt(InSize * 0.26f));
                badge.fontStyle = FontStyle.Bold;
                badge.alignment = TextAnchor.LowerRight;
                badge.color = Color.white;
                badge.raycastTarget = false;
                var shadow = badgeRect.gameObject.AddComponent<Outline>();
                shadow.effectColor = new Color(0.0f, 0.0f, 0.0f, 0.9f);
                shadow.effectDistance = new Vector2(1.0f, -1.0f);
            }

            if (InTooltip != null)
            {
                rect.gameObject.AddComponent<TooltipTrigger>().Provider = InTooltip;
            }

            if (InOnClick != null)
            {
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                var colors = button.colors;
                colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
                colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
                button.colors = colors;
                button.onClick.AddListener(() => InOnClick());
            }

            return image;
        }

        /// <summary>아이콘을 격자로 늘어놓는 줄 (자동 줄바꿈).</summary>
        public static RectTransform IconGrid(Transform InParent, float InCellSize, float InSpacing, int InColumns)
        {
            var rect = (RectTransform)new GameObject("IconGrid", typeof(RectTransform)).transform;
            rect.SetParent(InParent, false);
            Image(rect, RowColor);
            var grid = rect.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(InCellSize, InCellSize);
            grid.spacing = new Vector2(InSpacing, InSpacing);
            grid.padding = new RectOffset(10, 10, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, InColumns);
            // 높이는 부모 VerticalLayoutGroup 이 GridLayoutGroup 의 선호 높이로 잡는다.
            return rect;
        }

        public static ScrollRect ScrollArea(RectTransform InParent, out RectTransform OutContent)
        {
            var viewport = Stretch(InParent, "Viewport");
            Image(viewport, new Color(0, 0, 0, 0.01f), true);
            viewport.gameObject.AddComponent<RectMask2D>();
            OutContent = Rect(viewport, "Content", new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), Vector2.zero, Vector2.zero);
            OutContent.pivot = new Vector2(0.5f, 1.0f);
            VerticalList(OutContent, 4.0f, 6);
            var scroll = InParent.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = OutContent;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30.0f;
            return scroll;
        }

        public static int Wrap(int InValue, int InCount)
        {
            if (InCount <= 0)
            {
                return 0;
            }

            int value = InValue % InCount;
            return value < 0 ? value + InCount : value;
        }
    }

    public class Bar
    {
        public readonly RectTransform Rect;
        public readonly Image Fill;
        public readonly Text Label;

        public Bar(RectTransform InRect, Image InFill, Text InLabel)
        {
            Rect = InRect;
            Fill = InFill;
            Label = InLabel;
        }

        public void Set(float InRatio, string InText)
        {
            Fill.fillAmount = Mathf.Clamp01(InRatio);
            if (Label.text != InText)
            {
                Label.text = InText;
            }
        }

        public void SetVisible(bool InVisible)
        {
            if (Rect.gameObject.activeSelf != InVisible)
            {
                Rect.gameObject.SetActive(InVisible);
            }
        }
    }
}
