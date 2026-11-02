using Godot;

/// <summary>
/// 物品的静态定义（数据层核心）。
///
/// 设计要点：
/// 1. 通用字段（身份/外观/格宽高/重量/堆叠/价值）对所有物品一致。
/// 2. 分类特有的数据放在 <see cref="Stats"/> 字典里 —— 新增物品类型或字段只需改数据，
///    不用改这个类，也不用改加载代码。
/// 3. 需要挂载“资源型”分类数据时（例如武器引用 GunData），用 <see cref="CategoryData"/>。
///
/// 注意：Godot 4 的 C# 导出属性保持 PascalCase，因此数据文件（.tres / JSON 映射）
/// 里对应的键名也必须是 PascalCase（历史上这里踩过坑）。
/// </summary>
[GlobalClass]
public partial class ItemData : Resource
{
    // ---------- 身份 ----------
    [Export] public string Id { get; set; } = "";
    [Export] public string Name { get; set; } = "";
    [Export] public ItemCategory Category { get; set; } = ItemCategory.Misc;
    /// <summary>稀有度：白 / 绿 / 蓝 / 紫 / 金 / 红。</summary>
    [Export] public ItemRarity Rarity { get; set; } = ItemRarity.Common;
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";

    // ---------- 外观 ----------
    /// <summary>
    /// **占几列**（格子背包用）。和 <see cref="GridH"/> 一起决定在容器里占多大块。
    /// </summary>
    [Export] public int GridW { get; set; } = 1;

    /// <summary>**占几行**。</summary>
    [Export] public int GridH { get; set; } = 1;

    /// <summary>图标资源路径（数据文件里填写，加载后填充 Icon）。</summary>
    [Export] public string IconPath { get; set; } = "";
    /// <summary>实际图标纹理（由 ItemDatabase 依据 IconPath 载入）。</summary>
    [Export] public Texture2D Icon { get; set; }

    // ---------- 重量与物理 ----------
    /// <summary>单件重量（kg）。</summary>
    [Export] public float Weight { get; set; } = 0.1f;
    /// <summary>单堆叠上限（1 表示不可堆叠）。</summary>
    [Export] public int StackLimit { get; set; } = 1;

    // ---------- 经济 ----------
    /// <summary>单价（用于变现 / 结算统计）。</summary>
    [Export] public int Value { get; set; } = 0;

    // ---------- 扩展位 ----------
    /// <summary>
    /// 分类特有的简单数据。示例：
    /// 医疗 → { "HealAmount": 15.0, "UseTime": 1.5, "StopsBleeding": true }
    /// 弹药 → { "Caliber": "5.56", "Damage": 32.0 }
    /// 护甲 → { "ArmorClass": 2, "MaxDurability": 40.0 }
    /// 容器的内部格子不走这里，走 CategoryData（ContainerData.GridW / GridH）。
    /// </summary>
    [Export] public Godot.Collections.Dictionary Stats { get; set; } = new();

    /// <summary>
    /// 分类特有的资源引用（按 Category 解释）。例如武器可以在这里挂 GunData。
    /// 这样物品层不需要认识具体武器类型也能携带它。
    /// </summary>
    [Export] public Resource CategoryData { get; set; }

    // ---------- 便捷访问器（避免各处直接操作 Variant） ----------

    public bool HasStat(string key) => Stats != null && Stats.ContainsKey(key);

    public float GetStatFloat(string key, float fallback = 0f)
        => HasStat(key) ? (float)Stats[key].AsDouble() : fallback;

    public int GetStatInt(string key, int fallback = 0)
        => HasStat(key) ? Stats[key].AsInt32() : fallback;

    public bool GetStatBool(string key, bool fallback = false)
        => HasStat(key) ? Stats[key].AsBool() : fallback;

    public string GetStatString(string key, string fallback = "")
        => HasStat(key) ? Stats[key].AsString() : fallback;

    // ---------- 稀有度便捷访问 ----------

    /// <summary>稀有度展示颜色（UI 直接用）。</summary>
    public Color RarityColor => ItemRarityInfo.GetColor(Rarity);

    /// <summary>稀有度中文名（普通 / 精良 / 稀有 / 史诗 / 传说 / 神话）。</summary>
    public string RarityName => ItemRarityInfo.GetDisplayName(Rarity);

    /// <summary>稀有度等级序号（0~5）。</summary>
    public int RarityTier => ItemRarityInfo.GetTier(Rarity);

    // ---------- 分类特定便捷访问（字段名集中在 ItemStats） ----------

    /// <summary>护甲最大耐久（非护甲为 0）。</summary>
    public float MaxDurability => GetStatFloat(ItemStats.MaxDurability);

    /// <summary>护甲防护等级。</summary>
    public int ArmorClass => GetStatInt(ItemStats.ArmorClass);

    /// <summary>药物可用次数（非药物为 0）。</summary>
    public int MaxUses => GetStatInt(ItemStats.MaxUses);

    /// <summary>药物单次治疗量。</summary>
    public float HealAmount => GetStatFloat(ItemStats.HealAmount);

    /// <summary>护甲修复量（修复类医疗物品）。</summary>
    public float RepairAmount => GetStatFloat(ItemStats.RepairAmount);

    /// <summary>弹药口径。</summary>
    public string Caliber => GetStatString(ItemStats.Caliber);

    /// <summary>是否带耐久（护甲类）。</summary>
    public bool HasDurability => MaxDurability > 0f;

    /// <summary>是否带使用次数（药物类）。</summary>
    public bool HasUses => MaxUses > 0;

    /// <summary>是否为护甲修复类物品（医疗类，修复护甲耐久）。</summary>
    public bool IsRepairItem => RepairAmount > 0f;

    // ---------- 武器（远程 / 近战） ----------

    /// <summary>武器数据（仅武器类；CategoryData 需为 WeaponData 子类）。</summary>
    public WeaponData WeaponStats => CategoryData as WeaponData;

    /// <summary>是否武器类。</summary>
    public bool IsWeapon => Category == ItemCategory.Weapon;

    /// <summary>是否远程武器（必须同时是武器类）。</summary>
    public bool IsRangedWeapon => IsWeapon && WeaponStats?.Kind == WeaponKind.Ranged;

    /// <summary>是否近战武器（必须同时是武器类）。</summary>
    public bool IsMeleeWeapon => IsWeapon && WeaponStats?.Kind == WeaponKind.Melee;

    /// <summary>武器种类（非武器返回 None）。</summary>
    public WeaponKind WeaponKind => WeaponStats?.Kind ?? WeaponKind.None;

    // ---------- 配件（改装件） ----------

    /// <summary>配件数据（仅配件类；CategoryData 需为 AttachmentData）。</summary>
    public AttachmentData AttachmentStats => CategoryData as AttachmentData;

    /// <summary>是否配件类。</summary>
    public bool IsAttachment => Category == ItemCategory.Attachment;

    // ---------- 容器 ----------

    /// <summary>容器数据（仅容器类；CategoryData 需为 ContainerData）。约定每个容器只有一个仓。</summary>
    public ContainerData ContainerStats => CategoryData as ContainerData;

    /// <summary>是否容器（能装东西）。</summary>
    public bool IsContainer => Category == ItemCategory.Container && ContainerStats != null;

    /// <summary>容器内部有几列（格子背包）。非容器为 0。</summary>
    public int ContainerGridW => ContainerStats?.GridW ?? 0;

    /// <summary>容器内部有几行。非容器为 0。</summary>
    public int ContainerGridH => ContainerStats?.GridH ?? 0;

    /// <summary>容器内部总格数。</summary>
    public int ContainerCellCount => ContainerGridW * ContainerGridH;

    // ---------- 派生属性 ----------

    /// <summary>是否可堆叠（只有<b>弹药</b>和<b>药品</b>类可以堆叠，见 <see cref="CanCategoryStack"/>）。</summary>
    public bool IsStackable => StackLimit > 1;

    /// <summary>
    /// 允许堆叠的分类 —— **只有这些分类**的 StackLimit 可以大于 1。
    ///
    /// 弹药和药品都是一次性的小件（子弹、绷带），按"叠"管理更合理；
    /// 武器 / 护甲 / 容器这些有独立状态的按单件管理。
    /// 要放开新分类，往这里加一个即可。
    /// </summary>
    public static bool CanCategoryStack(ItemCategory category)
        => category is ItemCategory.Ammo or ItemCategory.Medical;

    /// <summary>一叠满编时的总重量。</summary>
    public float WeightAtFullStack => Weight * StackLimit;

    /// <summary>按数量计算总重量。</summary>
    public float GetTotalWeight(int count) => Weight * count;

    /// <summary>按数量计算总价值。</summary>
    public int GetTotalValue(int count) => Value * count;

    /// <summary>
    /// 数据是否合法（供数据层校验）。包含基础字段 + 分类规则：
    /// - 格宽 / 格高必须 >= 1（容器内部格子同理）；
    /// - 只有弹药和药品类可以堆叠（StackLimit > 1）；
    /// - 武器数据只能挂在武器类物品上（近战 / 远程武器必须是 Weapon 类）；
    /// - 配件数据只能挂在配件类物品上；
    /// - 护甲类必须有耐久上限（MaxDurability）；
    /// - 药物类必须有使用次数（MaxUses），以及治疗量或护甲修复量（至少其一）；
    /// - 武器类必须挂 WeaponData（远程 RangedWeaponData / 近战 MeleeWeaponData）；
    /// - 配件类必须挂 AttachmentData，且指定配件类型（AttachmentType 非空）。
    /// </summary>
    public bool IsValid(out string reason)
    {
        // ---------- 基础字段 ----------
        if (string.IsNullOrWhiteSpace(Id))
        {
            reason = "缺少 Id";
            return false;
        }
        if (StackLimit < 1)
        {
            reason = "StackLimit 必须 >= 1";
            return false;
        }
        if (Weight < 0f)
        {
            reason = "Weight 不能为负";
            return false;
        }
        if (GridW < 1 || GridH < 1)
        {
            reason = $"GridW / GridH 必须 >= 1（当前 {GridW}×{GridH}）";
            return false;
        }

        // ---------- 规则：只有弹药 / 药品可以堆叠 ----------
        if (StackLimit > 1 && !CanCategoryStack(Category))
        {
            reason = $"只有弹药和药品可以堆叠，但「{Category}」把 StackLimit 设成了 {StackLimit}（请改为 1）";
            return false;
        }

        // ---------- 规则：武器数据只能挂在武器类物品上 ----------
        // 即：近战 / 远程武器必须是 Weapon 类；杂物等其它分类不可以是武器。
        if (CategoryData is WeaponData && Category != ItemCategory.Weapon)
        {
            reason = $"「{Category}」类物品不能挂武器数据（{CategoryData.GetType().Name}）—— 近战 / 远程武器必须是「{ItemCategory.Weapon}」类物品";
            return false;
        }

        // ---------- 规则：配件数据只能挂在配件类物品上 ----------
        if (CategoryData is AttachmentData && Category != ItemCategory.Attachment)
        {
            reason = $"「{Category}」类物品不能挂配件数据（{CategoryData.GetType().Name}）—— 配件必须是「{ItemCategory.Attachment}」类物品";
            return false;
        }

        // ---------- 规则：容器数据只能挂在容器类物品上 ----------
        if (CategoryData is ContainerData && Category != ItemCategory.Container)
        {
            reason = $"「{Category}」类物品不能挂容器数据（{CategoryData.GetType().Name}）—— 容器类物品（含背包）必须是「{ItemCategory.Container}」类";
            return false;
        }

        // ---------- 规则：分类必填字段 ----------
        switch (Category)
        {
            case ItemCategory.Armor:
                if (MaxDurability <= 0f)
                {
                    reason = $"护甲类必须有耐久（Stats.{ItemStats.MaxDurability} > 0）";
                    return false;
                }
                break;

            case ItemCategory.Medical:
                if (MaxUses <= 0)
                {
                    reason = $"药物类必须有使用次数（Stats.{ItemStats.MaxUses} > 0）";
                    return false;
                }
                // 医疗类至少有其一：治疗量（治人）或护甲修复量（修护甲）
                if (HealAmount <= 0f && RepairAmount <= 0f)
                {
                    reason = $"药物类必须有治疗量（Stats.{ItemStats.HealAmount}）或护甲修复量（Stats.{ItemStats.RepairAmount}），至少其一 > 0";
                    return false;
                }
                break;

            case ItemCategory.Weapon:
                if (WeaponStats == null)
                {
                    reason = "武器类必须挂 WeaponData（CategoryData 需为 RangedWeaponData 或 MeleeWeaponData）";
                    return false;
                }
                break;

            case ItemCategory.Attachment:
                if (AttachmentStats == null)
                {
                    reason = "配件类必须挂 AttachmentData（CategoryData 需为 AttachmentData）";
                    return false;
                }
                if (!SlotTypes.IsValid(AttachmentStats.SlotType))
                {
                    reason = $"配件的 SlotType 非法：「{AttachmentStats.SlotType}」" +
                             $"（必须是 {string.Join(" / ", SlotTypes.All)} 之一）";
                    return false;
                }
                if (AttachmentStats.Modifiers != null)
                {
                    foreach (var modifier in AttachmentStats.Modifiers)
                    {
                        if (modifier == null)
                        {
                            reason = "配件的 Modifiers 里有空项";
                            return false;
                        }
                        if (!StatNames.IsValid(modifier.StatName))
                        {
                            reason = $"配件改了非法的属性名：「{modifier.StatName}」" +
                                     $"（必须是 {string.Join(" / ", StatNames.All)} 之一）";
                            return false;
                        }
                    }
                }
                break;

            case ItemCategory.Container:
                if (ContainerStats == null)
                {
                    reason = "容器类必须挂 ContainerData（CategoryData 需为 ContainerData）";
                    return false;
                }
                if (ContainerStats.GridW < 1 || ContainerStats.GridH < 1)
                {
                    reason = $"容器的内部格子必须 >= 1×1（ContainerData.GridW / GridH，当前 {ContainerStats.GridW}×{ContainerStats.GridH}）";
                    return false;
                }
                break;
        }

        reason = "";
        return true;
    }

    public override string ToString() => $"{Name}({Id}) [{Category}]";
}
