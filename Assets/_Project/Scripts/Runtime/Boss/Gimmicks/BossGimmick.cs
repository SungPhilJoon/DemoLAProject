using Framework;
using UnityEngine;

namespace DemoLA.Boss
{
    public enum EGimmickState
    {
        Idle = 0,
        Running = 1,
        Succeeded = 2,
        Failed = 3,
    }

    /// <summary>
    /// 보스 기믹 (xlsx 분류 '기믹'/'파티 기믹'). 보스 오브젝트에 컴포넌트로 붙이고 GimmickId 로 StateTree 에서 시작한다.
    /// 수치(시간, 개수, 반경)는 인스펙터에서 바꾸는 자리 표시 값이다.
    /// </summary>
    public abstract class BossGimmick : BaseBehaviour
    {
        [SerializeField] private string gimmickId;
        [SerializeField] private string patternId;

        public EGimmickState State { get; private set; }
        public BossActor Boss { get; private set; }

        public string GimmickId
        {
            get { return gimmickId; }
            set { gimmickId = value; }
        }

        /// <summary>xlsx 패턴 ID (예: G1-01).</summary>
        public string PatternId
        {
            get { return patternId; }
            set { patternId = value; }
        }

        /// <summary>HUD 에 띄울 진행 상황 한 줄.</summary>
        public virtual string StatusText
        {
            get { return string.Empty; }
        }

        public void Bind(BossActor InBoss)
        {
            Boss = InBoss;
        }

        /// <summary>실제 인원 / 기준 인원 비율. 개수 · 게이지 같은 인원 의존 값을 줄인다 (기본: 없음).</summary>
        public virtual void ApplyPartyScale(float InScale)
        {
        }

        public void Begin()
        {
            State = EGimmickState.Running;
            OnBegin();
        }

        public void ResetGimmick()
        {
            OnReset();
            State = EGimmickState.Idle;
        }

        protected void Complete(bool InSucceeded)
        {
            if (State != EGimmickState.Running)
            {
                return;
            }

            State = InSucceeded == true ? EGimmickState.Succeeded : EGimmickState.Failed;
            OnFinished(InSucceeded);
            if (Boss != null)
            {
                Boss.NotifyGimmickFinished(this, InSucceeded);
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>진행 중이면 한 프레임 진행 (Update · 테스트).</summary>
        public void Tick(float InDeltaTime)
        {
            if (State == EGimmickState.Running && Boss != null)
            {
                OnTick(InDeltaTime);
            }
        }

        protected abstract void OnBegin();

        protected virtual void OnTick(float InDeltaTime)
        {
        }

        protected virtual void OnFinished(bool InSucceeded)
        {
        }

        protected virtual void OnReset()
        {
        }
    }
}
