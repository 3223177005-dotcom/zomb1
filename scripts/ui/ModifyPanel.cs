using Godot;
using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// **改装面板**（仿 TaCZ）：
/// - 一列槽位，每个槽显示「槽名 + 装着什么 / 无」
/// - 把配件**拖到槽上**就装上（复用 <see cref="ItemSlot"/> 的拖拽）
/// - **右键槽**卸下
/// - 底部实时显示数值变化（基础 → 当前）
/// </summary>
public partial class ModifyPanel : Control
{
    private const string DefaultHint = "把配件拖到对应的槽上装配件；也可以把槽里的配件拖出去卸下。";

    /// <summary>装/卸之后通知外面刷新（背包、负重等）。</summary>
    public Action Changed { get; set; }

    /// <summary>「关闭自己」的回调（放进 <see cref="PanelStack"/>，Esc 按顺序关）。</summary>
    private readonly Action _closeSelf = null;

    public ModifyPanel() { _closeSelf = Close; }

    private ItemStack _gun;
    private PanelContainer _card;
    private Label _title;
    private Label _hint;
    private VBoxContainer _slotList;
    private VBoxContainer _bagList;
    private Label _bagTitle;
    private Label _stats;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        // 遮罩
        var dim = new ColorRect { Color = new Color(0.02f, 0.02f, 0.03f, 0.74f) };
        dim.MouseFilter = MouseFilterEnum.Stop;
        AddChild(dim);
        // ★ 锚点必须在 AddChild **之后**设，否则不会铺满父级
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Pass };
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _card = new PanelContainer { CustomMinimumSize = new Vector2(620, 505) };

        // ★ 必须有背景，否则会透出背后的背包列表
        var cardStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.075f, 0.085f, 0.105f, 0.99f),
            BorderColor = new Color(0.62f, 0.50f, 0.24f, 0.95f),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        };
        _card.AddThemeStyleboxOverride("panel", cardStyle);

        center.AddChild(_card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        _card.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);

        // ── 标题行 ──
        var head = new HBoxContainer();
        box.AddChild(head);

        _title = new Label { Text = "改装", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _title.AddThemeFontSizeOverride("font_size", 22);
        _title.AddThemeColorOverride("font_color", new Color(0.95f, 0.88f, 0.55f));
        head.AddChild(_title);

        var close = new Button { Text = "关闭" };
        close.AddThemeFontSizeOverride("font_size", 16);
        close.Pressed += Close;
        head.AddChild(close);

        _hint = new Label { Text = DefaultHint };
        _hint.AddThemeFontSizeOverride("font_size", 15);
        _hint.AddThemeColorOverride("font_color", new Color(0.62f, 0.64f, 0.70f));
        box.AddChild(_hint);

        box.AddChild(new HSeparator());

        // ── 槽位列表 ──
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, 92),
        };
        box.AddChild(scroll);

        _slotList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _slotList.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_slotList);

        box.AddChild(new HSeparator());

        // ── 背包里的配件（拖上去装） ──
        _bagTitle = new Label { Text = BagTitleText(0) };
        _bagTitle.AddThemeFontSizeOverride("font_size", 15);
        _bagTitle.AddThemeColorOverride("font_color", new Color(0.62f, 0.64f, 0.70f));
        box.AddChild(_bagTitle);

        var bagScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, 240),
        };
        box.AddChild(bagScroll);

        _bagList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _bagList.AddThemeConstantOverride("separation", 2);
        bagScroll.AddChild(_bagList);

        box.AddChild(new HSeparator());

        // ── 数值汇总 ──
        _stats = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _stats.AddThemeFontSizeOverride("font_size", 15);
        _stats.AddThemeColorOverride("font_color", new Color(0.80f, 0.82f, 0.88f));
        box.AddChild(_stats);
    }

    /// <summary>打开某把枪的改装面板。</summary>
    public void Open(ItemStack gun)
    {
        if (gun?.Data == null) return;
        if (!WeaponMods.CanModify(gun)) return;

        _gun = gun;
        Visible = true;
        MoveToFront();
        PanelStack.Set(_closeSelf, true);
        // 自己也铺满父级（父级是 Tab 面板的根 Control）
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Rebuild();
    }

    public void Close()
    {
        _gun = null;
        Visible = false;
        PanelStack.Set(_closeSelf, false);
    }

    /// <summary>重建槽位列表与数值（装/卸之后调用）。</summary>
    private void Rebuild()
    {
        if (_gun == null) return;

        _title.Text = $"改装  {_gun.Data.Name}";

        foreach (var child in _slotList.GetChildren())
        {
            _slotList.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var weaponSlot in WeaponMods.Slots(_gun))
            _slotList.AddChild(MakeSlotRow(weaponSlot));

        BuildBagList();
        UpdateStats();
        Changed?.Invoke();
    }

    /// <summary>
    /// 列出**身上所有配件**（不是整个背包网格）：
    /// 从基础口袋、各装备容器、以及它们里面的容器里，把 <see cref="ItemData.AttachmentStats"/>
    /// 非空的物品全部捞出来，用旧的文字行 <see cref="ItemSlot"/> 显示，拖到上面的槽位就能装。
    ///
    /// 为什么不用 `GridPanel`：改装时玩家要找的是"我有哪些配件"，
    /// 把整个 6×5 背包摊出来反而要自己在空格子里找；过滤成一张清单更直接。
    /// </summary>
    private void BuildBagList()
    {
        if (_bagList == null) return;

        foreach (var child in _bagList.GetChildren())
        {
            _bagList.RemoveChild(child);
            child.QueueFree();
        }

        var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
        if (player == null) return;

        // ① 逐口袋收集配件（含口袋里的容器，最多下探到嵌套上限）
        var found = new List<AttachmentRef>();
        found.AddRange(AttachmentFinder.Collect(player.Pocket, "基础口袋"));

        for (int i = 0; i < player.Equipment.SlotCount; i++)
        {
            var equipped = player.Equipment.Get(i);
            if (equipped?.Contents == null) continue;

            string label = equipped.Data?.Name ?? $"槽 {i + 1}";
            found.AddRange(AttachmentFinder.Collect(equipped.Contents, label));
        }

        if (found.Count == 0)
        {
            _bagTitle.Text = BagTitleText(0);

            var empty = new Label { Text = "（身上没有配件）" };
            empty.AddThemeFontSizeOverride("font_size", 15);
            empty.AddThemeColorOverride("font_color", new Color(0.42f, 0.44f, 0.50f));
            _bagList.AddChild(empty);
            return;
        }

        _bagTitle.Text = BagTitleText(found.Count);

        // ② 按所在口袋分组显示（只有多个口袋有配件时才显示组标题，省空间）
        int groups = CountDistinct(found);
        string lastWhere = null;
        foreach (var entry in found)
        {
            if (groups > 1 && entry.Where != lastWhere)
            {
                lastWhere = entry.Where;

                var header = new Label
                {
                    Text = entry.Where,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                header.AddThemeFontSizeOverride("font_size", 13);
                header.AddThemeColorOverride("font_color", new Color(0.62f, 0.64f, 0.70f));
                _bagList.AddChild(header);
            }

            _bagList.AddChild(MakeBagRow(entry.Pocket, entry.Stack, entry.Note));
        }
    }

    /// <summary>配件清单的小标题（带件数）。</summary>
    private static string BagTitleText(int count)
        => count > 0
            ? $"背包里的配件（{count} 件，拖到上面的槽位安装）"
            : "背包里的配件（拖到上面的槽位安装）";

    /// <summary>配件分布在几个口袋里（决定要不要显示组标题）。</summary>
    private static int CountDistinct(List<AttachmentRef> found)
    {
        var names = new HashSet<string>();
        foreach (var entry in found) names.Add(entry.Where);
        return names.Count;
    }

    /// <summary>背包里的一件配件行（可拖出，拖到槽位上安装）。</summary>
    private ItemSlot MakeBagRow(Inventory pocket, ItemStack stack, string note = "")
    {
        var summary = new ItemSummary(stack.Data.Id, stack.Data, stack.Count,
            stack.TotalWeight, 1);

        var content = ItemRow.Build(summary, note: string.IsNullOrEmpty(note) ? null : note, indent: 1);

        var row = new ItemSlot
        {
            Pocket = pocket,
            Items = new List<ItemStack> { stack },
            Changed = Rebuild,
            TooltipText = ItemTooltip.Build(stack),
            // 拖拽划过放不下的地方 → 在提示行里说明原因
            Message = text => { if (_hint != null) _hint.Text = text ?? DefaultHint; },
        };

        row.AddChild(content);
        ItemRow.IgnoreMouse(content);
        return row;
    }

    /// <summary>一个槽位行：槽名 + 当前内容 + 卸下按钮；整行可接收拖拽。</summary>
    private ItemSlot MakeSlotRow(WeaponSlot weaponSlot)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);

        // 槽名
        var name = new Label
        {
            Text = weaponSlot.DisplayName,
            CustomMinimumSize = new Vector2(96, 0),
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        name.AddThemeFontSizeOverride("font_size", 17);
        name.AddThemeColorOverride("font_color", new Color(0.70f, 0.73f, 0.80f));
        row.AddChild(name);

        // 内容
        bool empty = weaponSlot.IsEmpty;
        var content = new Label
        {
            Text = empty ? "无" : (weaponSlot.Installed.Data?.Name ?? "无"),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        content.AddThemeFontSizeOverride("font_size", 17);
        content.AddThemeColorOverride("font_color",
            empty ? new Color(0.42f, 0.44f, 0.50f) : new Color(0.86f, 0.88f, 0.92f));
        row.AddChild(content);

        // 卸下
        if (!empty)
        {
            var uninstall = new Button { Text = "卸下" };
            uninstall.AddThemeFontSizeOverride("font_size", 14);
            uninstall.Pressed += () =>
            {
                var removed = WeaponMods.TryUninstall(weaponSlot);
                if (removed == null) { Rebuild(); return; }

                // ★ 负重 / 格子不够就**装回去**，不做这次卸下
                if (!TakeBack(removed))
                {
                    weaponSlot.Installed = removed;
                    _hint.Text = $"卸不下来：背包负重已满或没有空格子（{removed.Data?.Name}）";
                }
                else
                {
                    _hint.Text = DefaultHint;
                }

                Rebuild();
            };
            row.AddChild(uninstall);
        }

        var slot = new ItemSlot
        {
            Name = "SlotRow",
            WeaponHost = _gun,
            WeaponTarget = weaponSlot,
            Changed = Rebuild,
            Items = new List<ItemStack>(),
            // 拖拽时把"为什么装不上"显示在提示行，正常时恢复默认提示
            Message = text => { if (_hint != null) _hint.Text = text ?? DefaultHint; },
            TooltipText = empty
                ? $"{weaponSlot.DisplayName}槽：空\n把「{SlotTypes.DisplayName(weaponSlot.SlotType)}」类配件拖到这里"
                : $"{weaponSlot.DisplayName}：{weaponSlot.Installed.Data?.Name}",
        };

        slot.AddChild(row);
        ItemRow.IgnoreMouse(row);
        return slot;
    }

    /// <summary>卸下的配件塞回玩家身上。
    /// **返回 false = 负重 / 格子不够**，调用方负责把它装回槽里。
    /// </summary>
    private bool TakeBack(ItemStack item)
    {
        var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
        if (player == null || item == null) return false;

        // TryTake 内部已经做了全局负重 + 口袋格子校验
        return player.TryTake(item, out _) == ContainCode.Success;
    }

    /// <summary>底部数值：基础 → 当前。</summary>
    private void UpdateStats()
    {
        if (_gun?.Data?.WeaponStats is not RangedWeaponData ranged || ranged.GunStats == null)
        {
            _stats.Text = "（该武器没有枪械数值）";
            return;
        }

        var d = ranged.GunStats;

        // 没变化就只显示当前值，变了才显示「旧 → 新」
        string Show(double before, double after, string unit = "")
            => Mathf.IsEqualApprox((float)before, (float)after)
                ? $"{after:0.##}{unit}"
                : $"{before:0.##} → {after:0.##}{unit}";

        var sb = new StringBuilder();

        sb.Append($"伤害 {d.Damage:0.##}    弹匣 {Show(d.MagazineSize, WeaponMods.MagazineSize(_gun, d))}    ");
        sb.Append($"换弹 {Show(d.ReloadTime, WeaponMods.ReloadTime(_gun, d), "s")}\n");

        sb.Append($"音噪 {Show(d.NoiseRadius, WeaponMods.NoiseRadius(_gun, d))}    ");
        sb.Append($"腰射 {Show(d.HipSpread, WeaponMods.Inaccuracy(_gun, d), "°")}    ");
        sb.Append($"瞄准 {Show(d.AimSpread, WeaponMods.AimInaccuracy(_gun, d), "°")}\n");

        sb.Append($"枪重 {_gun.TotalWeight:0.##} kg    占 {_gun.Data?.GridW ?? 1}×{_gun.Data?.GridH ?? 1} 格");

        _stats.Text = sb.ToString();
    }

    // Esc 由 EquipmentPanel 统一处理（走 PanelStack，保证"先关最新打开的"）
}
