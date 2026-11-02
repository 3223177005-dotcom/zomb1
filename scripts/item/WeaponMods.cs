using Godot;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

/// <summary>配件装/卸的结果（带原因，UI 可以直接显示）。</summary>
public struct InstallResult
{
    public bool Ok;
    public string Reason;
    public WeaponSlot Slot;

    public static InstallResult Fail(string reason)
        => new InstallResult { Ok = false, Reason = reason, Slot = null };

    public static InstallResult Success(WeaponSlot slot)
        => new InstallResult { Ok = true, Reason = "", Slot = slot };
}

/// <summary>
/// **改枪系统**：管理每把枪的槽位状态、装/卸配件、汇总数值。
///
/// 分三层：
/// - <b>数据</b>：`WeaponData.Slots`（有哪些槽）+ `AttachmentData`（改哪些属性）—— 共享，不改
/// - <b>运行时</b>：本类用 <see cref="ConditionalWeakTable{TKey,TValue}"/> 按 <see cref="ItemStack"/>
///   实例存槽位状态，**不需要给 ItemStack 加字段**
/// - <b>数值</b>：<see cref="Apply"/> = <c>(base + Σaddend) × Πmultiplier</c>（见 <see cref="StatMath"/>）
/// </summary>
public static class WeaponMods
{
    /// <summary>每把枪的槽位状态（键是 ItemStack 实例，随实例回收）。</summary>
    private static readonly ConditionalWeakTable<ItemStack, List<WeaponSlot>> Table = new();

    /// <summary>
    /// 玩家当前手持的那把枪。由 <see cref="Player"/> 在切换武器时设置。
    ///
    /// ★ 单机 MVP 的简化：<see cref="Weapon"/> 开火时要用它算有效数值。
    ///   将来有多把枪同时开火（无人机/炮塔）时，改成显式传参。
    /// </summary>
    public static ItemStack Active { get; set; }

    // ---------- 槽位 ----------

    /// <summary>这把枪的槽位列表（首次访问时按 <see cref="WeaponData.Slots"/> 建好）。</summary>
    public static List<WeaponSlot> Slots(ItemStack gun)
    {
        if (gun?.Data == null) return new List<WeaponSlot>();

        if (Table.TryGetValue(gun, out var existing)) return existing;

        var created = new List<WeaponSlot>();
        var weapon = gun.Data.WeaponStats;

        if (weapon?.Slots != null)
        {
            foreach (var slotData in weapon.Slots)
            {
                if (slotData == null) continue;

                // 容量 > 1 时建出多个同类槽（当前数据一律 1）
                for (int i = 0; i < Mathf.Max(1, slotData.Capacity); i++)
                    created.Add(new WeaponSlot(slotData.SlotType));
            }
        }

        Table.Add(gun, created);
        return created;
    }

    /// <summary>这把枪有没有可装的槽。</summary>
    public static bool CanModify(ItemStack gun) => Slots(gun).Count > 0;

    /// <summary>找出装着的所有配件。</summary>
    public static List<ItemStack> Installed(ItemStack gun)
    {
        var result = new List<ItemStack>();
        foreach (var slot in Slots(gun))
            if (!slot.IsEmpty) result.Add(slot.Installed);
        return result;
    }

    // ---------- 装 / 卸 ----------

    /// <summary>这个配件能不能装到这把枪上（只看类型与空位，不改状态）。</summary>
    public static InstallResult CanInstall(ItemStack gun, ItemStack attachment)
    {
        if (gun?.Data == null) return InstallResult.Fail("不是武器");
        if (attachment?.Data == null) return InstallResult.Fail("不是物品");

        var weapon = gun.Data.WeaponStats;
        if (weapon == null) return InstallResult.Fail($"{gun.Data.Name} 不是武器");

        var data = attachment.Data.AttachmentStats;
        if (data == null) return InstallResult.Fail($"{attachment.Data.Name} 不是配件");

        if (!data.IsValid)
            return InstallResult.Fail($"{attachment.Data.Name} 的数据不合法");

        if (!weapon.HasSlotOfType(data.SlotType))
            return InstallResult.Fail(
                $"{gun.Data.Name} 没有「{SlotTypes.DisplayName(data.SlotType)}」槽");

        foreach (var slot in Slots(gun))
            if (slot.SlotType == data.SlotType && slot.IsEmpty)
                return InstallResult.Success(slot);

        return InstallResult.Fail($"「{SlotTypes.DisplayName(data.SlotType)}」槽已经装了东西");
    }

    /// <summary>
    /// 装上配件。成功时 <paramref name="attachment"/> 会被消耗（调用方负责从原容器移除）。
    /// </summary>
    public static InstallResult TryInstall(ItemStack gun, ItemStack attachment, out ItemStack stored)
    {
        stored = null;

        var check = CanInstall(gun, attachment);
        if (!check.Ok) return check;

        // ★ 装进去的必须是**副本**：原对象会被调用方从背包里 RemoveFrom，
        //   数量归零后 WeaponSlot.IsEmpty 会误判成"空槽"。
        var copy = new ItemStack(attachment.Data, 1);
        check.Slot.Installed = copy;
        stored = copy;
        return check;
    }

    /// <summary>卸下某个槽里的配件。</summary>
    public static ItemStack TryUninstall(WeaponSlot slot)
    {
        if (slot == null || slot.IsEmpty) return null;
        return slot.Take();
    }

    /// <summary>找出装在某类槽上的配件（没有返回 null）。</summary>
    public static ItemStack FirstOf(ItemStack gun, string slotType)
    {
        foreach (var slot in Slots(gun))
            if (slot.SlotType == slotType && !slot.IsEmpty) return slot.Installed;
        return null;
    }

    // ---------- 数值汇总 ----------

    /// <summary>收集**所有已装配件**对某个属性的修正器。</summary>
    public static List<StatModifier> Collect(ItemStack gun, string statName)
    {
        var result = new List<StatModifier>();
        if (string.IsNullOrWhiteSpace(statName)) return result;

        foreach (var slot in Slots(gun))
        {
            if (slot.IsEmpty) continue;

            var data = slot.Installed.Data?.AttachmentStats;
            if (data == null) continue;

            result.AddRange(data.For(statName));
        }
        return result;
    }

    /// <summary>
    /// 算出这把枪某个属性的**有效值**：
    /// <c>(基础值 + Σ所有配件的 addend) × Π所有配件的 multiplier</c>。
    /// </summary>
    public static float Apply(ItemStack gun, string statName, float baseValue)
        => StatMath.Apply(baseValue, Collect(gun, statName));

    // ---------- 便捷：常用属性 ----------

    public static float Inaccuracy(ItemStack gun, GunData data)
        => data == null ? 0f : Apply(gun, StatNames.Inaccuracy, data.HipSpread);

    public static float AimInaccuracy(ItemStack gun, GunData data)
        => data == null ? 0f : Apply(gun, StatNames.AimInaccuracy, data.AimSpread);

    public static float AimTime(ItemStack gun, GunData data)
        => data == null ? 0f : Mathf.Max(0.01f, Apply(gun, StatNames.Ads, data.AimTime));

    public static int MagazineSize(ItemStack gun, GunData data)
        => data == null ? 0 : Mathf.Max(1, Mathf.RoundToInt(Apply(gun, StatNames.MagazineSize, data.MagazineSize)));

    public static float ReloadTime(ItemStack gun, GunData data)
        => data == null ? 0f : Mathf.Max(0.05f, Apply(gun, StatNames.ReloadTime, data.ReloadTime));

    public static float NoiseRadius(ItemStack gun, GunData data)
        => data == null ? 0f : Mathf.Max(0f, Apply(gun, StatNames.Silence, data.NoiseRadius));

    /// <summary>配件的总自重（计入枪的负重）。</summary>
    public static float ExtraWeight(ItemStack gun)
    {
        float total = 0f;
        foreach (var item in Installed(gun)) total += item.TotalWeight;
        return total;
    }
}
