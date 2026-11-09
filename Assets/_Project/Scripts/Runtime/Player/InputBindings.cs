using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DemoLA.Player
{
    /// <summary>
    /// 입력 설정 (Input System InputActionAsset 기반).
    ///  - 에셋: Assets/_Project/Input/DemoLAControls.inputactions (에디터 셋업이 DefaultBindings 로 처음 한 번 만든다 — 이후엔 에셋이 원본)
    ///  - 키 변경: PerformInteractiveRebinding → 덮어쓰기를 PlayerPrefs 에 JSON 으로 저장
    ///  - 모바일 가상 패드(OnScreenStick/Button)는 게임패드 경로에 붙으므로 같은 액션을 그대로 쓴다.
    /// </summary>
    public class InputBindings
    {
        public const string GameplayMap = "Gameplay";
        public const string AssetPath = "Assets/_Project/Input/DemoLAControls.inputactions";
        private const string OverridesKey = "DemoLA.InputOverrides";

        /// <summary>(액션, 타입, 바인딩들). 게임패드 경로는 모바일 가상 패드와 공유한다.</summary>
        public static readonly (string Action, InputActionType Type, string[] Paths)[] DefaultBindings =
        {
            ("Move", InputActionType.Button, new[] { "<Mouse>/rightButton" }),
            ("Stick", InputActionType.Value, new[] { "<Gamepad>/leftStick" }),
            ("Point", InputActionType.Value, new[] { "<Pointer>/position" }),
            ("Select", InputActionType.Button, new[] { "<Mouse>/leftButton", "<Touchscreen>/primaryTouch/tap" }),
            ("Skill1", InputActionType.Button, new[] { "<Keyboard>/q", "<Gamepad>/buttonWest" }),
            ("Skill2", InputActionType.Button, new[] { "<Keyboard>/w", "<Gamepad>/buttonNorth" }),
            ("Skill3", InputActionType.Button, new[] { "<Keyboard>/e", "<Gamepad>/buttonSouth" }),
            ("Skill4", InputActionType.Button, new[] { "<Keyboard>/r", "<Gamepad>/rightShoulder" }),
            ("Skill5", InputActionType.Button, new[] { "<Keyboard>/a", "<Gamepad>/leftShoulder" }),
            ("Skill6", InputActionType.Button, new[] { "<Keyboard>/s", "<Gamepad>/rightTrigger" }),
            ("Skill7", InputActionType.Button, new[] { "<Keyboard>/d", "<Gamepad>/leftTrigger" }),
            ("Skill8", InputActionType.Button, new[] { "<Keyboard>/f", "<Gamepad>/dpad/up" }),
            ("Awakening", InputActionType.Button, new[] { "<Keyboard>/v", "<Gamepad>/dpad/down" }),
            ("Identity", InputActionType.Button, new[] { "<Keyboard>/z", "<Gamepad>/rightStickPress" }),
            ("Identity2", InputActionType.Button, new[] { "<Keyboard>/x", "<Gamepad>/leftStickPress" }),
            ("Dodge", InputActionType.Button, new[] { "<Keyboard>/space", "<Gamepad>/buttonEast" }),
            ("Item1", InputActionType.Button, new[] { "<Keyboard>/1", "<Gamepad>/dpad/left" }),
            ("Item2", InputActionType.Button, new[] { "<Keyboard>/2", "<Gamepad>/dpad/right" }),
            ("Item3", InputActionType.Button, new[] { "<Keyboard>/3" }),
            ("Item4", InputActionType.Button, new[] { "<Keyboard>/4" }),
            ("Esther1", InputActionType.Button, new[] { "<Keyboard>/5" }),
            ("Esther2", InputActionType.Button, new[] { "<Keyboard>/6" }),
            ("Esther3", InputActionType.Button, new[] { "<Keyboard>/7" }),
            ("BuildWindow", InputActionType.Button, new[] { "<Keyboard>/p", "<Gamepad>/start" }),
            ("DpsMeter", InputActionType.Button, new[] { "<Keyboard>/f8" }),
            ("Zoom", InputActionType.Value, new[] { "<Mouse>/scroll/y" }),
        };

        public static InputBindings Current { get; private set; }

        public InputActionAsset Asset { get; private set; }
        private InputActionMap map;
        private InputActionRebindingExtensions.RebindingOperation rebinding;

        public InputBindings(InputActionAsset InAsset)
        {
            Asset = InAsset != null ? InAsset : CreateDefaultAsset();
            map = Asset.FindActionMap(GameplayMap, true);
            addMissingActions();
            LoadOverrides();
            Current = this;
        }

        /// <summary>
        /// 예전에 저장된 입력 에셋에 새로 생긴 동작(예: Identity2 = X)이 없으면 기본 키로 채운다.
        /// </summary>
        private void addMissingActions()
        {
            bool wasEnabled = map.enabled;
            for (int i = 0; i < DefaultBindings.Length; ++i)
            {
                var entry = DefaultBindings[i];
                if (map.FindAction(entry.Action) != null)
                {
                    continue;
                }

                if (map.enabled == true)
                {
                    map.Disable();
                }

                var action = map.AddAction(entry.Action, entry.Type);
                for (int b = 0; b < entry.Paths.Length; ++b)
                {
                    action.AddBinding(entry.Paths[b]);
                }
            }

            if (wasEnabled == true && map.enabled == false)
            {
                map.Enable();
            }
        }

        /// <summary>DefaultBindings 로 메모리 에셋을 만든다 (에셋이 없을 때 · 에디터 셋업이 파일로 저장할 때).</summary>
        public static InputActionAsset CreateDefaultAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "DemoLAControls";
            var gameplay = asset.AddActionMap(GameplayMap);
            for (int i = 0; i < DefaultBindings.Length; ++i)
            {
                var entry = DefaultBindings[i];
                var action = gameplay.AddAction(entry.Action, entry.Type);
                if (entry.Action == "Stick")
                {
                    action.expectedControlType = "Vector2";
                }
                else if (entry.Action == "Point")
                {
                    action.expectedControlType = "Vector2";
                }
                else if (entry.Action == "Zoom")
                {
                    action.expectedControlType = "Axis";
                }

                for (int b = 0; b < entry.Paths.Length; ++b)
                {
                    action.AddBinding(entry.Paths[b]);
                }
            }

            return asset;
        }

        public InputAction this[string InName]
        {
            get { return map.FindAction(InName, true); }
        }

        public void Enable()
        {
            map.Enable();
        }

        public void Disable()
        {
            map.Disable();
        }

        public void Rebind(InputAction InAction, int InBindingIndex, Action InOnDone)
        {
            rebinding?.Dispose();
            bool wasEnabled = InAction.enabled;
            InAction.Disable();
            rebinding = InAction.PerformInteractiveRebinding(InBindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(InOperation => finish(InAction, wasEnabled, InOnDone))
                .OnCancel(InOperation => finish(InAction, wasEnabled, InOnDone))
                .Start();
        }

        public void ResetBinding(InputAction InAction, int InBindingIndex)
        {
            InAction.RemoveBindingOverride(InBindingIndex);
            SaveOverrides();
        }

        public void SaveOverrides()
        {
            try
            {
                PlayerPrefs.SetString(OverridesKey, Asset.SaveBindingOverridesAsJson());
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Input] 키 설정 저장 실패: {e.Message}");
            }
        }

        public void LoadOverrides()
        {
            string json = PlayerPrefs.GetString(OverridesKey, string.Empty);
            if (string.IsNullOrEmpty(json) == false)
            {
                Asset.LoadBindingOverridesFromJson(json);
            }
        }

        private void finish(InputAction InAction, bool InWasEnabled, Action InOnDone)
        {
            rebinding?.Dispose();
            rebinding = null;
            if (InWasEnabled == true)
            {
                InAction.Enable();
            }

            SaveOverrides();
            InOnDone?.Invoke();
        }
    }
}
