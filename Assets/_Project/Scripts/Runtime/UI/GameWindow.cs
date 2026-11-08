using System;
using DemoLA.Core;
using Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DemoLA.UI
{
    /// <summary>
    /// 독립 창 하나 (장비 · 각인 · 카드 …). 제목 표시줄을 끌어 옮기고, 클릭하면 맨 앞으로 온다.
    /// 내용은 Drawer 가 Content 에 다시 그린다 (Refresh). 바닥 줄에는 공통 "적용 + 저장" 과 상태 문구.
    /// </summary>
    public class GameWindow : BaseBehaviour, IPointerDownHandler
    {
        public RectTransform Root { get; private set; }
        public RectTransform Content { get; private set; }
        public RectTransform Footer { get; private set; }
        public Text Status { get; private set; }

        private Text titleText;
        private Action<GameWindow> drawer;
        private Action onOpened;

        public bool IsOpen
        {
            get { return gameObject.activeSelf == true; }
        }

        public static GameWindow Create(Transform InCanvas, string InTitle, string InHotkey, EIconKind InIcon, Vector2 InSize, Vector2 InPosition,
            Action<GameWindow> InDrawer, Action InOnApply, Action InOnOpened)
        {
            var root = UIKit.Anchored(InCanvas, $"Window_{InTitle}", new Vector2(0.5f, 0.5f), InPosition, InSize);
            root.pivot = new Vector2(0.5f, 0.5f);
            UIKit.Image(root, UIKit.PanelColor, true);
            var outline = root.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.0f, 0.0f, 0.0f, 0.7f);
            outline.effectDistance = new Vector2(2.0f, -2.0f);

            var window = root.gameObject.AddComponent<GameWindow>();
            window.Root = root;
            window.drawer = InDrawer;
            window.onOpened = InOnOpened;

            // 제목 표시줄 (드래그 핸들)
            var titleBar = UIKit.Rect(root, "TitleBar", new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, -46.0f), Vector2.zero);
            UIKit.Image(titleBar, new Color(0.12f, 0.13f, 0.17f, 1.0f), true);
            titleBar.gameObject.AddComponent<WindowDragHandle>().Target = root;

            var icon = UIKit.Anchored(titleBar, "Icon", new Vector2(0.0f, 0.5f), new Vector2(8.0f, 0.0f), new Vector2(34.0f, 34.0f));
            icon.pivot = new Vector2(0.0f, 0.5f);
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = IconFactory.Get(InIcon, InTitle, 4);
            iconImage.raycastTarget = false;

            var title = UIKit.Rect(titleBar, "Title", Vector2.zero, Vector2.one, new Vector2(52.0f, 0.0f), new Vector2(-60.0f, 0.0f));
            window.titleText = UIKit.Label(title, $"{InTitle}  <size=15><color=#8A8F99>[{InHotkey}]</color></size>", 22, TextAnchor.MiddleLeft, UIKit.AccentColor);
            window.titleText.supportRichText = true;

            var close = UIKit.Button(titleBar, "X", 20, window.Close);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1.0f, 0.5f);
            closeRect.pivot = new Vector2(1.0f, 0.5f);
            closeRect.anchoredPosition = new Vector2(-8.0f, 0.0f);
            closeRect.sizeDelta = new Vector2(36.0f, 32.0f);

            // 본문 (스크롤)
            var body = UIKit.Rect(root, "Body", Vector2.zero, Vector2.one, new Vector2(10.0f, 58.0f), new Vector2(-10.0f, -54.0f));
            UIKit.Image(body, new Color(0.0f, 0.0f, 0.0f, 0.25f));
            RectTransform content;
            UIKit.ScrollArea(body, out content);
            window.Content = content;

            // 바닥 줄
            var footer = UIKit.Rect(root, "Footer", new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(10.0f, 8.0f), new Vector2(-10.0f, 50.0f));
            var footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 8.0f;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = false;
            window.Footer = footer;
            if (InOnApply != null)
            {
                UIKit.RowButton(footer, "적용 + 저장", 150, InOnApply);
            }

            window.Status = UIKit.RowLabel(footer, string.Empty, 0, 16);
            root.gameObject.SetActive(false);
            return window;
        }

        public void Open()
        {
            gameObject.SetActive(true);
            Root.SetAsLastSibling();
            onOpened?.Invoke();
            Refresh();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            TooltipView.Hide();
        }

        public void Toggle()
        {
            if (IsOpen == true)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Refresh()
        {
            if (IsOpen == false)
            {
                return;
            }

            for (int i = Content.childCount - 1; i >= 0; --i)
            {
                ObjectUtil.SafeDestroy(Content.GetChild(i).gameObject);
            }

            drawer?.Invoke(this);
        }

        public void OnPointerDown(PointerEventData InEventData)
        {
            Root.SetAsLastSibling();
        }
    }

    /// <summary>제목 표시줄을 끌면 창을 옮긴다. 창이 화면 밖으로 완전히 나가지 않게 막는다.</summary>
    public class WindowDragHandle : MonoBehaviour, IDragHandler, IBeginDragHandler
    {
        public RectTransform Target;
        private Canvas canvas;

        public void OnBeginDrag(PointerEventData InEventData)
        {
            canvas = GetComponentInParent<Canvas>();
            Target.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData InEventData)
        {
            if (Target == null)
            {
                return;
            }

            float scale = canvas != null ? canvas.scaleFactor : 1.0f;
            var position = Target.anchoredPosition + InEventData.delta / Mathf.Max(0.01f, scale);
            var parent = (RectTransform)Target.parent;
            var half = parent.rect.size * 0.5f;
            var size = Target.rect.size;
            // 최소 80px 는 화면 안에 남긴다.
            position.x = Mathf.Clamp(position.x, -half.x - size.x * 0.5f + 80.0f, half.x + size.x * 0.5f - 80.0f);
            position.y = Mathf.Clamp(position.y, -half.y + size.y * 0.5f - 20.0f, half.y - 23.0f);
            Target.anchoredPosition = position;
        }
    }
}
