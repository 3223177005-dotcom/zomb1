using Godot;
using System.Collections.Generic;

/// <summary>
/// 丧尸尸体的战利品生成。
///
/// 简单加权：医疗最常见、武器/背包少见、稀有度越高越少。
/// 将来接 <see cref="LootTable"/>（数据驱动）时，把 <see cref="Roll"/> 换成读表即可。
/// </summary>
public static class ZombieLoot
{
    /// <summary>各物品的掉落权重（Id → 权重）。</summary>
    private static readonly (string id, int weight)[] Table =
    {
        ("bandage", 100),   // 医疗：最常见
        ("dagger", 40),     // 近战武器
        ("svd", 14),        // 枪：少见
        ("backpack", 8),    // 容器：更少见
        ("cash", 12),       // ↓ 高价值战利品：概率低，但搜到就赚
        ("circuit", 7),
        ("gold", 3),
        ("intel", 1),       // 最值钱，也最罕见
        ("tacgrip", 9),     // ↓ 枪械配件（SVD 能装）
        ("silencer", 5),
        ("extmag", 5),
        ("ammo_762x54r", 45),   // 7.62×54R 弹药（SVD 口径）
    };

    /// <summary>一具尸体最少 / 最多种类数（**保证不止一种**）。</summary>
    private const int MinKinds = 2;
    private const int MaxKinds = 3;

    /// <summary>摇一次掉落，返回要放进容器里的堆叠（**种类互不重复**）。</summary>
    public static List<ItemStack> Roll()
    {
        var result = new List<ItemStack>();
        var db = ItemDatabase.Instance;
        if (db == null) return result;

        int total = 0;
        foreach (var (_, weight) in Table) total += weight;

        int kinds = GD.RandRange(MinKinds, MaxKinds);
        var picked = new HashSet<string>();

        // 最多尝试几十次，避免小概率死循环
        for (int attempt = 0; attempt < 60 && picked.Count < kinds; attempt++)
        {
            var item = Pick(db, total);
            if (item == null || !picked.Add(item.Id)) continue;

            int count = item.StackLimit > 1 ? GD.RandRange(1, item.StackLimit) : 1;
            result.Add(new ItemStack(item, count));
        }
        return result;
    }

    /// <summary>
    /// **房间箱子**的掉落表：只出**金（传说）**和**红（神话）**两档。
    /// 箱子数量固定、每局重新刷新，所以它是这局里最值钱的目标。
    /// </summary>
    private static readonly (string id, int weight)[] ChestTable =
    {
        ("watch", 65),     // 金  传说
        ("sample", 35),    // 红  神话
    };

    /// <summary>摇一个房间箱子的内容：1~2 件，种类不重复。</summary>
    public static List<ItemStack> RollChest()
    {
        var result = new List<ItemStack>();
        var db = ItemDatabase.Instance;
        if (db == null) return result;

        int total = 0;
        foreach (var (_, weight) in ChestTable) total += weight;

        int kinds = GD.RandRange(1, 2);
        var picked = new HashSet<string>();

        for (int attempt = 0; attempt < 40 && picked.Count < kinds; attempt++)
        {
            int roll = GD.RandRange(0, total - 1);
            foreach (var (id, weight) in ChestTable)
            {
                roll -= weight;
                if (roll >= 0) continue;

                if (!picked.Add(id)) break;

                var item = db.GetItem(id);
                if (item != null) result.Add(new ItemStack(item, 1));
                break;
            }
        }
        return result;
    }

    private static ItemData Pick(ItemDatabase db, int totalWeight)
    {
        int roll = GD.RandRange(0, totalWeight - 1);

        foreach (var (id, weight) in Table)
        {
            roll -= weight;
            if (roll >= 0) continue;

            var item = db.GetItem(id);
            if (item != null) return item;
        }
        return null;
    }
}
