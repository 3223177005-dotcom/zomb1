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

    // ---------- 移植自 ZS 的 weapon.json「mods[slot]」三项 ----------

    /// <summary>
    /// 配件挂点 X（0~1，占枪贴图宽度的比例）。&lt;0 表示**未指定**，用 <see cref="SlotTypes.DefaultMount"/>。
    /// 用比例而不是像素：不同枪的贴图尺寸不同，固定像素一定会错位。
    /// </summary>
    [Export] public float MountX { get; set; } = -1.0f;

    /// <summary>配件挂点 Y（占枪贴图高度的比例）。**允许负数**（负数 = 挂在枪身上方），只有 MountX 当哨兵。</summary>
    [Export] public float MountY { get; set; } = 0.0f;

    /// <summary>
    /// 必装槽（ZS 的 <c>mods[slot].needed</c>）：为真时这个槽不能被拆空。
    /// 用于枪管 / 握把这类"枪必须带着"的部件。
    /// </summary>
    [Export] public bool Needed { get; set; } = false;

    /// <summary>是否在数据里指定了挂点（只看 MountX：Y 允许是负数，不能当哨兵）。</summary>
    public bool HasMount => MountX >= 0.0f;

    /// <summary>挂点比例（数据优先，否则用槽类型的默认值）。</summary>
    public Vector2 MountFraction => HasMount
        ? new Vector2(MountX, MountY)
        : SlotTypes.DefaultMount(SlotType);

    /// <summary>中文名（UI 直接用）。</summary>
    public string DisplayName => SlotTypes.DisplayName(SlotType);

    public override string ToString() => $"槽[{SlotType}×{Capacity}]";
}
