using Godot;
using System.Collections.Generic;

/// <summary>
/// **配件的数值定义**：装在哪一类槽、改哪些属性、每条改多少。
///
/// 挂载位置：`ItemData.CategoryData`（配件类物品必须挂，且只有配件类能挂）。
/// 远程 / 近战都可以装（槽位定义在 `WeaponData` 基类上），靠**槽类型匹配**自然隔离。
///
/// ★ 这里只描述"改什么"，**不改任何运行时数值** ——
/// 真正的汇总在阶段 4：把枪的基础值和所有已装配件的修正器喂给 <see cref="StatMath.Apply"/>。
/// </summary>
[GlobalClass]
public partial class AttachmentData : Resource
{
	/// <summary>能装在哪种槽上，必须是 <see cref="SlotTypes"/> 里的 8 种之一。</summary>
	[Export] public string SlotType { get; set; } = SlotTypes.Muzzle;

	/// <summary>这个配件改的所有属性（每条一项）。</summary>
	[Export] public Godot.Collections.Array<AttachmentModifier> Modifiers { get; set; } = new();

	/// <summary>数据是否合法（槽类型合法 + 每条修正的属性名合法）。</summary>
	public bool IsValid
	{
		get
		{
			if (!SlotTypes.IsValid(SlotType)) return false;

			if (Modifiers != null)
				foreach (var m in Modifiers)
					if (m == null || !m.IsValid) return false;

			return true;
		}
	}

	/// <summary>槽类型的中文名。</summary>
	public string SlotDisplayName => SlotTypes.DisplayName(SlotType);

	/// <summary>
	/// 取出某个属性上的**全部**修正器（喂给 <see cref="StatMath.Apply"/>）。
	/// 这个配件不改该属性时返回空列表。
	/// </summary>
	public List<StatModifier> For(string statName)
	{
		var result = new List<StatModifier>();
		if (Modifiers == null || string.IsNullOrWhiteSpace(statName)) return result;

		foreach (var m in Modifiers)
		{
			if (m == null || m.StatName != statName) continue;
			result.Add(m.ToModifier());
		}
		return result;
	}

	/// <summary>这个配件涉及哪几个属性（UI 展开显示用，按出现顺序去重）。</summary>
	public List<string> ModifiedStats()
	{
		var result = new List<string>();
		if (Modifiers == null) return result;

		foreach (var m in Modifiers)
			if (m != null && !result.Contains(m.StatName)) result.Add(m.StatName);

		return result;
	}

	public override string ToString() => $"配件[{SlotType}] 改 {Modifiers?.Count ?? 0} 条";
}
