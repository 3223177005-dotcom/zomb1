/// <summary>
/// Stats 字典里的标准字段名。
/// 集中定义，避免各处散落魔法字符串；也方便将来做改名/迁移。
/// </summary>
public static class ItemStats
{
    // ---------- 医疗（Medical）----------
    /// <summary>每次使用的治疗量。</summary>
    public const string HealAmount = "HealAmount";
    /// <summary>可用次数（消耗品用 1）。</summary>
    public const string MaxUses = "MaxUses";
    /// <summary>单次使用耗时（秒）。</summary>
    public const string UseTime = "UseTime";
    /// <summary>是否止血。</summary>
    public const string StopsBleeding = "StopsBleeding";
    /// <summary>是否治疗感染。</summary>
    public const string CuresInfection = "CuresInfection";
    /// <summary>是否解除疼痛。</summary>
    public const string RemovesPain = "RemovesPain";
    /// <summary>修复护甲耐久（修复类医疗物品使用）。</summary>
    public const string RepairAmount = "RepairAmount";

    // ---------- 护甲（Armor）----------
    /// <summary>最大耐久（运行时从最大值开始扣）。</summary>
    public const string MaxDurability = "MaxDurability";
    /// <summary>防护等级。</summary>
    public const string ArmorClass = "ArmorClass";
    /// <summary>装备槽位（Helmet / Body ...）。</summary>
    public const string Slot = "Slot";

    // ---------- 弹药（Ammo）----------
    /// <summary>口径。</summary>
    public const string Caliber = "Caliber";
    /// <summary>单发伤害。</summary>
    public const string AmmoDamage = "AmmoDamage";

    // ---------- 食物（Food）----------
    /// <summary>恢复饱食度。</summary>
    public const string HungerRestore = "HungerRestore";
    /// <summary>恢复水分。</summary>
    public const string ThirstRestore = "ThirstRestore";

    // ---------- 钥匙（Key）----------
    /// <summary>对应的锁 Id。</summary>
    public const string KeyId = "KeyId";

    // ---------- 武器（Weapon）----------
    /// <summary>口径（与弹药匹配）。</summary>
    public const string WeaponCaliber = "WeaponCaliber";
    /// <summary>弹匣容量。</summary>
    public const string MagazineSize = "MagazineSize";
}
