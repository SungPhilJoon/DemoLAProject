// 레이드 하늘 (발탄 레이드 분위기): 거의 검은 천정 → 핏빛 하늘 → 지평선의 붉은 주황 빛띠, 천천히 흐르는 검붉은 구름.
// 스카이박스 셰이더 (LightMode 태그 없음 → URP 에서도 그대로 쓴다). 값은 M_Sky_Raid 머티리얼에서 조절한다.
Shader "DemoLA/Skybox/Raid Sky"
{
    Properties
    {
        [Header(Sky Gradient)]
        _ZenithColor ("Zenith", Color) = (0.035, 0.010, 0.014, 1)
        _SkyColor ("Sky", Color) = (0.20, 0.035, 0.040, 1)
        _HorizonColor ("Horizon", Color) = (0.52, 0.09, 0.05, 1)
        _GroundColor ("Below Horizon", Color) = (0.07, 0.02, 0.02, 1)
        _HorizonHeight ("Horizon Height", Range(-0.3, 0.3)) = -0.02
        _SkyFalloff ("Sky Falloff", Range(0.5, 8)) = 2.2

        [Header(Horizon Glow)]
        _GlowColor ("Glow", Color) = (0.95, 0.24, 0.08, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 3)) = 0.6
        _GlowSharpness ("Glow Sharpness", Range(1, 40)) = 9

        [Header(Clouds)]
        _CloudColor ("Cloud", Color) = (0.30, 0.05, 0.045, 1)
        _CloudLitColor ("Cloud Lit (horizon)", Color) = (0.85, 0.25, 0.10, 1)
        _CloudScale ("Cloud Scale", Float) = 1.6
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.55
        _CloudSoftness ("Cloud Softness", Range(0.05, 1)) = 0.35
        _CloudOpacity ("Cloud Opacity", Range(0, 1)) = 0.85
        _CloudSpeed ("Cloud Speed", Float) = 0.012

        _Exposure ("Exposure", Range(0, 4)) = 1.0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _ZenithColor, _SkyColor, _HorizonColor, _GroundColor, _GlowColor, _CloudColor, _CloudLitColor;
            half _HorizonHeight, _SkyFalloff, _GlowIntensity, _GlowSharpness;
            half _CloudScale, _CloudCoverage, _CloudSoftness, _CloudOpacity, _CloudSpeed, _Exposure;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash(i + float3(0, 0, 0)), hash(i + float3(1, 0, 0)), f.x),
                                 lerp(hash(i + float3(0, 1, 0)), hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(hash(i + float3(0, 0, 1)), hash(i + float3(1, 0, 1)), f.x),
                                 lerp(hash(i + float3(0, 1, 1)), hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float fbm(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 5; ++i)
                {
                    v += a * noise(p);
                    p = p * 2.03 + float3(1.7, 9.2, 4.1);
                    a *= 0.5;
                }

                return v;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y - _HorizonHeight;

                // 하늘: 지평선 → 하늘 → 천정
                float up = saturate(y);
                half3 sky = lerp(_HorizonColor.rgb, _SkyColor.rgb, saturate(pow(up * 2.5, 0.55)));
                sky = lerp(sky, _ZenithColor.rgb, saturate(pow(up, 1.0 / _SkyFalloff)));
                // 지평선 아래: 어두운 땅빛
                half3 ground = lerp(_HorizonColor.rgb * 0.45, _GroundColor.rgb, saturate(-y * 5.0));
                half3 col = y >= 0.0 ? sky : ground;

                // 지평선 빛띠 (불길 같은 붉은 주황)
                float glow = exp(-abs(y) * _GlowSharpness) * _GlowIntensity;
                col += _GlowColor.rgb * glow;

                // 구름: 하늘 돔에 투영한 fbm. 지평선 쪽은 빛을 받아 밝고, 위쪽은 검붉다.
                if (y > 0.0)
                {
                    float t = _Time.y * _CloudSpeed;
                    float2 uv = d.xz / (d.y + 0.18) * _CloudScale;
                    float n = fbm(float3(uv + float2(t, t * 0.35), t * 0.6));
                    float edge = 1.0 - _CloudCoverage;
                    float c = smoothstep(edge, edge + _CloudSoftness, n);
                    c *= saturate(y * 8.0);
                    half3 cloud = lerp(_CloudColor.rgb, _CloudLitColor.rgb, exp(-y * 6.0));
                    col = lerp(col, cloud, c * _CloudOpacity);
                }

                return half4(col * _Exposure, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
