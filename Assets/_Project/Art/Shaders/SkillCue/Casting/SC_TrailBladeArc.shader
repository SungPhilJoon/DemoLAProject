// 시전 중 · 칼날 잔광: 칼끝을 따라 날카로운 빛의 호가 남고, 오래된 쪽은 꼬리 색으로 식으며 사라진다.
Shader "DemoLA/SkillCue/Casting/TrailBladeArc"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (3.0, 3.4, 4.5, 1)
        _Intensity ("Intensity", Range(0, 10)) = 2.2
        [HDR] _TailColor ("Tail Color", Color) = (0.4, 0.8, 3.0, 1)
        _EdgeSharpness ("Edge Sharpness", Range(1, 16)) = 6
        _AgePower ("Age Falloff", Range(0.3, 4)) = 1.5
        _BodyGlow ("Body Glow", Range(0, 1)) = 0.35
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
                float _EdgeSharpness;
                float _AgePower;
                float _BodyGlow;
            CBUFFER_END

            #include "../SkillCueCommon.hlsl"

            half4 frag(SkillCueVaryings IN) : SV_Target
            {
                float u = IN.uv.x; // 나이 0 최신 → 1
                float v = IN.uv.y; // 0 손잡이 → 1 칼끝
                float life = pow(saturate(1.0 - u), _AgePower);
                float edge = pow(v, _EdgeSharpness);
                float body = smoothstep(0.0, 0.7, v) * _BodyGlow;
                float baseFade = smoothstep(0.0, 0.15, v);
                float head = SC_Band(u, 0.0, 0.08);
                float g = (edge * 1.6 + body) * life * baseFade + head * edge * 1.5;
                float white = SC_Hot(edge * life, 0.55) * 1.2;
                float3 col = lerp(_TailColor.rgb, _Color.rgb, life) * g + white.xxx;
                return half4(col * _Intensity * _Fade, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
