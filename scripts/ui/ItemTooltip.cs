using Godot;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 物品**悬停提示**的文字生成器：把一件物品的所有属性拼成多行文本，
/// 塞给 `Control.TooltipText` 就能用。
///
/// 覆盖：背包里的物品行、搜索容器里的物品行（见 EquipmentPanel）。
/// </summary>
public static class ItemTooltip
{
    /// <summary>按物品内容生成提示文本。</summary>
    public static string Build(ItemStack stack, string note = "")
    {
        if (stack?.Data == null) return "";

        var d = stack.Data;
        var sb = new StringBuilder();

        // ───── 标题 ─────
        sb.Append(d.Name);
        if (stack.Count > 1) sb.Append($" ×{stack.Count}");
        sb.Append('\n');

        // ───── 基础属性 ─────
        sb.Append($"稀有度：{ItemRarityInfo.GetDisplayName(d.Rarity)}\n");
        sb.Append($"类型：{CategoryName(d.Category)}\n");
        sb.Append($"单件 {d.Weight:0.###} kg  占 {d.GridW}×{d.GridH} 格\n");

        if (stack.Count > 1)
            sb.Append($"合计 {stack.TotalWeight:0.###} kg\n");

        sb.Append($"价值 {d.Value * Mathf.Max(1, stack.Count)}\n");

        // ───── 武器 ─────
        if (d.WeaponStats is RangedWeaponData ranged && ranged.GunStats != null)
        {
            var g = ranged.GunStats;
            sb.Append('\n');
            sb.Append($"伤害 {g.Damage:0.##}  射速 {g.FireRate:0.##} 发/秒\n");
            sb.Append($"弹匣 {g.MagazineSize}  换弹 {g.ReloadTime:0.##} s\n");
            sb.Append($"射程 {g.BulletRange:0} 像素  音噪 {g.NoiseRadius:0}\n");
            sb.Append($"腰射散布 {g.HipSpread:0.##}°  瞄准 {g.AimSpread:0.##}°\n");

            if (!string.IsNullOrWhiteSpace(g.Caliber))
                sb.Append($"口径：{CaliberName(g.Caliber)}\n");

            AppendSlots(sb, stack);
        }
        else if (d.WeaponStats is MeleeWeaponData melee && melee.MeleeStats != null)
        {
            sb.Append('\n');
            sb.Append($"近战伤害 {melee.MeleeStats.Damage:0.##}  冷却 {melee.MeleeStats.Cooldown:0.##} s\n");
        }

        // ───── 配件 ─────
        var attachment = d.AttachmentStats;
        if (attachment != null)
        {
            sb.Append('\n');
            sb.Append($"可装于：{attachment.SlotDisplayName}\n");

            if (attachment.Modifiers != null)
            {
                foreach (var m in attachment.Modifiers)
                {
                    if (m == null) continue;
                    if (Mathf.IsZeroApprox(m.Addend) && Mathf.IsZeroApprox(m.Multiplier - 1.0f)) continue;

                    var parts = new List<string>();
                    if (!Mathf.IsZeroApprox(m.Addend))
                        parts.Add($"{(m.Addend > 0 ? "+" : "")}{m.Addend:0.##}");
                    if (!Mathf.IsZeroApprox(m.Multiplier - 1.0f))
                        parts.Add($"×{m.Multiplier:0.###}");

                    if (parts.Count > 0)
                        sb.Append($"  {StatNames.DisplayName(m.StatName)}  {string.Join(" / ", parts)}\n");
                }
            }
        }

        // ───── 容器 ─────
        if (d.IsContainer && d.CategoryData is ContainerData container)
        {
            sb.Append('\n');
            sb.Append($"容量 {container.GridW}×{container.GridH}（{container.CellCount} 格）\n");
        }
        else if (stack.IsContainer && stack.Contents != null)
        {
            sb.Append('\n');
            sb.Append($"内容 {stack.Contents.UsedCells} / {stack.Contents.CellCount} 格  {stack.Contents.UsedWeight:0.##} kg\n");
        }

        // ───── 次数 ─────
        if (d.HasUses)
            sb.Append($"剩余次数 {stack.Uses}/{d.MaxUses}\n");

        if (d.HasDurability)
            sb.Append($"耐久 {stack.Durability:0}/{d.MaxDurability:0}\n");

        if (d.Rarity >= ItemRarity.Rare)
            sb.Append("★ 高价值物品\n");

        if (!string.IsNullOrWhiteSpace(note))
            sb.Append($"{note}\n");

        // ───── 描述 ─────
        if (!string.IsNullOrWhiteSpace(d.Description))
            sb.Append($"\n{d.Description}\n");

        // ───── 操作提示 ─────
        sb.Append('\n');
        if (stack.IsContainer)
            sb.Append("（拖到别处 / 右键丢弃）");
        else
            sb.Append("（拖动移动  双击快速拿走  右键菜单）");

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// **逐槽列出**这把枪的每个槽装着什么 —— 没装就显示「无」。
    /// 这样一眼能看出"哪几个槽还空着"。
    /// </summary>
    private static void AppendSlots(StringBuilder sb, ItemStack gun)
    {
        var weapon = gun?.Data?.WeaponStats;
        if (weapon?.Slots == null || weapon.Slots.Count == 0) return;

        // 运行时的槽（含已装配件），按数据里的顺序对齐
        var runtime = WeaponMods.Slots(gun);

        sb.Append("改装槽：\n");

        foreach (var data in weapon.Slots)
        {
            if (data == null) continue;

            // 容量 > 1 时同类型会有多个槽，全部列出来
            var contents = new List<string>();
            foreach (var slot in runtime)
            {
                if (slot.SlotType != data.SlotType) continue;
                contents.Add(slot.IsEmpty ? "无" : (slot.Installed.Data?.Name ?? "无"));
            }

            if (contents.Count == 0) contents.Add("无");

            sb.Append($"  {SlotTypes.DisplayName(data.SlotType)}：{string.Join(" / ", contents)}\n");
        }
    }

    /// <summary>弹药 Id → 显示名。</summary>
    private static string CaliberName(string ammoId)
        => ItemDatabase.Instance?.GetItem(ammoId)?.Name ?? ammoId;

    /// <summary>物品分类的中文名。</summary>
    public static string CategoryName(ItemCategory category) => category switch
    {
        ItemCategory.Misc => "杂物",
        ItemCategory.Weapon => "武器",
        ItemCategory.Attachment => "配件",
        ItemCategory.Ammo => "弹药",
        ItemCategory.Medical => "医疗",
        ItemCategory.Food => "食物",
        ItemCategory.Armor => "护甲",
        ItemCategory.Container => "容器",
        ItemCategory.Loot => "战利品",
        ItemCategory.Key => "钥匙",
        ItemCategory.Throwable => "投掷物",
        _ => category.ToString(),
    };
}
