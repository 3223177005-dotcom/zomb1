using System.Collections.Generic;

/// <summary>
/// 地面容器的**树视图**：把一格里的东西（含嵌套容器内部）摊平成带层级的列表。
///
/// 规则：
/// - 同一层里，**非容器**物品按类型合并（3 个绷带 = 一行「绷带 ×3」）；
/// - **容器**各自单独成行 —— 否则两个背包的内容物会串在一起；
/// - 容器行后面紧跟着它的内容物，缩进一层（<see cref="Depth"/> 递增）。
/// </summary>
public static class GroundTree
{
    /// <summary>把一格地面内容摊平成树（深度优先）。</summary>
    public static List<GroundNode> Flatten(Inventory cell)
    {
        var result = new List<GroundNode>();
        if (cell != null) Walk(cell, 0, result);
        return result;
    }

    private static void Walk(Inventory pocket, int depth, List<GroundNode> outList)
    {
        var order = new List<string>();
        var merged = new Dictionary<string, GroundNode>();
        var containers = new List<ItemStack>();

        foreach (var stack in pocket.Stacks)
        {
            if (stack?.Data == null) continue;

            if (stack.IsContainer)
            {
                containers.Add(stack);       // 容器不合并，各自单独一行
                continue;
            }

            string id = stack.Data.Id;
            if (!merged.TryGetValue(id, out var node))
            {
                order.Add(id);
                node = new GroundNode(pocket, stack.Data, depth, false);
                merged[id] = node;
            }
            node.AddStack(stack);
        }

        // 先出散装物品（按首次出现顺序），再出容器（各自带子树）
        foreach (var id in order) outList.Add(merged[id]);

        foreach (var container in containers)
        {
            var node = new GroundNode(pocket, container.Data, depth, true);
            node.AddStack(container);
            outList.Add(node);

            // 容器的内容物缩进一层 —— 注意这里走的是容器的 Contents，不是当前 pocket
            if (container.Contents != null)
                Walk(container.Contents, depth + 1, outList);
        }
    }
}

/// <summary>
/// 物品树里的一行。
///
/// <see cref="Parent"/> 是这些堆叠**所在的容器**（地面格，或某个容器的 Contents），
/// 捡走时直接从它里面移除，所以不需要额外记录"路径"。
/// </summary>
public sealed class GroundNode
{
    private readonly List<ItemStack> _stacks = new();

    public GroundNode(Inventory parent, ItemData data, int depth, bool isContainer)
    {
        Parent = parent;
        Data = data;
        Depth = depth;
        IsContainer = isContainer;
    }

    /// <summary>这些堆叠所在的容器。</summary>
    public Inventory Parent { get; }

    public ItemData Data { get; }

    /// <summary>嵌套层级：0 = 直接在地上，1 = 在某容器里……</summary>
    public int Depth { get; }

    /// <summary>这一行是不是容器（可以进去看里面的东西）。</summary>
    public bool IsContainer { get; }

    /// <summary>这一行涉及的堆叠（同层同类型的若干个）。</summary>
    public IReadOnlyList<ItemStack> Stacks => _stacks;

    /// <summary>总件数。</summary>
    public int Count { get; private set; }

    /// <summary>总重量（kg，含容器内容物）。</summary>
    public float Weight { get; private set; }

    public void AddStack(ItemStack stack)
    {
        if (stack?.Data == null) return;

        _stacks.Add(stack);
        Count += stack.Count;
        Weight += stack.TotalWeight;
    }

    public override string ToString()
        => $"{new string(' ', Depth * 2)}{(IsContainer ? "▾ " : "")}{Data?.Name} ×{Count}  {Weight:0.00}kg  {Data?.GridW}×{Data?.GridH} 格";
}
