using System.Collections.Generic;
using DemoLA.Attributes;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Effects;
using Framework;
using Framework.Ability;
using UnityEngine;
using UnityEngine.UI;

namespace DemoLA.UI
{
    /// <summary>
    /// 체력바 옆 버프/디버프 아이콘 줄. 대상의 활성 GameplayEffect 를 읽어
    ///  - 표시: 지속시간이 있는 효과 전부 + 표시용 태그(아래 표)를 주는 무한 효과 (빌드 패시브 · 쿨다운은 숨김)
    ///  - 구분: 태그 표의 성격 → 없으면 "적이 걸었으면 디버프, 아니면 버프"
    ///  - 아이콘: 남은 시간만큼 어두운 시계방향 덮개 + 남은 초 + 중첩 수. 마우스를 올리면 설명 툴팁.
    /// </summary>
    public class StatusEffectBar : BaseBehaviour
    {
        public enum EFilter
        {
            Buffs = 0,
            Debuffs = 1,
        }

        private struct TagInfo
        {
            public string Name;
            public string Description;
            public bool IsDebuff;

            public TagInfo(string InName, bool InIsDebuff, string InDescription)
            {
                Name = InName;
                IsDebuff = InIsDebuff;
                Description = InDescription;
            }
        }

        private class Entry
        {
            public string Key;
            public string Name;
            public string Description;
            public bool IsDebuff;
            public float Remaining;
            public float Duration;
            public int Stacks;
            public string Source;
            public readonly List<string> Modifiers = new List<string>();
        }

        private class Slot
        {
            public RectTransform Rect;
            public Image Icon;
            public Image Sweep;
            public Text Time;
            public Text Stacks;
            public Entry Entry;
        }

        private static readonly Dictionary<int, TagInfo> TagTable = new Dictionary<int, TagInfo>
        {
            { (int)ETag.State_Stunned, new TagInfo("기절", true, "행동할 수 없습니다.") },
            { (int)ETag.State_Airborne, new TagInfo("공중에 뜸", true, "띄워져 행동할 수 없습니다.") },
            { (int)ETag.State_Invulnerable, new TagInfo("무적", false, "피해를 받지 않습니다.") },
            { (int)ETag.State_SuperArmor, new TagInfo("슈퍼아머", false, "경직 · 이동 불가 효과에 면역입니다.") },
            { (int)ETag.State_Groggy, new TagInfo("무력화", true, "아무 행동도 하지 못합니다.") },
            { (int)ETag.State_Silenced, new TagInfo("침묵", true, "스킬을 사용할 수 없습니다.") },
            { (int)ETag.State_Feared, new TagInfo("공포", true, "제어할 수 없이 도망칩니다.") },
            { (int)ETag.State_Frozen, new TagInfo("빙결", true, "얼어붙어 움직일 수 없습니다.") },
            { (int)ETag.State_Grabbed, new TagInfo("붙잡힘", true, "붙잡혀 움직일 수 없습니다.") },
            { (int)ETag.Buff_DeathWard, new TagInfo("방어 구슬", false, "즉사 공격을 한 번 막아 줍니다.") },
            { (int)ETag.Buff_DamageReduction, new TagInfo("피해 감소", false, "받는 피해가 감소합니다.") },
            { (int)ETag.Buff_Armor, new TagInfo("갑옷", false, "받는 피해가 크게 감소합니다. 그로기 중 부위 파괴로 중첩을 없앨 수 있습니다.") },
            { (int)ETag.Buff_SpeedUp, new TagInfo("가속", false, "이동 속도가 증가합니다.") },
            { (int)ETag.Buff_Shield, new TagInfo("보호막", false, "보호막이 피해를 대신 받습니다.") },
            { (int)ETag.Debuff_Bleed, new TagInfo("출혈", true, "주기적으로 피해를 받습니다.") },
            { (int)ETag.Debuff_OrbCurse, new TagInfo("구슬 저주", true, "같은 색 구슬을 다시 먹으면 위험합니다.") },
            { (int)ETag.Debuff_Darkness, new TagInfo("어둠", true, "시야가 가려집니다.") },
            { (int)ETag.Debuff_Slow, new TagInfo("둔화", true, "이동 속도가 감소합니다.") },
        };

        /// <summary>이 태그만 주는 효과는 표시하지 않는다 (내부 상태).</summary>
        private static readonly HashSet<int> HiddenTags = new HashSet<int>
        {
            (int)ETag.State_Dead, (int)ETag.State_Casting, (int)ETag.State_Dashing, (int)ETag.State_Falling,
            (int)ETag.Boss_CounterWindow, (int)ETag.Boss_StaggerCheck, (int)ETag.Boss_Untargetable,
            (int)ETag.Boss_Channeling, (int)ETag.Boss_PatternBusy,
        };

        private const float RefreshInterval = 0.1f;

        private CombatActor target;
        private EFilter filter;
        private float iconSize;
        private int maxIcons;
        private bool rightToLeft;
        private float refreshTimer;
        private readonly List<Slot> slots = new List<Slot>();
        private readonly List<Entry> entries = new List<Entry>();

        /// <summary>InParent 안에 아이콘 줄을 만든다. InRightToLeft 면 오른쪽 끝부터 왼쪽으로 쌓는다.</summary>
        public static StatusEffectBar Create(RectTransform InParent, EFilter InFilter, float InIconSize, int InMaxIcons, bool InRightToLeft)
        {
            var bar = InParent.gameObject.AddComponent<StatusEffectBar>();
            bar.filter = InFilter;
            bar.iconSize = InIconSize;
            bar.maxIcons = InMaxIcons;
            bar.rightToLeft = InRightToLeft;
            var layout = InParent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 3.0f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = InRightToLeft ? TextAnchor.LowerRight : TextAnchor.LowerLeft;
            layout.reverseArrangement = InRightToLeft;
            return bar;
        }

        public void SetTarget(CombatActor InTarget)
        {
            target = InTarget;
            refreshTimer = 0.0f;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            refreshTimer -= deltaTime;
            if (refreshTimer <= 0.0f)
            {
                refreshTimer = RefreshInterval;
                collect();
                bind();
            }

            // 남은 시간 표시는 매 프레임 부드럽게.
            for (int i = 0; i < slots.Count; ++i)
            {
                var slot = slots[i];
                if (slot.Entry == null || slot.Rect.gameObject.activeSelf == false)
                {
                    continue;
                }

                slot.Entry.Remaining = Mathf.Max(0.0f, slot.Entry.Remaining - deltaTime);
                drawTime(slot);
            }
        }

        // ------------------------------------------------------------------ collect

        private void collect()
        {
            entries.Clear();
            if (target == null || target.Asc == null || target.IsDead == true)
            {
                return;
            }

            var effects = target.Asc.Effects;
            for (int i = 0; i < effects.Count; ++i)
            {
                var entry = describe(effects[i]);
                if (entry == null || entry.IsDebuff != (filter == EFilter.Debuffs))
                {
                    continue;
                }

                // 같은 이름은 하나로 합치고 가장 긴 남은 시간을 쓴다 (예: 여러 명이 건 같은 디버프).
                var existing = entries.Find(e => e.Key == entry.Key);
                if (existing != null)
                {
                    existing.Stacks = Mathf.Max(existing.Stacks, entry.Stacks);
                    if (entry.Remaining > existing.Remaining)
                    {
                        existing.Remaining = entry.Remaining;
                        existing.Duration = entry.Duration;
                    }

                    continue;
                }

                entries.Add(entry);
            }

            // 무한(시간 없음) → 남은 시간 긴 순.
            entries.Sort((a, b) =>
            {
                bool aInfinite = a.Duration <= 0.0f;
                bool bInfinite = b.Duration <= 0.0f;
                if (aInfinite != bInfinite)
                {
                    return aInfinite ? -1 : 1;
                }

                return b.Remaining.CompareTo(a.Remaining);
            });
        }

        private Entry describe(ActiveGameplayEffect InEffect)
        {
            var spec = InEffect.Spec;
            var def = spec != null ? spec.Def : null;
            if (def == null || def.IsCooldown == true || def.DurationPolicy == EDurationPolicy.Instant)
            {
                return null;
            }

            // 대표 태그: 표에 있는 첫 태그.
            TagInfo info = default(TagInfo);
            bool hasInfo = false;
            bool onlyHidden = def.GrantedTags.Count > 0;
            for (int t = 0; t < def.GrantedTags.Count; ++t)
            {
                int tag = def.GrantedTags.GetTag(t);
                if (HiddenTags.Contains(tag) == false)
                {
                    onlyHidden = false;
                }

                if (hasInfo == false && TagTable.TryGetValue(tag, out info) == true)
                {
                    hasInfo = true;
                }
            }

            bool infinite = def.DurationPolicy == EDurationPolicy.Infinite;
            EffectNames.Info registered;
            bool isNamed = EffectNames.TryGet(def, out registered);
            // 무한 효과는 표시용 태그가 있을 때만 (장비 · 각인 · 카드 같은 빌드 패시브는 숨김).
            if (onlyHidden == true || (infinite == true && hasInfo == false && isNamed == false))
            {
                return null;
            }

            var source = spec.Source != null ? spec.Source.OwnerActor as CombatActor : null;
            var entry = new Entry();
            entry.Stacks = InEffect.StackCount;
            entry.Remaining = infinite ? 0.0f : InEffect.Remaining;
            entry.Duration = infinite ? 0.0f : (spec.DurationOverride >= 0.0f ? spec.DurationOverride : def.Duration);
            entry.Source = source != null ? source.name : string.Empty;

            for (int m = 0; m < def.Modifiers.Length; ++m)
            {
                var modifier = def.Modifiers[m];
                float value = InEffect.Magnitudes != null && m < InEffect.Magnitudes.Length ? InEffect.Magnitudes[m] : modifier.Value;
                entry.Modifiers.Add(modifierText(modifier.Attribute, modifier.Op, value * InEffect.StackCount));
            }

            EffectNames.Info named;
            if (EffectNames.TryGet(def, out named) == true)
            {
                entry.Name = named.Name;
                entry.Description = named.Description;
                bool hostileSource = source != null && CombatRegistry.IsHostile(source.Team, target.Team) == true;
                entry.IsDebuff = named.IsDebuff || hostileSource;
            }
            else if (hasInfo == true)
            {
                entry.Name = info.Name;
                entry.Description = info.Description;
                entry.IsDebuff = info.IsDebuff;
            }
            else
            {
                bool hostile = source != null && CombatRegistry.IsHostile(source.Team, target.Team) == true;
                entry.IsDebuff = hostile;
                entry.Name = entry.Modifiers.Count > 0 ? entry.Modifiers[0] : (hostile ? "약화 효과" : "강화 효과");
                entry.Description = hostile ? "적에게 받은 약화 효과입니다." : "아군에게 받은 강화 효과입니다.";
            }

            entry.Key = $"{entry.Name}|{def.Id}";
            return entry;
        }

        private static string modifierText(int InAttribute, EModifierOp InOp, float InValue)
        {
            string name = attrName(InAttribute);
            string sign = InValue >= 0.0f ? "+" : string.Empty;
            switch (InOp)
            {
                case EModifierOp.MultiplyPercent:
                    return $"{name} {sign}{InValue:0.##}%";
                case EModifierOp.Override:
                    return $"{name} = {InValue:0.##}";
                default:
                    return $"{name} {sign}{InValue:0.##}";
            }
        }

        private static string attrName(int InAttribute)
        {
            return DemoLA.Build.KoreanText.Attr((EAttr)InAttribute);
        }

        // ------------------------------------------------------------------ view

        private void bind()
        {
            int count = Mathf.Min(entries.Count, maxIcons);
            while (slots.Count < count)
            {
                slots.Add(createSlot());
            }

            for (int i = 0; i < slots.Count; ++i)
            {
                var slot = slots[i];
                bool active = i < count;
                if (slot.Rect.gameObject.activeSelf != active)
                {
                    slot.Rect.gameObject.SetActive(active);
                }

                if (active == false)
                {
                    slot.Entry = null;
                    continue;
                }

                var entry = entries[i];
                slot.Entry = entry;
                slot.Icon.sprite = IconFactory.Get(entry.IsDebuff ? EIconKind.Debuff : EIconKind.Buff, entry.Name, entry.IsDebuff ? 5 : 2);
                slot.Stacks.text = entry.Stacks > 1 ? entry.Stacks.ToString() : string.Empty;
                drawTime(slot);
            }
        }

        private void drawTime(Slot InSlot)
        {
            var entry = InSlot.Entry;
            if (entry.Duration <= 0.0f)
            {
                InSlot.Sweep.fillAmount = 0.0f;
                InSlot.Time.text = string.Empty;
                return;
            }

            // 지나간 시간만큼 어두운 덮개가 시계방향으로 찬다.
            InSlot.Sweep.fillAmount = 1.0f - Mathf.Clamp01(entry.Remaining / entry.Duration);
            InSlot.Time.text = entry.Remaining >= 10.0f ? $"{entry.Remaining:0}" : $"{entry.Remaining:0.0}";
            // 3초 이하면 깜빡여 곧 끝남을 알린다.
            InSlot.Icon.color = entry.Remaining <= 3.0f && Mathf.Repeat(Time.time * 3.0f, 1.0f) < 0.4f ? new Color(1.0f, 1.0f, 1.0f, 0.45f) : Color.white;
        }

        private Slot createSlot()
        {
            var slot = new Slot();
            Slot captured = slot;
            slot.Icon = UIKit.IconCell(transform, null, iconSize, string.Empty, () => tooltip(captured.Entry));
            slot.Rect = (RectTransform)slot.Icon.transform;

            var sweepRect = UIKit.Stretch(slot.Rect, "Sweep", 2.0f);
            slot.Sweep = UIKit.Image(sweepRect, new Color(0.0f, 0.0f, 0.0f, 0.6f));
            slot.Sweep.sprite = UIKit.WhiteSprite;
            slot.Sweep.type = Image.Type.Filled;
            slot.Sweep.fillMethod = Image.FillMethod.Radial360;
            slot.Sweep.fillOrigin = (int)Image.Origin360.Top;
            slot.Sweep.fillClockwise = true;

            slot.Time = cornerText(slot.Rect, TextAnchor.LowerCenter, Mathf.RoundToInt(iconSize * 0.36f));
            slot.Stacks = cornerText(slot.Rect, TextAnchor.UpperRight, Mathf.RoundToInt(iconSize * 0.34f));
            slot.Stacks.color = new Color(1.0f, 0.9f, 0.5f);
            return slot;
        }

        private static Text cornerText(RectTransform InParent, TextAnchor InAnchor, int InSize)
        {
            var rect = UIKit.Rect(InParent, "Text", Vector2.zero, Vector2.one, new Vector2(1.0f, -2.0f), new Vector2(-2.0f, 1.0f));
            var text = rect.gameObject.AddComponent<Text>();
            text.font = UIKit.Font;
            text.fontSize = Mathf.Max(10, InSize);
            text.fontStyle = FontStyle.Bold;
            text.alignment = InAnchor;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.0f, 0.0f, 0.0f, 0.95f);
            outline.effectDistance = new Vector2(1.0f, -1.0f);
            return text;
        }

        private static TooltipContent tooltip(Entry InEntry)
        {
            if (InEntry == null)
            {
                return null;
            }

            var content = new TooltipContent
            {
                Icon = IconFactory.Get(InEntry.IsDebuff ? EIconKind.Debuff : EIconKind.Buff, InEntry.Name, InEntry.IsDebuff ? 5 : 2),
                Title = InEntry.Name,
                TitleColor = InEntry.IsDebuff ? TooltipContent.Bad : TooltipContent.Good,
                Subtitle = $"{(InEntry.IsDebuff ? "디버프" : "버프")} · {(InEntry.Duration > 0.0f ? $"남은 시간 {InEntry.Remaining:0.0}초 / {InEntry.Duration:0.#}초" : "지속")}{(InEntry.Stacks > 1 ? $" · {InEntry.Stacks}중첩" : string.Empty)}",
            };

            if (string.IsNullOrEmpty(InEntry.Description) == false)
            {
                content.Line(InEntry.Description);
            }

            if (InEntry.Modifiers.Count > 0)
            {
                content.Header("효과");
                for (int i = 0; i < InEntry.Modifiers.Count; ++i)
                {
                    content.Line(InEntry.Modifiers[i], InEntry.IsDebuff ? TooltipContent.Bad : TooltipContent.Good);
                }
            }

            if (string.IsNullOrEmpty(InEntry.Source) == false)
            {
                content.Line($"건 대상: {InEntry.Source}", TooltipContent.Dim);
            }

            return content;
        }
    }
}
