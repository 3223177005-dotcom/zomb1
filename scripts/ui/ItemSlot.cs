using Godot;
using System.Collections.Generic;

/// <summary>
/// 物品列表里的一行，支持**拖放换口袋**：
///
/// - 从**物品行**拖出 → 载荷 = 这一行代表的那组堆叠；
/// - 拖到**任何一行**上 → 放进那一行所属的口袋（<see cref="Pocket"/>）。
///
/// 「口袋的小标题行」也是一个 ItemSlot（只是 <see cref="Items"/> 为空，拖不出去），
/// 这样整个口袋区域都能当放置目标。
/// </summary>
public partial class ItemSlot : PanelContainer
{
    /// <summary>这一行所属的口袋（拖放的落点）。</summary>
    public Inventory Pocket { get; set; }

    /// <summary>这一行代表的那组堆叠；空表示这行不可拖出（标题/说明行）。</summary>
    public List<ItemStack> Items { get; set; }

    /// <summary>搬运成功后回调（用来刷新面板）。</summary>
    public System.Action Changed { get; set; }

    /// <summary>右键请求菜单（参数：本行、鼠标位置）。只有物品行会触发。</summary>
    public System.Action<ItemSlot, Vector2> ContextRequested { get; set; }

    /// <summary>
    /// **单击**这一行。只有没设 <see cref="Activated"/> 时才触发，
    /// 避免和"双击快速拿走"打架。
    /// </summary>
    public System.Action<ItemSlot> Clicked { get; set; }

    /// <summary>**双击**这一行（用于"快速拿走"）。</summary>
    public System.Action<ItemSlot> Activated { get; set; }

    /// <summary>改装面板用：这一行代表**某个配件槽**（把配件拖上来就装进这个槽）。</summary>
    public WeaponSlot WeaponTarget { get; set; }

    /// <summary>改装面板用：这个槽属于哪把枪。</summary>
    public ItemStack WeaponHost { get; set; }

    /// <summary>
    /// 装备栏的槽位索引（&gt;= 0 表示这一行代表**装备栏**里的东西，不在任何口袋里）。
    /// 右键装备槽时用它造一个"代理行"，复用同一套菜单。
    /// </summary>
    public int EquipSlotIndex { get; set; } = -1;

    /// <summary>
    /// 操作反馈文字：<c>null</c> = 恢复正常提示，非空 = 显示这条原因。
    /// 拖拽时鼠标划过**放不下**的目标会触发，用来告诉玩家"为什么装不上"。
    /// </summary>
    public System.Action<string> Message { get; set; }

    private StyleBoxFlat _normal;
    private StyleBoxFlat _highlight;
    private bool _highlighted;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;   // 要收鼠标事件才能拖放

        _normal = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };

        _highlight = new StyleBoxFlat
        {
            BgColor = new Color(0.35f, 0.62f, 0.95f, 0.22f),
            BorderColor = new Color(0.45f, 0.72f, 1.0f, 0.90f),
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
        };

        AddThemeStyleboxOverride("panel", _normal);
    }

    // ---------- 拖出 ----------

    public override Variant _GetDragData(Vector2 atPosition)
    {
        // ★ 配件槽：**先卸下**（带负重/格子校验）再走普通拖拽
        if (WeaponTarget != null && WeaponHost != null)
        {
            if (WeaponTarget.IsEmpty) return default;

            var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
            if (player == null) return default;

            var removed = WeaponMods.TryUninstall(WeaponTarget);
            if (removed == null) return default;

            var code = player.TryTake(removed, out var stored);
            if (code != ContainCode.Success)
            {
                WeaponTarget.Installed = removed;                // 放不下 → 装回去
                Message?.Invoke($"卸不下来：负重已满或没有空格子（{removed.Data?.Name}）");
                return default;
            }

            var dragging = stored ?? removed;
            var source = FindHolder(player, dragging);
            if (source == null)
            {
                WeaponTarget.Installed = removed;
                return default;
            }

            ItemDrag.Begin(source, new List<ItemStack> { dragging });
            ShowDragPreview(dragging);
            Message?.Invoke(null);
            return true;
        }

        if (Pocket == null || Items == null || Items.Count == 0) return default;

        ItemDrag.Begin(Pocket, Items);

        // 跟着鼠标走的预览标签
        var first = Items[0];
        var preview = new Label { Text = $"{first.Data.Name} ×{ItemDrag.Count}" };
        preview.AddThemeFontSizeOverride("font_size", 18);
        preview.AddThemeColorOverride("font_color", UiStyle.ItemText(first.Data));
        SetDragPreview(preview);

        return true;   // 非空 = 开始拖拽（真载荷在 ItemDrag 里）
    }

    /// <summary>拖拽时跟鼠标的小标签。</summary>
    private void ShowDragPreview(ItemStack stack)
    {
        var preview = new Label { Text = $"{stack.Data.Name} ×{stack.Count}" };
        preview.AddThemeFontSizeOverride("font_size", 18);
        preview.AddThemeColorOverride("font_color", UiStyle.ItemText(stack.Data));
        SetDragPreview(preview);
    }

    /// <summary>这件东西现在在玩家的哪个口袋里（找不到返回 null）。</summary>
    private static Inventory FindHolder(Player player, ItemStack item)
    {
        if (player?.Pocket != null)
            foreach (var s in player.Pocket.Stacks)
                if (ReferenceEquals(s, item)) return player.Pocket;

        for (int i = 0; i < player.Equipment.SlotCount; i++)
        {
            var pocket = player.Equipment.Get(i)?.Contents;
            if (pocket == null) continue;

            foreach (var s in pocket.Stacks)
                if (ReferenceEquals(s, item)) return pocket;
        }
        return null;
    }

    // ---------- 右键菜单 ----------

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mb) return;
        if (!mb.Pressed) return;
        if (Items == null || Items.Count == 0) return;   // 标题行没有菜单 / 双击

        // 左键双击 = 快速拿走
        if (mb.ButtonIndex == MouseButton.Left && mb.DoubleClick)
        {
            Activated?.Invoke(this);
            AcceptEvent();
            return;
        }

        // 左键单击（只有没接双击行为时才用 —— 例如地面拾取行）
        if (mb.ButtonIndex == MouseButton.Left && !mb.DoubleClick && Activated == null && Clicked != null)
        {
            Clicked.Invoke(this);
            AcceptEvent();
            return;
        }

        // 右键 = 菜单
        if (mb.ButtonIndex == MouseButton.Right)
        {
            ContextRequested?.Invoke(this, mb.GlobalPosition);
            AcceptEvent();
        }
    }

    // ---------- 放下 ----------

    /// <summary>本行代表的那一叠（标题行没有）。</summary>
    private ItemStack OwnStack => Items != null && Items.Count > 0 ? Items[0] : null;

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        bool ok = CanAcceptDrop();
        SetHighlight(ok);       // 能放就高亮，给玩家反馈

        // ★ 拖拽中划过放不下的目标 → 告诉玩家原因
        if (ItemDrag.Active) Message?.Invoke(ok ? null : ExplainReject());

        return ok;
    }

    /// <summary>拖到这里放不下时，给一句人话。</summary>
    private string ExplainReject()
    {
        if (WeaponTarget != null)
        {
            if (ItemDrag.Items.Count != 1) return "一次只能装一个配件";

            var attachment = ItemDrag.Items[0]?.Data?.AttachmentStats;
            if (attachment == null) return $"{ItemDrag.Items[0]?.Data?.Name} 不是配件";
            if (!WeaponTarget.IsEmpty) return $"{WeaponTarget.DisplayName}槽已经装了东西";
            if (attachment.SlotType != WeaponTarget.SlotType)
                return $"{WeaponTarget.DisplayName}槽只收「{SlotTypes.DisplayName(WeaponTarget.SlotType)}」类配件";

            return "装不上";
        }

        if (IsWeaponHost) return "不是配件，装不到枪上";

        return "放不进去（负重已满或没有空格子）";
    }

    /// <summary>
    /// 两种落点，优先级从高到低：
    /// ① **并进本行那一叠** —— 同类型而且还有空位（"拆开的拖回去合并"走这条，同口袋内也允许）；
    /// ② **放进本行所属的口袋** —— 开一个新堆叠。
    /// </summary>
    /// <summary>这一行是不是一把**有改装槽的枪**（拖配件上去 = 装机）。</summary>
    private bool IsWeaponHost => OwnStack?.Data?.WeaponStats != null;

    private bool CanAcceptDrop()
    {
        // ⓪ 落在"配件槽"上 → 只接**类型匹配且槽是空的**那一件
        if (WeaponTarget != null && WeaponHost != null)
        {
            if (ItemDrag.Items.Count != 1) return false;

            var attachment = ItemDrag.Items[0]?.Data?.AttachmentStats;
            if (attachment == null || !WeaponTarget.IsEmpty) return false;
            if (attachment.SlotType != WeaponTarget.SlotType) return false;

            return WeaponMods.CanInstall(WeaponHost, ItemDrag.Items[0]).Ok;
        }

        // ① 落在"枪"上 → 装配件（拖一件配件到枪那一行）
        if (IsWeaponHost && ItemDrag.Items.Count == 1)
            return WeaponMods.CanInstall(OwnStack, ItemDrag.Items[0]).Ok;

        var own = OwnStack;
        if (own != null && ItemDrag.CanMergeInto(own)) return true;

        return ItemDrag.CanDrop(Pocket);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        SetHighlight(false);

        // ⓪ 落在"配件槽"上 → 装进这个槽
        if (WeaponTarget != null && WeaponHost != null && ItemDrag.Items.Count == 1)
        {
            var single = ItemDrag.Items[0];
            var result = WeaponMods.TryInstall(WeaponHost, single, out _);

            if (result.Ok)
            {
                ItemDrag.TakeFromSource(single, 1);
                Changed?.Invoke();
            }
            return;
        }

        // ① 落在"枪"上 → 装配件
        if (IsWeaponHost && ItemDrag.Items.Count == 1)
        {
            var payload = ItemDrag.Items[0];
            var check = WeaponMods.TryInstall(OwnStack, payload, out _);

            if (check.Ok)
            {
                ItemDrag.TakeFromSource(payload, 1);
                Changed?.Invoke();
                return;
            }
        }

        var own = OwnStack;

        // ① 先试并叠
        int moved = own != null ? ItemDrag.TryMergeInto(own) : 0;

        // ② 并不了（类型不同 / 已满 / 落在标题行）就当成"放进这个口袋"
        if (moved == 0) moved = ItemDrag.TryMove(Pocket);

        ItemDrag.End();

        if (moved > 0) Changed?.Invoke();
    }

    public override void _Notification(int what)
    {
        // 鼠标移开就取消高亮（拖到别处放下时也一样）
        if (what == (int)NotificationMouseExit) SetHighlight(false);
    }

    private void SetHighlight(bool on)
    {
        if (_highlighted == on || _normal == null) return;

        _highlighted = on;
        AddThemeStyleboxOverride("panel", on ? _highlight : _normal);
    }
}
