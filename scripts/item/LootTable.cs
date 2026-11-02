using Godot;
using System.Collections.Generic;

/// <summary>
/// 掉落表条目：一条掉落规则。
/// 两种用法（可同时启用）：
/// - 独立概率：按 <see cref="Chance"/> 单独判定，命中则掉 <see cref="MinCount"/>~<see cref="MaxCount"/> 个。
/// - 加权抽取：由 <see cref="LootTable.WeightedPickCount"/> 决定抽几条，按 <see cref="Weight"/> 加权。
/// </summary>
[GlobalClass]
public partial class LootEntry : Resource
{
    /// <summary>物品 Id（对应 ItemData.Id）。</summary>
    [Export] public string ItemId { get; set; } = "";

    /// <summary>独立掉落概率 0~1（1 = 必掉）。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float Chance { get; set; } = 1.0f;

    /// <summary>最少数量。</summary>
    [Export] public int MinCount { get; set; } = 1;

    /// <summary>最多数量。</summary>
    [Export] public int MaxCount { get; set; } = 1;

    /// <summary>加权抽取时的权重（越大越容易被抽中）。</summary>
    [Export] public float Weight { get; set; } = 1.0f;
}

/// <summary>
/// 掉落表：一组掉落规则，用于箱子 / 尸体 / 敌人掉落。
/// 与具体容器解耦，所以可以挂到任意 LootContainer 或 EnemyData 上。
/// </summary>
[GlobalClass]
public partial class LootTable : Resource
{
    /// <summary>掉落规则列表。</summary>
    [Export] public Godot.Collections.Array<LootEntry> Entries { get; set; } = new();

    /// <summary>额外做多少次加权抽取（0 = 不做）。</summary>
    [Export] public int WeightedPickCount { get; set; } = 0;

    public bool IsEmpty => Entries == null || Entries.Count == 0;

    /// <summary>掷一次掉落表，返回本次掉落的物品堆叠。</summary>
    public List<ItemStack> Roll()
    {
        var result = new List<ItemStack>();
        if (IsEmpty)
            return result;

        // 1) 独立概率判定
        foreach (var entry in Entries)
        {
            if (!IsUsable(entry)) continue;
            if (GD.Randf() > entry.Chance) continue;
            AddEntry(result, entry);
        }

        // 2) 加权抽取（不重复抽同一条）
        if (WeightedPickCount > 0)
        {
            var pool = new List<LootEntry>();
            foreach (var entry in Entries)
                if (IsUsable(entry) && entry.Weight > 0f)
                    pool.Add(entry);

            int picks = Mathf.Min(WeightedPickCount, pool.Count);
            for (int i = 0; i < picks; i++)
            {
                var picked = PickWeighted(pool);
                if (picked == null) break;
                pool.Remove(picked);
                AddEntry(result, picked);
            }
        }

        return result;
    }

    private static bool IsUsable(LootEntry entry)
        => entry != null && !string.IsNullOrWhiteSpace(entry.ItemId);

    private static void AddEntry(List<ItemStack> result, LootEntry entry)
    {
        var data = ItemDatabase.Instance?.GetItem(entry.ItemId);
        if (data == null)
        {
            GD.PushWarning($"LootTable: 未知物品 Id '{entry.ItemId}'，已跳过");
            return;
        }

        int lo = Mathf.Min(entry.MinCount, entry.MaxCount);
        int hi = Mathf.Max(entry.MinCount, entry.MaxCount);
        int count = Mathf.Max(1, (int)GD.RandRange(lo, hi));
        result.Add(new ItemStack(data, count));
    }

    private static LootEntry PickWeighted(List<LootEntry> pool)
    {
        float total = 0f;
        foreach (var e in pool)
            total += Mathf.Max(0f, e.Weight);
        if (total <= 0f)
            return null;

        float roll = GD.Randf() * total;
        float acc = 0f;
        foreach (var e in pool)
        {
            acc += Mathf.Max(0f, e.Weight);
            if (roll <= acc)
                return e;
        }
        return pool[pool.Count - 1];
    }
}
