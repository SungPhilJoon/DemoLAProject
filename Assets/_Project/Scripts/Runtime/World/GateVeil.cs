using UnityEngine;

namespace DemoLA.World
{
    /// <summary>입구 빛 막이 천천히 일렁인다. 모두 준비되면 밝아진다 (RaidLobby 가 Charged 를 켠다).</summary>
    public class GateVeil : MonoBehaviour
    {
        public static bool Charged;
        private Renderer cachedRenderer;
        private MaterialPropertyBlock block;

        private void Update()
        {
            if (cachedRenderer == null)
            {
                cachedRenderer = GetComponent<Renderer>();
                block = new MaterialPropertyBlock();
            }

            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * (Charged ? 6.0f : 1.5f));
            var color = Charged ? new Color(1.0f, 0.85f, 0.4f, Mathf.Lerp(0.55f, 0.9f, wave)) : new Color(0.45f, 0.3f, 0.95f, Mathf.Lerp(0.35f, 0.6f, wave));
            cachedRenderer.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            cachedRenderer.SetPropertyBlock(block);
        }
    }
}
