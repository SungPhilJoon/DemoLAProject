using System;
using System.Collections.Generic;
using DemoLA.World;
using Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DemoLA.UI
{
    /// <summary>
    /// 레이드 입장 대기실 창. 관문 선택 · 파티원 준비 상태 · 준비 완료 버튼 · 입장 카운트다운.
    /// Enter 로도 준비/취소. 설정 창(P · 단축키)은 대기실에서 자유롭게 연다.
    /// </summary>
    public class LobbyView : BaseBehaviour
    {
        private class Row
        {
            public Image Swatch;
            public Text Name;
            public Text State;
            public Image Background;
        }

        private Canvas canvas;
        private RectTransform panel;
        private Text gateText;
        private Text statusText;
        private Button readyButton;
        private Image readyImage;
        private RectTransform listRoot;
        private readonly List<Row> rows = new List<Row>();
        private RaidLobby lobby;
        private Func<string> gateLabel;
        private Action<int> onGateStep;
        private Action onOpenSettings;

        public void Build(RaidLobby InLobby, Func<string> InGateLabel, Action<int> InOnGateStep, Action InOnOpenSettings)
        {
            lobby = InLobby;
            gateLabel = InGateLabel;
            onGateStep = InOnGateStep;
            onOpenSettings = InOnOpenSettings;
            if (canvas != null)
            {
                return;
            }

            canvas = UIKit.CreateCanvas("Lobby", 15);
            canvas.transform.SetParent(transform, false);
            panel = UIKit.Anchored(canvas.transform, "Panel", new Vector2(0.5f, 1.0f), new Vector2(0.0f, -12.0f), new Vector2(640.0f, 370.0f));
            UIKit.Image(panel, UIKit.PanelColor, true);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.8f, 0.65f, 0.35f, 0.5f);
            outline.effectDistance = new Vector2(2.0f, -2.0f);

            var title = UIKit.Rect(panel, "Title", new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(16.0f, -50.0f), new Vector2(-16.0f, -8.0f));
            UIKit.Label(title, "레이드 입장 대기실", 26, TextAnchor.MiddleLeft, UIKit.AccentColor);

            // 관문 선택
            var gateRow = UIKit.Rect(panel, "Gate", new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(16.0f, -98.0f), new Vector2(-16.0f, -56.0f));
            UIKit.Image(gateRow, UIKit.RowColor);
            var gateLayout = gateRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            gateLayout.spacing = 6.0f;
            gateLayout.padding = new RectOffset(8, 8, 4, 4);
            gateLayout.childControlWidth = true;
            gateLayout.childControlHeight = true;
            gateLayout.childForceExpandWidth = false;
            gateLayout.childAlignment = TextAnchor.MiddleLeft;
            UIKit.RowLabel(gateRow, "관문", 70, 18);
            UIKit.RowButton(gateRow, "◀", 40, () => onGateStep?.Invoke(-1));
            gateText = UIKit.RowLabel(gateRow, string.Empty, 0, 20);
            gateText.alignment = TextAnchor.MiddleCenter;
            gateText.color = UIKit.AccentColor;
            UIKit.RowButton(gateRow, "▶", 40, () => onGateStep?.Invoke(1));

            // 파티원 목록
            listRoot = UIKit.Rect(panel, "Party", new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(16.0f, -262.0f), new Vector2(-16.0f, -104.0f));
            var listLayout = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 4.0f;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;

            // 상태 · 버튼
            var statusRect = UIKit.Rect(panel, "Status", new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(16.0f, 62.0f), new Vector2(-16.0f, 100.0f));
            statusText = UIKit.Label(statusRect, string.Empty, 20, TextAnchor.MiddleCenter, Color.white);

            var buttons = UIKit.Rect(panel, "Buttons", new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(16.0f, 10.0f), new Vector2(-16.0f, 56.0f));
            var buttonLayout = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 10.0f;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = true;
            UIKit.Button(buttons, "캐릭터 설정 (P)", 20, () => onOpenSettings?.Invoke());
            readyButton = UIKit.Button(buttons, string.Empty, 22, toggleReady);
            readyImage = readyButton.GetComponent<Image>();

            InLobby.OnChanged += refresh;
            refresh();
        }

        public void SetVisible(bool InVisible)
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(InVisible);
            }

            if (InVisible == true)
            {
                refresh();
            }
        }

        private void toggleReady()
        {
            if (lobby != null && lobby.IsActive == true)
            {
                lobby.SetPlayerReady(lobby.IsPlayerReady == false);
            }
        }

        private void Update()
        {
            if (lobby == null || panel == null || panel.gameObject.activeSelf == false || lobby.IsActive == false)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame == true || keyboard.numpadEnterKey.wasPressedThisFrame == true))
            {
                toggleReady();
            }

            drawStatus();
        }

        private void refresh()
        {
            if (panel == null || lobby == null)
            {
                return;
            }

            gateText.text = gateLabel != null ? gateLabel() : string.Empty;
            var members = lobby.Members;
            while (rows.Count < members.Count)
            {
                rows.Add(createRow());
            }

            for (int i = 0; i < rows.Count; ++i)
            {
                bool active = i < members.Count;
                rows[i].Background.gameObject.SetActive(active);
                if (active == false)
                {
                    continue;
                }

                var member = members[i];
                var character = member.Character;
                var row = rows[i];
                row.Swatch.color = character != null ? character.ClassDefinition.BodyColor : Color.gray;
                row.Name.text = character != null ? (member.IsPlayer ? $"{character.ClassDefinition.DisplayName}  (나)" : $"{character.ClassDefinition.DisplayName}  (파티원)") : "-";
                row.State.text = member.Ready ? "준비 완료" : "준비 중...";
                row.State.color = member.Ready ? new Color(0.45f, 1.0f, 0.5f) : new Color(0.75f, 0.75f, 0.75f);
                row.Background.color = member.IsPlayer ? new Color(UIKit.AccentColor.r, UIKit.AccentColor.g, UIKit.AccentColor.b, 0.12f) : UIKit.RowColor;
            }

            bool ready = lobby.IsPlayerReady;
            UIKit.SetButtonText(readyButton, ready ? "준비 취소 (Enter)" : "준비 완료 (Enter)");
            readyImage.color = ready ? new Color(0.45f, 0.25f, 0.2f) : new Color(0.2f, 0.45f, 0.25f);
            drawStatus();
        }

        private void drawStatus()
        {
            if (lobby.Countdown >= 0.0f)
            {
                statusText.text = $"모든 파티원 준비 완료 — {Mathf.CeilToInt(lobby.Countdown)}초 후 입장";
                statusText.color = new Color(1.0f, 0.85f, 0.4f);
                return;
            }

            statusText.text = lobby.IsPlayerReady
                ? $"다른 파티원을 기다리는 중 ({lobby.ReadyCount}/{lobby.Members.Count})"
                : $"설정을 마치고 준비 완료를 누르세요 ({lobby.ReadyCount}/{lobby.Members.Count})";
            statusText.color = Color.white;
        }

        private Row createRow()
        {
            var row = new Row();
            var layout = UIKit.HorizontalRow(listRoot, 36, 10);
            row.Background = layout.GetComponent<Image>();
            var swatchRect = (RectTransform)new GameObject("Swatch", typeof(RectTransform)).transform;
            swatchRect.SetParent(layout.transform, false);
            var element = swatchRect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = 28.0f;
            row.Swatch = swatchRect.gameObject.AddComponent<Image>();
            row.Name = UIKit.RowLabel(layout.transform, string.Empty, 0, 19);
            row.State = UIKit.RowLabel(layout.transform, string.Empty, 140, 19);
            row.State.alignment = TextAnchor.MiddleRight;
            return row;
        }
    }
}
