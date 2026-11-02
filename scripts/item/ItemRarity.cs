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

	/// <summary>稀有度对应的展示颜色（深色背景下可辨识）。</summary>
	public static Color GetColor(ItemRarity rarity) => rarity switch
	{
		ItemRarity.Uncommon => new Color(0.45f, 0.85f, 0.40f),   // 绿
		ItemRarity.Rare => new Color(0.35f, 0.62f, 0.95f),       // 蓝
		ItemRarity.Epic => new Color(0.72f, 0.42f, 0.92f),       // 紫
		ItemRarity.Legendary => new Color(0.96f, 0.80f, 0.26f),  // 金
		ItemRarity.Mythic => new Color(0.92f, 0.25f, 0.28f),     // 红
		_ => new Color(0.86f, 0.86f, 0.86f),                     // 白（普通）
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
