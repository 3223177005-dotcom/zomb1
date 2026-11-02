using Godot;
using System.Collections.Generic;

/// <summary>
/// **格子容器**：二维占位表 + 首次适配排布。
///
/// 这是格子背包改造的核心。和旧的 <see cref="Inventory"/>（文字列表）的区别：
/// - **占位是二维的**：一件 4×2 的枪会同时占用 8 个格子
/// - **位置有意义**：`ItemStack.GridX/GridY` 记录左上角，`Rotated` 记录是否转了 90°
/// - **没有体积概念**：能放下就是能放，占格数天然限制总量
///
/// 内部 `_cells[x, y]` 存的是 <see cref="ItemStack"/> 的**引用** ——
/// 同一件 4×2 的物品会在 8 个格子里存同一个引用。
/// </summary>
public class GridInventory
{
    private readonly ItemStack[,] _cells;

    public GridInventory(int w, int h)
    {
        W = Mathf.Max(1, w);
        H = Mathf.Max(1, h);
        _cells = new ItemStack[W, H];
    }

    /// <summary>列数。</summary>
    public int W { get; }

    /// <summary>行数。</summary>
    public int H { get; }

    /// <summary>总格数。</summary>
    public int CellCount => W * H;

    /// <summary>已占格数。</summary>
    public int UsedCells
    {
        get
        {
            int n = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (_cells[x, y] != null) n++;
            return n;
        }
    }

    /// <summary>空格数。</summary>
    public int FreeCells => CellCount - UsedCells;

    /// <summary>容器里所有物品（去重）。</summary>
    public List<ItemStack> Items
    {
        get
        {
            var seen = new HashSet<ItemStack>();
            var list = new List<ItemStack>();

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var s = _cells[x, y];
                    if (s != null && seen.Add(s)) list.Add(s);
                }
            return list;
        }
    }

    /// <summary>这一格被谁占了（空格返回 null）。</summary>
    public ItemStack At(int x, int y)
        => x >= 0 && y >= 0 && x < W && y < H ? _cells[x, y] : null;

    /// <summary>物品实际占的宽高（考虑旋转）。</summary>
    public static Vector2I SizeOf(ItemStack stack, bool rotated)
    {
        if (stack?.Data == null) return Vector2I.One;

        int w = Mathf.Max(1, stack.Data.GridW);
        int h = Mathf.Max(1, stack.Data.GridH);
        return rotated ? new Vector2I(h, w) : new Vector2I(w, h);
    }

    /// <summary>能不能把 <paramref name="stack"/> 放在 (x, y)。</summary>
    public bool CanPlace(ItemStack stack, int x, int y, bool rotated)
    {
        if (stack?.Data == null) return false;

        var size = SizeOf(stack, rotated);
        if (x < 0 || y < 0 || x + size.X > W || y + size.Y > H) return false;

        // 自己已经在这几个格子里的话不算冲突（移动自己的情况）
        for (int dy = 0; dy < size.Y; dy++)
            for (int dx = 0; dx < size.X; dx++)
            {
                var occupant = _cells[x + dx, y + dy];
                if (occupant != null && !ReferenceEquals(occupant, stack)) return false;
            }
        return true;
    }

    /// <summary>
    /// **首次适配**：从上到下、从左到右扫，找第一个放得下的位置。
    /// 返回 false 表示完全放不进去。
    /// </summary>
    public bool TryFindSpot(ItemStack stack, out int x, out int y, out bool rotated)
    {
        x = y = 0;
        rotated = false;

        if (stack?.Data == null) return false;

        foreach (bool rot in new[] { false, true })
        {
            var size = SizeOf(stack, rot);
            if (size.X > W || size.Y > H) continue;   // 这个朝向根本放不下

            for (int yy = 0; yy <= H - size.Y; yy++)
                for (int xx = 0; xx <= W - size.X; xx++)
                {
                    if (!CanPlace(stack, xx, yy, rot)) continue;
                    x = xx;
                    y = yy;
                    rotated = rot;
                    return true;
                }
        }
        return false;
    }

    /// <summary>放到指定位置（会先从旧位置移除）。</summary>
    public bool Place(ItemStack stack, int x, int y, bool rotated)
    {
        if (!CanPlace(stack, x, y, rotated)) return false;

        Remove(stack);

        var size = SizeOf(stack, rotated);
        for (int dy = 0; dy < size.Y; dy++)
            for (int dx = 0; dx < size.X; dx++)
                _cells[x + dx, y + dy] = stack;

        stack.GridX = x;
        stack.GridY = y;
        stack.Rotated = rotated;
        return true;
    }

    /// <summary>自动排布（首次适配）。放不下返回 false。</summary>
    public bool TryAutoPlace(ItemStack stack)
    {
        if (!TryFindSpot(stack, out int x, out int y, out bool rot)) return false;
        return Place(stack, x, y, rot);
    }

    /// <summary>
    /// 把一件物品从容器里拿走（清空它占的所有格子）。
    /// 注意：**不会销毁物品**，只是解除占位。
    /// </summary>
    public void Remove(ItemStack stack)
    {
        if (stack == null) return;

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (ReferenceEquals(_cells[x, y], stack))
                    _cells[x, y] = null;

        stack.GridX = -1;
        stack.GridY = -1;
    }

    /// <summary>清空整个容器（只解除占位，不销毁物品）。</summary>
    public void Clear()
    {
        foreach (var s in Items) Remove(s);
    }

    /// <summary>
    /// 检查占位表是否自洽（调试用）：
    /// 每件物品的所有格子都指向它自己、且数量等于它的 W×H。
    /// </summary>
    public bool Validate(out string reason)
    {
        reason = "";

        foreach (var item in Items)
        {
            var size = SizeOf(item, item.Rotated);
            int n = 0;

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (ReferenceEquals(_cells[x, y], item)) n++;

            if (n != size.X * size.Y)
            {
                reason = $"{item.Data?.Name} 占位格数 {n} ≠ {size.X}x{size.Y}";
                return false;
            }
        }
        return true;
    }

    public override string ToString() => $"格子容器 {W}x{H} 已占 {UsedCells}/{CellCount}";
}
