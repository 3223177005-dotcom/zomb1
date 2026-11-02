using Godot;
using System.Collections.Generic;

/// <summary>
/// 物品拖拽的**载荷**（正在被拖的东西 + 从哪个口袋拖出来的）。
///
/// 为什么不用 Godot 的 <c>Variant</c> 传：<see cref="ItemStack"/> 是纯 C# 类、
/// 不是 Godot Object，塞不进 Variant。所以用这个静态"剪贴板"，拖拽期间只可能有一个载荷。
///
/// 载荷是**一组堆叠**而不是单件 —— 因为地面物品树的一行可能是"3 个绷带"，
/// 拖它就该把 3 个都拖走。**格子里的物品一律是单叠载荷**（一格就是一份）。
///
/// 阶段 3.5 起多了**来源格坐标**（<see cref="SourceX"/> / <see cref="SourceY"/>）和
/// **目标格放置**（<see cref="TryPlaceAt"/>）—— 从格子拖到格子是按坐标搬的，
/// 不再只是"容器 → 容器"。
/// </summary>
public static class ItemDrag
{
    private static readonly List<ItemStack> Payloads = new();

    /// <summary>从哪个口袋拖出来的。</summary>
    public static Inventory Source { get; private set; }

    /// <summary>从哪个格子拖出来的（-1 = 不是从格子里拖的，比如文字行）。</summary>
    public static int SourceX { get; private set; } = -1;

    /// <summary>同上，行坐标。</summary>
    public static int SourceY { get; private set; } = -1;

    /// <summary>是否是从某个格子拖起来的。</summary>
    public static bool FromGrid => SourceX >= 0 && SourceY >= 0;

    /// <summary>正在拖的那些堆叠。</summary>
    public static IReadOnlyList<ItemStack> Items => Payloads;

    /// <summary>载荷只有一份堆叠时返回它（格子拖放只接受单叠）；否则 null。</summary>
    public static ItemStack Single => Payloads.Count == 1 ? Payloads[0] : null;

    public static bool Active => Source != null && Payloads.Count > 0;

    /// <summary>这一组的总件数。</summary>
    public static int Count
    {
        get
        {
            int n = 0;
            foreach (var s in Payloads) n += s.Count;
            return n;
        }
    }

    /// <summary>开始拖拽。</summary>
    public static void Begin(Inventory source, IReadOnlyList<ItemStack> stacks,
                             int sourceX = -1, int sourceY = -1)
    {
        Source = source;
        SourceX = sourceX;
        SourceY = sourceY;

        Payloads.Clear();
        if (stacks == null) return;
        foreach (var s in stacks)
            if (s != null) Payloads.Add(s);
    }

    /// <summary>结束拖拽（放下或取消都要调）。</summary>
    public static void End()
    {
        Source = null;
        SourceX = -1;
        SourceY = -1;
        Payloads.Clear();
    }

    // ---------- 容器级落点（文字行 / 标题行用） ----------

    /// <summary>能不能往这个口袋放（同一个口袋不行；一叠都放不下也不行）。</summary>
    public static bool CanDrop(Inventory target)
    {
        if (!Active || target == null) return false;
        if (ReferenceEquals(Source, target)) return false;

        foreach (var stack in Payloads)
            if (target.CanHold(stack) == ContainCode.Success) return true;

        return false;
    }

    /// <summary>
    /// 能不能**并进指定的那一叠**（把两叠合起来）。
    /// 要求：同类型、那一叠还有空位、而且不是自己。
    /// 这是"拆开后拖回去"的关键 —— 同口袋内也能操作。
    /// </summary>
    public static bool CanMergeInto(ItemStack target)
    {
        if (!Active || target?.Data == null) return false;
        if (target.RemainingStackSpace <= 0) return false;

        foreach (var stack in Payloads)
        {
            if (ReferenceEquals(stack, target)) continue;
            if (stack.Data == target.Data) return true;
        }
        return false;
    }

    /// <summary>
    /// 把载荷**并进指定的那一叠**，返回实际合并的件数。
    /// 目标叠满（达到 StackLimit）就停，剩下的留在原处。
    /// </summary>
    public static int TryMergeInto(ItemStack target)
    {
        if (!CanMergeInto(target)) return 0;

        int moved = 0;

        // 从后往前：源列表会被改动，倒着走不影响前面的下标
        for (int i = Payloads.Count - 1; i >= 0; i--)
        {
            var stack = Payloads[i];
            if (ReferenceEquals(stack, target) || stack.Data != target.Data) continue;

            int space = target.RemainingStackSpace;
            if (space <= 0) break;

            int count = stack.Count;
            int move = Mathf.Min(space, count);

            target.Count += move;
            TakeFromSource(stack, move);   // 搬空时它会自己从源列表移除
            moved += move;

            if (move < count) break;          // 目标满了
        }
        return moved;
    }

    /// <summary>
    /// 把载荷搬进目标口袋：**能放几叠放几叠**（放不下的留在原处）。
    /// 返回实际搬走的件数。落点由目标口袋自动排布。
    /// </summary>
    public static int TryMove(Inventory target)
    {
        if (!CanDrop(target)) return 0;

        int moved = 0;

        // 从后往前：从 Source 移除不影响前面还没处理的下标
        for (int i = Payloads.Count - 1; i >= 0; i--)
        {
            var stack = Payloads[i];
            int count = stack.Count;

            if (target.CanHold(stack) != ContainCode.Success) continue;

            // Add 是移动语义：会新建堆叠并把内容物接管过去；放不下的部分原样返回
            var leftover = target.Add(stack);
            int added = count - (leftover?.Count ?? 0);

            if (added <= 0) continue;

            TakeFromSource(stack, added);
            moved += added;
        }
        return moved;
    }

    /// <summary>放不下时的原因（给提示用）。</summary>
    public static ContainCode Reason(Inventory target)
    {
        if (Payloads.Count == 0 || target == null) return ContainCode.Success;

        ContainCode worst = ContainCode.Success;
        foreach (var stack in Payloads)
        {
            var code = target.CanHold(stack);
            if (code != ContainCode.Success) worst = code;
        }
        return worst;
    }

    // ---------- 格子级落点（GridPanel 用） ----------

    /// <summary>
    /// 能不能把载荷**放到 (x, y) 这一格**。
    /// <paramref name="rotated"/> 输出实际要用的朝向（优先保持原朝向，放不下再试旋转 90°）。
    ///
    /// 判定顺序与 <see cref="TryPlaceAt"/> 完全一致，所以高亮和真正放下不会打架。
    /// </summary>
    public static ContainCode CanPlaceAt(Inventory target, int x, int y, out bool rotated)
    {
        rotated = false;

        var stack = Single;
        if (!Active || target == null || stack?.Data == null) return ContainCode.NotAContainer;

        // ① 落在**自己**身上 = 原地不动
        var occupant = target.At(x, y);
        if (ReferenceEquals(occupant, stack)) return ContainCode.Success;

        // ② 落在**同类堆叠**身上 = 合并（只要还有堆叠空位）
        if (occupant != null)
        {
            if (occupant.Data == stack.Data && occupant.RemainingStackSpace > 0)
                return ContainCode.Success;
            return ContainCode.NoSpace;
        }

        // ③ 空格：先按原朝向，放不下再试旋转
        if (target.CanPlace(stack, x, y, stack.Rotated))
        {
            rotated = stack.Rotated;
            return ContainCode.Success;
        }
        if (target.CanPlace(stack, x, y, !stack.Rotated))
        {
            rotated = !stack.Rotated;
            return ContainCode.Success;
        }

        return target.FitsWhenEmpty(stack) ? ContainCode.NoSpace : ContainCode.TooBig;
    }

    /// <summary>
    /// 把载荷**搬到 (x, y)**：
    /// - 落在同类堆叠上 → 并进去（装不下的留一部分在原处，合并语义）；
    /// - 落在空格 → 整份搬过去（跨口袋时先从源里 <see cref="Inventory.Detach"/>）。
    ///
    /// 返回结果码；<see cref="ContainCode.Success"/> 表示搬运完成。
    /// </summary>
    public static ContainCode TryPlaceAt(Inventory target, int x, int y)
    {
        var stack = Single;
        var code = CanPlaceAt(target, x, y, out bool rotated);
        if (code != ContainCode.Success || stack?.Data == null) return code;

        var occupant = target.At(x, y);

        // ① 原位
        if (ReferenceEquals(occupant, stack)) return ContainCode.Success;

        // ② 并进同一叠
        if (occupant != null)
        {
            int move = Mathf.Min(occupant.RemainingStackSpace, stack.Count);
            if (move <= 0) return ContainCode.NoSpace;

            occupant.Count += move;
            TakeFromSource(stack, move);
            return ContainCode.Success;
        }

        // ③ 搬到空格：跨口袋时先把这一份从源里摘出来（不改数量）
        if (Source != null && !ReferenceEquals(Source, target))
        {
            if (!TakeFromSource(stack, stack.Count))
                return ContainCode.NoSpace;   // 源里已经没有这一份了（状态不一致）
        }

        return target.Place(stack, x, y, rotated) ? ContainCode.Success : ContainCode.NoSpace;
    }

    /// <summary>
    /// 从**源**里扣掉 <paramref name="count"/> 件。
    ///
    /// 载荷是源里的一份引用时按引用扣（搬空就摘出来）；
    /// 载荷是**临时克隆**（汇总行之类）时不按引用扣，改成按物品 Id 从源里扣同样数量
    /// —— 否则源里那份会留下来变成复制。
    ///
    /// 返回是否扣成功（源里没有这件东西时返回 false，调用方应放弃搬运）。
    /// </summary>
    public static bool TakeFromSource(ItemStack stack, int count)
    {
        if (Source == null || stack?.Data == null || count <= 0) return false;

        if (Source.Contains(stack))
        {
            if (count >= stack.Count) return Source.Detach(stack);
            stack.Count -= count;
            return true;
        }

        return Source.Remove(stack.Data.Id, count);
    }
}
