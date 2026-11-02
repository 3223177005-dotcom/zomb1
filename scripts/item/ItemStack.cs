using Godot;

/// <summary>
/// 物品堆叠实例：某个物品定义 + 数量 + 实例状态（耐久 / 使用次数）。
///
/// 说明：
/// - 定义（ItemData）是全局共享的一份；实例（ItemStack）携带可变状态，
///   所以容器里存的是 ItemStack 而不是 ItemData。
/// - 只有**弹药 / 药品**类可以堆叠（Count > 1，见 <see cref="ItemData.CanCategoryStack"/>）；
///   护甲类用 <see cref="Durability"/>，药物类用 <see cref="Uses"/>，两者都从物品数据取初值。
/// - 目前用纯 C# 类（轻量、无需管理 Godot 引用计数），仅在容器/掉落逻辑内部使用。
///   将来若要经信号传递或做存档序列化，可以改为 Resource 或加一个 ToDictionary()。
/// </summary>
public class ItemStack
{
    /// <summary>物品定义。</summary>
    public ItemData Data { get; set; }

    // ---------- 格子背包：在容器里的位置 ----------

    /// <summary>在容器里的左上角**列**（-1 = 未放置）。</summary>
    public int GridX { get; set; } = -1;

    /// <summary>在容器里的左上角**行**（-1 = 未放置）。</summary>
    public int GridY { get; set; } = -1;

    /// <summary>是否旋转了 90°（宽高互换）。</summary>
    public bool Rotated { get; set; }

    /// <summary>数量（>= 1；为 0 视为空）。</summary>
    public int Count { get; set; }

    /// <summary>当前耐久（护甲类）。创建时初始化为 Data.MaxDurability。</summary>
    public float Durability { get; set; }

    /// <summary>剩余使用次数（药物类）。创建时初始化为 Data.MaxUses。</summary>
    public int Uses { get; set; }

    public ItemStack()
    {
    }

    public ItemStack(ItemData data, int count)
    {
        Data = data;
        Count = count;
        InitInstanceState();
    }

    /// <summary>按物品数据初始化实例状态（耐久 / 使用次数）。</summary>
    public void InitInstanceState()
    {
        Durability = Data?.MaxDurability ?? 0f;
        Uses = Data?.MaxUses ?? 0;
    }

    public bool IsEmpty => Data == null || Count <= 0;

    // ---------- 容器（可内含内容物） ----------

    /// <summary>所属容器（根物品为 null）。由 <see cref="Inventory"/> 在放入 / 移除时维护。</summary>
    public Inventory Parent { get; internal set; }

    /// <summary>内容物（容器类物品才有；非容器恒为 null）。</summary>
    public Inventory Contents { get; private set; }

    /// <summary>是否是容器（能装东西）。</summary>
    public bool IsContainer => Data != null && Data.IsContainer;

    /// <summary>
    /// 取得内容物容器；容器类物品首次调用时按物品数据创建。
    /// 非容器类返回 null。
    /// </summary>
    public Inventory EnsureContents()
    {
        if (!IsContainer) return null;
        // 容器自身没有承重上限 —— 重量上限是玩家全局的一份（Player.MaxCarryWeight）；
        // 容量由容器数据的网格宽高决定（ContainerData.GridW / GridH）
        Contents ??= new Inventory(float.MaxValue, Data.ContainerGridW, Data.ContainerGridH) { Owner = this };
        return Contents;
    }

    /// <summary>把另一叠的内容物接管过来（仅容器；接管后源叠的内容清空）。</summary>
    public void AdoptContents(ItemStack source)
    {
        if (source == null || source.Contents == null || !IsContainer) return;

        Contents = source.Contents;
        Contents.Owner = this;      // ★ 内容物换主，指向接管者
        source.Contents = null;
    }

    // ---------- 递归查找（对应 CDDA item_contents::best_pocket） ----------

    /// <summary>
    /// 在**本物品的口袋**及其嵌套里，为 <paramref name="it"/> 找落点；找不到返回 null。
    /// 对应 CDDA <c>item_contents::best_pocket</c>
    /// （CDDA 一个物品有多个口袋，我们只有一个，所以少一层口袋遍历）。
    ///
    /// 阶段 2 起口袋是**格子**的：能不能装下由 <see cref="Inventory.CanHold"/> 判定，
    /// 不再有「体积约束传播」这一层（原来的 ContainerConstraint 已删除）。
    /// </summary>
    public Inventory FindPocket(ItemStack it, ItemStack avoid)
    {
        var pocket = Contents;
        if (pocket == null) return null;
        if (it?.Data == null || it.IsEmpty) return null;

        // ① 本口袋能装就装这里（外层优先，对应 CDDA best_pocket 的默认行为）
        if (pocket.CanHold(it) == ContainCode.Success) return pocket;

        // ② 本口袋装不下 → 递归下探。
        //    注意：CDDA 的 valid_pockets 是在 can_contain 之前收集的，
        //    所以【即使本口袋装不下也仍然会搜索嵌套口袋】，这里保持一致。
        return pocket.FindNestedPocket(it, avoid);
    }

    // ---------- 耐久（护甲） ----------

    /// <summary>该物品是否带耐久。</summary>
    public bool HasDurability => Data != null && Data.HasDurability;

    /// <summary>耐久是否耗尽。</summary>
    public bool IsBroken => HasDurability && Durability <= 0f;

    /// <summary>耐久百分比（0~1；无耐久物品恒为 1）。</summary>
    public float DurabilityRatio
        => HasDurability ? Mathf.Clamp(Durability / Data.MaxDurability, 0f, 1f) : 1f;

    /// <summary>扣耐久（不会低于 0）。</summary>
    public void ApplyDurabilityLoss(float amount)
    {
        if (HasDurability && amount > 0f)
            Durability = Mathf.Max(0f, Durability - amount);
    }

    /// <summary>耐久上限（来自物品数据）。</summary>
    public float MaxDurability => Data?.MaxDurability ?? 0f;

    /// <summary>耐久是否已满。</summary>
    public bool IsFullDurability => !HasDurability || Durability >= MaxDurability;

    /// <summary>剩余可修复量。</summary>
    public float MissingDurability => HasDurability ? Mathf.Max(0f, MaxDurability - Durability) : 0f;

    /// <summary>修复耐久（不会超过上限）；返回实际修复量。</summary>
    public float Repair(float amount)
    {
        if (!HasDurability || amount <= 0f)
            return 0f;

        float before = Durability;
        Durability = Mathf.Min(MaxDurability, Durability + amount);
        return Durability - before;
    }

    /// <summary>修满耐久（藏身处修复用）；返回实际修复量。</summary>
    public float RepairToFull() => Repair(MissingDurability);

    // ---------- 使用次数（药物） ----------

    /// <summary>该物品是否带使用次数。</summary>
    public bool HasUses => Data != null && Data.HasUses;

    /// <summary>是否还能使用。</summary>
    public bool CanUse => HasUses ? Uses > 0 : !IsEmpty;

    /// <summary>消耗一次使用次数；成功返回 true。</summary>
    public bool ConsumeUse()
    {
        if (!HasUses || Uses <= 0)
            return false;

        Uses--;
        return true;
    }

    // ---------- 堆叠 ----------

    /// <summary>这一叠的总重量（**含内含物与已装配件，递归**）。</summary>
    public float TotalWeight
    {
        get
        {
            if (Data == null) return 0f;

            float own = Data.Weight * Count;

            // 已装配件也算在这把枪的重量里
            float attachments = WeaponMods.ExtraWeight(this);

            if (Contents == null || Contents.IsEmpty) return own + attachments;
            return own + attachments + Contents.UsedWeight;
        }
    }

    /// <summary>这一叠的总价值（**含内含物，递归**）。</summary>
    public int TotalValue
    {
        get
        {
            if (Data == null) return 0;

            int own = Data.Value * Count;
            if (Contents == null || Contents.IsEmpty) return own;
            return own + Contents.TotalValue;
        }
    }

    /// <summary>内含物的件数（含嵌套，递归）。</summary>
    public int ContainedItemCount
    {
        get
        {
            if (Contents == null) return 0;

            int n = 0;
            foreach (var stack in Contents.Stacks)
                n += stack.Count + stack.ContainedItemCount;
            return n;
        }
    }

    /// <summary>还能再堆多少个（不考虑目标格剩余空间时）。</summary>
    public int RemainingStackSpace => Data != null ? Data.StackLimit - Count : 0;

    public bool IsFull => Data == null || Count >= Data.StackLimit;

    /// <summary>深拷贝（容器会递归拷贝内容物）。</summary>
    public ItemStack Clone()
    {
        var clone = new ItemStack(Data, Count);
        clone.Durability = Durability;
        clone.Uses = Uses;

        if (Contents != null && !Contents.IsEmpty)
        {
            var copy = clone.EnsureContents();
            foreach (var stack in Contents.Stacks)
                copy?.Add(stack.Clone());
        }
        return clone;
    }

    /// <summary>
    /// 从这一叠里分出最多 amount 个，返回新的一叠；不足则分出全部。
    /// 新的一份继承当前耐久 / 使用次数；整叠被拿空时内容物一并转移。
    /// </summary>
    public ItemStack Split(int amount)
    {
        if (amount <= 0 || IsEmpty)
            return new ItemStack(Data, 0);

        int take = amount < Count ? amount : Count;
        Count -= take;

        var split = new ItemStack(Data, take);
        split.Durability = Durability;
        split.Uses = Uses;

        // 容器不可堆叠，正常不会走到这里；防御性处理，避免内容物丢失
        if (Count <= 0 && Contents != null)
            split.AdoptContents(this);

        return split;
    }

    /// <summary>用于调试/日志的可读描述。</summary>
    public override string ToString()
    {
        if (Data == null)
            return "(空)";

        string extra = "";
        if (HasDurability)
            extra += $" 耐久={Durability:0.#}/{Data.MaxDurability:0.#}";
        if (HasUses)
            extra += $" 次数={Uses}/{Data.MaxUses}";
        if (IsContainer)
            extra += $" [容器 {Contents?.UsedCells ?? 0}/{Data.ContainerGridW * Data.ContainerGridH} 格]";

        return $"{Data.Name} x{Count}{extra}";
    }
}
