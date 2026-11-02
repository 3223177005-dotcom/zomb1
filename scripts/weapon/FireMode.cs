using System;

/// <summary>
/// 开火模式。用标志位组合，所以一把武器可以同时拥有多种。
/// 例：SupportedModes = FireMode.Auto | FireMode.Semi
/// </summary>
[Flags]
public enum FireMode
{
	None = 0,

	/// <summary>全自动：按住持续射击。</summary>
	Auto = 1,

	/// <summary>半自动：单击一发。</summary>
	Semi = 2,

	/// <summary>栓动：单击一发，但射速很低（拉栓时间靠低 FireRate 表达）。</summary>
	Bolt = 4,

	/// <summary>点射：单击打固定发数。</summary>
	Burst = 8,
}

/// <summary>开火模式的显示名与循环顺序。</summary>
public static class FireModeInfo
{
	/// <summary>
	/// 按 B 键循环切换的顺序（火力从强到弱，参考战地的做法）。
	/// 注意：这个顺序与枚举的位顺序无关，是单独定义的手感顺序。
	/// </summary>
	public static readonly FireMode[] CycleOrder =
	{
		FireMode.Auto,
		FireMode.Burst,
		FireMode.Semi,
		FireMode.Bolt,
	};

	/// <summary>显示名（HUD 直接用）。</summary>
	public static string GetDisplayName(FireMode mode) => mode switch
	{
		FireMode.Auto => "全自动",
		FireMode.Burst => "点射",
		FireMode.Semi => "半自动",
		FireMode.Bolt => "栓动",
		_ => "—",
	};

	/// <summary>组合里是否包含某个模式。</summary>
	public static bool Has(FireMode set, FireMode mode) => (set & mode) != 0;

	/// <summary>支持的模式数量。</summary>
	public static int Count(FireMode set)
	{
		int n = 0;
		foreach (var m in CycleOrder)
			if (Has(set, m)) n++;
		return n;
	}

	/// <summary>按循环顺序取第一个已支持的模式；没有则返回 None。</summary>
	public static FireMode First(FireMode set)
	{
		foreach (var m in CycleOrder)
			if (Has(set, m)) return m;
		return FireMode.None;
	}

	/// <summary>按循环顺序取下一个已支持的模式（会跳过不支持的）。</summary>
	public static FireMode Next(FireMode set, FireMode current)
	{
		int start = Array.IndexOf(CycleOrder, current);
		for (int i = 1; i <= CycleOrder.Length; i++)
		{
			var candidate = CycleOrder[(start + i) % CycleOrder.Length];
			if (Has(set, candidate)) return candidate;
		}
		return current;
	}
}
