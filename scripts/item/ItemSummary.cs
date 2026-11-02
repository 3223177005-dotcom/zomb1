/// <summary>
/// 容器内容按**物品类型**汇总后的一行（同一类型的多个堆叠合并成一行）。
/// 背包面板和地面拾取面板共用这一种结构。
///
/// 阶段 2 起不再有「体积」—— 物品在格子背包里的占位由 <see cref="ItemData.GridW"/> × <see cref="ItemData.GridH"/> 表达。
/// </summary>
public readonly struct ItemSummary
{
    public ItemSummary(string itemId, ItemData data, int count, float weight, int stackCount)
    {
        ItemId = itemId;
        Data = data;
        Count = count;
        Weight = weight;
        StackCount = stackCount;
    }

    public string ItemId { get; }
    public ItemData Data { get; }

    /// <summary>该类型的总件数。</summary>
    public int Count { get; }

    /// <summary>该类型的总重量（kg）。</summary>
    public float Weight { get; }

    /// <summary>占了几个堆叠。</summary>
    public int StackCount { get; }

    public override string ToString()
        => $"{Data?.Name} ×{Count}  {Weight:0.00}kg  {Data?.GridW}×{Data?.GridH} 格";
}
