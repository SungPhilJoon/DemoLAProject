using System;
using Cysharp.Threading.Tasks;
using DemoLA.Core;
using DemoLA.UI;
using Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DemoLA.Network
{
    /// <summary>
    /// Relay 로비 창 (F7). 호스트 → 참가 코드 공유 → 친구가 코드로 참가 → 호스트가 "레이드 시작".
    /// 빈 자리는 AI 아군이 채운다. 나가면 오프라인 레이드로 돌아간다.
    /// GameBootstrap 이 있는 씬에 자동으로 붙는다 (RuntimeInitializeOnLoadMethod).
    /// </summary>
    public class NetworkLobbyView : BaseBehaviour
    {
        private Canvas canvas;
        private Text status;
        private InputField codeInput;
        private InputField addressInput;
        private bool busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void autoCreate()
        {
            if (FindAnyObjectByType<GameBootstrap>() != null && FindAnyObjectByType<NetworkLobbyView>() == null)
            {
                new GameObject("[NetworkLobby]").AddComponent<NetworkLobbyView>();
            }
        }

        protected override void OnUnityAwake()
        {
            canvas = UIKit.CreateCanvas("NetworkLobby", 25);
            canvas.transform.SetParent(transform, false);
            var panel = UIKit.Anchored(canvas.transform, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560.0f, 340.0f));
            panel.pivot = new Vector2(0.5f, 0.5f);
            UIKit.Image(panel, UIKit.PanelColor, true);
            var list = UIKit.Rect(panel, "List", Vector2.zero, Vector2.one, new Vector2(16.0f, 16.0f), new Vector2(-16.0f, -16.0f));
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8.0f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            var title = UIKit.RowLabel(list, "멀티플레이 (Relay · LAN) — F7 닫기", 0, 22);
            title.color = UIKit.AccentColor;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 36.0f;

            var hostRow = UIKit.HorizontalRow(list, 44, 8).transform;
            UIKit.RowButton(hostRow, "호스트 (참가 코드 생성)", 260, () => run(host));
            UIKit.RowButton(hostRow, "레이드 시작", 150, startRaid);

            var joinRow = UIKit.HorizontalRow(list, 44, 8).transform;
            codeInput = createInput(joinRow, 200);
            UIKit.RowButton(joinRow, "코드로 참가", 150, () => run(join));
            UIKit.RowButton(joinRow, "나가기", 100, () => run(leave));

            // LAN 직접 접속 (Relay 없이 같은 PC / 같은 네트워크 — 테스트 · 개발용, NetDirectConnect). 시작은 위 "레이드 시작".
            var lanRow = UIKit.HorizontalRow(list, 44, 8).transform;
            UIKit.RowButton(lanRow, "LAN 호스트 (7777)", 180, () => status.text = Testing.NetDirectConnect.StartHost(7777) ? "LAN 호스트 시작 — 다른 창에서 LAN 참가 후 '레이드 시작'" : "LAN 호스트 실패");
            addressInput = createInput(lanRow, 160);
            addressInput.characterValidation = InputField.CharacterValidation.None;
            addressInput.text = "127.0.0.1";
            UIKit.RowButton(lanRow, "LAN 참가", 110, () => status.text = Testing.NetDirectConnect.StartClient(addressInput.text, 7777) ? $"LAN 참가 중 ({addressInput.text}) — 호스트가 레이드를 시작하면 합류" : "LAN 참가 실패");

            status = UIKit.RowLabel(list, "오프라인 — 호스트하거나 코드로 참가하세요. (UGS 프로젝트 연결 필요)", 0, 17);
            status.gameObject.AddComponent<LayoutElement>().preferredHeight = 120.0f;
            canvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f7Key.wasPressedThisFrame == true)
            {
                canvas.gameObject.SetActive(canvas.gameObject.activeSelf == false);
            }
        }

        private async UniTask host()
        {
            string code = await RelaySessionService.HostAsync();
            status.text = $"호스트 중 · 참가 코드: <b>{code}</b>\n친구에게 코드를 알려 주고, 모두 들어오면 '레이드 시작'.";
            status.supportRichText = true;
            GUIUtility.systemCopyBuffer = code;
        }

        private async UniTask join()
        {
            await RelaySessionService.JoinAsync(codeInput.text);
            status.text = $"참가 완료 · {RelaySessionService.JoinCode} — 호스트가 레이드를 시작하면 합류합니다.";
        }

        private async UniTask leave()
        {
            await RelaySessionService.LeaveAsync();
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening == true)
            {
                NetworkManager.Singleton.Shutdown();
            }

            var bootstrap = GameBootstrap.Current;
            if (bootstrap != null)
            {
                bootstrap.NetworkOwnsRaid = false;
                bootstrap.EnterLobby();
            }

            status.text = "오프라인으로 돌아왔습니다.";
        }

        private void startRaid()
        {
            var director = NetworkRaidDirector.Instance;
            var session = Framework.Managers.Get<GameSession>();
            if (director == null || NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer == false)
            {
                status.text = "호스트만 레이드를 시작할 수 있습니다.";
                return;
            }

            director.ServerStartRaid(session != null ? session.Save.SelectedBoss : "Gate1");
            canvas.gameObject.SetActive(false);
        }

        private void run(Func<UniTask> InAction)
        {
            if (busy == true)
            {
                return;
            }

            busy = true;
            status.text = "처리 중...";
            runAsync(InAction).Forget();
        }

        private async UniTaskVoid runAsync(Func<UniTask> InAction)
        {
            try
            {
                await InAction();
            }
            catch (Exception e)
            {
                status.text = $"실패: {e.Message}";
                Debug.LogException(e);
            }
            finally
            {
                busy = false;
            }
        }

        private static InputField createInput(Transform InRow, float InWidth)
        {
            var rect = (RectTransform)new GameObject("Code", typeof(RectTransform)).transform;
            rect.SetParent(InRow, false);
            var background = UIKit.Image(rect, new Color(0.0f, 0.0f, 0.0f, 0.5f), true);
            rect.gameObject.AddComponent<LayoutElement>().preferredWidth = InWidth;
            var text = UIKit.Label(rect, string.Empty, 20, TextAnchor.MiddleLeft, Color.white);
            var placeholder = UIKit.Label(rect, "참가 코드", 20, TextAnchor.MiddleLeft, new Color(1.0f, 1.0f, 1.0f, 0.35f));
            var input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterValidation = InputField.CharacterValidation.Alphanumeric;
            return input;
        }
    }
}
