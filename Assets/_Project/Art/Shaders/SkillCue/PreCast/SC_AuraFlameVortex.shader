// 시전 전 · 소용돌이 불꽃 아우라: 바닥에 나선 팔이 돌며 펼쳐지고, 몸을 감싸는 불꽃이 비틀리며 치솟는다.
Shader "DemoLA/SkillCue/PreCast/AuraFlameVortex"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (4.0, 1.4, 0.35, 1)
        _Intensity ("Intensity", Range(0, 10)) = 2.0
        [HDR] _TipColor ("Flame Tip Color", Color) = (4.0, 3.0, 1.2, 1)
        _Arms ("Spiral Arms", Range(1, 8)) = 3
        _Swirl ("Swirl", Range(0, 12)) = 5
        _FlameScale ("Flame Scale", Range(0.5, 8)) = 3
        _FlameSpeed ("Flame Speed", Range(0, 8)) = 3
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
                float4 _TipColor;
                float _Arms;
                float _Swirl;
                float _FlameScale;
                float _FlameSpeed;
            CBUFFER_END

            #include "../SkillCueCommon.hlsl"

            half4 frag(SkillCueVaryings IN) : SV_Target
            {
                float t = _CueTime;
                float envelope = _Intensity * _Fade;
                if (IN.part < 0.5)
                {
                    float2 d = IN.uv * 2.0 - 1.0;
                    float r = length(d);
                    float ang = atan2(d.y, d.x) / SC_TAU + 0.5;
                    float reach = saturate(_Progress * 1.4 + 0.25);
                    float spiral = frac(ang * _Arms + r * _Swirl * 0.5 - t * 0.6);
                    float arms = smoothstep(0.0, 0.5, spiral) * smoothstep(1.0, 0.5, spiral);

                    float s = sin(t * 0.7);
                    float c = cos(t * 0.7);
                    float2 rd = float2(d.x * c - d.y * s, d.x * s + d.y * c);
                    float n = SC_Fbm(rd * 3.0 + _Seed);

                    float mask = saturate(1.0 - r / reach);
                    float edge = saturate((1.0 - r) * 8.0);
                    float g = (arms * pow(mask, 0.7) * (0.5 + 0.9 * n) + pow(mask, 4.0) * 0.6) * edge;
                    float tip = arms * n * mask * edge;
                    float3 col = _Color.rgb * g + _TipColor.rgb * tip * 0.6 + (SC_Hot(g, 0.85) * float3(1.0, 1.0, 1.0)) * 0.5;
                    return half4(col * envelope, 1.0);
                }

                // 몸을 감싸는 불꽃: 둘레를 따라 비틀리며 올라간다 (이음매 없는 둘레 노이즈).
                float v = IN.uv.y;
                float u = frac(IN.uv.x + v * 0.25 + t * 0.15);
                float n1 = SC_NoiseWrap(u, v * _FlameScale - t * _FlameSpeed + _Seed, 8.0);
                float n2 = SC_NoiseWrap(u, v * _FlameScale * 2.0 - t * _FlameSpeed * 1.6 + _Seed, 16.0);
                float n = n1 * 0.65 + n2 * 0.35;
                float height = v / max(0.15, saturate(_Progress * 2.0 + 0.3));
                float flame = smoothstep(height * 0.85, height * 0.85 + 0.25, n) * saturate(1.0 - height);
                float base = pow(1.0 - v, 5.0) * 0.5;
                float g2 = flame + base;
                float3 col2 = lerp(_Color.rgb, _TipColor.rgb, saturate(v * 1.3)) * g2 + (SC_Hot(flame, 0.7) * float3(1.0, 1.0, 1.0)) * 0.6;
                return half4(col2 * envelope, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
