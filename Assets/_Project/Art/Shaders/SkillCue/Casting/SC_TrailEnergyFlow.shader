// 시전 중 · 에너지 흐름: 궤적 안을 노이즈 에너지가 흘러가며 꼬리가 실처럼 풀어지고, 불티가 튄다.
Shader "DemoLA/SkillCue/Casting/TrailEnergyFlow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.0, 3.2, 4.0, 1)
        _Intensity ("Intensity", Range(0, 10)) = 2.4
        [HDR] _TailColor ("Tail Color", Color) = (2.2, 0.4, 3.0, 1)
        _NoiseScale ("Noise Scale", Range(0.5, 10)) = 3
        _FlowSpeed ("Flow Speed", Range(0, 12)) = 5
        _Filament ("Filaments", Range(0, 2)) = 0.8
        _Sparks ("Sparks", Range(0, 2)) = 0.8
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
                float _NoiseScale;
                float _FlowSpeed;
                float _Filament;
                float _Sparks;
            CBUFFER_END

            #include "../SkillCueCommon.hlsl"

            half4 frag(SkillCueVaryings IN) : SV_Target
            {
                float t = _CueTime;
                float u = IN.uv.x;
                float v = IN.uv.y;
                float life = saturate(1.0 - u);

                float2 p = float2(u * _NoiseScale * 2.0 - t * _FlowSpeed * 0.3, v * _NoiseScale);
                float n = SC_Fbm(p + _Seed);
                float n2 = SC_Fbm(p * 2.1 + float2(t * _FlowSpeed * 0.2, 0.0) + 3.7);

                float shape = smoothstep(0.0, 0.4, v);
                float erode = smoothstep(u * 0.9, u * 0.9 + 0.3, n);
                float filaments = pow(1.0 - abs(n2 * 2.0 - 1.0), 6.0) * _Filament;
                float edge = pow(v, 8.0);

                float2 sp = float2(u * 30.0 - t * 2.0, v * 6.0);
                float2 id = floor(sp);
                float2 f = frac(sp) - 0.5;
                float h = SC_Hash21(id + _Seed);
                float spark = step(0.93, h) * smoothstep(0.25, 0.0, length(f)) * life;

                float g = (shape * erode * (0.5 + n) + filaments * shape + edge * 1.5) * life;
                float white = SC_Hot(edge * life, 0.5) + spark * _Sparks * 1.5;
                float3 col = lerp(_TailColor.rgb, _Color.rgb, life) * g + white.xxx;
                return half4(col * _Intensity * _Fade, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
