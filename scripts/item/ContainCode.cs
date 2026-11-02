/// <summary>
/// 容器能否容纳某物品的判定结果。
///
/// 前 4 项对应 CDDA 的 <c>item_pocket::contain_code</c>
/// （已按需求去掉长度、刚性，以及液体 / 气体 / 弹种 / 配件 / 特性相关项）；
/// 后 3 项是本项目的树结构扩展（CDDA 没有深度上限，也不把循环当作结果码）。
///
/// ★ 关键区分：**「绝对放不下」和「只是现在满了」必须分开**——
///   否则 UI 无法区分「你该换容器」和「你该清容器」。
/// </summary>
public enum ContainCode
{
	Success,

	// ---------- 以下 4 项对应 CDDA contain_code ----------

	/// <summary>绝对：就算容器空着，单件也超重 → 得换容器。CDDA: ERR_TOO_HEAVY</summary>
	TooHeavy,

	/// <summary>绝对：就算容器空着，单件也太大 → 得换容器。CDDA: ERR_TOO_BIG</summary>
	TooBig,

	/// <summary>剩余：容器的**剩余承重**不够 → 清一清就能放。CDDA: ERR_CANNOT_SUPPORT</summary>
	CannotSupport,

	/// <summary>剩余：容器的**剩余格子**不够 → 清一清就能放。CDDA: ERR_NO_SPACE</summary>
	NoSpace,

	// ---------- 以下 3 项是本项目扩展 ----------

	/// <summary>目标不是容器，装不了东西。</summary>
	NotAContainer,

	/// <summary>会形成循环引用（把容器放进它自己内部）。</summary>
	WouldCycle,

	/// <summary>超过嵌套深度上限。</summary>
	TooDeep,
}

/// <summary>ContainCode 的中文说明（给 UI / 日志用）。</summary>
public static class ContainCodeText
{
	public static string Describe(ContainCode code) => code switch
	{
		ContainCode.Success => "可以放入",
		ContainCode.TooHeavy => "这件东西对容器来说太重了（换容器才行）",
		ContainCode.TooBig => "这件东西对容器来说太大了（换容器才行）",
		ContainCode.CannotSupport => "容器剩余承重不够（清一清）",
		ContainCode.NoSpace => "容器剩余空间不够（清一清）",
		ContainCode.NotAContainer => "目标不是容器",
		ContainCode.WouldCycle => "不能把容器放进它自己内部",
		ContainCode.TooDeep => "嵌套太深了",
		_ => code.ToString(),
	};

	/// <summary>是否属于「换容器才行」（绝对放不下）这一类。</summary>
	public static bool IsAbsolute(ContainCode code)
		=> code is ContainCode.TooHeavy or ContainCode.TooBig;

	/// <summary>是否属于「清一清就行」（剩余量不足）这一类。</summary>
	public static bool IsRemaining(ContainCode code)
		=> code is ContainCode.CannotSupport or ContainCode.NoSpace;

	/// <summary>极简说明（给列表行用，越短越好）。</summary>
	public static string Short(ContainCode code) => code switch
	{
		ContainCode.TooHeavy => "太重",
		ContainCode.TooBig => "太大",
		ContainCode.CannotSupport => "负重不够",
		ContainCode.NoSpace => "空间不够",
		ContainCode.NotAContainer => "没地方放",
		ContainCode.WouldCycle => "会成环",
		ContainCode.TooDeep => "嵌套太深",
		_ => "",
	};
}
