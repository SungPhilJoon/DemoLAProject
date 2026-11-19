// 시전 후 · 별빛 섬광: 마지막 타격에 칼끝에서 십자 별빛이 터지고 칼날 전체가 빛난다.
Shader "DemoLA/SkillCue/PostCast/BurstStarFlare"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (3.0, 3.0, 4.5, 1)
        _Intensity ("Intensity", Range(0, 10)) = 3.0
        _Rays ("Ray Points", Range(2, 16)) = 4
        _RayLength ("Ray Length", Range(0.2, 1)) = 1
        _RotateSpeed ("Rotate Speed", Range(-3, 3)) = 0.4
        _BladeGlow ("Blade Glow", Range(0, 3)) = 1.2
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
                float _Rays;
                float _RayLength;
                float _RotateSpeed;
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
                float expand = smoothstep(0.0, 0.25, _Progress);
                float ang = (pol.y + t * _RotateSpeed * 0.05) * SC_TAU;
                float rays = pow(abs(cos(ang * _Rays * 0.5)), 60.0);
                float rays2 = pow(abs(cos((ang + 0.39) * _Rays * 0.5)), 30.0) * 0.5;
                float rayMask = saturate(1.0 - r / max(_RayLength * expand, 0.01));
                float core = pow(saturate(1.0 - r / (0.25 * expand + 0.02)), 3.0);
                float halo = pow(saturate(1.0 - r), 4.0) * 0.6;
                float ring = SC_Band(r, _Progress * 0.9, 0.05) * (1.0 - _Progress);
                float g = (rays + rays2) * rayMask * rayMask * 1.5 + core * 2.0 + halo + ring;
                float white = core * 1.5 + SC_Hot(rays * rayMask, 0.7);
                return SkillCueOutput(g, white);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
