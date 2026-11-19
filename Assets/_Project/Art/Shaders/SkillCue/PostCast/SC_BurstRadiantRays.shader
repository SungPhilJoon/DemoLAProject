// 시전 후 · 방사 광선 (웅장): 칼끝에서 수많은 광선이 사방으로 뿜어지고 빛 입자가 바깥으로 흩어진다.
Shader "DemoLA/SkillCue/PostCast/BurstRadiantRays"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (4.5, 3.4, 1.6, 1)
        _Intensity ("Intensity", Range(0, 10)) = 3.0
        _RayCount ("Ray Count", Range(8, 96)) = 40
        _RaySharpness ("Ray Sharpness", Range(1, 12)) = 5
        _Sparks ("Sparks", Range(0, 1)) = 0.5
        _BladeGlow ("Blade Glow", Range(0, 3)) = 1.4
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
                float _RayCount;
                float _RaySharpness;
                float _Sparks;
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

                float t = _CueTime;
                float2 pol = SC_Polar(IN.uv);
                float r = pol.x;
                float a = pol.y;
                float n1 = SC_NoiseWrap(a, t * 0.8 + _Seed, floor(_RayCount));
                float n2 = SC_NoiseWrap(a, t * 1.3 + _Seed * 1.7 + 11.0, max(4.0, floor(_RayCount * 0.5)));
                float rays = pow(saturate(n1 * n2 * 1.8), _RaySharpness);
                float len = smoothstep(0.0, 0.3, _Progress) * lerp(0.6, 1.0, n2);
                rays *= saturate(1.0 - r / max(len, 0.01)) * smoothstep(0.02, 0.12, r);

                float2 sp = float2(a * 48.0, r * 6.0 - t * 4.0);
                float2 id = floor(sp);
                float2 f = frac(sp) - 0.5;
                float h = SC_Hash21(id + _Seed);
                float spark = step(1.0 - _Sparks * 0.2, h) * smoothstep(0.3, 0.0, length(f * float2(1.0, 0.5))) * saturate(1.0 - r);

                float core = pow(saturate(1.0 - r * 3.0), 2.0);
                float g = rays * 2.0 + spark * 1.5 + core * 2.0 + pow(saturate(1.0 - r), 3.0) * 0.4;
                return SkillCueOutput(g, core * 1.5 + spark + SC_Hot(rays, 0.7));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
