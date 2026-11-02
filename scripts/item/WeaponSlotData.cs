using Godot;

/// <summary>
/// **武器数据层**的一个槽位定义：这个槽是什么类型、能装几个。
///
/// ★ 注意：**这里不存"装了什么"** —— `WeaponData` 是全局共享的 Resource，
/// 存了装配状态会让全世界同一型号的枪共享同一个配件。
/// "装了什么"在运行时的 <c>WeaponSlot</c>（普通 C# 类）里。
/// </summary>
[GlobalClass]
public partial class WeaponSlotData : Resource
{
    /// <summary>槽类型，必须是 <see cref="SlotTypes"/> 里的 8 种之一。</summary>
    [Export] public string SlotType { get; set; } = SlotTypes.Muzzle;

    /// <summary>容量（能装几个）。**当前一律 1**，留着以后做双导轨/双弹匣。</summary>
    [Export] public int Capacity { get; set; } = 1;

    /// <summary>数据是否合法。</summary>
    public bool IsValid => SlotTypes.IsValid(SlotType) && Capacity > 0;

    /// <summary>中文名（UI 直接用）。</summary>
    public string DisplayName => SlotTypes.DisplayName(SlotType);

    public override string ToString() => $"槽[{SlotType}×{Capacity}]";
}
