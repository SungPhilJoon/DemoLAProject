using System.Collections.Generic;
using DemoLA.Core;
using Framework.Ability;
using UnityEngine;

namespace DemoLA.Cue
{
    /// <summary>
    /// GameplayCue 연출 (문서: 스킬 이펙트는 GameplayCue). 에셋이 정해지기 전이라 기본 구체가 잠깐 커졌다 사라지는 플래시로 대신한다.
    /// 실제 VFX 가 생기면 핸들러만 바꾸고 이펙트/어빌리티 코드는 그대로 둔다.
    /// </summary>
    public class FlashCueHandler : IGameplayCueHandler
    {
        private readonly Color color;
        private readonly float size;
        private readonly float lifetime;

        public FlashCueHandler(Color InColor, float InSize, float InLifetime)
        {
            color = InColor;
            size = InSize;
            lifetime = InLifetime;
        }

        public void OnExecute(GameplayCueParams InParams)
        {
            CueFlash.Spawn(InParams.Location + Vector3.up, color, size, lifetime);
        }

        public void OnActive(GameplayCueParams InParams)
        {
        }

        public void OnRemove(GameplayCueParams InParams)
        {
        }
    }

    public static class GameplayCueHandlers
    {
        public static void RegisterDefaults()
        {
            GameplayCueManager.Register((int)ECue.Hit, new FlashCueHandler(new Color(1.0f, 0.9f, 0.6f, 0.8f), 0.6f, 0.15f));
            GameplayCueManager.Register((int)ECue.CritHit, new FlashCueHandler(new Color(1.0f, 0.5f, 0.1f, 0.9f), 0.9f, 0.2f));
            GameplayCueManager.Register((int)ECue.Countered, new FlashCueHandler(new Color(0.3f, 0.7f, 1.0f, 0.9f), 3.0f, 0.4f));
            GameplayCueManager.Register((int)ECue.StaggerBroken, new FlashCueHandler(new Color(1.0f, 1.0f, 0.3f, 0.9f), 4.0f, 0.5f));
            GameplayCueManager.Register((int)ECue.Heal, new FlashCueHandler(new Color(0.3f, 1.0f, 0.4f, 0.7f), 1.2f, 0.3f));
            GameplayCueManager.Register((int)ECue.Shield, new FlashCueHandler(new Color(0.6f, 0.8f, 1.0f, 0.6f), 1.4f, 0.3f));
            GameplayCueManager.Register((int)ECue.DeathWardConsumed, new FlashCueHandler(new Color(1.0f, 0.85f, 0.3f, 0.9f), 2.0f, 0.5f));
        }
    }

    /// <summary>풀링되는 플래시 구체.</summary>
    public class CueFlash : MonoBehaviour
    {
        private static readonly Stack<CueFlash> Pool = new Stack<CueFlash>();
        private static Material sharedMaterial;

        private Renderer cachedRenderer;
        private MaterialPropertyBlock block;
        private float elapsed;
        private float lifetime;
        private float size;
        private Color color;

        public static void Spawn(Vector3 InPosition, Color InColor, float InSize, float InLifetime)
        {
            CueFlash flash = null;
            while (Pool.Count > 0 && flash == null)
            {
                flash = Pool.Pop();
            }

            if (flash == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "CueFlash";
                ObjectUtil.SafeDestroy(go.GetComponent<Collider>());
                flash = go.AddComponent<CueFlash>();
                flash.cachedRenderer = go.GetComponent<Renderer>();
                if (sharedMaterial == null)
                {
                    sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                }

                flash.cachedRenderer.sharedMaterial = sharedMaterial;
                flash.cachedRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                flash.block = new MaterialPropertyBlock();
            }

            flash.transform.position = InPosition;
            flash.elapsed = 0.0f;
            flash.lifetime = Mathf.Max(0.05f, InLifetime);
            flash.size = InSize;
            flash.color = InColor;
            flash.gameObject.SetActive(true);
            flash.apply(0.0f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float ratio = elapsed / lifetime;
            if (ratio >= 1.0f)
            {
                gameObject.SetActive(false);
                Pool.Push(this);
                return;
            }

            apply(ratio);
        }

        private void apply(float InRatio)
        {
            transform.localScale = Vector3.one * Mathf.Lerp(size * 0.4f, size, InRatio);
            var c = color;
            c.a *= 1.0f - InRatio;
            block.SetColor("_Color", c);
            cachedRenderer.SetPropertyBlock(block);
        }
    }
}
