using Godot;
using System.Collections.Generic;

/// <summary>
/// 地面上的物品 —— 按**格子**存放，每格一个「地面容器」。
///
/// 对应 CDDA <c>submap::itm[x][y]</c>（每格一个 <c>colony&lt;item&gt;</c>）：
/// **地面上没有全局物品列表**，物品就存在它所在的那一格里。
///
/// 每格的容量用一个 <see cref="Inventory"/> 表示 —— 地面格子本身就是一个**格子口袋**
/// （CDDA 里这个上限来自地形 / 家具的 <c>max_volume</c>，我们暂时用统一的 10×10 格）。
///
/// 纯 C# 类，不含渲染；绘制由场景节点负责。
/// </summary>
public class WorldItems
{
    /// <summary>邻近格子的偏移，**由近到远**（先四正，再四斜）。溢出时按这个顺序找。</summary>
    private static readonly Vector2I[] Neighbors =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
    };

    private readonly Dictionary<Vector2I, Inventory> _cells = new();

    public WorldItems(MapGrid grid)
    {
        Grid = grid;
    }

    /// <summary>统一的地图格子（与寻路共用同一套）。</summary>
    public MapGrid Grid { get; }

    /// <summary>单格地面的**格子数**（列 × 行）。将来可按地形 / 家具分别给定。</summary>
    public int CellGridW { get; set; } = 10;
    public int CellGridH { get; set; } = 10;

    /// <summary>战利品容器（尸体 / 箱子）的内部格子。8×5 够放几件战利品，UI 上也刚好一屏。</summary>
    private const int LootGridW = 6;
    private const int LootGridH = 5;

    /// <summary>有多少格地上有东西。</summary>
    public int OccupiedCellCount => _cells.Count;

    /// <summary>当前有物品的格子（供渲染遍历）。</summary>
    public IEnumerable<Vector2I> OccupiedCells => _cells.Keys;

    /// <summary>地上物品的总堆叠数（所有格子合计）。</summary>
    public int TotalStackCount
    {
        get
        {
            int n = 0;
            foreach (var inv in _cells.Values) n += inv.StackCount;
            return n;
        }
    }

    // ---------- 取格子 ----------

    /// <summary>取某格的容器；该格没东西则返回 null（不创建）。</summary>
    public Inventory GetCell(Vector2I cell)
        => _cells.TryGetValue(cell, out var inv) ? inv : null;

    /// <summary>取某格的容器；没有就创建。</summary>
    public Inventory GetOrCreateCell(Vector2I cell)
    {
        if (_cells.TryGetValue(cell, out var inv)) return inv;

        inv = new Inventory(float.MaxValue, CellGridW, CellGridH);
        _cells[cell] = inv;
        return inv;
    }

    /// <summary>世界坐标那一格的容器（可能为 null）。</summary>
    public Inventory GetCellAt(Vector2 world) => GetCell(Grid.WorldToCell(world));

    // ---------- 丢 / 查 / 清 ----------

    /// <summary>
    /// 把一叠物品丢到地上。
    ///
    /// 本格放不下的部分会**溢出到邻近格子**（对应 CDDA <c>add_item_or_charges</c> 的 overflow，
    /// 简化为半径 1、由近到远；CDDA 还要求目标格「有路可达」，我们暂时不做）。
    ///
    /// 返回**没放下的部分**（null 表示全部落地）。
    /// </summary>
    public ItemStack Drop(Vector2 world, ItemStack stack)
    {
        if (stack?.Data == null || stack.IsEmpty) return stack;

        var origin = Grid.WorldToCell(world);

        // ① 先放本格
        var leftover = Grid.Contains(origin) ? GetOrCreateCell(origin).Add(stack) : stack;
        if (leftover == null) return null;

        // ② 溢出去邻近格子
        foreach (var offset in Neighbors)
        {
            if (leftover == null) break;

            var cell = origin + offset;
            if (!Grid.Contains(cell)) continue;

            leftover = GetOrCreateCell(cell).Add(leftover);
        }

        PruneEmpty();
        return leftover;
    }

    /// <summary>直接丢进指定格子（不溢出）；返回没放下的部分。</summary>
    public ItemStack DropInCell(Vector2I cell, ItemStack stack)
    {
        if (stack?.Data == null || stack.IsEmpty) return stack;
        if (!Grid.Contains(cell)) return stack;

        var leftover = GetOrCreateCell(cell).Add(stack);
        PruneEmpty();
        return leftover;
    }

    /// <summary>某格地上的第 index 个堆叠；没有则 null。</summary>
    public ItemStack Peek(Vector2I cell, int index = 0)
    {
        var inv = GetCell(cell);
        if (inv == null || index < 0 || index >= inv.StackCount) return null;
        return inv.Stacks[index];
    }

    /// <summary>清空某格。</summary>
    public void ClearCell(Vector2I cell)
    {
        if (_cells.TryGetValue(cell, out var inv)) inv.Clear();
        _cells.Remove(cell);
    }

    /// <summary>清空全部地面物品（含战利品容器）。</summary>
    public void ClearAll()
    {
        _cells.Clear();
        _lootSpots.Clear();
    }

    // ---------- 战利品容器（尸体 / 可搜刮点） ----------
    //
    // 与「露天物品」（_cells）**分开存放**：露天物品按 F 就捡，
    // 战利品容器要先读条搜。地图上用不同颜色画。

    private readonly Dictionary<Vector2I, LootSpot> _lootSpots = new();

    /// <summary>地面上有几个战利品容器。</summary>
    public int LootSpotCount => _lootSpots.Count;

    /// <summary>所有战利品容器所在的格子。</summary>
    public IEnumerable<Vector2I> LootSpotCells => _lootSpots.Keys;

    /// <summary>
    /// 生成一个战利品容器（把 <paramref name="items"/> 装进去）。
    ///
    /// ★ 该格**已经有**战利品容器时**合并进去**，不覆盖 ——
    /// 否则两只丧尸死在同一格，后一只会把前一只的战利品顶掉。
    /// </summary>
    public LootSpot SpawnLootSpot(Vector2I cell, IEnumerable<ItemStack> items)
    {
        if (_lootSpots.TryGetValue(cell, out var existing))
        {
            existing.AddItems(items);   // 合并 + 重新要求搜索
            return existing;
        }

        // 尸体当成一个很大的口袋，塞进去多少都行
        var contents = new Inventory(float.MaxValue, LootGridW, LootGridH);

        if (items != null)
            foreach (var item in items)
                contents.Add(item);

        var spot = new LootSpot(cell, contents);
        _lootSpots[cell] = spot;
        return spot;
    }

    /// <summary>取某格的战利品容器（没有返回 null）。</summary>
    public LootSpot GetLootSpot(Vector2I cell)
        => _lootSpots.TryGetValue(cell, out var spot) ? spot : null;

    /// <summary>移除某格的战利品容器（搜空之后清掉）。</summary>
    public void RemoveLootSpot(Vector2I cell) => _lootSpots.Remove(cell);

    // ---------- 就近查询 ----------

    /// <summary>
    /// 收集世界点 <paramref name="center"/> 半径内的所有地面堆叠。
    ///
    /// 物品位置取**所在格的中心**（按格存放的必然结果）。
    /// 返回结果**不排序**，排序策略交给调用方（例如"朝向优先"）。
    /// </summary>
    public List<GroundStack> CollectNear(Vector2 center, float radius)
    {
        var result = new List<GroundStack>();
        if (radius <= 0.0f) return result;

        var centerCell = Grid.WorldToCell(center);
        int cellRadius = Mathf.CeilToInt(radius / Grid.CellSize);

        for (int dx = -cellRadius; dx <= cellRadius; dx++)
        {
            for (int dy = -cellRadius; dy <= cellRadius; dy++)
            {
                var cell = new Vector2I(centerCell.X + dx, centerCell.Y + dy);

                var inv = GetCell(cell);
                if (inv == null) continue;

                var pos = Grid.CellCenter(cell);
                float dist = pos.DistanceTo(center);
                if (dist > radius) continue;

                foreach (var stack in inv.Stacks)
                    result.Add(new GroundStack(cell, pos, stack, dist));
            }
        }
        return result;
    }

    /// <summary>移除已经空掉的格子容器，保持字典干净。</summary>
    public void PruneEmpty()
    {
        List<Vector2I> empty = null;
        foreach (var pair in _cells)
        {
            if (pair.Value.IsEmpty) (empty ??= new List<Vector2I>()).Add(pair.Key);
        }
        if (empty == null) return;

        foreach (var cell in empty) _cells.Remove(cell);
    }

    // ---------- 汇总（给 UI 用） ----------

    /// <summary>
    /// 把一格里的东西**按物品类型**汇总（同一类型的多个堆叠合并成一行）。
    /// 实际逻辑在 <see cref="Inventory.Summarize"/> —— 地面和背包共用同一套。
    /// </summary>
    public List<ItemSummary> SummarizeCell(Vector2I cell)
        => GetCell(cell)?.Summarize() ?? new List<ItemSummary>();
}

/// <summary>
/// 地面上的一叠物品 —— 一次"就近查询"的结果。
///
/// 位置取**所在格的中心**（因为物品是按格存放的）。
/// 默认值（<see cref="IsValid"/> 为 false）表示"没有目标"。
/// </summary>
public readonly struct GroundStack
{
    public GroundStack(Vector2I cell, Vector2 position, ItemStack stack, float distance)
    {
        Cell = cell;
        Position = position;
        Stack = stack;
        Distance = distance;
    }

    /// <summary>所在格子。</summary>
    public Vector2I Cell { get; }

    /// <summary>世界坐标（格子中心）。</summary>
    public Vector2 Position { get; }

    /// <summary>那一叠物品。</summary>
    public ItemStack Stack { get; }

    /// <summary>到查询中心的距离（像素）。</summary>
    public float Distance { get; }

    public bool IsValid => Stack != null;

    public override string ToString()
        => IsValid ? $"{Stack} @ {Cell} ({Distance:0.#}px)" : "(无目标)";
}

