using Godot;
using System.Collections.Generic;

/// <summary>
/// 地面上的**战利品容器**（丧尸尸体、可搜刮点）。
///
/// 和「露天物品」的区别（这是本类存在的理由）：
/// - **露天物品**（<see cref="WorldItems"/> 的 <c>_cells</c>）直接摆在地上，按 F 就捡；
/// - **战利品容器**要**搜**：先读条，读条时长由内容里**最高稀有度**决定，
///   搜完才揭示里面有什么。
///
/// 地图上用**另一种颜色**画（见 <see cref="GroundItemsLayer"/>），一眼能和露天物品区分开。
///
/// ★ 多只丧尸死在同一格 → **合并到同一个容器**（不会互相顶掉）。
/// </summary>
public class LootSpot
{
    public LootSpot(Vector2I cell, Inventory contents)
    {
        Cell = cell;
        Contents = contents;
    }

    /// <summary>所在格。</summary>
    public Vector2I Cell { get; }

    /// <summary>里面的东西（本质就是一个容器）。</summary>
    public Inventory Contents { get; }

    /// <summary>搜完要多久（秒）—— 每次读取都按当前内容重算，合并后自动更新。</summary>
    public float SearchTime => ComputeSearchTime(Contents);

    /// <summary>内容里最高的稀有度（同上，实时计算）。</summary>
    public ItemRarity BestRarity => ComputeBestRarity(Contents);

    /// <summary>是否已经搜过（搜过之后再看就不必重新读条）。</summary>
    public bool Searched { get; set; }

    /// <summary>里面的东西是不是搬空了。</summary>
    public bool IsEmpty => Contents == null || Contents.StackCount == 0;

    /// <summary>
    /// 往这个容器里再塞东西（第二只丧尸倒在同一格时用）。
    /// **会重新要求搜索** —— 有新尸体叠上来，得再摸一遍。
    /// </summary>
    public void AddItems(IEnumerable<ItemStack> items)
    {
        if (Contents == null || items == null) return;

        foreach (var item in items) Contents.Add(item);
        Searched = false;
    }

    /// <summary>
    /// 单件物品对应的搜索时长（秒）。**品质越高越久**。
    ///
    /// 量级参考《逃离塔科夫》：塔科夫是按**容器类型**给总时长（尸体 2~3s、保险箱 6~8s），
    /// 我们只有"尸体"一种容器，做类型分档没意义，所以沿用**按物品稀有度**的结构，
    /// 但把秒数换算到塔科夫的尺度上。
    /// </summary>
    public static float TimeForRarity(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => 1.0f,
        ItemRarity.Uncommon => 1.6f,
        ItemRarity.Rare => 2.4f,
        ItemRarity.Epic => 3.4f,
        ItemRarity.Legendary => 4.6f,
        ItemRarity.Mythic => 6.0f,
        _ => 1.0f,
    };

    /// <summary>整箱的搜索时长 = 里面**最高稀有度**那一件的时长（空箱给个最短时长）。</summary>
    public static float ComputeSearchTime(Inventory contents)
        => TimeForRarity(ComputeBestRarity(contents));

    private static ItemRarity ComputeBestRarity(Inventory contents)
    {
        var best = ItemRarity.Common;
        if (contents == null) return best;

        foreach (var stack in contents.Stacks)
        {
            if (stack?.Data == null) continue;
            if (stack.Data.Rarity > best) best = stack.Data.Rarity;
        }
        return best;
    }

    public override string ToString()
        => $"{BestRarity} 箱  {Contents?.StackCount ?? 0} 叠  {SearchTime:0.0}s";
}
