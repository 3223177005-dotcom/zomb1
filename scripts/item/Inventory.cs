using Godot;
using System.Collections.Generic;

/// <summary>
/// **格子口袋**（一个容器）：二维占位表 + 自动排布，外加**负重**上限。
///
/// 阶段 2 的改造结果：内部存储从「文字列表」换成了 <see cref="GridInventory"/>，
/// 对外接口（<see cref="Add"/> / <see cref="RemoveFrom"/> / <see cref="TryInsert"/> …）保持不变，
/// 调用方不需要知道格子怎么摆 —— 放进去时自动**首次适配**排布。
///
/// 已彻底删掉的：体积（L）相关的 <c>MaxVolume</c> / <c>UsedVolume</c> / <c>FreeVolume</c>，
/// 以及体积约束传播（原 <c>ContainerConstraint</c>）—— 现在「放不放得下」只看格子。
///
/// 保留的：
/// - **重量**上限与用量（<see cref="MaxWeight"/> / <see cref="UsedWeight"/> / <see cref="FreeWeight"/>）；
/// - 递归下探（外层口袋装不下 → 找装在里面的容器）；
/// - 循环防护 / 嵌套深度上限。
///
/// 纯 C# 类（和 ItemStack / ContainerData 一致），不是 Godot 对象。
/// </summary>
public class Inventory
{
    /// <summary>嵌套深度上限（容器最多能套几层）。根口袋深度为 0。</summary>
    public const int MaxNestingDepth = 3;

    /// <summary>内容物（**放入顺序**，UI 按这个顺序列出）。</summary>
    private readonly List<ItemStack> _stacks = new();

    /// <summary>占位表（位置信息的唯一来源）。</summary>
    private readonly GridInventory _grid;

    public Inventory(float maxWeight, int gridW, int gridH)
    {
        MaxWeight = Mathf.Max(0.0f, maxWeight);
        _grid = new GridInventory(gridW, gridH);
    }

    // ---------- 上限 / 树结构 ----------

    /// <summary>最大可承载重量（kg）。</summary>
    public float MaxWeight { get; set; }

    /// <summary>占位表（画格子 UI、拖拽算目标格时直接读它）。</summary>
    public GridInventory Grid => _grid;

    /// <summary>口袋有几列。</summary>
    public int GridW => _grid.W;

    /// <summary>口袋有几行。</summary>
    public int GridH => _grid.H;

    /// <summary>总格数。</summary>
    public int CellCount => _grid.CellCount;

    /// <summary>已占格数。</summary>
    public int UsedCells => _grid.UsedCells;

    /// <summary>空格数。</summary>
    public int FreeCells => _grid.FreeCells;

    /// <summary>这一格被谁占了（空格返回 null）。</summary>
    public ItemStack At(int x, int y) => _grid.At(x, y);

    /// <summary>本容器所属的物品（玩家基础口袋为 null）。由 <see cref="ItemStack.EnsureContents"/> 设置。</summary>
    public ItemStack Owner { get; internal set; }

    /// <summary>嵌套深度：根口袋为 0，沿 Owner 链向上算出。</summary>
    public int Depth
    {
        get
        {
            int d = 0;
            var inv = this;
            while (inv?.Owner != null)
            {
                d++;
                inv = inv.Owner.Parent;
            }
            return d;
        }
    }

    // ---------- 内容物 ----------

    /// <summary>容器里所有堆叠（按放入顺序）。</summary>
    public IReadOnlyList<ItemStack> Stacks => _stacks;

    /// <summary>堆叠数量（注意不是件数）。</summary>
    public int StackCount => _stacks.Count;

    public bool IsEmpty => _stacks.Count == 0;

    /// <summary>已占用重量（kg）。</summary>
    public float UsedWeight
    {
        get
        {
            float w = 0.0f;
            foreach (var s in _stacks) w += s.TotalWeight;
            return w;
        }
    }

    /// <summary>剩余可承重（kg）。</summary>
    public float FreeWeight => Mathf.Max(0.0f, MaxWeight - UsedWeight);

    /// <summary>容器内物品总价值。</summary>
    public int TotalValue
    {
        get
        {
            int v = 0;
            foreach (var s in _stacks) v += s.TotalValue;
            return v;
        }
    }

    // ---------- 查询 ----------

    /// <summary>某物品的总件数（跨所有堆叠）。</summary>
    public int CountOf(string itemId)
    {
        int n = 0;
        foreach (var s in _stacks)
            if (s?.Data != null && s.Data.Id == itemId) n += s.Count;
        return n;
    }

    public bool Has(string itemId, int count = 1) => CountOf(itemId) >= count;

    /// <summary>按物品 Id 找第一个堆叠。</summary>
    public ItemStack FindStack(string itemId)
    {
        foreach (var s in _stacks)
            if (s?.Data != null && s.Data.Id == itemId) return s;
        return null;
    }

    /// <summary>取本容器里**某一类型的全部堆叠**（拖拽按类型整组搬运时用）。</summary>
    public List<ItemStack> FindStacks(string itemId)
    {
        var result = new List<ItemStack>();
        foreach (var s in _stacks)
            if (s?.Data != null && s.Data.Id == itemId) result.Add(s);
        return result;
    }

    /// <summary>
    /// 从某个堆叠里**分出一部分**，作为同一容器里的一个新堆叠。
    ///
    /// 返回分出来的新堆叠；数量非法（&lt;=0 或 &gt;= 原数量）、找不到该堆叠、
    /// 或**网格里已经腾不出位置**时返回 null（原堆叠不受影响）。
    /// 原堆叠数量相应减少，**耐用度 / 使用次数**一并复制过去。
    /// </summary>
    public ItemStack SplitStack(ItemStack stack, int count)
    {
        if (stack?.Data == null) return null;
        if (!_stacks.Contains(stack)) return null;
        if (count <= 0 || count >= stack.Count) return null;

        var split = new ItemStack(stack.Data, count)
        {
            Durability = stack.Durability,
            Uses = stack.Uses,
        };

        // ★ 拆出来的那一份也要占一块格子；占位表满了就拆不了
        if (!Register(split)) return null;

        stack.Count -= count;
        return split;
    }

    /// <summary>
    /// 把内容**按物品类型**汇总（同一类型的多个堆叠合并成一行），顺序按首次出现。
    /// 背包面板与地面拾取面板共用。
    /// </summary>
    public List<ItemSummary> Summarize()
    {
        var result = new List<ItemSummary>();

        var order = new List<string>();
        var acc = new Dictionary<string, ItemSummary>();

        foreach (var stack in _stacks)
        {
            if (stack?.Data == null) continue;

            string id = stack.Data.Id;
            if (!acc.TryGetValue(id, out var e))
            {
                order.Add(id);
                acc[id] = new ItemSummary(id, stack.Data, 0, 0f, 0);
                e = acc[id];
            }

            acc[id] = new ItemSummary(
                id,
                e.Data,
                e.Count + stack.Count,
                e.Weight + stack.TotalWeight,
                e.StackCount + 1);
        }

        foreach (var id in order) result.Add(acc[id]);
        return result;
    }

    // ---------- 占位判定 ----------

    /// <summary>能不能把 <paramref name="stack"/> 放在 (x, y)（考虑旋转）。</summary>
    public bool CanPlace(ItemStack stack, int x, int y, bool rotated)
        => _grid.CanPlace(stack, x, y, rotated);

    /// <summary>
    /// 就算口袋**空着**，这件物品（任一朝向）能不能放进来。
    /// 放不进 → 对应 <see cref="ContainCode.TooBig"/>（换口袋才行）。
    /// </summary>
    public bool FitsWhenEmpty(ItemStack stack)
    {
        if (stack?.Data == null) return false;

        int w = Mathf.Max(1, stack.Data.GridW);
        int h = Mathf.Max(1, stack.Data.GridH);

        return (w <= GridW && h <= GridH) || (h <= GridW && w <= GridH);
    }

    /// <summary>
    /// 现在能不能再收下这件物品：能并进同类堆叠，**或**网格里还找得到一块放得下的空位。
    /// </summary>
    public bool CanAccept(ItemStack stack)
    {
        if (stack?.Data == null || stack.IsEmpty) return false;

        // ① 已有同类堆叠还有空位 → 不占新格子
        if (stack.Data.IsStackable)
        {
            foreach (var s in _stacks)
                if (s.Data == stack.Data && s.RemainingStackSpace > 0) return true;
        }

        // ② 网格里找一块空位（首次适配，含旋转）
        return _grid.TryFindSpot(stack, out _, out _, out _);
    }

    // ---------- 局部判定（对应 CDDA item_pocket::_can_contain） ----------

    /// <summary>
    /// 本口袋能否容纳该物品。分两条独立的线：
    ///   - **绝对**：就算口袋空着也放不下 → <see cref="ContainCode.TooHeavy"/> / <see cref="ContainCode.TooBig"/>
    ///   - **剩余**：整叠放不下 → <see cref="ContainCode.CannotSupport"/> / <see cref="ContainCode.NoSpace"/>
    /// </summary>
    public ContainCode CanHold(ItemStack it)
    {
        if (it?.Data == null || it.IsEmpty) return ContainCode.Success;

        // 单件重量（堆叠按件均摊；容器 Count 恒为 1）
        float unitWeight = it.Count > 0 ? it.TotalWeight / it.Count : 0f;

        // ---- 绝对判定：单件就超过口袋的总上限 ----
        if (unitWeight > MaxWeight) return ContainCode.TooHeavy;
        if (!FitsWhenEmpty(it)) return ContainCode.TooBig;

        // ---- 剩余判定：整叠放不下当前的剩余量 ----
        if (it.TotalWeight > FreeWeight) return ContainCode.CannotSupport;
        if (!CanAccept(it)) return ContainCode.NoSpace;

        return ContainCode.Success;
    }

    /// <summary>还能再放入多少个该物品（受**剩余负重**与**格子**双重限制）。</summary>
    public int MaxAddableCount(ItemData data)
    {
        if (data == null) return 0;

        int byWeight = data.Weight > 0.0001f
            ? Mathf.FloorToInt(FreeWeight / data.Weight)
            : int.MaxValue;

        if (byWeight <= 0) return 0;

        // 网格至少要能收下一叠（能并进同类堆叠，或有空位）
        var probe = new ItemStack(data, 1);
        return CanAccept(probe) ? byWeight : 0;
    }

    /// <summary>指定数量能否全部放入。</summary>
    public bool CanFit(ItemData data, int count = 1)
        => data != null && count > 0 && MaxAddableCount(data) >= count;

    // ---------- 递归查找（对应 CDDA best_pocket ⇄ best_pocket_in_contents） ----------

    /// <summary>
    /// 在本容器**装着的物品**里递归下探，找能容纳 <paramref name="it"/> 的落点。
    /// 对应 CDDA <c>item_pocket::best_pocket_in_contents</c>。找不到返回 null。
    ///
    /// 多个候选时**先找到的优先** —— CDDA 这里用 <c>better_pocket()</c> 比较
    /// （优先级 / 白名单 / 刚性），我们都没有这些，所以不做比较。
    /// </summary>
    /// <param name="avoid">
    /// 递归中要避开的物品（循环防护）—— 对应 CDDA 的 <c>avoid</c> 参数。
    /// </param>
    public Inventory FindNestedPocket(ItemStack it, ItemStack avoid)
    {
        if (it?.Data == null || it.IsEmpty) return null;

        foreach (var contained in _stacks)
        {
            // ★ CDDA 的循环防护（item_pocket.cpp:2286）：
            //     if( &contained_item == &it || &contained_item == avoid ) continue;
            if (ReferenceEquals(contained, it)) continue;
            if (ReferenceEquals(contained, avoid)) continue;
            if (!contained.IsContainer) continue;

            var nested = contained.FindPocket(it, avoid);
            if (nested != null) return nested;
        }

        return null;
    }

    // ---------- 放置（阶段 3 / 4 的拖拽会直接用这些） ----------

    /// <summary>
    /// 把一件物品放到**指定格子**。物品不在本口袋时一并收进来。
    /// 放不下（越界 / 重叠）返回 false。
    /// </summary>
    public bool Place(ItemStack stack, int x, int y, bool rotated)
    {
        if (stack?.Data == null) return false;
        if (!_grid.CanPlace(stack, x, y, rotated)) return false;

        if (!_stacks.Contains(stack)) _stacks.Add(stack);

        _grid.Place(stack, x, y, rotated);
        stack.Parent = this;
        return true;
    }

    /// <summary>自动排布（首次适配）放进本口袋；放不下返回 false。</summary>
    public bool TryAutoPlace(ItemStack stack) => Register(stack);

    /// <summary>把新堆叠登记进内容物列表并自动排布（**不检查重量**）。</summary>
    private bool Register(ItemStack stack)
    {
        if (stack?.Data == null || stack.IsEmpty) return false;
        if (!_grid.TryAutoPlace(stack)) return false;

        _stacks.Add(stack);
        stack.Parent = this;
        return true;
    }

    // ---------- 放入 ----------

    /// <summary>
    /// 放入一叠物品（**低级原语**）：先并入已有的同类堆叠，剩下的新开堆叠并**自动排布**。
    /// 只看**本口袋自己**的剩余量，**不做嵌套查找** —— 要嵌套请用 <see cref="TryInsert"/>。
    ///
    /// ⚠️ 这是**移动**语义：会新建堆叠并接管内容物，传入的对象不再是口袋里的那一份。
    /// 放不下的部分原样返回（返回 null 表示全部放下）。
    /// </summary>
    public ItemStack Add(ItemStack incoming) => AddCore(incoming, out _);

    /// <summary>内部核心：返回剩余；同时输出**实际存放的第一份堆叠**。</summary>
    private ItemStack AddCore(ItemStack incoming, out ItemStack stored)
    {
        stored = null;
        if (incoming?.Data == null || incoming.IsEmpty) return incoming;

        int allowed = Mathf.Min(incoming.Count, MaxAddableCount(incoming.Data));
        if (allowed <= 0) return incoming;

        int remaining = allowed;

        // 1) 先并入已有同类堆叠（不占新格）
        if (incoming.Data.IsStackable)
        {
            foreach (var stack in _stacks)
            {
                if (remaining <= 0) break;
                if (stack.Data != incoming.Data) continue;

                int space = stack.RemainingStackSpace;
                if (space <= 0) continue;

                int move = Mathf.Min(space, remaining);
                stack.Count += move;
                remaining -= move;
                stored ??= stack;
            }
        }

        // 2) 剩下的新开堆叠 —— 每一叠都要在网格里找到位置
        while (remaining > 0)
        {
            int piece = Mathf.Min(remaining, Mathf.Max(1, incoming.Data.StackLimit));

            var newStack = new ItemStack(incoming.Data, piece);
            newStack.Durability = incoming.Durability;   // 继承实例状态（耐久 / 使用次数）
            newStack.Uses = incoming.Uses;

            // ★ 先占格子，占到了才接管内容物（否则失败时内容物会丢）
            if (!Register(newStack)) break;

            // 整份都放进来时，内容物一并接管
            if (piece == incoming.Count)
                newStack.AdoptContents(incoming);

            stored ??= newStack;
            remaining -= piece;
        }

        int notAdded = incoming.Count - (allowed - remaining);
        return notAdded > 0 ? new ItemStack(incoming.Data, notAdded) : null;
    }

    /// <summary>
    /// 放入物品：**外层优先** —— 本口袋能装就装这里，装不下才递归下探到内层容器。
    /// 对应 CDDA <c>best_pocket()</c> 的默认行为。
    ///
    /// 成功时 <paramref name="stored"/> 是**口袋里实际存放的那一份**。
    /// 失败时返回具体原因码（见 <see cref="ContainCode"/>）。
    /// </summary>
    public ContainCode TryInsert(ItemStack it, out ItemStack stored)
    {
        stored = null;
        if (it?.Data == null || it.IsEmpty) return ContainCode.Success;

        // ---- 循环防护：it 不能出现在目标的祖先链上 ----
        if (WouldCycle(it, this)) return ContainCode.WouldCycle;

        // ---- 深度检查：放入容器时，目标深度 + 1 不得超过上限 ----
        if (it.IsContainer && Depth + 1 > MaxNestingDepth) return ContainCode.TooDeep;

        // ---- 找落点：外层优先 ----
        var own = CanHold(it);

        if (own == ContainCode.Success)
        {
            AddCore(it, out stored);
            return stored != null ? ContainCode.Success : CanHold(it);
        }

        var target = FindNestedPocket(it, null);
        if (target == null) return own;   // 本口袋失败的原因就是最终原因

        target.AddCore(it, out stored);
        return stored != null ? ContainCode.Success : target.CanHold(it);
    }

    /// <summary>
    /// <paramref name="it"/> 是否位于 <paramref name="target"/> 的祖先链上。
    /// 是则说明把它放进去会形成环（把容器放进它自己内部）。
    /// </summary>
    private static bool WouldCycle(ItemStack it, Inventory target)
    {
        var inv = target;
        while (inv?.Owner != null)
        {
            if (ReferenceEquals(inv.Owner, it)) return true;
            inv = inv.Owner.Parent;
        }
        return false;
    }

    // ---------- 移除 ----------

    /// <summary>本口袋是否装着这一份（引用比较）。</summary>
    public bool Contains(ItemStack stack) => stack != null && _stacks.Contains(stack);

    /// <summary>
    /// **把一份堆叠整个摘出来**（不销毁、不改数量）：从内容物列表和占位表里都删掉，
    /// <see cref="ItemStack.Parent"/> 置空。数量不变，供拖拽「搬到别处」使用。
    ///
    /// 与 <see cref="RemoveFrom"/> 的区别：后者按件扣，扣到 0 才算移除；
    /// 这里一个 Count 都不动，只是解除归属。
    /// </summary>
    public bool Detach(ItemStack stack)
    {
        if (stack == null || !_stacks.Remove(stack)) return false;

        _grid.Remove(stack);
        stack.Parent = null;
        return true;
    }

    /// <summary>按物品 Id 移除指定数量；数量不足则**不动任何东西**并返回 false。</summary>
    public bool Remove(string itemId, int count = 1)
    {
        if (count <= 0) return false;
        if (CountOf(itemId) < count) return false;

        int remaining = count;
        for (int i = _stacks.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var stack = _stacks[i];
            if (stack.Data.Id != itemId) continue;

            int take = Mathf.Min(stack.Count, remaining);
            stack.Count -= take;
            remaining -= take;

            if (stack.Count <= 0)
            {
                _stacks.RemoveAt(i);
                _grid.Remove(stack);
                stack.Parent = null;
            }
        }
        return true;
    }

    /// <summary>从指定堆叠拿走若干；该堆叠清零则整个移除（占的格子一并清空）。</summary>
    public virtual bool RemoveFrom(ItemStack stack, int count = 1)
    {
        if (stack == null || count <= 0) return false;
        if (!_stacks.Contains(stack)) return false;
        if (stack.Count < count) return false;

        stack.Count -= count;
        if (stack.Count <= 0)
        {
            _stacks.Remove(stack);
            _grid.Remove(stack);
            stack.Parent = null;
        }
        return true;
    }

    /// <summary>移除整个堆叠。</summary>
    public bool RemoveStack(ItemStack stack)
    {
        if (stack == null || !_stacks.Remove(stack)) return false;
        _grid.Remove(stack);
        stack.Parent = null;
        return true;
    }

    public void Clear()
    {
        foreach (var s in _stacks) s.Parent = null;
        _stacks.Clear();
        _grid.Clear();
    }

    /// <summary>
    /// 自检：占位表自洽（每件物品占的格数 == W×H），且内容物列表与占位表一致。
    /// </summary>
    public bool Validate(out string reason)
    {
        if (!_grid.Validate(out reason)) return false;

        var placed = _grid.Items;
        if (placed.Count != _stacks.Count)
        {
            reason = $"内容物 {_stacks.Count} 叠，但占位表里有 {placed.Count} 件";
            return false;
        }

        foreach (var s in _stacks)
        {
            if (!placed.Contains(s))
            {
                reason = $"{s.Data?.Name} 在内容物里但没占位";
                return false;
            }
        }

        return true;
    }

    public override string ToString()
        => $"口袋 {GridW}x{GridH} 已占 {UsedCells}/{CellCount} 格  {UsedWeight:0.00}/{MaxWeight:0.00}kg  {_stacks.Count} 叠  总价 {TotalValue}";
}
