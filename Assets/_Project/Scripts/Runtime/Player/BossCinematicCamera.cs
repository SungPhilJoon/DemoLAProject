using UnityEngine;
#if DEMOLA_CINEMACHINE
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
#endif

namespace DemoLA.Player
{
    /// <summary>
    /// 보스 도약 시네마틱 (Cinemachine). 쿼터뷰 카메라(QuarterViewCamera)보다 우선순위가 높은 CinemachineCamera 를 잠깐 켜서
    /// 낮은 각도에서 보스를 따라 올려다보고(도약), 끝나면 우선순위를 내려 쿼터뷰로 부드럽게 돌아온다 (CinemachineBrain 블렌드).
    /// BossPatternAbility(CinematicLeap 패턴)가 부르고, 네트워크 클라이언트는 NetworkRaidDirector 의 RPC 로 같은 연출을 본다.
    /// Cinemachine 이 없으면(DEMOLA_CINEMACHINE 미정의) 아무것도 하지 않는다.
    /// </summary>
    public class BossCinematicCamera : MonoBehaviour
    {
        private const int ActivePriority = 50;
        private const float BlendTime = 0.7f;

        private static BossCinematicCamera instance;

        private float remaining;
#if DEMOLA_CINEMACHINE
        private CinemachineCamera cinematic;
        private CinemachineFollow follow;
        private CinemachineRotationComposer composer;
#endif

        public static bool IsPlaying
        {
            get { return instance != null && instance.remaining > 0.0f; }
        }

        /// <summary>InBoss 를 InDuration 초 동안 비춘다 (도약 시간 + 착지 여운).</summary>
        public static void Play(Transform InBoss, float InDuration)
        {
#if DEMOLA_CINEMACHINE
            if (InBoss == null || Camera.main == null)
            {
                return;
            }

            if (instance == null)
            {
                var go = new GameObject("CM BossCinematic");
                instance = go.AddComponent<BossCinematicCamera>();
                instance.build();
            }

            instance.begin(InBoss, InDuration);
#endif
        }

#if DEMOLA_CINEMACHINE
        private void build()
        {
            var brain = Camera.main.GetComponent<CinemachineBrain>();
            if (brain != null)
            {
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, BlendTime);
            }

            cinematic = gameObject.AddComponent<CinemachineCamera>();
            cinematic.Priority = 0;
            var lens = cinematic.Lens;
            lens.FieldOfView = 50.0f;
            cinematic.Lens = lens;
            follow = gameObject.AddComponent<CinemachineFollow>();
            var tracker = follow.TrackerSettings;
            tracker.BindingMode = BindingMode.WorldSpace;
            tracker.PositionDamping = new Vector3(0.6f, 1.2f, 0.6f);
            follow.TrackerSettings = tracker;
            // 낮고 먼 각도: 도약하는 보스를 올려다본다.
            follow.FollowOffset = new Vector3(0.0f, 4.0f, -20.0f);
            composer = gameObject.AddComponent<CinemachineRotationComposer>();
            composer.Damping = new Vector2(0.4f, 0.4f);
        }

        private void begin(Transform InBoss, float InDuration)
        {
            // 위치는 바닥의 보스(루트)를 따라가고, 시선은 포물선으로 떠오르는 몸(Body)을 올려다본다.
            var body = InBoss.Find("Body");
            cinematic.Follow = InBoss;
            cinematic.LookAt = body != null ? body : InBoss;
            transform.position = InBoss.position + follow.FollowOffset;
            cinematic.Priority = ActivePriority;
            remaining = Mathf.Max(0.5f, InDuration);
        }

        private void Update()
        {
            if (remaining <= 0.0f)
            {
                return;
            }

            remaining -= Time.deltaTime;
            if (remaining <= 0.0f)
            {
                cinematic.Priority = 0;
            }
        }
#endif
    }
}
