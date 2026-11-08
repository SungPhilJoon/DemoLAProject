using System;
using DemoLA.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DemoLA.UI
{
    /// <summary>끌고 있는 것. Kind 로 받을 수 있는 칸을 가린다.</summary>
    public class DragPayload
    {
        public string Kind;
        public object Data;
        public Sprite Icon;
        /// <summary>어느 칸에도 놓지 않고 놓았을 때 (장착 칸을 밖으로 끌어 해제).</summary>
        public Action OnDroppedOutside;
    }

    /// <summary>
    /// 끌기 시작점 (도감 아이콘 · 장착 칸). 끄는 동안 반투명 아이콘이 마우스를 따라온다.
    /// 끌기 이벤트를 이 아이콘이 받으므로, 아이콘 위에서 끌면 스크롤되지 않는다 (휠 스크롤은 된다).
    /// </summary>
    public class DragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Func<DragPayload> Provider;

        public static DragPayload Current { get; private set; }
        internal static bool DroppedOnTarget;

        private static Canvas layer;
        private static Image ghost;

        public void OnBeginDrag(PointerEventData InEventData)
        {
            if (InEventData.button != PointerEventData.InputButton.Left || Provider == null)
            {
                return;
            }

            Current = Provider();
            if (Current == null)
            {
                return;
            }

            DroppedOnTarget = false;
            TooltipView.Hide();
            ensureGhost();
            ghost.sprite = Current.Icon;
            ghost.gameObject.SetActive(Current.Icon != null);
            follow(InEventData);
        }

        public void OnDrag(PointerEventData InEventData)
        {
            if (Current != null)
            {
                follow(InEventData);
            }
        }

        public void OnEndDrag(PointerEventData InEventData)
        {
            if (Current == null)
            {
                return;
            }

            var payload = Current;
            Current = null;
            if (ghost != null)
            {
                ghost.gameObject.SetActive(false);
            }

            if (DroppedOnTarget == false)
            {
                payload.OnDroppedOutside?.Invoke();
            }
        }

        private static void ensureGhost()
        {
            if (ghost != null)
            {
                return;
            }

            layer = UIKit.CreateCanvas("DragLayer", 180);
            DontDestroyOnLoad(layer.gameObject);
            ObjectUtil.SafeDestroy(layer.GetComponent<GraphicRaycaster>());
            var rect = UIKit.Anchored(layer.transform, "Ghost", Vector2.zero, Vector2.zero, new Vector2(56.0f, 56.0f));
            rect.pivot = new Vector2(0.5f, 0.5f);
            ghost = rect.gameObject.AddComponent<Image>();
            ghost.raycastTarget = false;
            ghost.color = new Color(1.0f, 1.0f, 1.0f, 0.75f);
        }

        private static void follow(PointerEventData InEventData)
        {
            var canvasRect = (RectTransform)layer.transform;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, InEventData.position, null, out local);
            ((RectTransform)ghost.transform).anchoredPosition = local + canvasRect.rect.size * 0.5f;
        }
    }

    /// <summary>놓기 칸. Accepts 가 true 인 것을 끌고 올라오면 테두리가 빛나고, 놓으면 OnDropped.</summary>
    public class DropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Func<DragPayload, bool> Accepts;
        public Action<DragPayload> OnDropped;
        private Outline highlight;

        public void OnPointerEnter(PointerEventData InEventData)
        {
            var payload = DragSource.Current;
            if (payload == null || Accepts == null || Accepts(payload) == false)
            {
                return;
            }

            if (highlight == null)
            {
                highlight = gameObject.AddComponent<Outline>();
                highlight.effectDistance = new Vector2(3.0f, -3.0f);
            }

            highlight.effectColor = new Color(0.4f, 1.0f, 0.5f, 0.95f);
            highlight.enabled = true;
        }

        public void OnPointerExit(PointerEventData InEventData)
        {
            if (highlight != null)
            {
                highlight.enabled = false;
            }
        }

        public void OnDrop(PointerEventData InEventData)
        {
            if (highlight != null)
            {
                highlight.enabled = false;
            }

            var payload = DragSource.Current;
            if (payload == null || Accepts == null || Accepts(payload) == false)
            {
                return;
            }

            DragSource.DroppedOnTarget = true;
            OnDropped?.Invoke(payload);
        }
    }

    /// <summary>우클릭 동작.</summary>
    public class RightClickAction : MonoBehaviour, IPointerClickHandler
    {
        public Action OnRightClick;

        public void OnPointerClick(PointerEventData InEventData)
        {
            if (InEventData.button == PointerEventData.InputButton.Right)
            {
                TooltipView.Hide();
                OnRightClick?.Invoke();
            }
        }
    }

    public static class DragDropExtensions
    {
        public static Image Draggable(this Image InCell, Func<DragPayload> InProvider)
        {
            InCell.gameObject.AddComponent<DragSource>().Provider = InProvider;
            return InCell;
        }

        public static Image Droppable(this Image InCell, Func<DragPayload, bool> InAccepts, Action<DragPayload> InOnDropped)
        {
            var target = InCell.gameObject.AddComponent<DropTarget>();
            target.Accepts = InAccepts;
            target.OnDropped = InOnDropped;
            return InCell;
        }

        public static Image OnRightClick(this Image InCell, Action InAction)
        {
            InCell.gameObject.AddComponent<RightClickAction>().OnRightClick = InAction;
            return InCell;
        }

        /// <summary>장착 표시: 초록 테두리 + 오른쪽 위 "장착".</summary>
        public static Image MarkEquipped(this Image InCell, bool InEquipped)
        {
            if (InEquipped == false)
            {
                return InCell;
            }

            var outline = InCell.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 1.0f, 0.5f, 0.9f);
            outline.effectDistance = new Vector2(2.0f, -2.0f);
            var rect = UIKit.Rect(InCell.transform, "Equipped", Vector2.zero, Vector2.one, new Vector2(2.0f, 2.0f), new Vector2(-2.0f, -2.0f));
            var text = rect.gameObject.AddComponent<Text>();
            text.font = UIKit.Font;
            text.text = "장착";
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.UpperRight;
            text.color = new Color(0.55f, 1.0f, 0.6f);
            text.raycastTarget = false;
            rect.gameObject.AddComponent<Outline>().effectColor = Color.black;
            return InCell;
        }
    }
}
