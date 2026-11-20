using Godot;

/// <summary>
/// 装备槽的**类型**（照 ZS 的 loadout：两个武器位 + 背包位；护甲/头盔/面罩留位待物品到位）。
///
/// 为什么要有类型：ZS 里每个位置只接受对应类别的东西（武器位不能放背包）。
/// 规则集中在 <see cref="EquipmentSlots.Accepts"/>，不要散在各个 UI 回调里。
/// </summary>
public enum EquipSlotKind
{
    /// <summary>武器位（远程 / 近战都行，行为由物品自身的 WeaponData.Kind 决定）。</summary>
    Weapon,

    /// <summary>背包位（容器）。</summary>
    Container,

    /// <summary>护甲位（等 items.json 出现护甲类别后启用）。</summary>
    Armor,

    /// <summary>头盔位（同上）。</summary>
    Helmet,

    /// <summary>面罩位（同上）。</summary>
    Mask,
}

/// <summary>
/// 玩家的装备栏：**只管存放**（装上 / 卸下 / 读取），槽位类型规则见 <see cref="EquipSlotKind"/>。
/// 纯 C# 类（和 ItemStack / Inventory 一致），不是 Godot 对象。
/// </summary>
public class EquipmentSlots
{
    private readonly ItemStack[] _slots;
    private readonly EquipSlotKind[] _kinds;

    public EquipmentSlots(params EquipSlotKind[] kinds)
    {
        _kinds = kinds is { Length: > 0 }
            ? kinds
            : new[] { EquipSlotKind.Weapon, EquipSlotKind.Weapon, EquipSlotKind.Container };
        _slots = new ItemStack[_kinds.Length];
    }

    /// <summary>槽位数量。</summary>
    public int SlotCount => _kinds.Length;

    /// <summary>索引是否有效。</summary>
    public bool IsValidSlot(int slotIndex) => slotIndex >= 0 && slotIndex < SlotCount;

    /// <summary>槽位类型（越界返回 Weapon）。</summary>
    public EquipSlotKind KindOf(int slotIndex)
        => IsValidSlot(slotIndex) ? _kinds[slotIndex] : EquipSlotKind.Weapon;

    /// <summary>取槽位里的物品；空槽或索引越界返回 null。</summary>
    public ItemStack Get(int slotIndex) => IsValidSlot(slotIndex) ? _slots[slotIndex] : null;

    public bool IsEmpty(int slotIndex) => Get(slotIndex) == null;

    /// <summary>这个槽能不能放这件东西（规则见 <see cref="EquipRules"/>）。</summary>
    public bool Accepts(int slotIndex, ItemStack stack)
        => IsValidSlot(slotIndex) && EquipRules.Accepts(KindOf(slotIndex), stack);

    /// <summary>装备物品，返回**被替换下来的旧物品**（原来为空则返回 null）。</summary>
    public ItemStack Equip(int slotIndex, ItemStack stack)
    {
        if (!IsValidSlot(slotIndex)) return null;

        var previous = _slots[slotIndex];
        _slots[slotIndex] = stack != null && !stack.IsEmpty ? stack : null;
        return previous;
    }

    /// <summary>卸下并返回槽里的物品（空槽返回 null）。</summary>
    public ItemStack Unequip(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return null;

        var previous = _slots[slotIndex];
        _slots[slotIndex] = null;
        return previous;
    }

    public void Clear()
    {
        for (int i = 0; i < _slots.Length; i++)
            _slots[i] = null;
    }

    /// <summary>已装备的槽位数量。</summary>
    public int EquippedCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null) n++;
            return n;
        }
    }

    /// <summary>第一个非空槽的索引；全空返回 -1。</summary>
    public int FirstOccupiedSlot
    {
        get
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null) return i;
            return -1;
        }
    }
}

