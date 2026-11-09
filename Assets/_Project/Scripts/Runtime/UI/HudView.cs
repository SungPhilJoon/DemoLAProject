using System.Collections.Generic;
using System.Text;
using DemoLA.Attributes;
using DemoLA.Boss;
using DemoLA.Build;
using DemoLA.Character;
using DemoLA.Combat;
using DemoLA.Core;
using DemoLA.Skills;
using Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DemoLA.UI
{
    /// <summary>
    /// 전투 HUD: 보스 체력(줄)/무력화/카운터/기믹 · 플레이어 체력/마나/스킬 쿨다운 · 파티 · 안내 문구 · 데미지 숫자 · 딜 미터.
    /// 배치는 일반적인 쿼터뷰 액션 RPG 형태이며 특정 게임 UI 를 그대로 옮기지 않았다.
    /// </summary>
    public class HudView : BaseBehaviour
    {
        private class SkillSlotView
        {
            public SkillAbility Ability;
            public Image Icon;
            public Image Cooldown;
            public Text Time;
        }

        private class FloatingNumber
        {
            public Text Text;
            public Vector3 World;
            public float Age;
        }

        private static readonly string[] Keys = { "Q", "W", "E", "R", "A", "S", "D", "F", "V" };

        private PartyCharacter player;
        private BossActor boss;
        private readonly List<PartyCharacter> party = new List<PartyCharacter>();

        private Canvas canvas;
        private Text bossName;
        private Bar bossBar;
        private Text bossLines;
        private Bar staggerBar;
        private Text bossState;
        private Text gimmickText;
        private Bar healthBar;
        private StatusEffectBar bossBuffs;
        private StatusEffectBar bossDebuffs;
        private StatusEffectBar playerBuffs;
        private StatusEffectBar playerDebuffs;
        private Bar manaBar;
        private Bar identityBar;
        private IdentityHudView identityView;
        private Text itemText;
        private readonly List<SkillSlotView> slots = new List<SkillSlotView>();
        private SkillSlotView dodgeSlot;
        private SkillSlotView awakeningSlot;
        private string skillSignature;

        private class ItemSlotView
        {
            public Image Icon;
            public Image Cooldown;
            public Text Count;
            public Text Time;
        }

        private readonly List<ItemSlotView> itemSlots = new List<ItemSlotView>();
        private RectTransform itemPanel;
        private string itemSignature;

        /// <summary>HUD 스킬 칸에 스킬을 놓을 때 (칸, 스킬 ID — 0 = 비움). GameBootstrap 이 빌드에 반영한다.</summary>
        public System.Action<int, int> SkillSlotSetter;
        /// <summary>HUD 배틀 아이템 칸에 아이템을 놓을 때 (칸, 아이템 Index — 0 = 비움).</summary>
        public System.Action<int, int> ItemSlotSetter;
        private readonly List<Bar> partyBars = new List<Bar>();
        private Text messageText;
        private float messageTime;
        private Text meterText;
        private Text helpText;
        private RectTransform numberRoot;
        private readonly List<FloatingNumber> numbers = new List<FloatingNumber>();
        private readonly Stack<Text> numberPool = new Stack<Text>();
        private readonly StringBuilder builder = new StringBuilder();

        private float playerDamage;
        private float meterStart = -1.0f;
        private float lastHitTime;

        public void Build(PartyCharacter InPlayer, BossActor InBoss, IReadOnlyList<PartyCharacter> InParty)
        {
            player = InPlayer;
            boss = InBoss;
            party.Clear();
            party.AddRange(InParty);
            if (canvas == null)
            {
                canvas = UIKit.CreateCanvas("HUD", 10);
                canvas.transform.SetParent(transform, false);
                buildLayout();
            }

            bossBuffs.SetTarget(boss);
            bossDebuffs.SetTarget(boss);
            playerBuffs.SetTarget(player);
            identityView.SetPlayer(player);
            playerDebuffs.SetTarget(player);
            buildSkillSlots();
            buildItemSlots();
            buildPartyBars();
            bossName.text = boss != null ? boss.Definition.DisplayName : string.Empty;
            playerDamage = 0.0f;
            meterStart = -1.0f;
        }

        private void OnEnable()
        {
            EventBus<DamageAppliedEvent>.Subscribe(handleDamage);
            EventBus<RaidMessageEvent>.Subscribe(handleMessage);
            EventBus<RaidResultEvent>.Subscribe(handleResult);
        }

        private void OnDisable()
        {
            EventBus<DamageAppliedEvent>.Unsubscribe(handleDamage);
            EventBus<RaidMessageEvent>.Unsubscribe(handleMessage);
            EventBus<RaidResultEvent>.Unsubscribe(handleResult);
        }

        // ------------------------------------------------------------------ layout

        private void buildLayout()
        {
            var root = canvas.transform;

            // 보스
            var bossPanel = UIKit.Anchored(root, "Boss", new Vector2(0.5f, 1.0f), new Vector2(0.0f, -16.0f), new Vector2(900.0f, 120.0f));
            var nameRect = UIKit.Rect(bossPanel, "Name", new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, -30.0f), Vector2.zero);
            bossName = UIKit.Label(nameRect, string.Empty, 24, TextAnchor.MiddleCenter, Color.white);
            bossBar = UIKit.CreateBar(bossPanel, "HP", new Color(0.8f, 0.15f, 0.15f), new Color(0.0f, 0.0f, 0.0f, 0.7f));
            place(bossBar.Rect, new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, -66.0f), new Vector2(-90.0f, -34.0f));
            var linesRect = UIKit.Rect(bossPanel, "Lines", new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(-86.0f, -66.0f), new Vector2(0.0f, -34.0f));
            bossLines = UIKit.Label(linesRect, string.Empty, 26, TextAnchor.MiddleLeft, UIKit.AccentColor);
            staggerBar = UIKit.CreateBar(bossPanel, "Stagger", new Color(0.95f, 0.85f, 0.2f), new Color(0.0f, 0.0f, 0.0f, 0.7f));
            place(staggerBar.Rect, new Vector2(0.2f, 1.0f), new Vector2(0.8f, 1.0f), new Vector2(0.0f, -90.0f), new Vector2(0.0f, -70.0f));
            var stateRect = UIKit.Rect(bossPanel, "State", new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(0.0f, -28.0f), new Vector2(0.0f, 0.0f));
            bossState = UIKit.Label(stateRect, string.Empty, 20, TextAnchor.MiddleCenter, new Color(0.6f, 0.85f, 1.0f));
            // 보스 버프(왼쪽) / 디버프(오른쪽): 체력바 바로 아래, 무력화 막대 양옆.
            var bossBuffRect = UIKit.Rect(bossPanel, "BossBuffs", new Vector2(0.0f, 1.0f), new Vector2(0.19f, 1.0f), new Vector2(0.0f, -104.0f), new Vector2(0.0f, -70.0f));
            bossBuffs = StatusEffectBar.Create(bossBuffRect, StatusEffectBar.EFilter.Buffs, 32.0f, 5, false);
            var bossDebuffRect = UIKit.Rect(bossPanel, "BossDebuffs", new Vector2(0.81f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, -104.0f), new Vector2(-90.0f, -70.0f));
            bossDebuffs = StatusEffectBar.Create(bossDebuffRect, StatusEffectBar.EFilter.Debuffs, 32.0f, 3, true);
            var gimmickRect = UIKit.Rect(bossPanel, "Gimmick", new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(0.0f, -56.0f), new Vector2(0.0f, -28.0f));
            gimmickText = UIKit.Label(gimmickRect, string.Empty, 20, TextAnchor.MiddleCenter, UIKit.AccentColor);

            // 안내 문구
            var messageRect = UIKit.Anchored(root, "Message", new Vector2(0.5f, 0.5f), new Vector2(0.0f, 250.0f), new Vector2(1200.0f, 60.0f));
            messageText = UIKit.Label(messageRect, string.Empty, 32, TextAnchor.MiddleCenter, Color.white);
            messageText.gameObject.AddComponent<Outline>().effectColor = Color.black;

            // 플레이어
            var bottom = UIKit.Anchored(root, "Player", new Vector2(0.5f, 0.0f), new Vector2(0.0f, 20.0f), new Vector2(900.0f, 170.0f));
            healthBar = UIKit.CreateBar(bottom, "HP", new Color(0.2f, 0.75f, 0.3f), new Color(0.0f, 0.0f, 0.0f, 0.7f));
            place(healthBar.Rect, new Vector2(0.0f, 1.0f), new Vector2(0.49f, 1.0f), new Vector2(0.0f, -28.0f), Vector2.zero);
            // 플레이어 버프(왼쪽에서) / 디버프(오른쪽에서): 체력 · 마나 막대 바로 위.
            var playerBuffRect = UIKit.Rect(bottom, "PlayerBuffs", new Vector2(0.0f, 1.0f), new Vector2(0.49f, 1.0f), new Vector2(0.0f, 4.0f), new Vector2(0.0f, 42.0f));
            playerBuffs = StatusEffectBar.Create(playerBuffRect, StatusEffectBar.EFilter.Buffs, 36.0f, 10, false);
            var playerDebuffRect = UIKit.Rect(bottom, "PlayerDebuffs", new Vector2(0.51f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, 4.0f), new Vector2(0.0f, 42.0f));
            playerDebuffs = StatusEffectBar.Create(playerDebuffRect, StatusEffectBar.EFilter.Debuffs, 36.0f, 10, true);
            manaBar = UIKit.CreateBar(bottom, "MP", new Color(0.25f, 0.45f, 0.95f), new Color(0.0f, 0.0f, 0.0f, 0.7f));
            place(manaBar.Rect, new Vector2(0.51f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, -28.0f), Vector2.zero);
            identityBar = UIKit.CreateBar(bottom, "Identity", new Color(0.8f, 0.4f, 0.9f), new Color(0.0f, 0.0f, 0.0f, 0.7f));
            place(identityBar.Rect, new Vector2(0.3f, 1.0f), new Vector2(0.7f, 1.0f), new Vector2(0.0f, -50.0f), new Vector2(0.0f, -32.0f));
            identityBar.Label.fontSize = 13;
            // 예전 한 줄 표시는 숨기고 아이덴티티 패널(Z / X)로 대신한다.
            identityBar.SetVisible(false);
            identityView = IdentityHudView.Create(bottom);
            var itemRect = UIKit.Rect(bottom, "Items", new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, -72.0f), new Vector2(0.0f, -52.0f));
            itemText = UIKit.Label(itemRect, string.Empty, 15, TextAnchor.MiddleCenter, new Color(1.0f, 1.0f, 1.0f, 0.85f));

            // 파티
            var partyRect = UIKit.Anchored(root, "Party", new Vector2(0.0f, 0.5f), new Vector2(20.0f, 0.0f), new Vector2(300.0f, 220.0f));
            partyRect.gameObject.AddComponent<VerticalLayoutGroup>().spacing = 6.0f;

            // 딜 미터
            var meterRect = UIKit.Anchored(root, "Meter", new Vector2(1.0f, 0.5f), new Vector2(-20.0f, 60.0f), new Vector2(340.0f, 140.0f));
            UIKit.Image(meterRect, UIKit.PanelColor);
            meterText = UIKit.Label(UIKit.Stretch(meterRect, "Pad", 10.0f), string.Empty, 18, TextAnchor.UpperLeft, Color.white);

            // 오른쪽 아래는 배틀 아이템 칸이 쓰므로 도움말은 그 위에 둔다.
            var helpRect = UIKit.Anchored(root, "Help", new Vector2(1.0f, 0.0f), new Vector2(-20.0f, 150.0f), new Vector2(420.0f, 70.0f));
            helpText = UIKit.Label(helpRect, "우클릭 이동 · QWERASDF 스킬 · V 각성기 · Z/X 아이덴티티 · Space 회피\n1~4 배틀 아이템 · 5/6/7 에스더 · P 장비 · K 스킬 · F5 대기실로", 15, TextAnchor.LowerRight, new Color(1.0f, 1.0f, 1.0f, 0.7f));

            // 에스더 (배틀 아이템 칸 · 도움말 위)
            var esther = EstherHudView.Create(root);
            esther.OnUse = i =>
            {
                var controller = FindAnyObjectByType<DemoLA.Player.PlayerController>();
                if (controller != null && controller.Commands != null)
                {
                    controller.Commands.UseEsther(i);
                }
            };

            numberRoot = UIKit.Stretch(root, "Numbers");
        }

        private static void place(RectTransform InRect, Vector2 InMin, Vector2 InMax, Vector2 InOffsetMin, Vector2 InOffsetMax)
        {
            InRect.anchorMin = InMin;
            InRect.anchorMax = InMax;
            InRect.offsetMin = InOffsetMin;
            InRect.offsetMax = InOffsetMax;
        }

        /// <summary>
        /// 스킬 바: Q W E R A S D F (장착 스킬 — 스킬 창이나 다른 칸에서 끌어 놓기 · 우클릭 비우기) + V 각성기 + Spc 회피.
        /// </summary>
        private void buildSkillSlots()
        {
            for (int i = 0; i < slots.Count; ++i)
            {
                ObjectUtil.SafeDestroy(slots[i].Icon.transform.parent.gameObject);
            }

            slots.Clear();
            foreach (var extra in new[] { dodgeSlot, awakeningSlot })
            {
                if (extra != null)
                {
                    ObjectUtil.SafeDestroy(extra.Icon.transform.parent.gameObject);
                }
            }

            dodgeSlot = null;
            awakeningSlot = null;
            skillSignature = currentSkillSignature();
            if (player == null)
            {
                return;
            }

            var bottom = canvas.transform.Find("Player");
            for (int i = 0; i < CharacterBuildData.SkillSlotCount; ++i)
            {
                var ability = player.GetSkillInSlot(i);
                var view = createSlot(bottom, i, ability, Keys[i], ability != null ? ability.Definition.IconColor : new Color(0.25f, 0.25f, 0.28f), ability != null ? ability.Definition.DisplayName : "(빈 칸)");
                makeSkillDropSlot(view, i);
                slots.Add(view);
            }

            if (player.Awakening != null)
            {
                awakeningSlot = createSlot(bottom, CharacterBuildData.SkillSlotCount, player.Awakening, "V", player.Awakening.Definition.IconColor, player.Awakening.Definition.DisplayName);
                var awakening = player.Awakening;
                awakeningSlot.Icon.raycastTarget = true;
                awakeningSlot.Icon.sprite = ItemTooltips.SkillIcon(player.Awakening.Definition);
                awakeningSlot.Icon.color = Color.white;
                awakeningSlot.Icon.gameObject.AddComponent<TooltipTrigger>().Provider = () => ItemTooltips.Skill(awakening.Definition, player.Build != null ? player.Build.GetSkill(awakening.Definition.SkillId) : null, player);
            }

            dodgeSlot = createSlot(bottom, CharacterBuildData.SkillSlotCount + 1, null, "Spc", new Color(0.7f, 0.7f, 0.7f), "회피");
        }

        private string currentSkillSignature()
        {
            if (player == null || player.Build == null || player.Build.SkillSlots == null)
            {
                return string.Empty;
            }

            return string.Join(",", player.Build.SkillSlots) + "|" + (player.Awakening != null ? player.Awakening.Definition.SkillId : 0);
        }

        private void makeSkillDropSlot(SkillSlotView InView, int InSlot)
        {
            var icon = InView.Icon;
            icon.raycastTarget = true;
            var ability = InView.Ability;
            if (ability != null)
            {
                icon.sprite = ItemTooltips.SkillIcon(ability.Definition);
                icon.color = Color.white;
                icon.gameObject.AddComponent<TooltipTrigger>().Provider = () => ItemTooltips.Skill(ability.Definition, player.Build != null ? player.Build.GetSkill(ability.Definition.SkillId) : null, player);
                int id = ability.Definition.SkillId;
                icon.Draggable(() => new DragPayload
                {
                    Kind = "skill",
                    Data = id,
                    Icon = icon.sprite,
                    OnDroppedOutside = () => SkillSlotSetter?.Invoke(InSlot, 0),
                });
            }

            icon.Droppable(p => p.Kind == "skill", p => SkillSlotSetter?.Invoke(InSlot, (int)p.Data));
            icon.OnRightClick(() => SkillSlotSetter?.Invoke(InSlot, 0));
        }

        // ------------------------------------------------------------------ battle items (HUD 1~4)

        private void buildItemSlots()
        {
            var bottom = canvas.transform.Find("Player");
            if (itemPanel == null)
            {
                // 스킬 바 오른쪽 2×2 칸.
                itemPanel = UIKit.Anchored(bottom, "Items", new Vector2(1.0f, 0.0f), new Vector2(14.0f, 0.0f), new Vector2(104.0f, 104.0f));
                itemPanel.pivot = new Vector2(0.0f, 0.0f);
                UIKit.Image(itemPanel, new Color(0.05f, 0.06f, 0.09f, 0.6f));
                var grid = itemPanel.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(48.0f, 48.0f);
                grid.spacing = new Vector2(4.0f, 4.0f);
                grid.padding = new RectOffset(2, 2, 2, 2);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 2;
            }

            for (int i = 0; i < itemSlots.Count; ++i)
            {
                ObjectUtil.SafeDestroy(itemSlots[i].Icon.gameObject);
            }

            itemSlots.Clear();
            itemSignature = currentItemSignature();
            if (player == null)
            {
                return;
            }

            for (int i = 0; i < PartyCharacter.BattleItemSlotCount; ++i)
            {
                int slot = i;
                var item = i < player.BattleItems.Count ? player.BattleItems[i] : null;
                var sprite = item != null ? ItemTooltips.BattleItemIcon(item.Row) : IconFactory.Get(EIconKind.Empty, "item", 0);
                var view = new ItemSlotView();
                view.Icon = UIKit.IconCell(itemPanel, sprite, 48.0f, string.Empty, () =>
                {
                    var current = slot < player.BattleItems.Count ? player.BattleItems[slot] : null;
                    var content = ItemTooltips.BattleItem(current != null ? current.Row : null);
                    content.Line($"단축키 {slot + 1} · 인벤토리(I)에서 끌어 놓기 · 우클릭 = 비우기", TooltipContent.Dim);
                    return content;
                });
                var cooldownRect = UIKit.Stretch(view.Icon.transform, "Cooldown", 2.0f);
                view.Cooldown = UIKit.Image(cooldownRect, new Color(0.0f, 0.0f, 0.0f, 0.65f));
                view.Cooldown.sprite = UIKit.WhiteSprite;
                view.Cooldown.type = Image.Type.Filled;
                view.Cooldown.fillMethod = Image.FillMethod.Radial360;
                view.Cooldown.fillAmount = 0.0f;
                UIKit.Label(view.Icon.transform, $"{i + 1}", 13, TextAnchor.UpperLeft, Color.white).gameObject.AddComponent<Outline>().effectColor = Color.black;
                view.Count = UIKit.Label(view.Icon.transform, string.Empty, 13, TextAnchor.LowerRight, Color.white);
                view.Count.gameObject.AddComponent<Outline>().effectColor = Color.black;
                view.Time = UIKit.Label(view.Icon.transform, string.Empty, 16, TextAnchor.MiddleCenter, Color.white);
                view.Icon.Droppable(p => p.Kind == "item" || p.Kind == "hud-item", p =>
                {
                    if (p.Kind == "hud-item")
                    {
                        // 칸끼리 바꾸기.
                        int from = (int)p.Data;
                        int mine = player.Build.BattleItemSlots[slot];
                        ItemSlotSetter?.Invoke(slot, player.Build.BattleItemSlots[from]);
                        ItemSlotSetter?.Invoke(from, mine);
                        return;
                    }

                    ItemSlotSetter?.Invoke(slot, (int)p.Data);
                });
                view.Icon.OnRightClick(() => ItemSlotSetter?.Invoke(slot, 0));
                if (item != null)
                {
                    view.Icon.Draggable(() => new DragPayload { Kind = "hud-item", Data = slot, Icon = sprite, OnDroppedOutside = () => ItemSlotSetter?.Invoke(slot, 0) });
                }

                itemSlots.Add(view);
            }
        }

        private string currentItemSignature()
        {
            if (player == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < player.BattleItems.Count; ++i)
            {
                builder.Append(player.BattleItems[i] != null ? player.BattleItems[i].Row.Index : 0).Append(',');
            }

            return builder.ToString();
        }

        private SkillSlotView createSlot(Transform InParent, int InIndex, SkillAbility InAbility, string InKey, Color InColor, string InName)
        {
            const float size = 78.0f;
            var rect = UIKit.Anchored(InParent, $"Slot{InIndex}", new Vector2(0.0f, 0.0f), new Vector2(InIndex * (size + 8.0f), 0.0f), new Vector2(size, size + 22.0f));
            var iconRect = UIKit.Rect(rect, "Icon", new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(0.0f, 0.0f), new Vector2(0.0f, size));
            var view = new SkillSlotView();
            view.Ability = InAbility;
            view.Icon = UIKit.Image(iconRect, InColor * 0.8f);
            var cooldownRect = UIKit.Stretch(iconRect, "Cooldown");
            view.Cooldown = UIKit.Image(cooldownRect, new Color(0.0f, 0.0f, 0.0f, 0.7f));
            view.Cooldown.sprite = UIKit.WhiteSprite;
            view.Cooldown.type = Image.Type.Filled;
            view.Cooldown.fillMethod = Image.FillMethod.Radial360;
            view.Cooldown.fillAmount = 0.0f;
            UIKit.Label(iconRect, InKey, 16, TextAnchor.UpperLeft, Color.white);
            view.Time = UIKit.Label(iconRect, string.Empty, 24, TextAnchor.MiddleCenter, Color.white);
            var nameRect = UIKit.Rect(rect, "Name", new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(-6.0f, -22.0f), new Vector2(6.0f, 0.0f));
            UIKit.Label(nameRect, InName, 13, TextAnchor.MiddleCenter, new Color(1.0f, 1.0f, 1.0f, 0.8f));
            // 드래그 앤 드랍 · 툴팁을 아이콘이 받도록 쿨다운 덮개 · 글자는 레이캐스트를 막지 않는다.
            view.Cooldown.raycastTarget = false;
            return view;
        }

        private void buildPartyBars()
        {
            var partyRect = canvas.transform.Find("Party");
            for (int i = 0; i < partyBars.Count; ++i)
            {
                ObjectUtil.SafeDestroy(partyBars[i].Rect.gameObject);
            }

            partyBars.Clear();
            for (int i = 0; i < party.Count; ++i)
            {
                var bar = UIKit.CreateBar(partyRect, $"Member{i}", new Color(0.25f, 0.7f, 0.35f), new Color(0.0f, 0.0f, 0.0f, 0.7f));
                var element = bar.Rect.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 34.0f;
                bar.Label.alignment = TextAnchor.MiddleLeft;
                bar.Label.fontSize = 16;
                partyBars.Add(bar);
            }
        }

        // ------------------------------------------------------------------ update

        private void Update()
        {
            if (canvas == null)
            {
                return;
            }

            updateBoss();
            updatePlayer();
            updateParty();
            updateMeter();
            updateNumbers(Time.deltaTime);

            if (messageTime > 0.0f)
            {
                messageTime -= Time.deltaTime;
                if (messageTime <= 0.0f)
                {
                    messageText.text = string.Empty;
                }
            }
        }

        private void updateBoss()
        {
            if (boss == null || boss.Stats == null)
            {
                bossBar.SetVisible(false);
                staggerBar.SetVisible(false);
                return;
            }

            bossBar.SetVisible(true);
            float exact = boss.HpLinesExact;
            float lineRatio = exact <= 0.0f ? 0.0f : exact - Mathf.Floor(exact - 0.0001f);
            bossBar.Set(lineRatio, $"{boss.Stats.Get(EAttr.Health):N0}");
            bossLines.text = $"x{boss.CurrentHpLine}";

            bool stagger = boss.HasTag((int)ETag.Boss_StaggerCheck);
            staggerBar.SetVisible(stagger);
            if (stagger == true)
            {
                float max = Mathf.Max(1.0f, boss.Stats.Get(EAttr.MaxStaggerGauge));
                staggerBar.Set(boss.Stats.Get(EAttr.StaggerGauge) / max, "무력화");
            }

            builder.Length = 0;
            var phase = boss.CurrentPhase;
            if (phase != null)
            {
                builder.Append(phase.DisplayName);
            }

            if (boss.HasTag((int)ETag.Boss_CounterWindow) == true)
            {
                builder.Append("   ◆ 카운터 가능 ◆");
            }

            if (boss.HasTag((int)ETag.State_Groggy) == true)
            {
                builder.Append("   [무력화 상태]");
            }

            if (boss.HasTag((int)ETag.Buff_Armor) == true)
            {
                builder.Append("   [갑옷]");
            }

            if (boss.ActivePattern != null)
            {
                builder.Append($"   {boss.ActivePattern.Definition.PatternId} {boss.ActivePattern.Definition.DisplayName}");
            }

            bossState.text = builder.ToString();
            gimmickText.text = boss.ActiveGimmick != null ? boss.ActiveGimmick.StatusText : string.Empty;
        }

        private void updatePlayer()
        {
            if (player == null || player.Stats == null)
            {
                return;
            }

            var stats = player.Stats;
            healthBar.Set(stats.HealthRatio, $"{stats.Get(EAttr.Health):N0} / {stats.Get(EAttr.MaxHealth):N0}{(stats.Get(EAttr.Shield) > 0.0f ? $"  (+{stats.Get(EAttr.Shield):N0})" : string.Empty)}");
            float maxMana = Mathf.Max(1.0f, stats.Get(EAttr.MaxMana));
            manaBar.Set(stats.Get(EAttr.Mana) / maxMana, $"{stats.Get(EAttr.Mana):N0}");
            itemText.text = string.Empty;
            if (currentSkillSignature() != skillSignature)
            {
                buildSkillSlots();
            }

            if (currentItemSignature() != itemSignature)
            {
                buildItemSlots();
            }

            for (int i = 0; i < itemSlots.Count; ++i)
            {
                var item = i < player.BattleItems.Count ? player.BattleItems[i] : null;
                var view = itemSlots[i];
                if (item == null)
                {
                    view.Count.text = string.Empty;
                    view.Time.text = string.Empty;
                    view.Cooldown.fillAmount = 0.0f;
                    continue;
                }

                float remaining = player.GetCooldownRemaining(item);
                float duration = player.Asc.GetCooldownDuration(item.AbilityId);
                view.Count.text = $"×{item.Remaining}";
                view.Cooldown.fillAmount = remaining > 0.0f && duration > 0.0f ? remaining / duration : (item.Remaining <= 0 ? 1.0f : 0.0f);
                view.Time.text = remaining > 0.0f ? $"{remaining:0}" : string.Empty;
            }

            for (int i = 0; i < slots.Count; ++i)
            {
                updateSlot(slots[i], slots[i].Ability != null ? slots[i].Ability.AbilityId : 0);
            }

            if (awakeningSlot != null && awakeningSlot.Ability != null)
            {
                updateSlot(awakeningSlot, awakeningSlot.Ability.AbilityId);
            }

            if (dodgeSlot != null && player.Dodge != null)
            {
                updateSlot(dodgeSlot, player.Dodge.AbilityId);
            }
        }

        private void updateSlot(SkillSlotView InSlot, int InAbilityId)
        {
            float remaining = player.Asc.GetCooldownRemaining(InAbilityId);
            float duration = player.Asc.GetCooldownDuration(InAbilityId);
            InSlot.Cooldown.fillAmount = remaining > 0.0f && duration > 0.0f ? remaining / duration : 0.0f;
            InSlot.Time.text = remaining > 0.0f ? (remaining >= 10.0f ? $"{remaining:0}" : $"{remaining:0.0}") : string.Empty;
        }

        private void updateParty()
        {
            for (int i = 0; i < party.Count && i < partyBars.Count; ++i)
            {
                var member = party[i];
                if (member == null || member.Stats == null)
                {
                    continue;
                }

                string prefix = member == player ? "▶ " : string.Empty;
                string state = member.IsDead == true ? " (사망)" : (member.HasTag((int)ETag.Buff_DeathWard) == true ? " [방어 구슬]" : string.Empty);
                partyBars[i].Set(member.Stats.HealthRatio, $"  {prefix}{member.GetName()}{state}");
            }
        }

        private void updateMeter()
        {
            float elapsed = meterStart < 0.0f ? 0.0f : Mathf.Max(1.0f, lastHitTime - meterStart);
            float dps = elapsed > 0.0f ? playerDamage / elapsed : 0.0f;
            builder.Length = 0;
            builder.AppendLine("딜 미터 (플레이어)");
            builder.AppendLine($"총 피해  {playerDamage:N0}");
            builder.AppendLine($"초당     {dps:N0}");
            if (player != null && player.Stats != null)
            {
                builder.Append($"공격력 {player.Stats.Get(EAttr.AttackPower):N0} · 치명 {player.Stats.Get(EAttr.CritRate):0.0}%");
            }

            meterText.text = builder.ToString();
        }

        private void updateNumbers(float InDeltaTime)
        {
            var camera = Camera.main;
            for (int i = numbers.Count - 1; i >= 0; --i)
            {
                var number = numbers[i];
                number.Age += InDeltaTime;
                if (number.Age >= 0.9f || camera == null)
                {
                    number.Text.gameObject.SetActive(false);
                    numberPool.Push(number.Text);
                    numbers.RemoveAt(i);
                    continue;
                }

                var screen = camera.WorldToScreenPoint(number.World + Vector3.up * (2.2f + number.Age * 1.5f));
                number.Text.rectTransform.position = screen;
                var color = number.Text.color;
                color.a = 1.0f - number.Age / 0.9f;
                number.Text.color = color;
            }
        }

        // ------------------------------------------------------------------ events

        private void handleDamage(DamageAppliedEvent InEvent)
        {
            if (player != null && InEvent.Source == player.Asc)
            {
                if (meterStart < 0.0f)
                {
                    meterStart = Time.time;
                }

                lastHitTime = Time.time;
                playerDamage += InEvent.HealthLost + InEvent.ShieldAbsorbed;
            }

            if (numberRoot == null || InEvent.Target == null)
            {
                return;
            }

            bool fromPlayer = player != null && InEvent.Source == player.Asc;
            bool toParty = InEvent.Target.OwnerActor is PartyCharacter;
            if (fromPlayer == false && toParty == false)
            {
                return;
            }

            var text = numberPool.Count > 0 ? numberPool.Pop() : null;
            if (text == null)
            {
                text = UIKit.Label(numberRoot, string.Empty, 22, TextAnchor.MiddleCenter, Color.white);
                var rect = text.rectTransform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.sizeDelta = new Vector2(240.0f, 40.0f);
                text.gameObject.AddComponent<Outline>().effectColor = Color.black;
            }

            text.gameObject.SetActive(true);
            string suffix = InEvent.Positional == EPositional.BackAttack ? " 백" : (InEvent.Positional == EPositional.HeadAttack ? " 헤드" : string.Empty);
            text.text = InEvent.DeathWardConsumed == true ? "방어!" : $"{InEvent.RawAmount:N0}{suffix}";
            text.fontSize = InEvent.IsCrit == true ? 30 : 22;
            text.color = toParty == true ? new Color(1.0f, 0.35f, 0.3f) : (InEvent.IsCrit == true ? new Color(1.0f, 0.75f, 0.2f) : Color.white);
            var number = new FloatingNumber();
            number.Text = text;
            number.World = InEvent.Target.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0.0f, Random.Range(-0.5f, 0.5f));
            numbers.Add(number);
        }

        private void handleMessage(RaidMessageEvent InEvent)
        {
            if (messageText == null)
            {
                return;
            }

            messageText.text = InEvent.Message;
            messageText.color = InEvent.IsWarning == true ? new Color(1.0f, 0.45f, 0.35f) : Color.white;
            messageTime = Mathf.Max(0.5f, InEvent.Duration);
        }

        private void handleResult(RaidResultEvent InEvent)
        {
            if (messageText == null)
            {
                return;
            }

            messageText.text = InEvent.Cleared == true ? $"클리어! ({InEvent.Reason}) — F5 대기실로" : $"전멸 ({InEvent.Reason}) — F5 대기실로";
            messageText.color = InEvent.Cleared == true ? UIKit.AccentColor : new Color(1.0f, 0.3f, 0.3f);
            messageTime = 999.0f;
        }
    }
}
