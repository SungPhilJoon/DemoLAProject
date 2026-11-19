// 시전 후 · 충격파: 칼에서 빛의 충격파 고리가 퍼지고, 그 뒤로 후광 광선이 번진다.
Shader "DemoLA/SkillCue/PostCast/BurstShockwave"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.4, 2.6, 4.5, 1)
        _Intensity ("Intensity", Range(0, 10)) = 2.8
        _RingWidth ("Ring Width", Range(0.01, 0.3)) = 0.08
        _RayCount ("God Ray Count", Range(4, 64)) = 24
        _GodRays ("God Rays", Range(0, 2)) = 1
        _BladeGlow ("Blade Glow", Range(0, 3)) = 1
        [HideInInspector] _Progress ("Progress (runtime)", Float) = 0.5
        [HideInInspector] _Fade ("Fade (runtime)", Float) = 1
        [HideInInspector] _CueTime ("Cue Time (runtime)", Float) = 0
        [HideInInspector] _Seed ("Seed (runtime)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "SkillCue"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex SkillCueVert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                // 공통 (SkillCueCommon.hlsl 규약)
                float4 _Color;
                float _Intensity;
                float _Progress;
                float _Fade;
                float _CueTime;
                float _Seed;
                float _RingWidth;
                float _RayCount;
                float _GodRays;
                float _BladeGlow;
            CBUFFER_END

            #include "../SkillCueCommon.hlsl"

            half4 frag(SkillCueVaryings IN) : SV_Target
            {
                if (IN.part > 0.5)
                {
                    float2 blade = SC_BladeLight(IN.uv, _CueTime, _Seed, _Progress);
                    return SkillCueOutput(blade.x * _BladeGlow, blade.y * _BladeGlow);
                }

                float2 pol = SC_Polar(IN.uv);
                float r = pol.x;
                float p = _Progress;
                float expand = 1.0 - pow(1.0 - p, 3.0);
                float ring = SC_Band(r, expand * 0.95, _RingWidth * (1.0 + p)) * (1.0 - p);
                float ring2 = SC_Band(r, expand * 0.7, _RingWidth * 0.5) * (1.0 - p) * 0.6;
                float rayN = SC_NoiseWrap(pol.y, _Seed + _CueTime * 0.5, floor(_RayCount));
                float rays = pow(rayN, 3.0) * saturate(1.0 - r) * smoothstep(0.0, 0.15, r) * _GodRays * (1.0 - p * 0.7);
                float core = pow(saturate(1.0 - r / (0.3 * (1.0 - p) + 0.05)), 2.0);
                float g = ring * 2.0 + ring2 + rays + core * 1.5;
                return SkillCueOutput(g, core + SC_Hot(ring, 0.6));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
