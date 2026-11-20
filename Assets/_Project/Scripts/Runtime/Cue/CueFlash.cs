using System.Collections.Generic;
using DemoLA.Core;
using UnityEngine;

namespace DemoLA.Cue
{
    /// <summary>
    /// 풀링되는 플래시 구체. 구체 · 머티리얼 에셋은 프리팹 WorldPrefabs.CueFlash 에 미리 구워 두고, 풀이 비면 그 프리팹을 Instantiate 한다.
    /// 프리팹이 없으면 Build 로 코드 조립 (Sprites/Default 런타임 머티리얼).
    /// </summary>
    public class CueFlash : MonoBehaviour
    {
        private static readonly Stack<CueFlash> Pool = new Stack<CueFlash>();
        private static Material sharedMaterial;

        [SerializeField] private Renderer cachedRenderer;
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
                flash = WorldPrefabs.Instantiate<CueFlash>(WorldPrefabs.CueFlash, InPosition, Quaternion.identity);
                if (flash == null)
                {
                    flash = Build(null);
                }
            }

            if (flash.block == null)
            {
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

        /// <summary>구체(콜라이더 없음) + CueFlash 를 만든다 (프리팹 굽기 · 코드 조립 공용). InMaterial 이 null 이면 런타임 머티리얼.</summary>
        public static CueFlash Build(Material InMaterial)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "CueFlash";
            ObjectUtil.SafeDestroy(go.GetComponent<Collider>());
            var flash = go.AddComponent<CueFlash>();
            flash.cachedRenderer = go.GetComponent<Renderer>();
            if (InMaterial == null && sharedMaterial == null)
            {
                sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            flash.cachedRenderer.sharedMaterial = InMaterial != null ? InMaterial : sharedMaterial;
            flash.cachedRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return flash;
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
