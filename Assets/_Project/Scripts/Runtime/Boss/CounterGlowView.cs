using System.Collections.Generic;
using DemoLA.Core;
using UnityEngine;

namespace DemoLA.Boss
{
    /// <summary>
    /// 카운터 가능 구간(Boss_CounterWindow 태그) 동안 보스 몸에 파란 빛을 입힌다 (셰이더 DemoLA/CounterGlow).
    /// 몸 렌더러마다 두 번째 재질로 빛 껍데기를 덧그리고, 태그가 켜지면 번쩍(1.8) → 1.0 으로 자리 잡고, 꺼지면 빠르게 사라진다.
    /// 태그는 네트워크로 복제되므로 클라이언트 표시용 보스에서도 같이 빛난다.
    /// 재질: GameDatabase.CounterGlowMaterial (빌드에 셰이더가 포함되도록 에셋으로 참조). 없으면 Shader.Find.
    /// </summary>
    public class CounterGlowView : MonoBehaviour
    {
        public const string ShaderName = "DemoLA/CounterGlow";
        public const string MaterialPath = "Assets/_Project/Art/Materials/M_CounterGlow.mat";
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        private static Material sharedMaterial;

        [Tooltip("켜질 때 번쩍이는 최대 밝기")]
        public float FlashIntensity = 1.8f;
        [Tooltip("번쩍임 → 유지 밝기로 내려오는 시간 (초)")]
        public float FlashTime = 0.18f;
        public float HoldIntensity = 1.0f;
        [Tooltip("태그가 꺼진 뒤 사라지는 시간 (초)")]
        public float FadeOutTime = 0.15f;

        private readonly List<Renderer> renderers = new List<Renderer>();
        private Material material;
        private BossActor boss;
        private bool wasOpen;
        private float openedAt;
        private float intensity;

        /// <summary>GameBootstrap 이 GameDatabase.CounterGlowMaterial 로 넣는다 (에디터에서는 비어 있으면 에셋을 직접 읽는다).</summary>
        public static Material SharedMaterial
        {
            get
            {
#if UNITY_EDITOR
                if (sharedMaterial == null)
                {
                    sharedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                }
#endif
                if (sharedMaterial == null)
                {
                    var shader = Shader.Find(ShaderName);
                    if (shader != null)
                    {
                        sharedMaterial = new Material(shader) { name = "M_CounterGlow (runtime)" };
                    }
                }

                return sharedMaterial;
            }
            set { sharedMaterial = value; }
        }

        public float CurrentIntensity
        {
            get { return intensity; }
        }

        /// <summary>빛을 입힐 몸 렌더러 (발밑 표시 등은 빼고 넘긴다).</summary>
        public void Bind(IEnumerable<Renderer> InRenderers)
        {
            var template = SharedMaterial;
            if (template == null)
            {
                return;
            }

            material = new Material(template) { name = "M_CounterGlow (instance)" };
            material.SetFloat(IntensityId, 0.0f);
            foreach (var renderer in InRenderers)
            {
                if (renderer == null || renderers.Contains(renderer) == true)
                {
                    continue;
                }

                var materials = renderer.sharedMaterials;
                var extended = new Material[materials.Length + 1];
                materials.CopyTo(extended, 0);
                extended[materials.Length] = material;
                renderer.sharedMaterials = extended;
                renderers.Add(renderer);
            }
        }

        private void Awake()
        {
            boss = GetComponent<BossActor>();
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime, Time.time);
        }

        /// <summary>테스트에서 직접 진행시킬 수 있게 분리.</summary>
        public void Tick(float InDeltaTime, float InTime)
        {
            if (boss == null)
            {
                boss = GetComponent<BossActor>();
            }

            bool open = boss != null && boss.IsDead == false && boss.HasTag((int)ETag.Boss_CounterWindow) == true;
            if (open == true && wasOpen == false)
            {
                openedAt = InTime;
            }

            wasOpen = open;
            if (open == true)
            {
                float t = FlashTime > 0.0f ? Mathf.Clamp01((InTime - openedAt) / FlashTime) : 1.0f;
                intensity = Mathf.Lerp(FlashIntensity, HoldIntensity, t * t);
            }
            else
            {
                intensity = FadeOutTime > 0.0f ? Mathf.MoveTowards(intensity, 0.0f, InDeltaTime * FlashIntensity / FadeOutTime) : 0.0f;
            }

            if (material != null)
            {
                material.SetFloat(IntensityId, intensity);
            }
        }

        private void OnDestroy()
        {
            ObjectUtil.SafeDestroy(material);
        }
    }
}
