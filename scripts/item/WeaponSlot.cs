using Godot;

/// <summary>
/// **运行时的**配件槽：这个槽是什么类型、里面装了什么。
///
/// ★ 和 <see cref="WeaponSlotData"/> 的区别：
/// - `WeaponSlotData` 是**共享数据**（.tres 里定义"这把枪有哪些槽"）；
/// - 本类是**每把枪一份的实例状态**（"这个槽里装了哪个消音器"）。
///
/// 混在一起会让全世界同型号的枪共享同一个配件。
/// </summary>
public sealed class WeaponSlot
{
    public WeaponSlot(string slotType)
    {
        SlotType = slotType;
    }

    /// <summary>从数据层建槽（带上挂点 / 必装标记）。</summary>
    public WeaponSlot(WeaponSlotData data) : this(data?.SlotType ?? SlotTypes.Muzzle)
    {
        Source = data;
    }

    /// <summary>槽类型，取值见 <see cref="SlotTypes"/>。</summary>
    public string SlotType { get; }

    /// <summary>数据层定义（可能为 null：直接按类型字符串建槽时）。</summary>
    public WeaponSlotData Source { get; }

    /// <summary>必装槽：为真时不能被拆空（ZS 的 mods[slot].needed）。</summary>
    public bool Needed => Source?.Needed ?? false;

    /// <summary>挂点比例（数据优先，否则槽类型默认值）。</summary>
    public Vector2 MountFraction => Source?.MountFraction ?? SlotTypes.DefaultMount(SlotType);

    /// <summary>装着的配件（null = 空槽）。</summary>
    public ItemStack Installed { get; set; }

    /// <summary>是不是空的。</summary>
    public bool IsEmpty => Installed == null || Installed.IsEmpty;

    /// <summary>槽类型中文名。</summary>
    public string DisplayName => SlotTypes.DisplayName(SlotType);

    /// <summary>装上 / 卸下时调用（方便 UI 刷新）。</summary>
    public ItemStack Take()
    {
        var item = Installed;
        Installed = null;
        return item;
    }

    public override string ToString()
        => $"{SlotType}=( {(!IsEmpty ? Installed.Data?.Name : "空")} )";
}
