using Godot;

/// <summary>
/// 玩家的装备栏：若干**同质**武器槽。
///
/// 关键约定：槽位**不区分类型** —— 远程武器和近战武器占用的是同一种槽，
/// 哪把武器放哪个槽完全由玩家决定。
/// 武器行为（远程 / 近战）由**物品自身**的 WeaponData.Kind 决定，与槽位编号无关。
///
/// 职责边界：只做「存放」——装备 / 卸下 / 读取。
/// 纯 C# 类（和 ItemStack / Inventory 一致），不是 Godot 对象。
/// </summary>
public class EquipmentSlots
{
    private readonly ItemStack[] _slots;

    public EquipmentSlots(int slotCount)
    {
        SlotCount = Mathf.Max(1, slotCount);
        _slots = new ItemStack[SlotCount];
    }

    /// <summary>槽位数量。</summary>
    public int SlotCount { get; }

    /// <summary>索引是否有效。</summary>
    public bool IsValidSlot(int slotIndex) => slotIndex >= 0 && slotIndex < SlotCount;

    /// <summary>取槽位里的物品；空槽或索引越界返回 null。</summary>
    public ItemStack Get(int slotIndex) => IsValidSlot(slotIndex) ? _slots[slotIndex] : null;

    public bool IsEmpty(int slotIndex) => Get(slotIndex) == null;

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
