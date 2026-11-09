// 카운터 가능 구간 동안 보스 몸에 덧그리는 파란 빛 (URP).
//  - 몸 메시를 법선 방향으로 살짝 부풀린 껍데기를 가산 혼합으로 그린다 (원래 재질 위에 두 번째 재질로 붙인다).
//  - 외곽(프레넬) 빛 + 몸을 타고 올라가는 띠 + 맥동. 밝기는 _Intensity (CounterGlowView 가 0~1+ 로 움직인다).
Shader "DemoLA/CounterGlow"
{
    Properties
    {
        [HDR] _Color ("Glow Color", Color) = (0.25, 0.6, 2.4, 1)
        _Intensity ("Intensity", Range(0, 3)) = 0
        _Inflate ("Inflate (m)", Range(0, 0.2)) = 0.03
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.2
        _RimStrength ("Rim Strength", Range(0, 4)) = 1.6
        _Fill ("Inner Fill", Range(0, 1)) = 0.1
        _BandScale ("Band Scale", Range(0.1, 10)) = 1.0
        _BandSpeed ("Band Speed", Range(-5, 5)) = 1.2
        _BandStrength ("Band Strength", Range(0, 2)) = 0.5
        _PulseSpeed ("Pulse Speed", Range(0, 20)) = 7
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "CounterGlow"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Inflate;
                half _RimPower;
                half _RimStrength;
                half _Fill;
                half _BandScale;
                half _BandSpeed;
                half _BandStrength;
                half _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz) + normalWS * _Inflate;
                OUT.positionWS = positionWS;
                OUT.normalWS = normalWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                if (_Intensity <= 0.001h)
                {
                    discard;
                }

                float3 normalWS = normalize(IN.normalWS);
                float3 viewWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half rim = pow(1.0h - saturate(dot(normalWS, viewWS)), _RimPower);

                // 아래에서 위로 흐르는 부드러운 띠 (삼각파 → smoothstep).
                half band = frac(IN.positionWS.y * _BandScale - _Time.y * _BandSpeed);
                band = smoothstep(0.0h, 0.5h, band) * smoothstep(1.0h, 0.5h, band);

                half pulse = 0.78h + 0.22h * sin(_Time.y * _PulseSpeed);
                half glow = (rim * _RimStrength + _Fill + band * _BandStrength * (0.35h + rim)) * pulse * _Intensity;
                return half4(_Color.rgb * glow, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
