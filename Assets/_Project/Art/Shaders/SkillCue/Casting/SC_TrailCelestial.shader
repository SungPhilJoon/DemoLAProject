// 시전 중 · 천상 궤적 (웅장): 금빛 궤적에 여러 겹의 빛 띠가 물결치고, 별가루가 반짝이며 남는다.
Shader "DemoLA/SkillCue/Casting/TrailCelestial"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (4.5, 3.2, 1.2, 1)
        _Intensity ("Intensity", Range(0, 10)) = 2.4
        [HDR] _TailColor ("Tail Color", Color) = (2.5, 0.7, 0.25, 1)
        _Bands ("Bands", Range(1, 4)) = 3
        _Wave ("Band Wave", Range(0, 0.15)) = 0.04
        _StarDensity ("Star Density", Range(0, 1)) = 0.5
        _Shimmer ("Shimmer Speed", Range(0, 20)) = 8
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
                float4 _TailColor;
                float _Bands;
                float _Wave;
                float _StarDensity;
                float _Shimmer;
            CBUFFER_END

            #include "../SkillCueCommon.hlsl"

            half4 frag(SkillCueVaryings IN) : SV_Target
            {
                float t = _CueTime;
                float u = IN.uv.x;
                float v = IN.uv.y;
                float life = saturate(1.0 - u);

                float bands = 0.0;
                [unroll]
                for (int i = 0; i < 4; ++i)
                {
                    float on = step((float)i + 0.5, _Bands);
                    float center = lerp(0.35, 0.95, ((float)i + 1.0) / (_Bands + 1.0));
                    float wave = sin(u * 12.0 + t * 3.0 + (float)i * 1.7) * _Wave;
                    bands += SC_Band(v, center + wave, 0.05 * (1.0 - u * 0.5)) * (0.6 + 0.1 * (float)i) * on;
                }

                float glow = pow(v, 3.0) * 1.3 + smoothstep(0.2, 1.0, v) * 0.35;
                float head = SC_Band(u, 0.0, 0.12) * v * v;

                float2 sp = float2(u * 40.0 + t * 0.5, v * 10.0);
                float2 id = floor(sp);
                float2 f = frac(sp) - 0.5;
                float h = SC_Hash21(id + _Seed);
                float star = step(1.0 - _StarDensity * 0.15, h) * pow(saturate(1.0 - length(f) * 3.0), 3.0) * (0.5 + 0.5 * sin(t * _Shimmer + h * 40.0));

                float g = (glow + bands * 0.9) * life + head * 1.5;
                float white = SC_Hot(pow(v, 6.0) * life, 0.4) + star * life * 1.5;
                float3 col = lerp(_TailColor.rgb, _Color.rgb, pow(life, 0.7)) * g + white.xxx;
                return half4(col * _Intensity * _Fade, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
