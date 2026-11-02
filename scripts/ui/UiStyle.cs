using Godot;

/// <summary>
/// UI 的统一样式约定。
///
/// ★ 核心规则：**任何显示物品名的地方，文字颜色都必须取自稀有度**
///   （<see cref="ItemRarityInfo.GetColor"/>），不要在各自的 UI 里写死颜色。
///   新增显示物品名的界面时，一律走这里的 <see cref="ItemText"/> / <see cref="BbItemName"/>。
/// </summary>
public static class UiStyle
{
    /// <summary>未选中 / 次要状态下保留色相的调暗系数。</summary>
    public const float DimFactor = 0.42f;

    /// <summary>空槽 / 无物品的占位颜色。</summary>
    public static Color EmptySlotColor { get; } = new Color(0.45f, 0.46f, 0.50f);

    /// <summary>标题 / 说明文字的颜色。</summary>
    public static Color CaptionColor { get; } = new Color(0.62f, 0.64f, 0.70f);

    /// <summary>物品名文字色 = 该物品的**稀有度颜色**。</summary>
    public static Color ItemText(ItemData data)
        => data != null ? ItemRarityInfo.GetColor(data.Rarity) : EmptySlotColor;

    /// <summary>
    /// 未选中 / 非当前项的物品名颜色：同一稀有度色**调暗**。
    /// 保留色相，所以仍然能辨认稀有度，同时又能区分选中状态。
    /// </summary>
    public static Color ItemTextDimmed(ItemData data) => Dim(ItemText(data));

    /// <summary>把颜色整体调暗（保持色相）。</summary>
    public static Color Dim(Color c, float factor = DimFactor)
        => new Color(c.R * factor, c.G * factor, c.B * factor, c.A);

    /// <summary>转成 BBCode 用的颜色串（RichTextLabel 用）。</summary>
    public static string BbColor(Color c) => "#" + c.ToHtml(false);

    /// <summary>把物品名包成带稀有度颜色的 BBCode。</summary>
    public static string BbItemName(ItemData data, string suffix = "")
        => $"[color={BbColor(ItemText(data))}]{data?.Name}{suffix}[/color]";

    /// <summary>同上，但**调暗**（表示未选中 / 不可用，色相保留）。</summary>
    public static string BbItemNameDimmed(ItemData data, string suffix = "")
        => $"[color={BbColor(ItemTextDimmed(data))}]{data?.Name}{suffix}[/color]";

    /// <summary>把普通文字包成 BBCode 颜色。</summary>
    public static string BbText(string text, Color color)
        => $"[color={BbColor(color)}]{text}[/color]";
}
