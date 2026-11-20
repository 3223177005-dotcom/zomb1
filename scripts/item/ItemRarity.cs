using Godot;

/// <summary>
/// 物品稀有度：六个等级，白 → 绿 → 蓝 → 紫 → 金 → 红。
/// 枚举值本身就是等级序号（0~5），可直接用于排序与比较。
/// </summary>
public enum ItemRarity
{
	/// <summary>白 —— 普通。</summary>
	Common = 0,

	/// <summary>绿 —— 精良。</summary>
	Uncommon = 1,

	/// <summary>蓝 —— 稀有。</summary>
	Rare = 2,

	/// <summary>紫 —— 史诗。</summary>
	Epic = 3,

	/// <summary>金 —— 传说。</summary>
	Legendary = 4,

	/// <summary>红 —— 神话。</summary>
	Mythic = 5,
}

/// <summary>
/// 稀有度的展示信息（颜色 / 中文名 / 等级）。
/// 与数据解耦：颜色只在这里定义，UI 直接取用，改配色不用碰物品数据。
/// </summary>
public static class ItemRarityInfo
{
	/// <summary>稀有度等级总数。</summary>
	public const int LevelCount = 6;

	/// <summary>按等级升序的全部稀有度。</summary>
	public static readonly ItemRarity[] All =
	{
		ItemRarity.Common,
		ItemRarity.Uncommon,
		ItemRarity.Rare,
		ItemRarity.Epic,
		ItemRarity.Legendary,
		ItemRarity.Mythic,
	};

	/// <summary>
	/// 稀有度展示颜色（**浅卡其底上的深色印章 / 军阶色**）。
	/// 浅色底板必须配深色字，否则文字会"发飘"；这组色同时也是格块边框色。
	/// </summary>
	public static Color GetColor(ItemRarity rarity) => rarity switch
	{
		ItemRarity.Uncommon => new Color(0.18f, 0.38f, 0.16f),   // 深军绿
		ItemRarity.Rare => new Color(0.14f, 0.31f, 0.45f),       // 深海军钢蓝
		ItemRarity.Epic => new Color(0.38f, 0.18f, 0.44f),       // 深勋章紫
		ItemRarity.Legendary => new Color(0.62f, 0.34f, 0.08f),  // 深古铜金
		ItemRarity.Mythic => new Color(0.64f, 0.13f, 0.11f),     // 兵工厂绯红
		_ => new Color(0.24f, 0.21f, 0.17f),                     // 深炭褐（普通）
	};

	/// <summary>稀有度中文名。</summary>
	public static string GetDisplayName(ItemRarity rarity) => rarity switch
	{
		ItemRarity.Uncommon => "精良",
		ItemRarity.Rare => "稀有",
		ItemRarity.Epic => "史诗",
		ItemRarity.Legendary => "传说",
		ItemRarity.Mythic => "神话",
		_ => "普通",
	};

	/// <summary>等级序号（0~5）。</summary>
	public static int GetTier(ItemRarity rarity) => (int)rarity;

	/// <summary>是否为有效稀有度（防止数据里写了非法值）。</summary>
	public static bool IsValid(ItemRarity rarity)
		=> (int)rarity >= 0 && (int)rarity < LevelCount;
}
