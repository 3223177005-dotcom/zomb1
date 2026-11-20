using Godot;

/// <summary>
/// 装备 / 卸下 的**事务逻辑**（装备栏 + 来源容器 + 目标容器的三方差错都在这里收口）。
///
/// 为什么单独一层：UI（拖放、右键菜单）和 Player 都要用同一套规则，
/// 写在面板回调里会出现"拖上去能装、点一下不能卸"这种分叉。
///
/// 规则（照 ZS 的 loadout）：
///   · 只能装到**接受该类别**的槽（<see cref="EquipmentSlots.Accepts"/>）；
///   · 装上去时从来源扣 1 个，被换下来的旧件放回玩家身上（放不下就装回槽里，绝不让东西凭空消失）；
///   · 卸下时放进目标容器，放不下就**保持装备状态**并返回失败。
/// </summary>
public static class EquipService
{
    /// <summary>这个槽能不能装这件东西（带拒绝原因，UI 可以直接显示）。</summary>
    public static bool CanEquip(EquipmentSlots slots, int slotIndex, ItemStack stack, out string reason)
    {
        if (slots == null || !slots.IsValidSlot(slotIndex))
        {
            reason = "槽位无效";
            return false;
        }
        if (stack?.Data == null || stack.IsEmpty)
        {
            reason = "没有物品";
            return false;
        }
        if (!slots.Accepts(slotIndex, stack))
        {
            reason = EquipRules.RejectReason(slots.KindOf(slotIndex));
            return false;
        }
        reason = "";
        return true;
    }

    /// <summary>
    /// 把 <paramref name="stack"/> 装进槽：从 <paramref name="source"/> 扣 1 个，
    /// 旧件回收到 <paramref name="source"/>（或 <paramref name="fallback"/>）。
    /// 失败时**不改任何状态**。
    /// </summary>
    public static bool TryEquip(EquipmentSlots slots, int slotIndex, ItemStack stack,
        Inventory source, Inventory fallback, out ItemStack replaced, out string reason)
    {
        replaced = null;

        if (!CanEquip(slots, slotIndex, stack, out reason)) return false;
        if (source == null)
        {
            reason = "来源容器为空";
            return false;
        }

        var previous = slots.Get(slotIndex);

        // ★ 装进槽里的必须是**独立的一份**：
        //   从来源扣料会把原实例的 Count 减到 0，而 EquipmentSlots.Equip 会把 Count==0
        //   当成空物品直接丢掉 —— 这正是"拖回去就消失"的原因。
        //
        // ★★ 复制必须走 Clone()，不能 new ItemStack(Data, 1)：
        //   构造函数会 InitInstanceState() 把耐久重置成满值 ——
        //   那就是"脱下来再穿上 = 免费满修"。
        var equipping = stack.Clone();
        equipping.Count = 1;              // 装备槽里永远是一件

        if (!source.Remove(stack.Data.Id, 1))
        {
            reason = "从来源扣除失败";
            return false;
        }

        slots.Equip(slotIndex, equipping);
        replaced = previous;

        // 旧件回收到来源；来源放不下就退回给它自己的 fallback，仍放不下就把旧件装回槽里
        if (previous != null && !ReferenceEquals(previous, stack))
        {
            var put = source.TryInsert(previous, out _);
            if (put != ContainCode.Success && fallback != null && !ReferenceEquals(fallback, source))
                put = fallback.TryInsert(previous, out _);

            if (put != ContainCode.Success)
            {
                // 旧的放不回去 → 撤销这次装备，保持原状
                slots.Unequip(slotIndex);
                slots.Equip(slotIndex, previous);
                var back = source.TryInsert(equipping, out _);
                if (back != ContainCode.Success && fallback != null)
                    fallback.TryInsert(equipping, out _);
                replaced = null;
                reason = "换下来的装备无处安放";
                return false;
            }
        }

        // ★ 装备栏变了 = 身上的东西变了，必须广播给 UI。
        //   漏了这一句的表现：把护甲拖到护甲方块上，护甲**当场装上了**，
        //   但背包格子还画着那一件，要再拖一次别的东西才消失。
        NotifyInventoryChanged();

        reason = "";
        return true;
    }

    /// <summary>卸下槽里的东西并放进 <paramref name="target"/>；放不下则保持装备状态。</summary>
    public static bool TryUnequip(EquipmentSlots slots, int slotIndex, Inventory target, out ItemStack moved, out string reason)
    {
        moved = null;

        if (slots == null || target == null || !slots.IsValidSlot(slotIndex))
        {
            reason = "槽位或目标无效";
            return false;
        }

        var equipped = slots.Get(slotIndex);
        if (equipped == null)
        {
            reason = "槽是空的";
            return false;
        }

        var code = target.TryInsert(equipped, out var stored);
        if (code == ContainCode.NoSpace)
        {
            reason = "背包放不下";
            return false;
        }
        if (code != ContainCode.Success)
        {
            reason = "背包拒收";
            return false;
        }

        slots.Unequip(slotIndex);
        moved = stored ?? equipped;
        NotifyInventoryChanged();
        reason = "";
        return true;
    }

    /// <summary>装备栏变了 → 通知 UI 重建背包格子（与 <c>Player.NotifyInventoryChanged</c> 同一条信号）。</summary>
    private static void NotifyInventoryChanged()
        => EventBus.Instance?.EmitSignal(EventBus.SignalName.InventoryChanged);
}





