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
        Esther = 17,
    }

    /// <summary>
    /// 아이콘 스프라이트를 만든다. (종류, 키, 등급) 으로 캐시한다.
    ///  - 틀: 종류마다 모양이 다르다 → 한눈에 구분 (스킬 = 사각 · 각인 = 원형 메달 · 카드 = 세로 카드 · 보석 = 마름모 ·
    ///        장비/악세/아이템 = 둥근 사각 · 버프/디버프 = 원형)
    ///  - 그림: IconLibrary(game-icons.net 글리프)에서 (종류, 키) 로 찾는다. 없으면 절차적 글리프로 대체.
    ///  - 색: 같은 종류 안에서는 이름 해시로 배경 색이 달라지고, 테두리는 등급 색.
    /// 라이브러리는 GameBootstrap 이 GameDatabase.Icons 로 넣는다 (에디터에서는 비어 있으면 에셋을 직접 읽는다).
    /// </summary>
    public static class IconFactory
    {
        private const int Size = 96;
        public const string LibraryPath = "Assets/_Project/Art/Icons/IconLibrary.asset";
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<Texture2D, Color32[]> GlyphPixels = new Dictionary<Texture2D, Color32[]>();
        private static IconLibrary library;

        private enum EFrame
        {
            Square = 0,
            Medallion = 1,
            Card = 2,
            Diamond = 3,
            Rounded = 4,
            Circle = 5,
        }

        public static IconLibrary Library
        {
            get
            {
#if UNITY_EDITOR
                if (library == null)
                {
                    library = UnityEditor.AssetDatabase.LoadAssetAtPath<IconLibrary>(LibraryPath);
                }
#endif
                return library;
            }
            set
            {
                if (library != value)
                {
                    library = value;
                    ClearCache();
                }
            }
        }

        public static void ClearCache()
        {
            Cache.Clear();
            GlyphPixels.Clear();
        }

        /// <summary>InGrade 0~5: 테두리 색 (일반/고급/희귀/영웅/전설/감소 느낌의 단계).</summary>
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
            var glyph = InKind != EIconKind.Empty && Library != null ? Library.Find(InKind, InKey) : null;
            paint(pixels, InKind, mainColor(InKind, InKey), gradeColor(InGrade), glyph);
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

        private static EFrame frameOf(EIconKind InKind)
        {
            switch (InKind)
            {
                case EIconKind.Engraving: return EFrame.Medallion;
                case EIconKind.Card: return EFrame.Card;
                case EIconKind.GemDamage:
                case EIconKind.GemCooldown: return EFrame.Diamond;
                case EIconKind.Weapon:
                case EIconKind.Armor:
                case EIconKind.Necklace:
                case EIconKind.Earring:
                case EIconKind.Ring:
                case EIconKind.Stone:
                case EIconKind.BattleItem: return EFrame.Rounded;
                case EIconKind.Buff:
                case EIconKind.Debuff:
                case EIconKind.Esther: return EFrame.Circle;
                default: return EFrame.Square;
            }
        }

        private static Color mainColor(EIconKind InKind, string InKey)
        {
            switch (InKind)
            {
                case EIconKind.GemDamage: return new Color(0.95f, 0.25f, 0.2f);
                case EIconKind.GemCooldown: return new Color(0.25f, 0.55f, 1.0f);
                case EIconKind.Buff: return new Color(0.35f, 0.85f, 0.4f);
                case EIconKind.Debuff: return new Color(0.95f, 0.3f, 0.3f);
                case EIconKind.Engraving: return new Color(0.95f, 0.75f, 0.35f);
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
            return Color.HSVToRGB(hue, 0.6f, 0.9f);
        }

        // ------------------------------------------------------------------ painting

        /// <summary>틀 모양의 부호 거리 (안쪽 +, -1..1 좌표).</summary>
        private static float frameDistance(EFrame InFrame, float InX, float InY)
        {
            float ax = Mathf.Abs(InX);
            float ay = Mathf.Abs(InY);
            switch (InFrame)
            {
                case EFrame.Medallion:
                case EFrame.Circle:
                    return 0.97f - Mathf.Sqrt(InX * InX + InY * InY);
                case EFrame.Card:
                    return roundedBox(ax, ay, 0.74f, 0.97f, 0.1f);
                case EFrame.Diamond:
                    return (1.0f - (ax + ay)) * 0.7071f;
                case EFrame.Rounded:
                    return roundedBox(ax, ay, 0.97f, 0.97f, 0.28f);
                default:
                    return roundedBox(ax, ay, 0.97f, 0.97f, 0.06f);
            }
        }

        private static float roundedBox(float InAx, float InAy, float InHalfW, float InHalfH, float InRadius)
        {
            float qx = InAx - (InHalfW - InRadius);
            float qy = InAy - (InHalfH - InRadius);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0.0f) * Mathf.Max(qx, 0.0f) + Mathf.Max(qy, 0.0f) * Mathf.Max(qy, 0.0f));
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0.0f);
            return InRadius - (outside + inside);
        }

        /// <summary>글리프가 차지할 크기 (틀 안쪽에 맞춘다).</summary>
        private static float glyphScale(EFrame InFrame)
        {
            switch (InFrame)
            {
                case EFrame.Medallion: return 0.62f;
                case EFrame.Card: return 0.62f;
                case EFrame.Diamond: return 0.5f;
                case EFrame.Circle: return 0.62f;
                default: return 0.72f;
            }
        }

        private static void paint(Color32[] OutPixels, EIconKind InKind, Color InMain, Color InBorder, Texture2D InGlyph)
        {
            var frame = frameOf(InKind);
            var glyphData = InGlyph != null ? pixelsOf(InGlyph) : null;
            float scale = glyphScale(frame);
            float px = 2.0f / Size;
            float borderWidth = frame == EFrame.Medallion ? 0.12f : 0.07f;

            // 배경 톤: 각인은 어두운 청동, 장비류는 중립 회청색, 나머지는 주색을 어둡게.
            Color bgTop;
            Color bgBottom;
            Color glyphColor;
            switch (frame)
            {
                case EFrame.Medallion:
                    bgTop = new Color(0.24f, 0.19f, 0.13f);
                    bgBottom = new Color(0.1f, 0.08f, 0.06f);
                    glyphColor = Color.Lerp(InMain, Color.white, 0.2f);
                    break;
                case EFrame.Card:
                    bgTop = Color.Lerp(InMain, new Color(0.15f, 0.13f, 0.2f), 0.55f);
                    bgBottom = Color.Lerp(InMain, Color.black, 0.85f);
                    glyphColor = new Color(0.98f, 0.95f, 0.88f);
                    break;
                case EFrame.Diamond:
                    bgTop = Color.Lerp(InMain, Color.white, 0.15f);
                    bgBottom = Color.Lerp(InMain, Color.black, 0.6f);
                    glyphColor = Color.white;
                    break;
                case EFrame.Rounded:
                    bgTop = new Color(0.22f, 0.25f, 0.3f);
                    bgBottom = new Color(0.08f, 0.09f, 0.11f);
                    glyphColor = Color.Lerp(InMain, Color.white, 0.45f);
                    break;
                case EFrame.Circle:
                    bgTop = Color.Lerp(InMain, Color.black, 0.45f);
                    bgBottom = Color.Lerp(InMain, Color.black, 0.8f);
                    glyphColor = Color.white;
                    break;
                default:
                    bgTop = Color.Lerp(InMain, Color.black, 0.25f);
                    bgBottom = Color.Lerp(InMain, Color.black, 0.8f);
                    glyphColor = Color.white;
                    break;
            }

            if (InKind == EIconKind.Empty)
            {
                bgTop = new Color(0.16f, 0.17f, 0.2f);
                bgBottom = new Color(0.09f, 0.1f, 0.12f);
            }

            for (int y = 0; y < Size; ++y)
            {
                for (int x = 0; x < Size; ++x)
                {
                    float u = (x + 0.5f) / Size;
                    float v = (y + 0.5f) / Size;
                    float nx = u * 2.0f - 1.0f;
                    float ny = v * 2.0f - 1.0f;
                    float d = frameDistance(frame, nx, ny);
                    float shapeAlpha = Mathf.Clamp01(d / px + 0.5f);
                    if (shapeAlpha <= 0.0f)
                    {
                        OutPixels[y * Size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // 배경: 위가 밝은 세로 그라데이션 + 가운데 은은한 빛.
                    var color = Color.Lerp(bgBottom, bgTop, v);
                    float glow = Mathf.Clamp01(1.0f - Mathf.Sqrt(nx * nx + (ny - 0.1f) * (ny - 0.1f)));
                    color = Color.Lerp(color, color * 1.6f, glow * 0.35f);

                    if (InKind != EIconKind.Empty)
                    {
                        float glyph;
                        float shadow;
                        if (glyphData != null)
                        {
                            glyph = sampleGlyph(glyphData, InGlyph.width, InGlyph.height, nx / scale, ny / scale);
                            shadow = sampleGlyph(glyphData, InGlyph.width, InGlyph.height, (nx - 0.045f) / scale, (ny + 0.045f) / scale);
                        }
                        else
                        {
                            glyph = glyphCoverage(InKind, nx / (scale * 1.25f), ny / (scale * 1.25f));
                            shadow = glyphCoverage(InKind, (nx - 0.045f) / (scale * 1.25f), (ny + 0.045f) / (scale * 1.25f));
                        }

                        color = Color.Lerp(color, Color.black, shadow * 0.6f);
                        if (glyph > 0.0f)
                        {
                            var fill = Color.Lerp(glyphColor * 0.8f, glyphColor, v);
                            color = Color.Lerp(color, fill, glyph);
                        }
                    }

                    // 테두리 (등급 색) + 안쪽 어두운 선.
                    if (d < borderWidth)
                    {
                        var border = Color.Lerp(InBorder * 0.75f, InBorder, Mathf.Clamp01(v + 0.2f));
                        color = Color.Lerp(color, border, Mathf.Clamp01((borderWidth - d) / px));
                    }
                    else if (d < borderWidth + px * 1.5f)
                    {
                        color = Color.Lerp(color, Color.black, 0.55f);
                    }

                    if (frame == EFrame.Medallion && Mathf.Abs(d - borderWidth * 0.5f) < px * 0.6f)
                    {
                        // 메달 테두리 안의 가는 홈.
                        color = Color.Lerp(color, Color.black, 0.35f);
                    }

                    color.a = shapeAlpha;
                    OutPixels[y * Size + x] = color;
                }
            }
        }

        private static Color32[] pixelsOf(Texture2D InTexture)
        {
            Color32[] data;
            if (GlyphPixels.TryGetValue(InTexture, out data) == true)
            {
                return data;
            }

            try
            {
                data = InTexture.isReadable == true ? InTexture.GetPixels32() : null;
            }
            catch (UnityException)
            {
                data = null;
            }

            // Read/Write 가 꺼진 텍스처면 null → 절차적 글리프 (IconGlyphImporter 가 Glyphs 폴더는 켜 준다).
            GlyphPixels[InTexture] = data;
            return data;
        }

        /// <summary>(-1..1) 좌표 → 글리프 알파 (쌍선형). 범위 밖은 0.</summary>
        private static float sampleGlyph(Color32[] InData, int InWidth, int InHeight, float InX, float InY)
        {
            float fx = (InX * 0.5f + 0.5f) * InWidth - 0.5f;
            float fy = (InY * 0.5f + 0.5f) * InHeight - 0.5f;
            if (fx < -1.0f || fy < -1.0f || fx > InWidth || fy > InHeight)
            {
                return 0.0f;
            }

            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;
            float a00 = alphaAt(InData, InWidth, InHeight, x0, y0);
            float a10 = alphaAt(InData, InWidth, InHeight, x0 + 1, y0);
            float a01 = alphaAt(InData, InWidth, InHeight, x0, y0 + 1);
            float a11 = alphaAt(InData, InWidth, InHeight, x0 + 1, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a00, a10, tx), Mathf.Lerp(a01, a11, tx), ty);
        }

        private static float alphaAt(Color32[] InData, int InWidth, int InHeight, int InX, int InY)
        {
            if (InX < 0 || InY < 0 || InX >= InWidth || InY >= InHeight)
            {
                return 0.0f;
            }

            return InData[InY * InWidth + InX].a / 255.0f;
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
