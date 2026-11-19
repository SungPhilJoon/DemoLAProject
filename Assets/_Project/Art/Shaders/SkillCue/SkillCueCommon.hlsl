// 스킬 GameplayCue 셰이더 공통 (URP). 시전 전(PreCast) · 시전 중(Casting) · 시전 후(PostCast) 폴더의 셰이더가 모두 include 한다.
//
// 프로퍼티 규약 (프레임워크 ShaderGameplayCueNotify · GameplayCueView · GameplayCueShaderIds 와 Skill Ability Tool 미리보기가 이 이름을 쓴다)
//   _Color, _Intensity : 머티리얼 값 (색 · 밝기)
//   _Progress          : 0 → 1 재생 진행도 (런타임)
//   _Fade              : 0 ~ 1 켜짐/꺼짐 엔벨로프 (런타임)
//   _CueTime           : 큐 시작 후 경과 초 (런타임 — 스크럽해도 같은 그림이 나오도록 _Time 대신 쓴다)
//   _Seed              : 인스턴스 난수 (런타임)
//
// 메시 규약 (프레임워크 GameplayCueGeometry — 아우라 = Aura, 무기 궤적 = Trail, 분출 = Burst)
//   정점 색 R = 부위. 아우라: 0 = 바닥 원판(UV 중심 0.5) / 1 = 몸을 감싸는 원통(U 둘레, V 높이)
//                    분출  : 0 = 카메라를 보는 섬광(UV 중심 0.5) / 1 = 칼날 띠(U 손잡이→끝, V 폭)
//   궤적: U = 나이 (0 최신 → 1 사라짐), V = 0 손잡이 → 1 칼끝
//
// 새 셰이더를 만들 때 (같은 폴더의 셰이더를 복사하는 것이 가장 쉽다):
//   1) Properties 에 공통 6개(_Color · _Intensity · 숨김 _Progress · _Fade · _CueTime · _Seed)
//   2) Core.hlsl include → CBUFFER_START(UnityPerMaterial) 에 공통 6개(float4 _Color, float _Intensity · _Progress · _Fade · _CueTime · _Seed) + 자기 프로퍼티 → CBUFFER_END → 이 파일 include
//   3) #pragma vertex SkillCueVert, frag 에서 SkillCueOutput(색 밝기, 흰빛) 을 돌려준다 (Blend One One 가산).
#ifndef DEMOLA_SKILLCUE_COMMON_INCLUDED
#define DEMOLA_SKILLCUE_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#define SC_TAU 6.28318530718

struct SkillCueAttributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct SkillCueVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float part : TEXCOORD1;
    float3 positionWS : TEXCOORD2;
    UNITY_VERTEX_OUTPUT_STEREO
};

SkillCueVaryings SkillCueVert(SkillCueAttributes IN)
{
    SkillCueVaryings OUT;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
    float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
    OUT.positionWS = positionWS;
    OUT.positionCS = TransformWorldToHClip(positionWS);
    OUT.uv = IN.uv;
    OUT.part = IN.color.r;
    return OUT;
}

// ------------------------------------------------------------------ noise

float SC_Hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float SC_Hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float SC_Noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = SC_Hash21(i);
    float b = SC_Hash21(i + float2(1.0, 0.0));
    float c = SC_Hash21(i + float2(0.0, 1.0));
    float d = SC_Hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float SC_Fbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    [unroll]
    for (int i = 0; i < 4; ++i)
    {
        v += a * SC_Noise(p);
        p = p * 2.03 + float2(17.1, 9.7);
        a *= 0.5;
    }
    return v;
}

// 둘레 방향으로 이음매 없이 반복되는 노이즈 (InU 0~1 둘레, InCells = 한 바퀴 칸 수).
float SC_NoiseWrap(float InU, float InV, float InCells)
{
    float x = InU * InCells;
    float a = SC_Noise(float2(x, InV));
    float b = SC_Noise(float2(x - InCells, InV));
    return lerp(a, b, InU);
}

// ------------------------------------------------------------------ shapes

// UV(0~1, 중심 0.5) → (반경 0 중심 ~ 1 가장자리, 각도 0~1)
float2 SC_Polar(float2 uv)
{
    float2 d = uv * 2.0 - 1.0;
    return float2(length(d), atan2(d.y, d.x) / SC_TAU + 0.5);
}

// InX 가 InCenter 에서 InWidth 안쪽일 때 1 인 부드러운 띠.
float SC_Band(float InX, float InCenter, float InWidth)
{
    return saturate(1.0 - abs(InX - InCenter) / max(InWidth, 1e-4));
}

// 가산 혼합 최종 색: (색 × InGlow + 흰빛 × InWhite) × 강도 × 엔벨로프.
half4 SkillCueOutput(float InGlow, float InWhite)
{
    float3 c = (_Color.rgb * max(InGlow, 0.0) + max(InWhite, 0.0).xxx) * _Intensity * _Fade;
    return half4(c, 1.0);
}

// InValue 가 InThreshold 를 넘는 만큼의 흰 코어 (0~1).
float SC_Hot(float InValue, float InThreshold)
{
    float hot = saturate((InValue - InThreshold) / max(1e-4, 1.0 - InThreshold));
    return hot * hot;
}

// 분출(시전 후) 칼날 띠 (U 손잡이→끝, V 폭): 칼날을 타고 끝으로 흐르는 빛. (색 밝기, 흰빛)
float2 SC_BladeLight(float2 uv, float InTime, float InSeed, float InProgress)
{
    float across = 1.0 - abs(uv.y * 2.0 - 1.0);
    float core = pow(across, 3.0);
    float flow = SC_Noise(float2(uv.x * 8.0 - InTime * 6.0, InSeed));
    float tip = smoothstep(0.4, 1.0, uv.x);
    float ends = smoothstep(0.0, 0.06, uv.x) * smoothstep(1.0, 0.85, uv.x);
    float glow = core * (0.5 + 0.9 * flow) * (0.45 + tip) * ends * (1.0 - InProgress * 0.5);
    float white = pow(across, 12.0) * ends * (0.6 + 0.6 * tip) * (1.0 - InProgress * 0.6);
    return float2(glow, white);
}

#endif
