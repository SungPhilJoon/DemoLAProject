using UnityEngine;

namespace DemoLA.Character
{
    /// <summary>
    /// 캐릭터 모델의 Animator 에 이동 속도("Speed", m/s)를 넘긴다.
    /// 속도는 루트 위치 변화로 계산한다 — NavMeshAgent 이동 · 직접 이동 · 대시 · 네트워크 보간 모두 같은 값이 나온다.
    /// </summary>
    public class CharacterAnimatorDriver : MonoBehaviour
    {
        public static readonly int SpeedId = Animator.StringToHash("Speed");

        [Tooltip("속도 변화 평활 시간 (초)")]
        [SerializeField] private float smoothTime = 0.08f;

        private Animator animator;
        private Vector3 lastPosition;
        private float speed;
        private float speedVelocity;

        public float Speed => speed;

        public void Bind(Animator InAnimator)
        {
            animator = InAnimator;
            lastPosition = transform.position;
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.0f)
            {
                return;
            }

            Vector3 delta = transform.position - lastPosition;
            delta.y = 0.0f;
            lastPosition = transform.position;
            speed = Mathf.SmoothDamp(speed, delta.magnitude / dt, ref speedVelocity, smoothTime);
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetFloat(SpeedId, speed);
            }
        }
    }
}
