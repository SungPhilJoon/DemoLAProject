using UnityEngine;

namespace DemoLA.World
{
    /// <summary>엄폐 기둥이 솟아나고 / 가라앉는 움직임.</summary>
    public class CoverPillarMotion : MonoBehaviour
    {
        private const float Duration = 0.8f;

        private float fromY;
        private float toY;
        private float elapsed = Duration;
        private bool destroyAtEnd;

        public void Rise(float InTargetY)
        {
            fromY = transform.position.y;
            toY = InTargetY;
            elapsed = 0.0f;
        }

        public void SinkAndDestroy()
        {
            fromY = transform.position.y;
            toY = -CoverPillars.PillarHeight * 0.5f;
            elapsed = 0.0f;
            destroyAtEnd = true;
        }

        private void Update()
        {
            if (elapsed >= Duration)
            {
                return;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            var p = transform.position;
            p.y = Mathf.Lerp(fromY, toY, t * t * (3.0f - 2.0f * t));
            transform.position = p;
            if (t >= 1.0f && destroyAtEnd == true)
            {
                Destroy(gameObject);
            }
        }
    }
}
