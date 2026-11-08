using DemoLA.Player;
using Framework;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace DemoLA.UI
{
    /// <summary>
    /// 모바일 가상 조작 (구현 보류 사항 답변 8): 조이패드 왼쪽 하단 · 스킬 버튼 오른쪽 상단 · 대쉬 중앙 하단 · 대상은 탭으로 지정.
    /// Input System 의 OnScreenStick / OnScreenButton 이 가상 게임패드를 누르므로 InputActionAsset 의 게임패드 바인딩을 그대로 쓴다.
    /// 모바일 플랫폼에서 자동으로 켜지고, PC 에서는 F9 로 켜고 끌 수 있다 (확인용).
    /// </summary>
    public class MobileControls : BaseBehaviour
    {
        private static readonly (string Label, string Path)[] SkillButtons =
        {
            ("Q", "<Gamepad>/buttonWest"), ("W", "<Gamepad>/buttonNorth"), ("E", "<Gamepad>/buttonSouth"), ("R", "<Gamepad>/rightShoulder"),
            ("A", "<Gamepad>/leftShoulder"), ("S", "<Gamepad>/rightTrigger"), ("D", "<Gamepad>/leftTrigger"), ("F", "<Gamepad>/dpad/up"),
            ("각성", "<Gamepad>/dpad/down"), ("고유", "<Gamepad>/rightStickPress"), ("물약", "<Gamepad>/dpad/left"), ("시정", "<Gamepad>/dpad/right"),
        };

        private Canvas canvas;

        public bool IsShown
        {
            get { return canvas != null && canvas.gameObject.activeSelf == true; }
        }

        public void Build()
        {
            if (canvas != null)
            {
                return;
            }

            canvas = UIKit.CreateCanvas("MobileControls", 15);
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;

            // 조이패드: 왼쪽 하단
            var stickBase = UIKit.Anchored(root, "StickBase", new Vector2(0.0f, 0.0f), new Vector2(60.0f, 60.0f), new Vector2(260.0f, 260.0f));
            UIKit.Image(stickBase, new Color(1.0f, 1.0f, 1.0f, 0.12f));
            var knob = UIKit.Anchored(stickBase, "Knob", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110.0f, 110.0f));
            knob.pivot = new Vector2(0.5f, 0.5f);
            UIKit.Image(knob, new Color(1.0f, 1.0f, 1.0f, 0.35f), true);
            var stick = addControl<OnScreenStick>(knob.gameObject, "<Gamepad>/leftStick");
            stick.movementRange = 90.0f;

            // 스킬 버튼: 오른쪽 상단 (4열 그리드)
            for (int i = 0; i < SkillButtons.Length; ++i)
            {
                int column = i % 4;
                int row = i / 4;
                var rect = UIKit.Anchored(root, $"Btn_{SkillButtons[i].Label}", new Vector2(1.0f, 1.0f), new Vector2(-30.0f - (3 - column) * 104.0f, -150.0f - row * 104.0f), new Vector2(92.0f, 92.0f));
                rect.pivot = new Vector2(1.0f, 1.0f);
                UIKit.Image(rect, new Color(0.2f, 0.25f, 0.35f, 0.75f), true);
                UIKit.Label(rect, SkillButtons[i].Label, 24, TextAnchor.MiddleCenter, Color.white);
                addControl<OnScreenButton>(rect.gameObject, SkillButtons[i].Path);
            }

            // 대쉬: 중앙 하단
            var dash = UIKit.Anchored(root, "Dash", new Vector2(0.5f, 0.0f), new Vector2(0.0f, 210.0f), new Vector2(150.0f, 80.0f));
            dash.pivot = new Vector2(0.5f, 0.0f);
            UIKit.Image(dash, new Color(0.35f, 0.3f, 0.2f, 0.8f), true);
            UIKit.Label(dash, "대쉬", 26, TextAnchor.MiddleCenter, Color.white);
            addControl<OnScreenButton>(dash.gameObject, "<Gamepad>/buttonEast");

            SetShown(Application.isMobilePlatform == true);
        }

        public void SetShown(bool InShown)
        {
            if (canvas != null)
            {
                canvas.gameObject.SetActive(InShown);
            }
        }

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame == true)
            {
                SetShown(IsShown == false);
            }
        }

        /// <summary>경로를 먼저 넣은 뒤 켠다 (빈 경로로 OnEnable 되면 장치를 만들지 못한다).</summary>
        private static T addControl<T>(GameObject InGameObject, string InPath) where T : OnScreenControl
        {
            bool wasActive = InGameObject.activeSelf;
            InGameObject.SetActive(false);
            var control = InGameObject.AddComponent<T>();
            control.controlPath = InPath;
            InGameObject.SetActive(wasActive);
            return control;
        }
    }
}
