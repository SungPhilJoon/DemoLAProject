// 시전 전 · 확장 고리 아우라: 발밑에서 고리가 연달아 퍼져 나가고, 몸을 감싼 빛이 위로 피어오른다.
Shader "DemoLA/SkillCue/PreCast/AuraExpandRing"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.2, 2.2, 4.0, 1)
        _Intensity ("Intensity", Range(0, 10)) = 2.2
        _RingCount ("Ring Count", Range(1, 6)) = 3
        _RingWidth ("Ring Width", Range(0.01, 0.3)) = 0.07
        _Expand ("Expand Speed", Range(0.1, 3)) = 1
        _WallFade ("Wall Height Fade", Range(0.3, 6)) = 1.6
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
                float _RingCount;
                float _RingWidth;
                float _Expand;
                float _WallFade;
            CBUFFER_END

            #include "../SkillCueCommon.hlsl"

            half4 frag(SkillCueVaryings IN) : SV_Target
            {
                float t = _CueTime;
                if (IN.part < 0.5)
                {
                    float2 pol = SC_Polar(IN.uv);
                    float r = pol.x;
                    float edge = saturate((1.0 - r) * 8.0);
                    // 진행도에 따라 아우라 반경이 커지고, 그 안에서 고리가 반복해서 퍼진다.
                    float reach = saturate(_Progress * 1.6 + 0.15);
                    float rings = 0.0;
                    [unroll]
                    for (int i = 0; i < 6; ++i)
                    {
                        float on = step((float)i + 0.5, _RingCount);
                        float phase = frac(t * 0.6 * _Expand + (float)i / max(_RingCount, 1.0));
                        rings += SC_Band(r, phase * reach, _RingWidth) * (1.0 - phase) * on;
                    }

                    float streak = SC_NoiseWrap(pol.y, t * 0.8 + _Seed, 24.0);
                    float floorGlow = pow(saturate(1.0 - r / max(reach, 0.05)), 2.0) * (0.3 + 0.45 * streak);
                    float rim = SC_Band(r, reach, 0.035) * 1.2;
                    float g = (rings * 1.4 + floorGlow + rim) * edge;
                    return SkillCueOutput(g, SC_Hot(g, 0.9) * 0.6);
                }

                // 몸을 감싼 원통: 아래가 밝고 위로 사라지며, 결이 위로 흐른다.
                float v = IN.uv.y;
                float rise = SC_NoiseWrap(IN.uv.x, v * 3.0 - t * 2.2 + _Seed, 18.0);
                float body = pow(1.0 - v, _WallFade) * (0.35 + 0.9 * rise);
                float wall = body * saturate(_Progress * 3.0 + 0.2);
                return SkillCueOutput(wall, SC_Hot(wall, 0.85) * 0.4);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
