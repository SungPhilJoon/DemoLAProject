using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DemoLA.Character
{
    /// <summary>
    /// 캐릭터 모델 애니메이션.
    ///  - 이동: Animator 의 컨트롤러(Idle/Run)에 이동 속도("Speed", m/s)를 넘긴다. 속도는 루트 위치 변화로 계산한다
    ///    — NavMeshAgent 이동 · 직접 이동 · 대시 · 네트워크 보간 모두 같은 값이 나온다.
    ///  - 동작(스킬 시전 등): PlayableGraph 의 믹서로 컨트롤러 위에 클립 하나를 섞어 재생한다 (PlayAction / StopAction).
    ///    직업마다 컨트롤러에 스킬 상태를 만들 필요가 없고, Humanoid 클립이면 모델이 달라도 리타깃된다.
    ///  그래프: [AnimatorControllerPlayable(이동)] ─┐
    ///          [AnimationClipPlayable(동작)]  ────┴ AnimationMixerPlayable → Animator
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

        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimatorControllerPlayable locomotion;
        private AnimationClipPlayable action;
        private float actionWeight;
        private float actionTarget;
        private float fadeRate;
        private float actionEndTime;
        private float actionBlendOut;
        private bool actionPlaying;

        public float Speed => speed;

        /// <summary>동작 클립이 재생 중인가 (블렌드 아웃 중이면 false).</summary>
        public bool IsPlayingAction => actionPlaying;

        public AnimationClip CurrentAction => action.IsValid() == true ? action.GetAnimationClip() : null;

        public void Bind(Animator InAnimator)
        {
            if (animator != InAnimator)
            {
                destroyGraph();
            }

            animator = InAnimator;
            lastPosition = transform.position;
            buildGraph();
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            lastPosition = transform.position;
            buildGraph();
        }

        private void OnDestroy()
        {
            destroyGraph();
        }

        /// <summary>
        /// 동작 클립을 이동 애니메이션 위에 섞어 재생한다. 진행 중인 동작은 바로 바꾼다.
        /// InSpeed = 클립 재생 배율, InStartTime = 클립 시작 위치(초).
        /// 루프가 아닌 클립은 끝나기 InBlendOut 초 전부터 이동으로 돌아간다. 루프 클립은 StopAction 까지 계속.
        /// InHold = true 면 클립이 끝나도 마지막 자세를 유지한다 (보스 패턴 단계 · 사망 — StopAction 으로 끝낸다).
        /// </summary>
        public void PlayAction(AnimationClip InClip, float InSpeed = 1.0f, float InStartTime = 0.0f, float InBlendIn = 0.08f, float InBlendOut = 0.15f, bool InHold = false)
        {
            buildGraph();
            if (InClip == null || graph.IsValid() == false)
            {
                return;
            }

            releaseAction();
            float speedScale = Mathf.Max(0.01f, InSpeed);
            action = AnimationClipPlayable.Create(graph, InClip);
            action.SetApplyFootIK(true);
            action.SetSpeed(speedScale);
            action.SetTime(Mathf.Clamp(InStartTime, 0.0f, InClip.length));
            action.SetTime(Mathf.Clamp(InStartTime, 0.0f, InClip.length)); // 두 번 = 이전 시간에서 넘어오는 이벤트 · 루트 이동 없이 바로 그 시각
            graph.Connect(action, 0, mixer, 1);

            actionPlaying = true;
            actionTarget = 1.0f;
            fadeRate = InBlendIn > 0.0f ? 1.0f / InBlendIn : float.MaxValue;
            actionBlendOut = Mathf.Max(0.0f, InBlendOut);
            actionEndTime = InClip.isLooping == true || InHold == true ? float.MaxValue : Mathf.Max(InStartTime, InClip.length - actionBlendOut * speedScale);
            if (InBlendIn <= 0.0f)
            {
                actionWeight = 1.0f;
            }

            applyWeights();
        }

        /// <summary>동작을 InBlendOut 초 동안 이동 애니메이션으로 되돌린다 (음수 = 재생할 때 정한 값).</summary>
        public void StopAction(float InBlendOut = -1.0f)
        {
            if (actionPlaying == false)
            {
                return;
            }

            actionPlaying = false;
            actionTarget = 0.0f;
            float blend = InBlendOut >= 0.0f ? InBlendOut : actionBlendOut;
            fadeRate = blend > 0.0f ? 1.0f / blend : float.MaxValue;
        }

        private void Update()
        {
            if (action.IsValid() == false)
            {
                return;
            }

            if (actionPlaying == true && action.GetTime() >= actionEndTime)
            {
                StopAction();
            }

            actionWeight = Mathf.MoveTowards(actionWeight, actionTarget, fadeRate == float.MaxValue ? 1.0f : fadeRate * Time.deltaTime);
            applyWeights();
            if (actionPlaying == false && actionWeight <= 0.0f)
            {
                releaseAction();
            }
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
            if (locomotion.IsValid() == true)
            {
                locomotion.SetFloat(SpeedId, speed);
            }
            else if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetFloat(SpeedId, speed);
            }
        }

        // ------------------------------------------------------------------ graph

        private void buildGraph()
        {
            if (graph.IsValid() == true || animator == null)
            {
                return;
            }

            graph = PlayableGraph.Create($"{name}.CharacterAnimation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            output.SetSourcePlayable(mixer);
            if (animator.runtimeAnimatorController != null)
            {
                locomotion = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
                graph.Connect(locomotion, 0, mixer, 0);
            }

            actionWeight = 0.0f;
            applyWeights();
            graph.Play();
        }

        private void applyWeights()
        {
            if (mixer.IsValid() == false)
            {
                return;
            }

            float weight = action.IsValid() == true ? actionWeight : 0.0f;
            mixer.SetInputWeight(0, 1.0f - weight);
            mixer.SetInputWeight(1, weight);
        }

        private void releaseAction()
        {
            if (action.IsValid() == true)
            {
                graph.Disconnect(mixer, 1);
                action.Destroy();
            }

            action = default;
            actionPlaying = false;
            applyWeights();
        }

        private void destroyGraph()
        {
            if (graph.IsValid() == true)
            {
                graph.Destroy();
            }

            graph = default;
            mixer = default;
            locomotion = default;
            action = default;
            actionPlaying = false;
            actionWeight = 0.0f;
        }
    }
}
