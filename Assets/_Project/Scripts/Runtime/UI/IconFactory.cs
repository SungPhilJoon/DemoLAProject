using System.Collections.Generic;
using UnityEngine;

namespace DemoLA.UI
{
    public enum EIconKind
    {
        Skill = 0,
        Engraving = 1,
        Card = 2,
        Weapon = 3,
        Armor = 4,
        Necklace = 5,
        Earring = 6,
        Ring = 7,
        Stone = 8,
        GemDamage = 9,
        GemCooldown = 10,
        BattleItem = 11,
        Stats = 12,
        Keys = 13,
        Empty = 14,
        Buff = 15,
        Debuff = 16,
    }

    /// <summary>
    /// 아이콘 아트가 없는 단계에서 쓰는 절차적 아이콘. 종류마다 모양(글리프)이 다르고,
    /// 같은 종류 안에서는 이름 해시로 색이 달라져 서로 구분된다. 결과는 (종류, 키, 등급) 로 캐시한다.
    /// 실제 아트가 생기면 Get 이 Addressables 스프라이트를 돌려주게 바꾸면 된다.
    /// </summary>
    public static class IconFactory
    {
        private const int Size = 64;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>InGrade 0~4: 테두리 색 (일반/고급/희귀/영웅/전설 느낌의 단계).</summary>
        public static Sprite Get(EIconKind InKind, string InKey, int InGrade = 2)
        {
            string cacheKey = $"{(int)InKind}|{InKey}|{InGrade}";
            Sprite sprite;
            if (Cache.TryGetValue(cacheKey, out sprite) == true && sprite != null)
            {
                return sprite;
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[Size * Size];
            var main = mainColor(InKind, InKey);
            paint(pixels, InKind, main, gradeColor(InGrade));
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100.0f);
            sprite.name = cacheKey;
            Cache[cacheKey] = sprite;
            return sprite;
        }

        public static Color GradeColor(int InGrade)
        {
            return gradeColor(InGrade);
        }

        private static Color gradeColor(int InGrade)
        {
            switch (Mathf.Clamp(InGrade, 0, 5))
            {
                case 5: return new Color(0.95f, 0.25f, 0.2f);
                case 0: return new Color(0.55f, 0.55f, 0.55f);
                case 1: return new Color(0.45f, 0.8f, 0.35f);
                case 2: return new Color(0.35f, 0.6f, 1.0f);
                case 3: return new Color(0.75f, 0.4f, 1.0f);
                default: return new Color(1.0f, 0.65f, 0.15f);
            }
        }

        private static Color mainColor(EIconKind InKind, string InKey)
        {
            switch (InKind)
            {
                case EIconKind.GemDamage: return new Color(0.95f, 0.25f, 0.2f);
                case EIconKind.GemCooldown: return new Color(0.25f, 0.55f, 1.0f);
                case EIconKind.Empty: return new Color(0.3f, 0.3f, 0.32f);
            }

            // 이름 해시 → 색상환 위치. 채도/명도는 고정해서 톤을 맞춘다.
            int hash = 17;
            if (InKey != null)
            {
                for (int i = 0; i < InKey.Length; ++i)
                {
                    hash = hash * 31 + InKey[i];
                }
            }

            float hue = Mathf.Repeat((hash & 0xFFFF) / 65535.0f + (int)InKind * 0.137f, 1.0f);
            return Color.HSVToRGB(hue, 0.55f, 0.95f);
        }

        // ------------------------------------------------------------------ painting

        private static void paint(Color32[] OutPixels, EIconKind InKind, Color InMain, Color InBorder)
        {
            var dark = InMain * 0.22f;
            dark.a = 1.0f;
            var mid = InMain * 0.45f;
            mid.a = 1.0f;
            for (int y = 0; y < Size; ++y)
            {
                for (int x = 0; x < Size; ++x)
                {
                    float u = (x + 0.5f) / Size;
                    float v = (y + 0.5f) / Size;
                    // 배경: 위로 갈수록 밝은 세로 그라데이션 + 가장자리 비네팅.
                    var color = Color.Lerp(dark, mid, v);
                    float edge = Mathf.Min(Mathf.Min(u, 1.0f - u), Mathf.Min(v, 1.0f - v));
                    color *= Mathf.Lerp(0.7f, 1.0f, Mathf.Clamp01(edge * 6.0f));

                    float glyph = glyphCoverage(InKind, u * 2.0f - 1.0f, v * 2.0f - 1.0f);
                    if (glyph > 0.0f)
                    {
                        // 글리프: 위쪽이 밝은 금속 느낌.
                        var fill = Color.Lerp(InMain, Color.white, 0.25f + 0.35f * v);
                        color = Color.Lerp(color, fill, glyph);
                    }

                    // 테두리 (등급 색) 2px + 안쪽 어두운 선 1px.
                    int border = Mathf.Min(Mathf.Min(x, Size - 1 - x), Mathf.Min(y, Size - 1 - y));
                    if (border < 2)
                    {
                        color = InBorder;
                    }
                    else if (border == 2)
                    {
                        color = Color.Lerp(color, Color.black, 0.6f);
                    }

                    color.a = 1.0f;
                    OutPixels[y * Size + x] = color;
                }
            }
        }

        /// <summary>(-1..1) 좌표에서 글리프 덮임 정도 0..1 (부드러운 가장자리).</summary>
        private static float glyphCoverage(EIconKind InKind, float InX, float InY)
        {
            switch (InKind)
            {
                case EIconKind.Skill:
                    // 방사형 섬광: 원 + 8갈래 빛줄기.
                    float r = Mathf.Sqrt(InX * InX + InY * InY);
                    float angle = Mathf.Atan2(InY, InX);
                    float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 4.0f)), 6.0f) * 0.55f;
                    return smooth(0.3f + rays - r);
                case EIconKind.Engraving:
                    // 펼친 책: 두 장의 사각형 + 가운데 접힌 선.
                    float book = Mathf.Min(0.55f - Mathf.Abs(InX), 0.42f - Mathf.Abs(InY + 0.02f * Mathf.Abs(InX)));
                    float spine = 0.03f - Mathf.Abs(InX);
                    return Mathf.Max(0.0f, smooth(book) - smooth(spine));
                case EIconKind.Card:
                    // 세로 카드 + 안쪽 테두리.
                    float outer = Mathf.Min(0.42f - Mathf.Abs(InX), 0.62f - Mathf.Abs(InY));
                    float inner = Mathf.Min(0.33f - Mathf.Abs(InX), 0.53f - Mathf.Abs(InY));
                    return Mathf.Max(smooth(outer) - smooth(inner) * 0.6f, 0.0f);
                case EIconKind.Weapon:
                    // 대각선 칼날 + 손잡이 가드.
                    float along = (InX + InY) * 0.7071f;
                    float across = (InX - InY) * 0.7071f;
                    float blade = Mathf.Min(0.09f - Mathf.Abs(across), 0.7f - Mathf.Abs(along - 0.05f));
                    float guard = Mathf.Min(0.3f - Mathf.Abs(across), 0.06f - Mathf.Abs(along + 0.45f));
                    return smooth(Mathf.Max(blade, guard));
                case EIconKind.Armor:
                    // 방패: 위는 사각, 아래는 뾰족.
                    float top = Mathf.Min(0.5f - Mathf.Abs(InX), 0.55f - InY);
                    float bottom = (InY + 0.65f) - Mathf.Abs(InX) * 1.3f;
                    return smooth(Mathf.Min(top, bottom));
                case EIconKind.Necklace:
                    // 아래로 처진 줄 + 펜던트.
                    float chain = 0.05f - Mathf.Abs(Mathf.Sqrt(InX * InX + (InY - 0.3f) * (InY - 0.3f)) - 0.5f);
                    float chainMask = smooth(0.35f - InY);
                    float pendant = 0.2f - Mathf.Sqrt(InX * InX + (InY + 0.35f) * (InY + 0.35f));
                    return Mathf.Max(smooth(chain) * chainMask, smooth(pendant));
                case EIconKind.Earring:
                    // 고리 + 늘어진 물방울.
                    float hook = 0.05f - Mathf.Abs(Mathf.Sqrt(InX * InX + (InY - 0.4f) * (InY - 0.4f)) - 0.18f);
                    float drop = 0.25f - Mathf.Sqrt(InX * InX * 1.6f + (InY + 0.2f) * (InY + 0.2f));
                    return smooth(Mathf.Max(hook, drop));
                case EIconKind.Ring:
                    // 반지: 고리 + 위쪽 보석.
                    float band = 0.09f - Mathf.Abs(Mathf.Sqrt(InX * InX + (InY + 0.1f) * (InY + 0.1f)) - 0.42f);
                    float jewel = 0.16f - (Mathf.Abs(InX) + Mathf.Abs(InY - 0.38f));
                    return smooth(Mathf.Max(band, jewel));
                case EIconKind.Stone:
                    // 육각형 돌.
                    float ax = Mathf.Abs(InX);
                    float ay = Mathf.Abs(InY);
                    return smooth(Mathf.Min(0.58f - ay, 0.58f * 1.15f - (ax * 0.866f + ay * 0.5f) * 1.15f));
                case EIconKind.GemDamage:
                case EIconKind.GemCooldown:
                    // 마름모 보석 + 위쪽 면.
                    float diamond = 0.62f - (Mathf.Abs(InX) + Mathf.Abs(InY) * 0.85f);
                    return smooth(diamond);
                case EIconKind.BattleItem:
                    // 물약 병: 둥근 몸통 + 병목.
                    float body = 0.42f - Mathf.Sqrt(InX * InX + (InY + 0.15f) * (InY + 0.15f));
                    float neck = Mathf.Min(0.12f - Mathf.Abs(InX), 0.2f - Mathf.Abs(InY - 0.38f));
                    return smooth(Mathf.Max(body, neck));
                case EIconKind.Stats:
                    // 막대 그래프 3개.
                    float b1 = Mathf.Min(0.1f - Mathf.Abs(InX + 0.35f), 0.25f - Mathf.Abs(InY + 0.3f));
                    float b2 = Mathf.Min(0.1f - Mathf.Abs(InX), 0.4f - Mathf.Abs(InY + 0.15f));
                    float b3 = Mathf.Min(0.1f - Mathf.Abs(InX - 0.35f), 0.55f - Mathf.Abs(InY));
                    return smooth(Mathf.Max(b1, Mathf.Max(b2, b3)));
                case EIconKind.Keys:
                    // 키캡: 둥근 사각형 테두리.
                    float key = Mathf.Min(0.48f - Mathf.Abs(InX), 0.48f - Mathf.Abs(InY));
                    float keyInner = Mathf.Min(0.34f - Mathf.Abs(InX), 0.34f - Mathf.Abs(InY - 0.05f));
                    return Mathf.Max(0.0f, smooth(key) - smooth(keyInner) * 0.7f);
                case EIconKind.Buff:
                    // 위쪽 화살표.
                    float upHead = (0.62f - InY) - Mathf.Abs(InX) * 1.1f;
                    float upHeadBase = InY - 0.0f;
                    float upShaft = Mathf.Min(0.16f - Mathf.Abs(InX), 0.55f - Mathf.Abs(InY + 0.25f));
                    return smooth(Mathf.Max(Mathf.Min(upHead, upHeadBase), upShaft));
                case EIconKind.Debuff:
                    // 아래쪽 화살표.
                    float downHead = (0.62f + InY) - Mathf.Abs(InX) * 1.1f;
                    float downHeadBase = -InY;
                    float downShaft = Mathf.Min(0.16f - Mathf.Abs(InX), 0.55f - Mathf.Abs(InY - 0.25f));
                    return smooth(Mathf.Max(Mathf.Min(downHead, downHeadBase), downShaft));
                default:
                    return 0.0f;
            }
        }

        private static float smooth(float InDistance)
        {
            // 거리 0 근처 약 1.5px 폭으로 부드럽게.
            return Mathf.Clamp01(InDistance * Size * 0.33f + 0.5f);
        }
    }
}
