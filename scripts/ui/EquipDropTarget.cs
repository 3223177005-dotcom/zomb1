using Godot;
using System.Collections.Generic;

/// <summary>
/// 装备栏槽位上的**拖放接收层**。
///
/// 为什么要有这个类：装备栏的槽是 `Button`，而拖放判定 `_CanDropData` / `_DropData`
/// 是 `Control` 的**虚方法** —— 不改 .tscn 就没法给现成的 Button 覆写。
/// 所以在每个按钮**上面盖一层透明的 Control**，专门接收拖放；
/// `MouseFilter = Pass` 让左键点击照常穿给按钮（切手持），右键也不受影响。
/// </summary>
public partial class EquipDropTarget : Control
{
    /// <summary>对应的装备槽索引。</summary>
    public int SlotIndex { get; set; } = -1;

    /// <summary>放下时回调（槽索引、物品）。</summary>
    public System.Action<int, ItemStack> Dropped { get; set; }

    public override void _Ready()
    {
        // Pass：自己收事件，但继续往上传给 Button（左键切手持要用）
        MouseFilter = MouseFilterEnum.Pass;
    }

    /// <summary>
    /// **从装备栏拖出去**（拖回背包）。
    ///
    /// 原理：先把它从装备槽**卸下并放进基础口袋**，再走现有的拖拽流程 ——
    /// 这样落点逻辑（并进同类堆叠 / 放进某个口袋）全部白捡，不用新写。
    ///
    /// 如果基础口袋放不下（或超重），就**取消拖拽并把物品装回原槽**。
    /// </summary>
    public override Variant _GetDragData(Vector2 atPosition)
    {
        var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
        if (player == null) return default;

        var equipped = player.Equipment.Unequip(SlotIndex);
        if (equipped == null) return default;

        // 拖出来的可能正是手上那把 → 先重算手上状态，别让枪还挂在手上
        player.RefreshActiveWeapon();

        var code = player.Pocket.TryInsert(equipped, out var stored);
        if (code != ContainCode.Success)
        {
            // 放不下 → 还原，别让东西凭空消失
            player.Equipment.Equip(SlotIndex, equipped);
            player.RefreshActiveWeapon();
            return default;
        }

        var dragging = stored ?? equipped;
        ItemDrag.Begin(player.Pocket, new List<ItemStack> { dragging });

        // 跟着鼠标走的预览
        var preview = new Label { Text = $"{dragging.Data.Name} ×{dragging.Count}" };
        preview.AddThemeFontSizeOverride("font_size", 18);
        preview.AddThemeColorOverride("font_color", UiStyle.ItemText(dragging.Data));
        SetDragPreview(preview);

        return true;   // 非空 = 开始拖拽（真载荷在 ItemDrag 里）
    }

    /// <summary>只接**武器和容器** —— 弹药、绷带不该装进装备栏。</summary>
    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (!ItemDrag.Active || ItemDrag.Items.Count != 1) return false;

        var item = ItemDrag.Items[0];
        if (item?.Data == null) return false;

        return item.Data.WeaponStats != null || item.Data.IsContainer;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (Dropped == null || ItemDrag.Items.Count != 1) return;
        Dropped(SlotIndex, ItemDrag.Items[0]);
    }
}
