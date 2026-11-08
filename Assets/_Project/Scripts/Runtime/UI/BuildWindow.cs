using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DemoLA.Attributes;
using DemoLA.Build;
using DemoLA.Character;
using DemoLA.Core;
using DemoLA.Data;
using Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DemoLA.UI
{
    /// <summary>
    /// 캐릭터 설정 UI. 기능마다 독립 창(장비 · 악세사리 · 각인 · 카드 · 보석 · 스킬 · 배틀 아이템 · 능력치 · 키 설정)을 띄운다.
    ///  - 창은 단축키 또는 오른쪽 위 메뉴 막대로 열고 닫는다. 제목 표시줄을 끌어 옮길 수 있다.
    ///  - 장비/악세사리/각인/카드/보석/스킬/배틀 아이템은 아이콘으로 보여 주고, 마우스를 올리면 툴팁으로 설명한다.
    ///  - 값은 CharacterBuildData 를 직접 바꾸고 "적용 + 저장" 시 BuildCalculator → BuildApplier 로 다시 붙인다.
    /// 외부(GameBootstrap) 와의 계약: Build / Toggle / IsOpen.
    /// </summary>
    public class BuildWindow : BaseBehaviour
    {
        private enum EWindow
        {
            Equipment = 0,
            Accessory = 1,
            Engraving = 2,
            Card = 3,
            Gem = 4,
            Skill = 5,
            Inventory = 6,
            Stats = 7,
            Keys = 8,
        }

        private struct WindowSpec
        {
            public string Title;
            public Key Hotkey;
            public string HotkeyLabel;
            public EIconKind Icon;
            public Vector2 Size;
            public Action<GameWindow> Draw;
        }

        private const float IconSize = 52.0f;
        private const float RowHeight = 60.0f;

        private Canvas canvas;
        private readonly Dictionary<EWindow, GameWindow> windows = new Dictionary<EWindow, GameWindow>();
        private readonly Dictionary<EWindow, WindowSpec> specs = new Dictionary<EWindow, WindowSpec>();
        private GameSession session;
        private PartyCharacter player;
        private CharacterBuildData build;
        private Action<ECharacterClass> onChangeClass;
        private bool rebinding;
        private bool pendingRefresh;

        /// <summary>"적용 + 저장" 을 눌렀을 때 (대기실 준비 해제 등).</summary>
        public static event Action OnApplied;

        /// <summary>창이 하나라도 열려 있는가 (열려 있는 동안 게임 입력을 막는다).</summary>
        public bool IsOpen
        {
            get
            {
                foreach (var pair in windows)
                {
                    if (pair.Value != null && pair.Value.IsOpen == true)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private GameDatabase database
        {
            get { return session.Database; }
        }

        public void Build(GameSession InSession, PartyCharacter InPlayer, Action<ECharacterClass> InOnChangeClass)
        {
            session = InSession;
            player = InPlayer;
            build = InPlayer != null ? InPlayer.Build : null;
            onChangeClass = InOnChangeClass;
            if (canvas == null)
            {
                canvas = UIKit.CreateCanvas("BuildWindows", 20);
                canvas.transform.SetParent(transform, false);
                defineWindows();
                createWindows();
                createLauncher();
            }

            refreshAll();
        }

        /// <summary>P 키: 장비(캐릭터) 창을 열고 닫는다.</summary>
        public void Toggle()
        {
            GameWindow window;
            if (windows.TryGetValue(EWindow.Equipment, out window) == true)
            {
                window.Toggle();
            }
        }

        /// <summary>Esc: 열린 창을 모두 닫는다.</summary>
        public void CloseAll()
        {
            foreach (var pair in windows)
            {
                pair.Value.Close();
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || canvas == null || rebinding == true)
            {
                return;
            }

            foreach (var pair in specs)
            {
                if (pair.Value.Hotkey != Key.None && keyboard[pair.Value.Hotkey].wasPressedThisFrame == true)
                {
                    windows[pair.Key].Toggle();
                }
            }
        }

        // ------------------------------------------------------------------ frame

        private void defineWindows()
        {
            // 장비(캐릭터) 창은 P (입력 설정의 BuildWindow 동작) — 여기서는 키를 따로 읽지 않는다.
            specs[EWindow.Equipment] = spec("장비", Key.None, EIconKind.Armor, new Vector2(1000, 780), drawEquipment, "P");
            specs[EWindow.Accessory] = spec("악세사리", Key.Y, EIconKind.Ring, new Vector2(1260, 780), drawAccessories);
            specs[EWindow.Engraving] = spec("각인", Key.N, EIconKind.Engraving, new Vector2(1060, 800), drawEngraving);
            specs[EWindow.Card] = spec("카드", Key.C, EIconKind.Card, new Vector2(860, 800), drawCards);
            specs[EWindow.Gem] = spec("보석", Key.G, EIconKind.GemDamage, new Vector2(860, 780), drawGems);
            specs[EWindow.Skill] = spec("스킬", Key.K, EIconKind.Skill, new Vector2(1240, 860), drawSkills);
            specs[EWindow.Inventory] = spec("인벤토리", Key.I, EIconKind.BattleItem, new Vector2(820, 560), drawInventory);
            specs[EWindow.Stats] = spec("능력치", Key.U, EIconKind.Stats, new Vector2(980, 720), drawStats);
            specs[EWindow.Keys] = spec("키 설정", Key.O, EIconKind.Keys, new Vector2(760, 720), drawKeys);
        }

        private static WindowSpec spec(string InTitle, Key InKey, EIconKind InIcon, Vector2 InSize, Action<GameWindow> InDraw, string InLabel = null)
        {
            return new WindowSpec
            {
                Title = InTitle,
                Hotkey = InKey,
                HotkeyLabel = InLabel ?? InKey.ToString(),
                Icon = InIcon,
                Size = InSize,
                Draw = InDraw,
            };
        }

        private void createWindows()
        {
            int index = 0;
            foreach (var pair in specs)
            {
                var kind = pair.Key;
                var s = pair.Value;
                // 열 때마다 겹치지 않게 조금씩 비켜 놓는다.
                var position = new Vector2(-120.0f + 40.0f * index, 40.0f - 26.0f * index);
                windows[kind] = GameWindow.Create(canvas.transform, s.Title, s.HotkeyLabel, s.Icon, s.Size, position, s.Draw,
                    kind == EWindow.Keys ? (Action)null : () => apply(windows[kind]), null);
                index += 1;
            }
        }

        /// <summary>오른쪽 위 메뉴 막대: 창마다 아이콘 버튼 (툴팁에 이름 + 단축키).</summary>
        private void createLauncher()
        {
            var bar = UIKit.Anchored(canvas.transform, "Launcher", new Vector2(1.0f, 1.0f), new Vector2(-16.0f, -16.0f), new Vector2(10.0f, 52.0f));
            UIKit.Image(bar, new Color(0.05f, 0.06f, 0.08f, 0.75f), true);
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4.0f;
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            var fitter = bar.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (var pair in specs)
            {
                var kind = pair.Key;
                var s = pair.Value;
                UIKit.IconCell(bar, IconFactory.Get(s.Icon, s.Title, 4), 42.0f, s.HotkeyLabel,
                    () => new TooltipContent { Title = s.Title, Subtitle = $"단축키 {s.HotkeyLabel} · Esc 모두 닫기" },
                    () => windows[kind].Toggle());
            }
        }

        private void open(EWindow InWindow)
        {
            GameWindow window;
            if (windows.TryGetValue(InWindow, out window) == true)
            {
                window.Open();
            }
        }

        private void refreshAll()
        {
            foreach (var pair in windows)
            {
                pair.Value.Refresh();
            }
        }

        private bool ready(GameWindow InWindow)
        {
            if (session == null || build == null || player == null)
            {
                header(InWindow, "캐릭터가 없습니다.");
                return false;
            }

            return true;
        }

        // ------------------------------------------------------------------ 도감 공통

        private const float SlotSize = 68.0f;
        private const float CodexSize = 56.0f;

        private struct EquipPayload
        {
            public int SetId;
            public EEquipSlot Slot;
        }

        private struct GemPayload
        {
            public EGemType Type;
            public int Level;
        }

        /// <summary>장착 변경 후 화면 갱신은 프레임 끝에 한다 (놓기 이벤트 도중 끌던 아이콘을 지우지 않게).</summary>
        private void changed(GameWindow InWindow, string InMessage)
        {
            if (InWindow != null && string.IsNullOrEmpty(InMessage) == false)
            {
                InWindow.Status.text = InMessage;
            }

            pendingRefresh = true;
        }

        private void LateUpdate()
        {
            if (pendingRefresh == true && DragSource.Current == null)
            {
                pendingRefresh = false;
                refreshAll();
            }
        }

        private static void hint(GameWindow InWindow, string InText)
        {
            UIKit.RowLabel(UIKit.HorizontalRow(InWindow.Content, 24, 6).transform, InText, 0, 14).color = new Color(0.65f, 0.68f, 0.72f);
        }

        /// <summary>도감 칸: 끌기 + 우클릭 + 장착 표시.</summary>
        private static Image codexCell(Transform InGrid, Sprite InSprite, string InBadge, Func<TooltipContent> InTooltip, bool InEquipped,
            Func<DragPayload> InDrag, Action InRightClick)
        {
            return UIKit.IconCell(InGrid, InSprite, CodexSize, InBadge, InTooltip).MarkEquipped(InEquipped).Draggable(InDrag).OnRightClick(InRightClick);
        }

        // ------------------------------------------------------------------ 장비

        private void drawEquipment(GameWindow InWindow)
        {
            if (ready(InWindow) == false)
            {
                return;
            }

            header(InWindow, "장착 중인 장비");
            hint(InWindow, "도감의 장비를 같은 부위 칸에 끌어 놓거나, 도감에서 우클릭하면 장착됩니다. 재련 단계는 부위에 남습니다.");
            var slots = UIKit.IconGrid(InWindow.Content, SlotSize, 10.0f, 6);
            for (int i = 0; i < build.Equipment.Count; ++i)
            {
                var state = build.Equipment[i];
                UIKit.IconCell(slots, ItemTooltips.EquipmentIcon(database, state), SlotSize, $"+{state.HoningLevel}", () => ItemTooltips.Equipment(database, state, build))
                    .Droppable(p => p.Kind == "equip" && ((EquipPayload)p.Data).Slot == state.Slot, p => equipPiece(InWindow, (EquipPayload)p.Data));
            }

            drawHoning(InWindow);

            header(InWindow, $"장비 도감 — 세트 {database.EquipmentSets.Count}종 × 6부위");
            for (int s = 0; s < database.EquipmentSets.Count; ++s)
            {
                var set = database.EquipmentSets[s];
                int count = 0;
                for (int i = 0; i < build.Equipment.Count; ++i)
                {
                    count += build.Equipment[i].SetId == set.SetId ? 1 : 0;
                }

                line(InWindow, $"{set.DisplayName}  ({count}/6 장착) — {set.Description}");
                var grid = UIKit.IconGrid(InWindow.Content, CodexSize, 8.0f, 12);
                foreach (EEquipSlot slot in Enum.GetValues(typeof(EEquipSlot)))
                {
                    var piece = set.GetPiece(slot);
                    if (piece == null)
                    {
                        continue;
                    }

                    var payload = new EquipPayload { SetId = set.SetId, Slot = slot };
                    var sprite = IconFactory.Get(slot == EEquipSlot.Weapon ? EIconKind.Weapon : EIconKind.Armor, piece.Name, 3);
                    var currentSet = set;
                    var currentSlot = slot;
                    codexCell(grid, sprite, string.Empty, () => ItemTooltips.EquipmentPiece(currentSet, currentSlot, build), build.GetEquipment(slot) != null && build.GetEquipment(slot).SetId == set.SetId,
                        () => new DragPayload { Kind = "equip", Data = payload, Icon = sprite },
                        () => equipPiece(InWindow, payload));
                }
            }
        }

        private void equipPiece(GameWindow InWindow, EquipPayload InPayload)
        {
            var state = build.GetEquipment(InPayload.Slot);
            var set = database.GetEquipmentSet(InPayload.SetId);
            if (state == null || set == null)
            {
                return;
            }

            if (state.SetId == InPayload.SetId)
            {
                InWindow.Status.text = "이미 장착 중입니다";
                return;
            }

            state.SetId = InPayload.SetId;
            var piece = set.GetPiece(InPayload.Slot);
            changed(InWindow, $"{(piece != null ? piece.Name : ItemTooltips.SlotName(InPayload.Slot))} 장착 (+{state.HoningLevel} 유지) — 적용 + 저장을 누르세요");
        }

        private void drawHoning(GameWindow InWindow)
        {
            var table = database.Honing;
            var advanced = database.AdvancedHoning;
            header(InWindow, "재련 — 실패 시 확률 증가 · 장인의 기운 100% 면 확정 · 보조 재료로 확률 추가 · 상급 재련은 경험치 방식");
            for (int i = 0; i < build.Equipment.Count; ++i)
            {
                var state = build.Equipment[i];
                var row = UIKit.HorizontalRow(InWindow.Content, RowHeight, 6).transform;
                var icon = UIKit.IconCell(row, ItemTooltips.EquipmentIcon(database, state), IconSize, $"+{state.HoningLevel}", () => ItemTooltips.Equipment(database, state, build));
                var info = UIKit.RowLabel(row, string.Empty, 0, 16);
                Action redraw = () =>
                {
                    info.text = $"{ItemTooltips.SlotName(state.Slot)}  +{state.HoningLevel}  확률 {HoningService.CurrentRate(state, table):0.##}%  장인 {state.ArtisanEnergy:0.0}%  상급 {state.AdvancedLevel}단계";
                    icon.sprite = ItemTooltips.EquipmentIcon(database, state);
                    var badge = icon.GetComponentInChildren<Text>();
                    if (badge != null)
                    {
                        badge.text = $"+{state.HoningLevel}";
                    }
                };
                redraw();
                UIKit.Selector(row, 70, () => $"보조 {state.AuxMaterials}", s =>
                {
                    var current = table != null ? table.GetStep(state.HoningLevel + 1) : null;
                    state.AuxMaterials = Mathf.Clamp(state.AuxMaterials + s * 5, 0, current != null ? current.AuxMaxCount : 0);
                    redraw();
                });
                UIKit.Selector(row, 70, () => $"품질 {state.Quality}", s => state.Quality = Mathf.Clamp(state.Quality + s * 10, 0, 100));
                UIKit.RowButton(row, "강화", 60, () =>
                {
                    var result = HoningService.TryHone(state, table, GameRandom.Value);
                    InWindow.Status.text = result.Attempted == false ? "최대 단계" : (result.Success == true ? $"성공! +{result.NewLevel}{(result.Guaranteed ? " (장인의 기운)" : string.Empty)}" : $"실패 (확률 {result.RateUsed:0.##}%)");
                    redraw();
                });
                UIKit.RowButton(row, "성공까지", 84, () =>
                {
                    int tries = 0;
                    int level = state.HoningLevel;
                    while (HoningService.CanHone(state, table) == true && state.HoningLevel == level && tries < 1000)
                    {
                        HoningService.TryHone(state, table, GameRandom.Value);
                        tries += 1;
                    }

                    InWindow.Status.text = $"{tries}회 시도 → +{state.HoningLevel}";
                    redraw();
                });
                UIKit.RowButton(row, "상급", 56, () =>
                {
                    var result = HoningService.TryAdvancedHone(state, advanced, GameRandom.Value);
                    InWindow.Status.text = result.Attempted == false ? "상급 재련 최대" : $"상급 재련 {result.Grade} +{result.ExpGained:0} 경험치{(result.LevelUp ? $" → {result.NewLevel}단계" : string.Empty)}";
                    redraw();
                });
                UIKit.RowButton(row, "-1", 40, () =>
                {
                    state.HoningLevel = Mathf.Max(0, state.HoningLevel - 1);
                    state.ArtisanEnergy = 0.0f;
                    state.FailBonusRate = 0.0f;
                    redraw();
                });
            }
        }

        // ------------------------------------------------------------------ 악세사리

        private void drawAccessories(GameWindow InWindow)
        {
            if (ready(InWindow) == false)
            {
                return;
            }

            var rules = database.EngravingRules;
            header(InWindow, $"악세사리 — 각인 2종 + 감소 각인 1종 · 특성 2종");
            hint(InWindow, "아래 각인 도감에서 각인을 끌어 악세사리의 각인 칸에 놓으세요. 각인 칸 우클릭 = 해제.");
            for (int i = 0; i < build.Accessories.Count; ++i)
            {
                var accessory = build.Accessories[i];
                var row = UIKit.HorizontalRow(InWindow.Content, RowHeight + 4, 6).transform;
                UIKit.IconCell(row, ItemTooltips.AccessoryIcon(accessory), IconSize, string.Empty, () => ItemTooltips.Accessory(database, accessory));
                UIKit.RowLabel(row, ItemTooltips.SlotName(accessory.Slot), 70, 15);

                engravingSubSlot(InWindow, row, () => accessory.EngravingA, v => accessory.EngravingA = v, () => accessory.PointsA, false);
                UIKit.Selector(row, 34, () => $"+{accessory.PointsA}", s => accessory.PointsA = cyclePoints(rules.AccessoryPointOptions, accessory.PointsA, s));
                engravingSubSlot(InWindow, row, () => accessory.EngravingB, v => accessory.EngravingB = v, () => accessory.PointsB, false);
                UIKit.Selector(row, 34, () => $"+{accessory.PointsB}", s => accessory.PointsB = cyclePoints(rules.AccessoryPointOptions, accessory.PointsB, s));
                engravingSubSlot(InWindow, row, () => accessory.NegativeEngraving, v => accessory.NegativeEngraving = v, () => accessory.NegativePoints, true);
                UIKit.Selector(row, 34, () => $"+{accessory.NegativePoints}", s => accessory.NegativePoints = Mathf.Clamp(accessory.NegativePoints + s, 0, 3));

                int statCount = Enum.GetValues(typeof(ECombatStat)).Length;
                UIKit.Selector(row, 50, () => KoreanText.Stat(accessory.StatA), s => accessory.StatA = (ECombatStat)UIKit.Wrap((int)accessory.StatA + s, statCount));
                UIKit.Selector(row, 44, () => $"{accessory.StatAValue:0}", s => accessory.StatAValue = Mathf.Clamp(accessory.StatAValue + s * 100.0f, 0.0f, 1000.0f));
                UIKit.Selector(row, 50, () => KoreanText.Stat(accessory.StatB), s => accessory.StatB = (ECombatStat)UIKit.Wrap((int)accessory.StatB + s, statCount));
                UIKit.Selector(row, 44, () => $"{accessory.StatBValue:0}", s => accessory.StatBValue = Mathf.Clamp(accessory.StatBValue + s * 100.0f, 0.0f, 1000.0f));
            }

            drawEngravingCodex(InWindow, null);
        }

        /// <summary>각인 하나를 받는 작은 칸 (악세사리 · 각인서 · 스톤 공용).</summary>
        private Image engravingSubSlot(GameWindow InWindow, Transform InRow, Func<int> InGet, Action<int> InSet, Func<int> InPoints, bool InNegative)
        {
            int id = InGet();
            var row = database.GetEngraving(id);
            var sprite = row != null ? ItemTooltips.EngravingIcon(database, id, EngravingCalculator.LevelOf(InPoints(), database.EngravingRules)) : IconFactory.Get(EIconKind.Empty, InNegative ? "neg" : "eng", InNegative ? 5 : 0);
            var cell = UIKit.IconCell(InRow, sprite, 48.0f, string.Empty,
                () => row != null ? ItemTooltips.Engraving(database, InGet(), pointsOf(InGet())) : new TooltipContent { Title = InNegative ? "(감소 각인 칸)" : "(각인 칸)", Subtitle = "각인을 끌어다 놓으세요", TitleColor = TooltipContent.Dim });
            cell.Droppable(p => p.Kind == (InNegative ? "engraving-neg" : "engraving") || (p.Kind == "engraving-slot" && ((SlotRef)p.Data).Negative == InNegative), p =>
            {
                if (p.Kind == "engraving-slot")
                {
                    var from = (SlotRef)p.Data;
                    int mine = InGet();
                    InSet(from.Get());
                    from.Set(mine);
                    changed(InWindow, "각인 위치를 바꿨습니다");
                    return;
                }

                InSet((int)p.Data);
                changed(InWindow, $"{engravingName((int)p.Data)} 장착");
            });
            cell.OnRightClick(() =>
            {
                if (InGet() != 0)
                {
                    string name = engravingName(InGet());
                    InSet(0);
                    changed(InWindow, $"{name} 해제");
                }
            });
            if (row != null)
            {
                var slotRef = new SlotRef { Get = InGet, Set = InSet, Negative = InNegative };
                cell.Draggable(() => new DragPayload
                {
                    Kind = "engraving-slot",
                    Data = slotRef,
                    Icon = sprite,
                    OnDroppedOutside = () =>
                    {
                        InSet(0);
                        changed(InWindow, $"{row.Name} 해제");
                    },
                });
            }

            return cell;
        }

        private class SlotRef
        {
            public Func<int> Get;
            public Action<int> Set;
            public bool Negative;
        }

        // ------------------------------------------------------------------ 각인

        private void drawEngraving(GameWindow InWindow)
        {
            if (ready(InWindow) == false)
            {
                return;
            }

            var rules = database.EngravingRules;
            var points = EngravingCalculator.ComputePoints(build, rules);

            header(InWindow, "활성 각인 (각인서 · 어빌리티 스톤 · 악세사리 합산, 적용 전 미리보기)");
            var active = UIKit.IconGrid(InWindow.Content, 60.0f, 8.0f, 13);
            foreach (var pair in points)
            {
                int id = pair.Key;
                int value = pair.Value;
                int level = EngravingCalculator.LevelOf(value, rules);
                UIKit.IconCell(active, ItemTooltips.EngravingIcon(database, id, level), 60.0f, $"Lv{level}", () => ItemTooltips.Engraving(database, id, value));
            }

            header(InWindow, $"각인서 (최대 활성도 {rules.MaxBookPoints}) · 어빌리티 스톤 (세공 최대 {rules.MaxStonePoints})");
            hint(InWindow, "도감의 각인을 칸에 끌어 놓거나 도감에서 우클릭(빈 각인서 칸에 장착 / 이미 장착이면 해제). 칸 우클릭 · 칸을 밖으로 끌기 = 해제.");
            for (int i = 0; i < build.Books.Count; ++i)
            {
                var book = build.Books[i];
                var row = UIKit.HorizontalRow(InWindow.Content, RowHeight, 6).transform;
                UIKit.RowLabel(row, $"각인서 {i + 1}", 90);
                engravingSubSlot(InWindow, row, () => book.EngravingId, v => book.EngravingId = v, () => book.Points, false);
                UIKit.RowLabel(row, engravingName(book.EngravingId), 170, 16);
                UIKit.Selector(row, 60, () => $"+{book.Points}", s => book.Points = Mathf.Clamp(book.Points + s * 3, 0, rules.MaxBookPoints));
            }

            var stone = build.Stone;
            var stoneRow = UIKit.HorizontalRow(InWindow.Content, RowHeight, 6).transform;
            UIKit.IconCell(stoneRow, ItemTooltips.StoneIcon(stone), IconSize, string.Empty, () => ItemTooltips.Stone(database, stone));
            engravingSubSlot(InWindow, stoneRow, () => stone.EngravingA, v => stone.EngravingA = v, () => stone.PointsA, false);
            UIKit.Selector(stoneRow, 40, () => $"+{stone.PointsA}", s => stone.PointsA = Mathf.Clamp(stone.PointsA + s, 0, rules.MaxStonePoints));
            engravingSubSlot(InWindow, stoneRow, () => stone.EngravingB, v => stone.EngravingB = v, () => stone.PointsB, false);
            UIKit.Selector(stoneRow, 40, () => $"+{stone.PointsB}", s => stone.PointsB = Mathf.Clamp(stone.PointsB + s, 0, rules.MaxStonePoints));
            engravingSubSlot(InWindow, stoneRow, () => stone.NegativeEngraving, v => stone.NegativeEngraving = v, () => stone.NegativePoints, true);
            UIKit.Selector(stoneRow, 40, () => $"+{stone.NegativePoints}", s => stone.NegativePoints = Mathf.Clamp(stone.NegativePoints + s, 0, rules.MaxStonePoints));

            drawEngravingCodex(InWindow, (id, negative) =>
            {
                if (negative == true)
                {
                    stone.NegativeEngraving = stone.NegativeEngraving == id ? 0 : id;
                    changed(InWindow, stone.NegativeEngraving == id ? $"{engravingName(id)} → 스톤 감소 각인" : $"{engravingName(id)} 해제");
                    return;
                }

                for (int i = 0; i < build.Books.Count; ++i)
                {
                    if (build.Books[i].EngravingId == id)
                    {
                        build.Books[i].EngravingId = 0;
                        changed(InWindow, $"{engravingName(id)} 각인서 해제");
                        return;
                    }
                }

                for (int i = 0; i < build.Books.Count; ++i)
                {
                    if (build.Books[i].EngravingId == 0)
                    {
                        build.Books[i].EngravingId = id;
                        changed(InWindow, $"{engravingName(id)} → 각인서 {i + 1}");
                        return;
                    }
                }

                InWindow.Status.text = "빈 각인서 칸이 없습니다 (칸을 우클릭해 먼저 해제하세요)";
            });
        }

        /// <summary>
        /// 각인 도감: 내 직업이 쓸 수 있는 모든 각인 (전투 · 직업 · 감소). 어디든 장착돼 있으면 "장착" 표시.
        /// InRightClick 이 null 이면 우클릭 동작 없음 (끌기만).
        /// </summary>
        private void drawEngravingCodex(GameWindow InWindow, Action<int, bool> InRightClick)
        {
            var points = EngravingCalculator.ComputePoints(build, database.EngravingRules);
            var groups = new[] { ("전투 각인", false, false), ("직업 각인", false, true), ("감소 각인", true, false) };
            int total = 0;
            for (int g = 0; g < groups.Length; ++g)
            {
                var ids = engravingIds(groups[g].Item2);
                ids.Remove(0);
                var filtered = ids.FindAll(id =>
                {
                    var row = database.GetEngraving(id);
                    return row != null && row.IsClass == groups[g].Item3;
                });
                total += filtered.Count;
                if (filtered.Count == 0)
                {
                    continue;
                }

                header(InWindow, $"각인 도감 — {groups[g].Item1} {filtered.Count}종");
                var grid = UIKit.IconGrid(InWindow.Content, CodexSize, 8.0f, 14);
                bool negative = groups[g].Item2;
                for (int i = 0; i < filtered.Count; ++i)
                {
                    int id = filtered[i];
                    int value;
                    points.TryGetValue(id, out value);
                    int level = EngravingCalculator.LevelOf(value, database.EngravingRules);
                    var sprite = ItemTooltips.EngravingIcon(database, id, Mathf.Max(1, level));
                    codexCell(grid, sprite, value > 0 ? $"Lv{level}" : string.Empty, () => ItemTooltips.Engraving(database, id, value), value > 0,
                        () => new DragPayload { Kind = negative ? "engraving-neg" : "engraving", Data = id, Icon = sprite },
                        InRightClick != null ? (Action)(() => InRightClick(id, negative)) : null);
                }
            }
        }

        private int pointsOf(int InEngravingId)
        {
            int value;
            return EngravingCalculator.ComputePoints(build, database.EngravingRules).TryGetValue(InEngravingId, out value) == true ? value : 0;
        }

        // ------------------------------------------------------------------ 카드

        private void drawCards(GameWindow InWindow)
        {
            if (ready(InWindow) == false)
            {
                return;
            }

            var sets = database.CardSets;
            header(InWindow, "장착 카드 6장 — 같은 세트 장수와 각성 합계로 세트 효과가 켜집니다");
            hint(InWindow, "도감의 카드를 칸에 끌어 놓거나 도감에서 우클릭(빈 칸에 장착 / 이미 장착이면 해제). 칸 우클릭 · 밖으로 끌기 = 해제.");
            var slotGrid = UIKit.IconGrid(InWindow.Content, 96.0f, 12.0f, 6);
            for (int i = 0; i < build.Cards.Count; ++i)
            {
                var slot = build.Cards[i];
                var sprite = ItemTooltips.CardIcon(database, slot);
                var cell = UIKit.IconCell(slotGrid, sprite, 96.0f, slot.CardId == 0 ? string.Empty : $"각성{slot.Awakening}", () => ItemTooltips.Card(database, build, slot));
                cell.Droppable(p => p.Kind == "card" || p.Kind == "card-slot", p =>
                {
                    if (p.Kind == "card-slot")
                    {
                        var from = (CardSlotState)p.Data;
                        int id = slot.CardId;
                        int awakening = slot.Awakening;
                        slot.CardId = from.CardId;
                        slot.Awakening = from.Awakening;
                        from.CardId = id;
                        from.Awakening = awakening;
                        changed(InWindow, "카드 위치를 바꿨습니다");
                        return;
                    }

                    int cardId = (int)p.Data;
                    unequipCard(cardId);
                    slot.CardId = cardId;
                    changed(InWindow, $"{database.GetCardName(cardId)} 장착");
                });
                cell.OnRightClick(() =>
                {
                    if (slot.CardId != 0)
                    {
                        string name = database.GetCardName(slot.CardId);
                        slot.CardId = 0;
                        changed(InWindow, $"{name} 해제");
                    }
                });
                if (slot.CardId != 0)
                {
                    cell.Draggable(() => new DragPayload
                    {
                        Kind = "card-slot",
                        Data = slot,
                        Icon = sprite,
                        OnDroppedOutside = () =>
                        {
                            slot.CardId = 0;
                            changed(InWindow, "카드 해제");
                        },
                    });
                }
            }

            var awakeningRow = UIKit.HorizontalRow(InWindow.Content, 40, 6).transform;
            for (int i = 0; i < build.Cards.Count; ++i)
            {
                var slot = build.Cards[i];
                UIKit.Selector(awakeningRow, 44, () => $"각{slot.Awakening}", s =>
                {
                    slot.Awakening = Mathf.Clamp(slot.Awakening + s, 0, 5);
                    changed(null, null);
                });
            }

            for (int s = 0; s < sets.Count; ++s)
            {
                var set = sets[s];
                header(InWindow, $"카드 도감 — {set.Name} ({set.CardNames.Length}장)");
                var grid = UIKit.IconGrid(InWindow.Content, 72.0f, 8.0f, 9);
                for (int c = 0; c < set.CardNames.Length; ++c)
                {
                    int cardId = set.CardId(c);
                    bool equipped = build.Cards.Exists(x => x.CardId == cardId);
                    var preview = new CardSlotState { CardId = cardId, Awakening = 5 };
                    var sprite = ItemTooltips.CardIcon(database, preview);
                    UIKit.IconCell(grid, sprite, 72.0f, string.Empty, () => ItemTooltips.Card(database, build, equipped ? build.Cards.Find(x => x.CardId == cardId) : preview))
                        .MarkEquipped(equipped)
                        .Draggable(() => new DragPayload { Kind = "card", Data = cardId, Icon = sprite })
                        .OnRightClick(() =>
                        {
                            if (build.Cards.Exists(x => x.CardId == cardId) == true)
                            {
                                unequipCard(cardId);
                                changed(InWindow, $"{database.GetCardName(cardId)} 해제");
                                return;
                            }

                            var empty = build.Cards.Find(x => x.CardId == 0);
                            if (empty == null)
                            {
                                InWindow.Status.text = "빈 카드 칸이 없습니다";
                                return;
                            }

                            empty.CardId = cardId;
                            changed(InWindow, $"{database.GetCardName(cardId)} 장착");
                        });
                }
            }
        }

        private void unequipCard(int InCardId)
        {
            for (int i = 0; i < build.Cards.Count; ++i)
            {
                if (build.Cards[i].CardId == InCardId)
                {
                    build.Cards[i].CardId = 0;
                }
            }
        }

        // ------------------------------------------------------------------ 보석

        private void drawGems(GameWindow InWindow)
        {
            if (ready(InWindow) == false)
            {
                return;
            }

            var skillIds = new List<int> { 0 };
            for (int i = 0; i < player.ClassDefinition.Skills.Count; ++i)
            {
                skillIds.Add(player.ClassDefinition.Skills[i].SkillId);
            }

            var table = database.Gems;
            int maxLevel = table != null ? table.MaxLevel : 10;
            header(InWindow, "보석 11칸 — 칸마다 적용할 스킬을 고릅니다 (같은 스킬 · 같은 종류는 하나만 적용)");
            hint(InWindow, "도감의 보석을 칸에 끌어 놓거나 도감에서 우클릭(빈 칸에 장착). 칸 우클릭 · 밖으로 끌기 = 해제.");
            var slotGrid = UIKit.IconGrid(InWindow.Content, 64.0f, 8.0f, 11);
            for (int i = 0; i < build.Gems.Count; ++i)
            {
                var gem = build.Gems[i];
                bool empty = gem.Level <= 0;
                var sprite = empty ? IconFactory.Get(EIconKind.Empty, "gem", 0) : ItemTooltips.GemIcon(gem);
                var cell = UIKit.IconCell(slotGrid, sprite, 64.0f, empty ? string.Empty : gem.Level.ToString(),
                    () => empty ? new TooltipContent { Title = "(빈 보석 칸)", TitleColor = TooltipContent.Dim } : ItemTooltips.Gem(database, gem));
                cell.Droppable(p => p.Kind == "gem", p =>
                {
                    var payload = (GemPayload)p.Data;
                    gem.Type = payload.Type;
                    gem.Level = payload.Level;
                    if (gem.SkillId == 0)
                    {
                        gem.SkillId = firstSkillWithoutGem(payload.Type, skillIds);
                    }

                    changed(InWindow, $"{payload.Level}레벨 {(payload.Type == EGemType.Damage ? "피해" : "재사용 대기시간")} 보석 장착");
                });
                cell.OnRightClick(() =>
                {
                    gem.Level = 0;
                    changed(InWindow, "보석 해제");
                });
                if (empty == false)
                {
                    cell.Draggable(() => new DragPayload
                    {
                        Kind = "gem-slot",
                        Data = gem,
                        Icon = sprite,
                        OnDroppedOutside = () =>
                        {
                            gem.Level = 0;
                            changed(InWindow, "보석 해제");
                        },
                    });
                }
            }

            header(InWindow, "칸별 적용 스킬");
            for (int i = 0; i < build.Gems.Count; ++i)
            {
                var gem = build.Gems[i];
                if (gem.Level <= 0)
                {
                    continue;
                }

                var row = UIKit.HorizontalRow(InWindow.Content, 48, 6).transform;
                UIKit.IconCell(row, ItemTooltips.GemIcon(gem), 40.0f, gem.Level.ToString(), () => ItemTooltips.Gem(database, gem));
                UIKit.RowLabel(row, $"{i + 1}번 칸 · {gem.Level}레벨 {(gem.Type == EGemType.Damage ? "피해" : "재사용")}", 200, 15);
                UIKit.Selector(row, 200, () => skillName(gem.SkillId), s => gem.SkillId = cycle(skillIds, gem.SkillId, s));
            }

            for (int t = 0; t < 2; ++t)
            {
                var type = t == 0 ? EGemType.Damage : EGemType.Cooldown;
                header(InWindow, $"보석 도감 — {(type == EGemType.Damage ? "피해 증가 보석" : "재사용 대기시간 감소 보석")} 1~{maxLevel}레벨");
                var grid = UIKit.IconGrid(InWindow.Content, CodexSize, 8.0f, 10);
                for (int level = 1; level <= maxLevel; ++level)
                {
                    var sample = new GemSlotState { Type = type, Level = level, SkillId = 0 };
                    var payload = new GemPayload { Type = type, Level = level };
                    var sprite = ItemTooltips.GemIcon(sample);
                    bool equipped = build.Gems.Exists(g => g.Level == level && g.Type == type);
                    codexCell(grid, sprite, level.ToString(), () => ItemTooltips.Gem(database, sample), equipped,
                        () => new DragPayload { Kind = "gem", Data = payload, Icon = sprite },
                        () =>
                        {
                            var emptySlot = build.Gems.Find(g => g.Level <= 0);
                            if (emptySlot == null)
                            {
                                InWindow.Status.text = "빈 보석 칸이 없습니다 (칸을 우클릭해 먼저 해제하세요)";
                                return;
                            }

                            emptySlot.Type = type;
                            emptySlot.Level = level;
                            emptySlot.SkillId = firstSkillWithoutGem(type, skillIds);
                            changed(InWindow, $"{level}레벨 보석 장착");
                        });
                }
            }
        }

        /// <summary>그 종류 보석이 아직 없는 첫 스킬 (각성기 제외).</summary>
        private int firstSkillWithoutGem(EGemType InType, List<int> InSkillIds)
        {
            for (int i = 1; i < InSkillIds.Count; ++i)
            {
                int id = InSkillIds[i];
                var skill = database.GetSkill(id);
                if (skill == null || skill.IsAwakening == true)
                {
                    continue;
                }

                if (build.Gems.Exists(g => g.Level > 0 && g.SkillId == id && g.Type == InType) == false)
                {
                    return id;
                }
            }

            return 0;
        }

        // ------------------------------------------------------------------ 스킬

        private static readonly string[] SkillKeys = { "Q", "W", "E", "R", "A", "S", "D", "F" };
        private int selectedSkillId;

        /// <summary>
        /// 스킬 창: 위 = 스킬 바(Q~F · V), 왼쪽 = 직업 스킬 목록(레벨 · 장착 여부), 오른쪽 = 고른 스킬의 상세 설명과 트라이포드.
        /// 목록의 아이콘을 스킬 바나 화면 아래 스킬 칸으로 끌어 놓아야 그 스킬을 쓸 수 있다.
        /// </summary>
        private void drawSkills(GameWindow InWindow)
        {
            if (ready(InWindow) == false)
            {
                return;
            }

            var cls = player.ClassDefinition;
            int used = 0;
            for (int i = 0; i < cls.Skills.Count; ++i)
            {
                var progress = build.GetSkill(cls.Skills[i].SkillId);
                used += progress != null ? BuildCalculator.SkillPointCost(progress.Level) : 0;
            }

            int max = database.MaxSkillPoints;
            header(InWindow, $"스킬 포인트 {used} / {max} (남은 포인트 {max - used})  ·  스킬 {cls.Skills.Count}개 중 8개를 단축키에 장착");
            hint(InWindow, "스킬 아이콘을 아래 스킬 바나 화면 아래 스킬 칸으로 끌어 놓으면 사용할 수 있습니다. 칸끼리 끌면 자리를 바꾸고, 칸 우클릭 · 밖으로 끌기 = 비우기.");

            // 스킬 바
            var bar = UIKit.IconGrid(InWindow.Content, 62.0f, 8.0f, 9);
            for (int i = 0; i < CharacterBuildData.SkillSlotCount; ++i)
            {
                int slot = i;
                int id = build.SkillSlots != null && i < build.SkillSlots.Length ? build.SkillSlots[i] : 0;
                var definition = id != 0 ? database.GetSkill(id) : null;
                var sprite = definition != null ? ItemTooltips.SkillIcon(definition) : IconFactory.Get(EIconKind.Empty, "skill", 0);
                var cell = UIKit.IconCell(bar, sprite, 62.0f, SkillKeys[i],
                    () => definition != null ? ItemTooltips.Skill(definition, build.GetSkill(definition.SkillId), player) : new TooltipContent { Title = $"[{SkillKeys[slot]}] 빈 칸", Subtitle = "스킬을 끌어다 놓으세요", TitleColor = TooltipContent.Dim });
                cell.Droppable(p => p.Kind == "skill", p => SetSkillSlot(slot, (int)p.Data));
                cell.OnRightClick(() => SetSkillSlot(slot, 0));
                if (definition != null)
                {
                    cell.Draggable(() => new DragPayload { Kind = "skill", Data = definition.SkillId, Icon = sprite, OnDroppedOutside = () => SetSkillSlot(slot, 0) });
                }
            }

            var awakening = cls.GetAwakening(build.SelectedAwakening);
            UIKit.IconCell(bar, ItemTooltips.SkillIcon(awakening), 62.0f, "V", () => ItemTooltips.Skill(awakening, build.GetSkill(awakening != null ? awakening.SkillId : 0), player));

            var awakeningRow = UIKit.HorizontalRow(InWindow.Content, 40, 6).transform;
            UIKit.RowLabel(awakeningRow, "각성기 (V)", 110, 16);
            UIKit.Selector(awakeningRow, 220, () => awakening != null ? awakening.DisplayName : "-", s =>
            {
                build.SelectedAwakening = UIKit.Wrap(build.SelectedAwakening + s, Mathf.Max(1, cls.Awakenings.Count));
                changed(InWindow, "각성기 변경 — 적용 + 저장을 누르세요");
            });

            if (selectedSkillId == 0 || cls.Skills.Find(x => x.SkillId == selectedSkillId) == null)
            {
                selectedSkillId = cls.Skills.Count > 0 ? cls.Skills[0].SkillId : 0;
            }

            // 목록 | 상세
            var panes = (RectTransform)new GameObject("Panes", typeof(RectTransform)).transform;
            panes.SetParent(InWindow.Content, false);
            var paneLayout = panes.gameObject.AddComponent<HorizontalLayoutGroup>();
            paneLayout.spacing = 10.0f;
            paneLayout.childControlWidth = true;
            paneLayout.childControlHeight = true;
            paneLayout.childForceExpandWidth = false;
            paneLayout.childForceExpandHeight = false;
            paneLayout.childAlignment = TextAnchor.UpperLeft;
            var left = column(panes, 520.0f);
            var right = column(panes, 0.0f);

            header(left, "직업 스킬");
            for (int i = 0; i < cls.Skills.Count; ++i)
            {
                var skill = cls.Skills[i];
                var progress = build.GetOrAddSkill(skill.SkillId);
                int slot = build.SkillSlotOf(skill.SkillId);
                bool selected = skill.SkillId == selectedSkillId;
                var row = UIKit.HorizontalRow(left, 56, 6);
                if (selected == true)
                {
                    row.GetComponent<Image>().color = new Color(UIKit.AccentColor.r, UIKit.AccentColor.g, UIKit.AccentColor.b, 0.18f);
                }

                var sprite = ItemTooltips.SkillIcon(skill);
                int skillId = skill.SkillId;
                var icon = UIKit.IconCell(row.transform, sprite, 48.0f, $"{progress.Level}", () => ItemTooltips.Skill(skill, progress, player), () =>
                {
                    selectedSkillId = skillId;
                    changed(null, null);
                });
                icon.Draggable(() => new DragPayload { Kind = "skill", Data = skillId, Icon = sprite });
                if (slot >= 0)
                {
                    icon.MarkEquipped(true);
                }

                var name = UIKit.RowLabel(row.transform, slot >= 0 ? $"[{SkillKeys[slot]}] {skill.DisplayName}" : skill.DisplayName, 0, 16);
                name.color = slot >= 0 ? Color.white : new Color(0.7f, 0.7f, 0.72f);
                UIKit.Selector(row.transform, 50, () => $"Lv{progress.Level}", s =>
                {
                    progress.Level = Mathf.Clamp(progress.Level + s, 1, skill.MaxLevel);
                    changed(null, null);
                });
                UIKit.RowButton(row.transform, selected ? "◆" : "▶", 34, () =>
                {
                    selectedSkillId = skillId;
                    changed(null, null);
                });
            }

            drawSkillDetail(InWindow, right, cls.Skills.Find(x => x.SkillId == selectedSkillId));
        }

        private void drawSkillDetail(GameWindow InWindow, RectTransform InColumn, DemoLA.Skills.SkillDefinition InSkill)
        {
            if (InSkill == null)
            {
                return;
            }

            var progress = build.GetOrAddSkill(InSkill.SkillId);
            var top = UIKit.HorizontalRow(InColumn, 84, 10).transform;
            UIKit.IconCell(top, ItemTooltips.SkillIcon(InSkill), 76.0f, $"{progress.Level}", () => ItemTooltips.Skill(InSkill, progress, player));
            int slot = build.SkillSlotOf(InSkill.SkillId);
            UIKit.RowLabel(top, $"{InSkill.DisplayName}\n<size=14><color=#9AA0AA>{(slot >= 0 ? $"단축키 {SkillKeys[slot]} 에 장착됨" : "장착 안 됨 — 스킬 바로 끌어 놓으세요")}</color></size>", 0, 22).supportRichText = true;

            // 트라이포드: 단계마다 선택지 아이콘. 클릭 = 선택, 우클릭 = 선택 해제.
            for (int t = 0; t < InSkill.Tiers.Count && t < 3; ++t)
            {
                int tierIndex = t;
                var tier = InSkill.Tiers[t];
                bool unlocked = progress.Level >= tier.UnlockSkillLevel;
                header(InColumn, $"트라이포드 {t + 1}단계 — 스킬 레벨 {tier.UnlockSkillLevel}{(unlocked ? string.Empty : " (잠김)")}");
                var options = UIKit.HorizontalRow(InColumn, 60, 8).transform;
                for (int o = 0; o < tier.Options.Count; ++o)
                {
                    int optionIndex = o;
                    var option = tier.Options[o];
                    bool chosen = progress.GetChoice(t) == o;
                    var cell = UIKit.IconCell(options, IconFactory.Get(EIconKind.Skill, "tripod" + option.Name, chosen ? 4 : (unlocked ? 2 : 0)), 52.0f, string.Empty,
                        () => ItemTooltips.Tripod(InSkill, tierIndex, progress.GetChoice(tierIndex)), () =>
                        {
                            if (unlocked == false)
                            {
                                InWindow.Status.text = $"스킬 레벨 {tier.UnlockSkillLevel} 이 되어야 고를 수 있습니다";
                                return;
                            }

                            progress.TripodChoices[tierIndex] = optionIndex;
                            changed(InWindow, $"{option.Name} 선택 — 적용 + 저장을 누르세요");
                        });
                    cell.MarkEquipped(chosen);
                    cell.OnRightClick(() =>
                    {
                        progress.TripodChoices[tierIndex] = -1;
                        changed(InWindow, "트라이포드 선택 해제");
                    });
                    var label = UIKit.RowLabel(options, option.Name, 120, 14);
                    label.color = chosen ? new Color(0.55f, 1.0f, 0.6f) : (unlocked ? Color.white : new Color(0.55f, 0.55f, 0.58f));
                }
            }

            // 상세 설명 (툴팁과 같은 내용)
            header(InColumn, "스킬 정보");
            var content = ItemTooltips.Skill(InSkill, progress, player);
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < content.Lines.Count; ++i)
            {
                var line = content.Lines[i];
                if (line.Value.a <= 0.0f)
                {
                    builder.Append('\n');
                    continue;
                }

                builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(line.Value)).Append('>').Append(line.Key).Append("</color>\n");
            }

            paragraph(InColumn, builder.ToString(), 15);
        }

        /// <summary>세로로 쌓는 열 (InWidth 0 = 남는 폭 전부).</summary>
        private static RectTransform column(Transform InParent, float InWidth)
        {
            var rect = (RectTransform)new GameObject("Column", typeof(RectTransform)).transform;
            rect.SetParent(InParent, false);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            if (InWidth > 0.0f)
            {
                element.preferredWidth = InWidth;
                element.minWidth = InWidth;
            }
            else
            {
                element.flexibleWidth = 1.0f;
            }

            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4.0f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;
            return rect;
        }

        /// <summary>여러 줄 글 (높이는 글 길이에 맞춰 레이아웃이 정한다).</summary>
        private static Text paragraph(Transform InParent, string InRichText, int InSize)
        {
            var rect = (RectTransform)new GameObject("Paragraph", typeof(RectTransform)).transform;
            rect.SetParent(InParent, false);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = UIKit.Font;
            text.fontSize = InSize;
            text.supportRichText = true;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.1f;
            text.text = InRichText;
            return text;
        }

        private static void header(Transform InParent, string InText)
        {
            var row = UIKit.HorizontalRow(InParent, 30, 6);
            row.GetComponent<Image>().color = new Color(UIKit.AccentColor.r, UIKit.AccentColor.g, UIKit.AccentColor.b, 0.15f);
            UIKit.RowLabel(row.transform, InText, 0, 15).color = UIKit.AccentColor;
        }

        /// <summary>스킬 바 InSlot 에 InSkillId 를 놓는다 (0 = 비우기). 다른 칸에 있던 스킬이면 자리를 바꾼다. HUD 에서도 부른다.</summary>
        public void SetSkillSlot(int InSlot, int InSkillId)
        {
            if (build == null || InSlot < 0 || InSlot >= CharacterBuildData.SkillSlotCount)
            {
                return;
            }

            if (InSkillId == 0)
            {
                build.SkillSlots[InSlot] = 0;
            }
            else
            {
                build.PlaceSkill(InSlot, InSkillId);
            }

            session.SaveAsync().Forget();
            var skill = InSkillId != 0 ? database.GetSkill(InSkillId) : null;
            changed(windows.ContainsKey(EWindow.Skill) ? windows[EWindow.Skill] : null, skill != null ? $"{skill.DisplayName} → {SkillKeys[InSlot]}" : $"{SkillKeys[InSlot]} 칸 비움");
        }

        /// <summary>배틀 아이템 칸 InSlot 에 InIndex 를 놓는다 (0 = 비우기). 같은 아이템이 다른 칸에 있으면 그 칸은 비운다. HUD 에서도 부른다.</summary>
        public void SetItemSlot(int InSlot, int InIndex)
        {
            if (build == null || InSlot < 0 || InSlot >= build.BattleItemSlots.Length)
            {
                return;
            }

            if (InIndex != 0)
            {
                for (int i = 0; i < build.BattleItemSlots.Length; ++i)
                {
                    if (i != InSlot && build.BattleItemSlots[i] == InIndex)
                    {
                        build.BattleItemSlots[i] = 0;
                    }
                }
            }

            build.BattleItemSlots[InSlot] = InIndex;
            player.RefreshBattleItems();
            session.SaveAsync().Forget();
            changed(windows.ContainsKey(EWindow.Inventory) ? windows[EWindow.Inventory] : null, InIndex != 0 ? $"{InSlot + 1}번 칸에 장착" : $"{InSlot + 1}번 칸 비움");
        }

        // ------------------------------------------------------------------ 인벤토리 (배틀 아이템)

        private void drawInventory(GameWindow InWindow)
        {
            if (ready(InWindow) == false)
            {
                return;
            }

            var table = database.Tables != null ? database.Tables.BattleItem : null;
            if (table == null)
            {
                header(InWindow, "배틀 아이템 표가 없습니다.");
                return;
            }

            header(InWindow, "빠른 사용 칸 (단축키 1~4) — 화면 아래 오른쪽 칸과 같습니다");
            hint(InWindow, "아래 가방의 아이템을 칸(여기 또는 화면 아래)으로 끌어 놓으세요. 빈 칸은 빈 칸으로 남습니다. 칸 우클릭 · 밖으로 끌기 = 비우기.");
            var slotGrid = UIKit.IconGrid(InWindow.Content, SlotSize, 12.0f, 4);
            for (int slot = 0; slot < build.BattleItemSlots.Length; ++slot)
            {
                int s = slot;
                var row = itemRow(table, build.BattleItemSlots[s]);
                var sprite = row != null ? ItemTooltips.BattleItemIcon(row) : IconFactory.Get(EIconKind.Empty, "item", 0);
                var cell = UIKit.IconCell(slotGrid, sprite, SlotSize, $"{s + 1}", () => ItemTooltips.BattleItem(itemRow(table, build.BattleItemSlots[s])));
                cell.Droppable(p => p.Kind == "item" || p.Kind == "item-slot", p =>
                {
                    if (p.Kind == "item-slot")
                    {
                        int from = (int)p.Data;
                        int mine = build.BattleItemSlots[s];
                        int theirs = build.BattleItemSlots[from];
                        build.BattleItemSlots[from] = 0;
                        SetItemSlot(s, theirs);
                        SetItemSlot(from, mine);
                        return;
                    }

                    SetItemSlot(s, (int)p.Data);
                });
                cell.OnRightClick(() => SetItemSlot(s, 0));
                if (row != null)
                {
                    cell.Draggable(() => new DragPayload { Kind = "item-slot", Data = s, Icon = sprite, OnDroppedOutside = () => SetItemSlot(s, 0) });
                }
            }

            header(InWindow, $"가방 — 배틀 아이템 {table.All.Count}종 (보유 수량은 레이드를 다시 시작하면 채워집니다)");
            var grid = UIKit.IconGrid(InWindow.Content, 64.0f, 8.0f, 10);
            for (int i = 0; i < table.All.Count; ++i)
            {
                var item = table.All[i];
                int index = item.Index;
                var sprite = ItemTooltips.BattleItemIcon(item);
                bool equipped = Array.IndexOf(build.BattleItemSlots, index) >= 0;
                UIKit.IconCell(grid, sprite, 64.0f, $"×{item.MaxCount}", () =>
                {
                    var content = ItemTooltips.BattleItem(item);
                    content.Line("빠른 사용 칸으로 끌어 놓거나 우클릭 (빈 칸에 장착 / 이미 장착이면 해제)", TooltipContent.Dim);
                    return content;
                })
                    .MarkEquipped(equipped)
                    .Draggable(() => new DragPayload { Kind = "item", Data = index, Icon = sprite })
                    .OnRightClick(() =>
                    {
                        int at = Array.IndexOf(build.BattleItemSlots, index);
                        if (at >= 0)
                        {
                            SetItemSlot(at, 0);
                            return;
                        }

                        at = Array.FindIndex(build.BattleItemSlots, v => itemRow(table, v) == null);
                        if (at < 0)
                        {
                            InWindow.Status.text = "빈 칸이 없습니다 (칸을 우클릭해 먼저 비우세요)";
                            return;
                        }

                        SetItemSlot(at, index);
                    });
            }
        }

        private static BattleItemRow itemRow(BattleItemTable InTable, int InIndex)
        {
            BattleItemRow item;
            return InTable.TryGet(InIndex, out item) == true ? item : null;
        }

        // ------------------------------------------------------------------ 능력치

        private void drawStats(GameWindow InWindow)
        {
            if (ready(InWindow) == false)
            {
                return;
            }

            var classRow = UIKit.HorizontalRow(InWindow.Content, 44, 6).transform;
            UIKit.RowLabel(classRow, "직업", 80);
            UIKit.Selector(classRow, 180, () => player != null ? player.ClassDefinition.DisplayName : "-", InStep =>
            {
                if (player == null || onChangeClass == null)
                {
                    return;
                }

                int count = Enum.GetValues(typeof(ECharacterClass)).Length;
                onChangeClass((ECharacterClass)UIKit.Wrap((int)player.ClassDefinition.Class + InStep, count));
            });
            UIKit.RowButton(classRow, "기본값으로", 140, () => resetBuild(InWindow));

            if (player.Stats != null)
            {
                var stats = player.Stats;
                header(InWindow, "현재 능력치 (적용된 상태)");
                line(InWindow, $"공격력 {stats.Get(EAttr.AttackPower):N0}   최대 생명력 {stats.Get(EAttr.MaxHealth):N0}   방어력 {stats.Get(EAttr.Defense):N0}");
                line(InWindow, $"치명 {stats.Get(EAttr.StatCrit):0} → 치명타 적중률 {stats.Get(EAttr.CritRate):0.0}%   치명타 피해 {stats.Get(EAttr.CritDamage):0}%");
                line(InWindow, $"특화 {stats.Get(EAttr.StatSpecialization):0}   신속 {stats.Get(EAttr.StatSwiftness):0} → 공속 {stats.Get(EAttr.AttackSpeed):0.0}% 이속 {stats.Get(EAttr.MoveSpeed):0.0}% 쿨감 {stats.Get(EAttr.CooldownReduction):0.0}%");
                line(InWindow, $"피해 증가(합) {stats.Get(EAttr.DamageIncrease):0.0}%   독립 곱 ×{DemoLA.Effects.SkillDamageExecution.IndependentMultiplier(player.Asc):0.000}   받는 피해 감소 {stats.Get(EAttr.IncomingDamageReduction):0.0}%");
            }

            var result = BuildCalculator.Compute(build, database);
            header(InWindow, "적용될 효과 (미리보기)");
            for (int i = 0; i < result.Summary.Count; ++i)
            {
                line(InWindow, result.Summary[i]);
            }

            header(InWindow, $"스킬 효과 {result.SkillEffects.Count}개 · 조건부 피해 {result.DamageModifiers.Count}개 · 공격 속성 {KoreanText.Element(result.AttackElement)} · 스킬 포인트 {result.UsedSkillPoints}");
            for (int i = 0; i < result.Warnings.Count; ++i)
            {
                UIKit.RowLabel(UIKit.HorizontalRow(InWindow.Content, 28, 6).transform, "⚠ " + result.Warnings[i], 0, 16).color = new Color(1.0f, 0.6f, 0.4f);
            }
        }

        // ------------------------------------------------------------------ 키 설정

        private void drawKeys(GameWindow InWindow)
        {
            var bindings = DemoLA.Player.InputBindings.Current;
            if (bindings == null || bindings.Asset == null)
            {
                header(InWindow, "InputActionAsset 이 없어 키 설정을 할 수 없습니다 (DemoLA/Setup 으로 씬을 다시 만드세요).");
                return;
            }

            header(InWindow, "'변경' 후 새 키를 누르세요 (Esc 취소). 저장은 자동.");
            foreach (var action in bindings.Asset.FindActionMap(DemoLA.Player.InputBindings.GameplayMap).actions)
            {
                for (int b = 0; b < action.bindings.Count; ++b)
                {
                    var binding = action.bindings[b];
                    if (binding.isComposite == true || binding.isPartOfComposite == true)
                    {
                        continue;
                    }

                    int index = b;
                    var row = UIKit.HorizontalRow(InWindow.Content, 34, 6).transform;
                    UIKit.RowLabel(row, action.name, 200, 16);
                    var label = UIKit.RowLabel(row, action.GetBindingDisplayString(index), 0, 16);
                    UIKit.RowButton(row, "변경", 80, () =>
                    {
                        label.text = "입력 대기...";
                        rebinding = true;
                        bindings.Rebind(action, index, () =>
                        {
                            rebinding = false;
                            label.text = action.GetBindingDisplayString(index);
                        });
                    });
                    UIKit.RowButton(row, "초기화", 80, () =>
                    {
                        bindings.ResetBinding(action, index);
                        label.text = action.GetBindingDisplayString(index);
                    });
                }
            }

            header(InWindow, "창 단축키: 장비 P · 인벤토리 I · 악세사리 Y · 각인 N · 카드 C · 보석 G · 스킬 K · 능력치 U · 키 설정 O · Esc 모두 닫기");
        }

        // ------------------------------------------------------------------ actions

        private void apply(GameWindow InWindow)
        {
            if (player == null)
            {
                return;
            }

            player.ApplyBuild(build);
            session.SaveAsync().Forget();
            InWindow.Status.text = "적용 + 저장 완료";
            refreshAll();
            OnApplied?.Invoke();
        }

        private void resetBuild(GameWindow InWindow)
        {
            session.ResetBuild(build.Class);
            build = session.GetBuild(build.Class);
            player.ApplyBuild(build);
            InWindow.Status.text = "기본값으로 되돌림";
            refreshAll();
        }

        // ------------------------------------------------------------------ helpers

        private static void header(GameWindow InWindow, string InText)
        {
            var row = UIKit.HorizontalRow(InWindow.Content, 32, 6);
            row.GetComponent<Image>().color = new Color(UIKit.AccentColor.r, UIKit.AccentColor.g, UIKit.AccentColor.b, 0.15f);
            UIKit.RowLabel(row.transform, InText, 0, 16).color = UIKit.AccentColor;
        }

        private static void line(GameWindow InWindow, string InText)
        {
            UIKit.RowLabel(UIKit.HorizontalRow(InWindow.Content, 28, 6).transform, InText, 0, 16);
        }

        private List<int> engravingIds(bool InNegative)
        {
            var ids = new List<int> { 0 };
            var engravings = database.Engravings;
            for (int i = 0; i < engravings.Count; ++i)
            {
                var row = engravings[i];
                if (row.IsNegative != InNegative || (row.IsClass == true && row.ClassRestriction != build.Class))
                {
                    continue;
                }

                ids.Add(row.Index);
            }

            return ids;
        }

        private string engravingName(int InId)
        {
            var engraving = database.GetEngraving(InId);
            return engraving != null ? engraving.Name : "(없음)";
        }

        private string skillName(int InSkillId)
        {
            var skill = database.GetSkill(InSkillId);
            return skill != null ? skill.DisplayName : "(없음)";
        }

        private static int cycle(List<int> InValues, int InCurrent, int InStep)
        {
            int index = InValues.IndexOf(InCurrent);
            return InValues[UIKit.Wrap((index < 0 ? 0 : index) + InStep, InValues.Count)];
        }

        private static int cyclePoints(int[] InOptions, int InCurrent, int InStep)
        {
            int index = Array.IndexOf(InOptions, InCurrent);
            return InOptions[UIKit.Wrap((index < 0 ? 0 : index) + InStep, InOptions.Length)];
        }
    }
}
