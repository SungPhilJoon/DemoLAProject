using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Character;
using DemoLA.Character.Identity;
using DemoLA.Core;
using Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DemoLA.UI
{
    /// <summary>
    /// 아이덴티티 패널 (스킬 바 왼쪽). 자원(게이지 막대 또는 구슬)과 Z / X 두 칸을 보여 준다.
    /// 칸은 쓸 수 있으면 밝게 테두리가 빛나고, 못 쓰면 어둡다. 마우스를 올리면 설명 툴팁.
    /// </summary>
    public class IdentityHudView : BaseBehaviour
    {
        private class KeySlot
        {
            public Image Icon;
            public Image Dim;
            public Outline Glow;
            public Text Name;
        }

        private static Sprite circleSprite;

        private PartyCharacter player;
        private RectTransform root;
        private Text title;
        private Bar gauge;
        private RectTransform orbRow;
        private readonly List<Image> orbs = new List<Image>();
        private KeySlot zSlot;
        private KeySlot xSlot;
        private IdentityAbility boundZ;
        private IdentitySecondaryAbility boundX;

        public static IdentityHudView Create(Transform InParent)
        {
            var rect = UIKit.Anchored(InParent, "Identity", new Vector2(0.0f, 0.0f), new Vector2(-250.0f, 0.0f), new Vector2(236.0f, 168.0f));
            UIKit.Image(rect, new Color(0.05f, 0.06f, 0.09f, 0.82f), true);
            var view = rect.gameObject.AddComponent<IdentityHudView>();
            view.root = rect;
            view.build();
            return view;
        }

        public void SetPlayer(PartyCharacter InPlayer)
        {
            player = InPlayer;
            boundZ = null;
            boundX = null;
        }

        private void build()
        {
            var titleRect = UIKit.Rect(root, "Title", new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(10.0f, -28.0f), new Vector2(-10.0f, -4.0f));
            title = UIKit.Label(titleRect, "아이덴티티", 17, TextAnchor.MiddleLeft, UIKit.AccentColor);

            gauge = UIKit.CreateBar(root, "Gauge", new Color(0.85f, 0.35f, 0.95f), new Color(0.0f, 0.0f, 0.0f, 0.7f));
            var gaugeRect = gauge.Rect;
            gaugeRect.anchorMin = new Vector2(0.0f, 1.0f);
            gaugeRect.anchorMax = new Vector2(1.0f, 1.0f);
            gaugeRect.offsetMin = new Vector2(10.0f, -50.0f);
            gaugeRect.offsetMax = new Vector2(-10.0f, -32.0f);
            gauge.Label.fontSize = 13;

            orbRow = UIKit.Rect(root, "Orbs", new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(10.0f, -50.0f), new Vector2(-10.0f, -30.0f));
            var orbLayout = orbRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            orbLayout.spacing = 6.0f;
            orbLayout.childAlignment = TextAnchor.MiddleLeft;
            orbLayout.childControlWidth = false;
            orbLayout.childControlHeight = false;
            orbLayout.childForceExpandWidth = false;

            zSlot = createSlot("Z", new Vector2(10.0f, 8.0f));
            xSlot = createSlot("X", new Vector2(122.0f, 8.0f));
        }

        private KeySlot createSlot(string InKey, Vector2 InPosition)
        {
            var slot = new KeySlot();
            var frame = UIKit.Anchored(root, $"Slot{InKey}", new Vector2(0.0f, 0.0f), InPosition, new Vector2(104.0f, 104.0f));
            UIKit.Image(frame, new Color(0.0f, 0.0f, 0.0f, 0.35f));
            slot.Glow = frame.gameObject.AddComponent<Outline>();
            slot.Glow.effectDistance = new Vector2(2.0f, -2.0f);

            var iconRect = UIKit.Anchored(frame, "Icon", new Vector2(0.5f, 1.0f), new Vector2(0.0f, -4.0f), new Vector2(66.0f, 66.0f));
            iconRect.pivot = new Vector2(0.5f, 1.0f);
            slot.Icon = iconRect.gameObject.AddComponent<Image>();
            slot.Icon.raycastTarget = true;
            var trigger = iconRect.gameObject.AddComponent<TooltipTrigger>();
            bool isZ = InKey == "Z";
            trigger.Provider = () => isZ ? tooltipZ() : tooltipX();

            var dimRect = UIKit.Stretch(iconRect, "Dim");
            slot.Dim = UIKit.Image(dimRect, new Color(0.0f, 0.0f, 0.0f, 0.6f));

            var keyRect = UIKit.Rect(iconRect, "Key", Vector2.zero, Vector2.one, new Vector2(4.0f, 2.0f), new Vector2(-2.0f, -2.0f));
            var key = UIKit.Label(keyRect, InKey, 18, TextAnchor.UpperLeft, Color.white);
            key.fontStyle = FontStyle.Bold;
            key.gameObject.AddComponent<Outline>().effectColor = Color.black;

            var nameRect = UIKit.Rect(frame, "Name", new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(2.0f, 2.0f), new Vector2(-2.0f, 32.0f));
            slot.Name = UIKit.Label(nameRect, string.Empty, 14, TextAnchor.MiddleCenter, Color.white);
            return slot;
        }

        private void Update()
        {
            bool visible = player != null && player.Identity != null;
            if (root.gameObject.activeSelf != visible)
            {
                root.gameObject.SetActive(visible);
            }

            if (visible == false)
            {
                return;
            }

            var z = player.Identity;
            var x = player.IdentityX;
            if (z != boundZ || x != boundX)
            {
                bind(z, x);
            }

            // 자원
            float value = player.Stats.Get(EAttr.IdentityGauge);
            var burst = z as SlayerBurstAbility;
            if (z.MaxOrbs > 0)
            {
                for (int i = 0; i < orbs.Count; ++i)
                {
                    orbs[i].color = i < z.Orbs ? new Color(0.95f, 0.85f, 0.35f) : new Color(0.25f, 0.25f, 0.3f);
                }

                // 다음 구슬까지 게이지 (구슬이 가득이면 꽉 참).
                float next = z.Orbs >= z.MaxOrbs ? 1.0f : Mathf.Clamp01(value / IdentityAbility.GaugePerOrb);
                gauge.Set(next, string.Empty);
            }
            else if (burst != null && burst.ModeActive == true)
            {
                gauge.Fill.color = new Color(1.0f, 0.45f, 0.15f);
                gauge.Set(burst.ModeRemainingRatio, burst.StatusText);
            }
            else
            {
                gauge.Fill.color = new Color(0.85f, 0.35f, 0.95f);
                float max = Mathf.Max(1.0f, player.Stats.Get(EAttr.MaxIdentityGauge));
                gauge.Set(value / max, $"{value:0} / {max:0}");
            }

            drawSlot(zSlot, z.HasResource && (burst == null || burst.ModeActive == false), z.DisplayName);
            drawSlot(xSlot, x != null && x.IsReady, x != null ? x.DisplayName : "-");
        }

        private void bind(IdentityAbility InZ, IdentitySecondaryAbility InX)
        {
            boundZ = InZ;
            boundX = InX;
            title.text = $"아이덴티티 · {player.ClassDefinition.DisplayName}";
            zSlot.Icon.sprite = IconFactory.Get(EIconKind.Skill, "identityZ" + InZ.DisplayName, 4);
            xSlot.Icon.sprite = IconFactory.Get(EIconKind.Skill, "identityX" + (InX != null ? InX.DisplayName : "-"), 3);

            for (int i = 0; i < orbs.Count; ++i)
            {
                ObjectUtil.SafeDestroy(orbs[i].gameObject);
            }

            orbs.Clear();
            bool orbType = InZ.MaxOrbs > 0;
            orbRow.gameObject.SetActive(orbType);
            // 구슬형은 구슬 줄 아래에 얇은 "다음 구슬" 막대, 게이지형은 굵은 막대.
            var gaugeRect = gauge.Rect;
            gaugeRect.offsetMin = orbType ? new Vector2(10.0f, -58.0f) : new Vector2(10.0f, -50.0f);
            gaugeRect.offsetMax = orbType ? new Vector2(-10.0f, -53.0f) : new Vector2(-10.0f, -32.0f);
            gauge.Fill.color = orbType ? new Color(0.95f, 0.85f, 0.35f, 0.8f) : new Color(0.85f, 0.35f, 0.95f);
            for (int i = 0; i < InZ.MaxOrbs; ++i)
            {
                var orbRect = (RectTransform)new GameObject("Orb", typeof(RectTransform)).transform;
                orbRect.SetParent(orbRow, false);
                orbRect.sizeDelta = new Vector2(18.0f, 18.0f);
                var image = orbRect.gameObject.AddComponent<Image>();
                image.sprite = circle();
                image.raycastTarget = false;
                orbs.Add(image);
            }
        }

        private static void drawSlot(KeySlot InSlot, bool InReady, string InName)
        {
            InSlot.Dim.enabled = InReady == false;
            InSlot.Name.text = InName;
            InSlot.Name.color = InReady ? Color.white : new Color(1.0f, 1.0f, 1.0f, 0.5f);
            // 준비되면 금색 테두리가 맥동한다.
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 6.0f);
            InSlot.Glow.effectColor = InReady ? new Color(1.0f, 0.8f, 0.3f, pulse) : new Color(0.0f, 0.0f, 0.0f, 0.0f);
        }

        private TooltipContent tooltipZ()
        {
            var z = player != null ? player.Identity : null;
            if (z == null)
            {
                return null;
            }

            var content = new TooltipContent { Icon = zSlot.Icon.sprite, Title = $"[Z] {z.DisplayName}", Subtitle = $"아이덴티티 · {z.StatusText}" };
            content.Line(z.Description);
            content.Line("스킬을 적중시키면 아이덴티티 게이지가 찬다.", TooltipContent.Dim);
            return content;
        }

        private TooltipContent tooltipX()
        {
            var x = player != null ? player.IdentityX : null;
            if (x == null)
            {
                return null;
            }

            var content = new TooltipContent { Icon = xSlot.Icon.sprite, Title = $"[X] {x.DisplayName}", Subtitle = x.IsReady ? "사용 가능" : "조건 미충족" };
            content.Line(x.Description);
            content.Line("Z 와 같은 게이지 / 구슬을 나눠 쓴다.", TooltipContent.Dim);
            return content;
        }

        private static Sprite circle()
        {
            if (circleSprite != null)
            {
                return circleSprite;
            }

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(size * 0.5f - 1.0f - d);
                    // 가장자리 살짝 어둡게 (구슬 느낌).
                    float shade = Mathf.Lerp(1.0f, 0.65f, Mathf.Clamp01(d / (size * 0.5f)));
                    pixels[y * size + x] = new Color(shade, shade, shade, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return circleSprite;
        }
    }
}
