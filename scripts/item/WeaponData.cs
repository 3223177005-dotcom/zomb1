using Godot;

/// <summary>武器种类：远程 / 近战。</summary>
public enum WeaponKind
{
	/// <summary>未分类（基类默认值，不应直接使用）。</summary>
	None,

	/// <summary>远程武器（枪械等）。</summary>
	Ranged,

	/// <summary>近战武器（刀、棍、斧等）。</summary>
	Melee,
}

/// <summary>
/// 武器数据基类：两种武器共有的部分。
///
/// 挂载位置：ItemData.CategoryData（武器类物品必须挂，且必须是 WeaponData 的子类）。
///
/// 职责分工：
/// - ItemData  → 背包 / 搜刮 / 价值 / 占用格子
/// - WeaponData → 战斗属性（本类体系），改枪系统只动这里，不碰物品系统
/// </summary>
[GlobalClass]
public partial class WeaponData : Resource
{
	/// <summary>
	/// 武器种类。由子类确定，不单独存储 —— 避免“数据写的种类和实际类型不一致”。
	/// </summary>
	public virtual WeaponKind Kind => WeaponKind.None;

	/// <summary>种类中文名（UI 直接用）。</summary>
	public string KindName => Kind switch
	{
		WeaponKind.Ranged => "远程武器",
		WeaponKind.Melee => "近战武器",
		_ => "武器",
	};

	/// <summary>
	/// 手持该武器时的移动速度倍率（1 = 不减速；0.9 = 慢 10%；1.1 = 快 10%）。
	/// 放在基类上 —— 所以远程武器和近战武器都可以配。
	/// </summary>
	[Export] public float MoveSpeedMultiplier { get; set; } = 1.0f;

	/// <summary>
	/// 改装槽位列表。
	/// 放在基类上 —— 所以远程武器和近战武器**都可以改装**。
	/// 每个槽位声明槽类型（<see cref="SlotTypes"/> 的 8 种之一），只有类型匹配的配件才装得上。
	/// **没写在这里的槽 = 关闭。**
	/// </summary>
	[Export] public Godot.Collections.Array<WeaponSlotData> Slots { get; set; } = new();

	/// <summary>该武器是否可改装（至少有一个槽位）。</summary>
	public bool CanModify => Slots != null && Slots.Count > 0;

	/// <summary>槽位数量。</summary>
	public int SlotCount => Slots?.Count ?? 0;

	/// <summary>是否开放了某种槽。</summary>
	public bool HasSlotOfType(string slotType)
		=> FindSlotByType(slotType) != null;

	/// <summary>按槽类型查找槽位定义（找不到返回 null）。</summary>
	public WeaponSlotData FindSlotByType(string slotType)
	{
		if (string.IsNullOrWhiteSpace(slotType) || Slots == null)
			return null;

		foreach (var slot in Slots)
		{
			if (slot != null && slot.SlotType == slotType)
				return slot;
		}
		return null;
	}

	/// <summary>
	/// 匹配判定：该配件能否装到这把武器上（**只看槽类型是否开放**）。
	/// 还要看"槽有没有空位"的话，得问运行时的枪实例 —— 见阶段 3。
	/// </summary>
	public bool CanFit(AttachmentData attachment)
		=> attachment != null && FindSlotByType(attachment.SlotType) != null;

	/// <summary>找到能装该配件的槽位定义（找不到返回 null）。</summary>
	public WeaponSlotData FindSlotFor(AttachmentData attachment)
		=> attachment == null ? null : FindSlotByType(attachment.SlotType);

	// 注意：具体的「安装」动作（写入槽位、占用状态、属性生效）尚未实现。

	// TODO(框架待补)：两种武器共有的字段（基础伤害 / 攻速 / 耐久消耗 等）
}
