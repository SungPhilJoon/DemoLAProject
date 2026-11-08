using Framework;
using UnityEngine;
#if DEMOLA_CINEMACHINE
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
#endif

namespace DemoLA.Player
{
    /// <summary>
    /// 쿼터뷰 카메라. 각도 고정 · 회전 없음 · 휠/핀치 줌(거리 배율 MinZoom~MaxZoom).
    /// Cinemachine 3 이 있으면 CinemachineCamera + CinemachineFollow(월드 고정 오프셋)로, 없으면 같은 규칙의 단순 추적.
    /// 카메라와 캐릭터 사이 구조물은 프레임워크 CameraOcclusionSystem(FadeOcclusionResponse)이 반투명 페이드한다.
    /// </summary>
    public class QuarterViewCamera : BaseBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0.0f, 17.0f, -12.0f);
        [SerializeField] private float pitch = 55.0f;
        [SerializeField] private float fieldOfView = 40.0f;
        [SerializeField] private float damping = 0.25f;
        [SerializeField] private float minZoom = 0.7f;
        [SerializeField] private float maxZoom = 1.2f;
        [SerializeField] private float zoomStep = 0.08f;

        private Transform target;
        private Camera mainCamera;
        private float zoom = 1.0f;
        private CameraOcclusionSystem occlusion;
#if DEMOLA_CINEMACHINE
        private CinemachineCamera virtualCamera;
        private CinemachineFollow follow;
        private CinemachineCameraOffset shakeOffset;
#endif

        private static float shakeAmplitude;
        private static float shakeDuration;
        private static float shakeElapsed;
        private Vector3 currentShake;

        /// <summary>카메라 흔들림 (보스 충돌 등). 진행 중인 흔들림보다 강하면 덮어쓴다.</summary>
        public static void Shake(float InAmplitude, float InDuration)
        {
            float remaining = shakeDuration > 0.0f ? shakeAmplitude * (1.0f - shakeElapsed / shakeDuration) : 0.0f;
            if (InAmplitude < remaining)
            {
                return;
            }

            shakeAmplitude = InAmplitude;
            shakeDuration = Mathf.Max(0.01f, InDuration);
            shakeElapsed = 0.0f;
        }

        private void Update()
        {
            currentShake = Vector3.zero;
            if (shakeDuration > 0.0f)
            {
                shakeElapsed += Time.deltaTime;
                float t = shakeElapsed / shakeDuration;
                if (t >= 1.0f)
                {
                    shakeDuration = 0.0f;
                }
                else
                {
                    // 감쇠하는 노이즈 흔들림.
                    float amp = shakeAmplitude * (1.0f - t) * (1.0f - t);
                    float time = Time.time * 35.0f;
                    currentShake = new Vector3(Mathf.PerlinNoise(time, 0.0f) - 0.5f, Mathf.PerlinNoise(0.0f, time) - 0.5f, 0.0f) * (2.0f * amp);
                }
            }
#if DEMOLA_CINEMACHINE
            if (shakeOffset != null)
            {
                shakeOffset.Offset = currentShake;
            }
#endif
        }

        public float Zoom
        {
            get { return zoom; }
        }

        protected override void OnUnityAwake()
        {
            PlayerController.OnZoom += handleZoom;
        }

        protected override void OnUnityDestroy()
        {
            PlayerController.OnZoom -= handleZoom;
        }

        public void SetTarget(Transform InTarget)
        {
            target = InTarget;
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                mainCamera = go.AddComponent<Camera>();
            }

            mainCamera.fieldOfView = fieldOfView;
            occlusion = mainCamera.GetComponent<CameraOcclusionSystem>();
            if (occlusion == null)
            {
                occlusion = mainCamera.gameObject.AddComponent<CameraOcclusionSystem>();
            }

            occlusion.SetTarget(InTarget);
            var rotation = Quaternion.Euler(pitch, 0.0f, 0.0f);
#if DEMOLA_CINEMACHINE
            if (mainCamera.GetComponent<CinemachineBrain>() == null)
            {
                mainCamera.gameObject.AddComponent<CinemachineBrain>();
            }

            if (virtualCamera == null)
            {
                var go = new GameObject("CM QuarterView");
                go.transform.SetParent(transform, false);
                virtualCamera = go.AddComponent<CinemachineCamera>();
                follow = go.AddComponent<CinemachineFollow>();
                shakeOffset = go.AddComponent<CinemachineCameraOffset>();
                var tracker = follow.TrackerSettings;
                tracker.BindingMode = BindingMode.WorldSpace;
                tracker.PositionDamping = Vector3.one * damping;
                follow.TrackerSettings = tracker;
                var lens = virtualCamera.Lens;
                lens.FieldOfView = fieldOfView;
                virtualCamera.Lens = lens;
            }

            follow.FollowOffset = offset * zoom;
            virtualCamera.transform.SetPositionAndRotation(InTarget.position + offset * zoom, rotation);
            virtualCamera.Follow = InTarget;
#else
            mainCamera.transform.SetPositionAndRotation(InTarget.position + offset * zoom, rotation);
#endif
        }

        private void handleZoom(float InDirection)
        {
            zoom = Mathf.Clamp(zoom - InDirection * zoomStep, minZoom, maxZoom);
#if DEMOLA_CINEMACHINE
            if (follow != null)
            {
                follow.FollowOffset = offset * zoom;
            }
#endif
        }

#if !DEMOLA_CINEMACHINE
        private void LateUpdate()
        {
            if (target == null || mainCamera == null)
            {
                return;
            }

            var desired = target.position + offset * zoom;
            float t = damping <= 0.0f ? 1.0f : 1.0f - Mathf.Exp(-Time.deltaTime / damping);
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, desired, t);
            mainCamera.transform.rotation = Quaternion.Euler(pitch, 0.0f, 0.0f);
            mainCamera.transform.position += mainCamera.transform.rotation * currentShake;
        }
#endif
    }
}
