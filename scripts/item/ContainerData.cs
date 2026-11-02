using Godot;

/// <summary>
/// 容器属性：挂在「能装东西」的物品上（<see cref="ItemCategory.Container"/> 类，背包属于这一类），
/// 由 ItemData.CategoryData 引用。
///
/// 约定：**每个容器只有一个仓**（一个 pocket），仓的大小是**二维格子**。
/// 容器实例里实际装了什么、摆在哪，存在 <see cref="ItemStack.Contents"/>（一个 <see cref="Inventory"/>）。
///
/// 纯数据资源，不含逻辑。
/// </summary>
[GlobalClass]
public partial class ContainerData : Resource
{
    /// <summary>内部有几列。</summary>
    [Export] public int GridW { get; set; } = 5;

    /// <summary>内部有几行。</summary>
    [Export] public int GridH { get; set; } = 5;

    // 注意：这里**没有**容器自身的承重上限。
    // 重量上限是玩家全局的一份（Player.MaxCarryWeight），装备容器不会提高它。

    /// <summary>内部总格数。</summary>
    public int CellCount => Mathf.Max(1, GridW) * Mathf.Max(1, GridH);

    public override string ToString() => $"容器 {GridW}×{GridH}（{CellCount} 格）";
}
