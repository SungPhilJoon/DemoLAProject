using DemoLA.Boss;
using DemoLA.Core;
using DemoLA.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if DEMOLA_CINEMACHINE
using Unity.Cinemachine;
#endif

namespace DemoLA.Player
{
    /// <summary>
    /// 보스 진입 시네마틱 (레이드에 처음 들어올 때). BossDefinition.PlayIntro · IntroDuration · IntroLines 로 정한다.
    ///  샷 A (0 ~ 2.5초)  경기장 전경을 돌며 보스를 비춘다 + 보스 이름 · 관문 자막
    ///  샷 B (2.5 ~ 6.9초) 보스 정면 아래에서 천천히 다가가며 올려다본다 (대사)
    ///  샷 C (6.9초 ~ 끝)  플레이어 어깨 뒤에서 보스를 본다 (마지막 대사) → 쿼터뷰로 블렌드 복귀
    /// 레터박스 · 자막은 uGUI, 카메라는 우선순위 높은 CinemachineCamera 를 직접 움직인다 (CinemachineBrain 이 쿼터뷰와 블렌드).
    /// 연출 동안 CinematicHold 로 AI · 입력을 멈추고, 보스 첫 패턴을 연출 길이만큼 미룬다. Space · Esc = 건너뛰기.
    /// 네트워크: 서버가 NetworkRaidDirector.IntroRpc 로 모든 피어에서 같이 재생한다 (AI 정지는 서버 기준).
    /// </summary>
    public class BossIntroCinematic : MonoBehaviour
    {
        private const float ShotA = 2.5f;
        private const float ShotB = 6.9f;
        private const float BarHeight = 120.0f;

        private BossActor boss;
        private Transform player;
        private BossDefinition definition;
        private float elapsed;
        private float duration;
        private Vector3 arenaCenter;
        private float baseYaw;

        private Canvas canvas;
        private readonly System.Collections.Generic.List<Canvas> hiddenCanvases = new System.Collections.Generic.List<Canvas>();
        private RectTransform topBar;
        private RectTransform bottomBar;
        private Text title;
        private Text subtitle;
        private Text speaker;
        private Text line;
#if DEMOLA_CINEMACHINE
        private CinemachineCamera shotCamera;
#endif

        public static BossIntroCinematic Current { get; private set; }

        public static bool IsPlaying
        {
            get { return Current != null; }
        }

        /// <summary>InBoss 의 진입 연출 (InPlayer = 내 캐릭터, 마지막 샷의 어깨 기준. null 이면 보스 앞).</summary>
        public static void Play(BossActor InBoss, Transform InPlayer)
        {
            if (InBoss == null || InBoss.Definition == null || InBoss.Definition.PlayIntro == false)
            {
                return;
            }

            if (Current != null)
            {
                Current.finish();
            }

            float duration = Mathf.Max(3.0f, InBoss.Definition.IntroDuration);
            CinematicHold.Hold(duration);
            if (NetAuthority.IsReplica == false)
            {
                InBoss.DelayFirstPattern(duration);
            }

            var go = new GameObject("BossIntroCinematic");
            Current = go.AddComponent<BossIntroCinematic>();
            Current.begin(InBoss, InPlayer, duration);
        }

        private void begin(BossActor InBoss, Transform InPlayer, float InDuration)
        {
            boss = InBoss;
            player = InPlayer;
            definition = InBoss.Definition;
            duration = InDuration;
            arenaCenter = DemoLA.World.ArenaFloor.Current != null ? DemoLA.World.ArenaFloor.Current.Center : InBoss.Position;
            baseYaw = InBoss.transform.eulerAngles.y;
            if (Application.isBatchMode == true)
            {
                return;
            }

            hideGameUi();
            buildUi();
#if DEMOLA_CINEMACHINE
            if (Camera.main != null && Camera.main.GetComponent<CinemachineBrain>() != null)
            {
                var cameraGo = new GameObject("CM BossIntro");
                cameraGo.transform.SetParent(transform, false);
                shotCamera = cameraGo.AddComponent<CinemachineCamera>();
                var lens = shotCamera.Lens;
                lens.FieldOfView = 42.0f;
                shotCamera.Lens = lens;
                shotCamera.Priority = 60;
                placeCamera(0.0f);
            }
#endif
        }

        /// <summary>연출 동안 HUD · 메뉴 등 다른 화면 UI 를 숨긴다 (끝나면 되살린다).</summary>
        private void hideGameUi()
        {
            foreach (var other in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (other != null && other.isRootCanvas == true && other.enabled == true && other.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    other.enabled = false;
                    hiddenCanvases.Add(other);
                }
            }
        }

        private void restoreGameUi()
        {
            for (int i = 0; i < hiddenCanvases.Count; ++i)
            {
                if (hiddenCanvases[i] != null)
                {
                    hiddenCanvases[i].enabled = true;
                }
            }

            hiddenCanvases.Clear();
        }

        private void buildUi()
        {
            canvas = UIKit.CreateCanvas("BossIntroUI", 900);
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;
            topBar = UIKit.Rect(root, "TopBar", new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), Vector2.zero, Vector2.zero);
            UIKit.Image(topBar, Color.black, true);
            bottomBar = UIKit.Rect(root, "BottomBar", new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), Vector2.zero, Vector2.zero);
            UIKit.Image(bottomBar, Color.black, true);

            var titleRect = UIKit.Rect(root, "Title", new Vector2(0.06f, 0.0f), new Vector2(0.7f, 0.0f), new Vector2(0.0f, BarHeight + 40.0f), new Vector2(0.0f, BarHeight + 120.0f));
            title = UIKit.Label(titleRect, definition.DisplayName, 58, TextAnchor.LowerLeft, new Color(1.0f, 0.86f, 0.55f, 0.0f));
            title.fontStyle = FontStyle.Bold;
            title.gameObject.AddComponent<Outline>().effectColor = new Color(0.0f, 0.0f, 0.0f, 0.8f);
            var subtitleRect = UIKit.Rect(root, "Subtitle", new Vector2(0.06f, 0.0f), new Vector2(0.7f, 0.0f), new Vector2(0.0f, BarHeight + 8.0f), new Vector2(0.0f, BarHeight + 44.0f));
            subtitle = UIKit.Label(subtitleRect, definition.IntroSubtitle ?? string.Empty, 24, TextAnchor.LowerLeft, new Color(1.0f, 1.0f, 1.0f, 0.0f));

            var speakerRect = UIKit.Rect(root, "Speaker", new Vector2(0.15f, 0.0f), new Vector2(0.85f, 0.0f), new Vector2(0.0f, 66.0f), new Vector2(0.0f, 100.0f));
            speaker = UIKit.Label(speakerRect, string.Empty, 20, TextAnchor.MiddleCenter, UIKit.AccentColor);
            var lineRect = UIKit.Rect(root, "Line", new Vector2(0.1f, 0.0f), new Vector2(0.9f, 0.0f), new Vector2(0.0f, 14.0f), new Vector2(0.0f, 66.0f));
            line = UIKit.Label(lineRect, string.Empty, 28, TextAnchor.MiddleCenter, Color.white);

            var skipRect = UIKit.Rect(root, "Skip", new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(-260.0f, -BarHeight + 12.0f), new Vector2(-24.0f, -BarHeight + 44.0f));
            UIKit.Label(skipRect, "Space · Esc  건너뛰기", 16, TextAnchor.MiddleRight, new Color(1.0f, 1.0f, 1.0f, 0.55f));
        }

        private void LateUpdate()
        {
            elapsed += Time.deltaTime;
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame == true || keyboard.escapeKey.wasPressedThisFrame == true))
            {
                skip();
                return;
            }

            if (boss == null || elapsed >= duration)
            {
                finish();
                return;
            }

            if (canvas != null)
            {
                updateUi();
            }

            placeCamera(elapsed);
        }

        private void updateUi()
        {
            // 레터박스: 0.5초 동안 들어오고 마지막 0.6초 동안 나간다.
            float inT = Mathf.Clamp01(elapsed / 0.5f);
            float outT = Mathf.Clamp01((duration - elapsed) / 0.6f);
            float bar = BarHeight * Mathf.Min(inT, outT);
            topBar.offsetMin = new Vector2(0.0f, -bar);
            bottomBar.offsetMax = new Vector2(0.0f, bar);

            // 보스 이름 · 관문: 샷 A 동안.
            float titleAlpha = Mathf.Clamp01((elapsed - 0.4f) / 0.4f) * Mathf.Clamp01((ShotA - elapsed) / 0.4f);
            title.color = new Color(title.color.r, title.color.g, title.color.b, titleAlpha);
            subtitle.color = new Color(1.0f, 1.0f, 1.0f, titleAlpha * 0.85f);

            // 대사 자막.
            string who = string.Empty;
            string text = string.Empty;
            float alpha = 0.0f;
            var lines = definition.IntroLines;
            for (int i = 0; i < lines.Count; ++i)
            {
                var entry = lines[i];
                float local = elapsed - entry.Start;
                if (local >= 0.0f && local <= entry.Duration)
                {
                    who = entry.Speaker;
                    text = entry.Text;
                    alpha = Mathf.Clamp01(local / 0.25f) * Mathf.Clamp01((entry.Duration - local) / 0.25f);
                }
            }

            speaker.text = who;
            line.text = text;
            speaker.color = new Color(speaker.color.r, speaker.color.g, speaker.color.b, alpha);
            line.color = new Color(1.0f, 1.0f, 1.0f, alpha);
        }

        /// <summary>시각 InTime 의 카메라 위치 · 시선.</summary>
        private void placeCamera(float InTime)
        {
#if DEMOLA_CINEMACHINE
            if (shotCamera == null || boss == null)
            {
                return;
            }

            var bossPos = boss.Position;
            var forward = Quaternion.Euler(0.0f, baseYaw, 0.0f) * Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            float head = Mathf.Max(2.5f, boss.Radius * 2.2f);
            Vector3 position;
            Vector3 look;
            if (InTime < ShotA)
            {
                // 샷 A: 경기장 위를 크게 돌며 보스를 내려다본다.
                float t = InTime / ShotA;
                float yaw = baseYaw + Mathf.Lerp(150.0f, 105.0f, smooth(t));
                position = arenaCenter + Quaternion.Euler(0.0f, yaw, 0.0f) * Vector3.forward * 30.0f + Vector3.up * Mathf.Lerp(16.0f, 11.0f, t);
                look = bossPos + Vector3.up * head * 0.6f;
            }
            else if (InTime < ShotB)
            {
                // 샷 B: 보스 정면 아래에서 천천히 다가가며 올려다본다.
                float t = smooth((InTime - ShotA) / (ShotB - ShotA));
                position = bossPos + forward * Mathf.Lerp(10.0f, 6.5f, t) + right * Mathf.Lerp(-1.5f, 1.2f, t) + Vector3.up * Mathf.Lerp(0.9f, 1.4f, t);
                look = bossPos + Vector3.up * head;
            }
            else
            {
                // 샷 C: 플레이어 어깨 뒤에서 보스를 본다.
                float t = smooth((InTime - ShotB) / Mathf.Max(0.1f, duration - ShotB));
                var anchor = player != null ? player.position : bossPos + forward * 12.0f;
                var toBoss = DemoLA.Combat.HitGeometry.Flat(bossPos - anchor);
                var dir = toBoss.sqrMagnitude > 0.01f ? toBoss.normalized : -forward;
                var side = Vector3.Cross(Vector3.up, dir);
                position = anchor - dir * Mathf.Lerp(6.5f, 8.0f, t) + side * 1.8f + Vector3.up * Mathf.Lerp(3.2f, 4.0f, t);
                look = bossPos + Vector3.up * head * 0.7f;
            }

            shotCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position, Vector3.up));
#endif
        }

        private static float smooth(float InT)
        {
            float t = Mathf.Clamp01(InT);
            return t * t * (3.0f - 2.0f * t);
        }

        private void skip()
        {
            if (boss != null && NetAuthority.IsReplica == false)
            {
                boss.DelayFirstPattern(0.0f);
            }

            finish();
        }

        private void OnDestroy()
        {
            // 씬이 내려가는 등 연출이 끝나지 않고 사라져도 정지는 풀어 둔다.
            restoreGameUi();
            if (Current == this)
            {
                Current = null;
                CinematicHold.Release();
            }
        }

        private void finish()
        {
            restoreGameUi();
            CinematicHold.Release();
            if (Current == this)
            {
                Current = null;
            }

            Destroy(gameObject);
        }
    }
}
