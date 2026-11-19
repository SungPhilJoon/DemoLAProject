// 시전 전 · 마법진 아우라: 바닥에 마법진이 커지며 룬 띠가 돌고, 몸 주위로 빛기둥이 솟는다.
Shader "DemoLA/SkillCue/PreCast/AuraRuneCircle"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2.6, 1.6, 4.0, 1)
        _Intensity ("Intensity", Range(0, 10)) = 2.0
        _RuneCount ("Rune Count", Range(4, 40)) = 18
        _RotateSpeed ("Rotate Speed", Range(-3, 3)) = 0.6
        _PillarCount ("Pillar Count", Range(2, 24)) = 10
        _Pulse ("Pulse", Range(0, 1)) = 0.2
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
                float _RuneCount;
                float _RotateSpeed;
                float _PillarCount;
                float _Pulse;
            CBUFFER_END

            #include "../SkillCueCommon.hlsl"

            half4 frag(SkillCueVaryings IN) : SV_Target
            {
                float t = _CueTime;
                float pulse = 1.0 - _Pulse + _Pulse * (0.5 + 0.5 * sin(t * 6.0));
                if (IN.part < 0.5)
                {
                    float2 pol = SC_Polar(IN.uv);
                    float grow = lerp(0.15, 1.0, smoothstep(0.0, 0.35, _Progress));
                    float rr = pol.x / grow;
                    float inside = step(rr, 1.0);

                    float outer = SC_Band(rr, 0.95, 0.025) + SC_Band(rr, 0.88, 0.012);
                    float inner = SC_Band(rr, 0.58, 0.015) + SC_Band(rr, 0.36, 0.02);

                    // 회전하는 룬 띠 (0.62 ~ 0.84): 칸마다 무작위 블록 글리프.
                    float a1 = frac(pol.y + t * _RotateSpeed * 0.05);
                    float cell = floor(a1 * _RuneCount);
                    float fx = frac(a1 * _RuneCount);
                    float band01 = (rr - 0.62) / 0.22;
                    float inBand = step(0.0, band01) * step(band01, 1.0);
                    float gx = floor(fx * 3.0);
                    float gy = floor(band01 * 4.0);
                    float bit = step(0.45, SC_Hash21(float2(cell * 3.0 + gx, gy) + _Seed));
                    float gap = step(0.18, frac(fx * 3.0)) * step(0.2, frac(band01 * 4.0)) * step(0.08, fx) * step(fx, 0.92);
                    float rune = inBand * bit * gap;

                    // 반대로 도는 눈금과 안쪽 6 갈래 선
                    float a2 = frac(pol.y - t * _RotateSpeed * 0.08);
                    float ticks = step(0.9, frac(a2 * _RuneCount * 2.0)) * step(0.38, rr) * step(rr, 0.56);
                    float spokes = pow(abs(cos(a2 * SC_TAU * 3.0)), 80.0) * step(rr, 0.36);
                    float center = pow(saturate(1.0 - rr * 2.4), 2.0) * 0.8;

                    float g = (outer * 1.5 + inner + rune * 0.9 + ticks * 0.6 + spokes * 0.8 + center) * pulse * inside;
                    return SkillCueOutput(g, SC_Hot(g, 0.8) * 0.5);
                }

                // 빛기둥: 둘레에 기둥 여러 개, 기둥마다 높이 · 속도가 다르다.
                float u = IN.uv.x;
                float v = IN.uv.y;
                float id = floor(u * _PillarCount);
                float h = SC_Hash11(id + _Seed * 7.0);
                float col = frac(u * _PillarCount);
                float pillar = SC_Band(col, 0.5, 0.18);
                float reach = lerp(0.4, 1.0, h) * smoothstep(0.0, 0.6, _Progress);
                float streak = SC_Noise(float2(id * 3.1, v * 6.0 - t * (3.0 + 2.0 * h)));
                float g2 = pillar * smoothstep(reach, reach - 0.3, v) * (0.35 + 0.9 * streak) + pow(1.0 - v, 6.0) * 0.5;
                return SkillCueOutput(g2 * pulse, SC_Hot(g2, 0.8) * 0.4);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
