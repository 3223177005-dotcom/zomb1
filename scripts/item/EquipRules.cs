using Godot;

/// <summary>
/// 装备槽的**接受规则**（纯数据判断，不碰节点、不查 player）。
///
/// 单独抽出来是为了让 UI（拖放预览）和逻辑层（<see cref="EquipService"/>）用同一份规则：
/// 规则写在 UI 里就会出现"预览能放、松手不能放"这种分叉。
/// </summary>
public static class EquipRules
{
    /// <summary>这个类型的槽能不能放这件东西（照 ZS 的 loadout 类别限制）。</summary>
    public static bool Accepts(EquipSlotKind kind, ItemStack stack)
    {
        if (stack?.Data == null || stack.IsEmpty) return false;

        return kind switch
        {
            EquipSlotKind.Weapon => stack.Data.WeaponStats != null,
            EquipSlotKind.Container => stack.Data.IsContainer,
            // 护甲位收"非头盔"的护甲；头盔位只收 IsHelmet 的（照 ZS 的部位划分）
            EquipSlotKind.Armor => stack.Data.IsUsableArmor && !stack.Data.ArmorIsHelmet,
            EquipSlotKind.Helmet => stack.Data.IsUsableArmor && stack.Data.ArmorIsHelmet,
            EquipSlotKind.Mask => false,
            _ => false,
        };
    }

    /// <summary>拒收时的说明（UI 直接显示）。</summary>
    public static string RejectReason(EquipSlotKind kind) => kind switch
    {
        EquipSlotKind.Weapon => "武器位只能放武器",
        EquipSlotKind.Container => "背包位只能放容器",
        EquipSlotKind.Armor => "护甲位只能放身体护甲",
        EquipSlotKind.Helmet => "头盔位只能放头盔",
        EquipSlotKind.Mask => "面罩位还没有可用物品",
        _ => "这个位置不能放东西",
    };
}


