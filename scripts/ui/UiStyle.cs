using Godot;

/// <summary>
/// UI 的统一样式约定 + 末日像素风九宫格（9-slice）皮肤工厂。
///
/// ★ 核心规则：**任何显示物品名的地方，文字颜色都必须取自稀有度**
///   （<see cref="ItemRarityInfo.GetColor"/>），不要在各自的 UI 里写死颜色。
///   新增显示物品名的界面时，一律走这里的 <see cref="ItemText"/> / <see cref="BbItemName"/>。
/// </summary>
public static class UiStyle
{
    /// <summary>未选中 / 不可用时，向空槽色混合的比例（浅底上"褪色油墨"，不是变黑）。</summary>
    public const float DimFactor = 0.52f;

    /// <summary>空槽 / 无物品的占位颜色（浅褐压痕色）。</summary>
    public static Color EmptySlotColor { get; } = new Color(0.52f, 0.47f, 0.39f);

    /// <summary>标题 / 说明文字的颜色（深胡桃褐）。</summary>
    public static Color CaptionColor { get; } = new Color(0.36f, 0.31f, 0.25f);

    /// <summary>主标题色（铁锈暗红印章色，浅卡其底上很有档案感）。</summary>
    public static Color AmberHeader { get; } = new Color(0.48f, 0.18f, 0.12f);

    /// <summary>物品名文字色 = 该物品的**稀有度颜色**。</summary>
    public static Color ItemText(ItemData data)
        => data != null ? ItemRarityInfo.GetColor(data.Rarity) : EmptySlotColor;

    /// <summary>未选中 / 非当前项的物品名颜色：同一稀有度色**调暗**。</summary>
    public static Color ItemTextDimmed(ItemData data) => Dim(ItemText(data));

    /// <summary>
    /// 把颜色向 <see cref="EmptySlotColor"/> 靠拢（浅底上表现"褪色油墨"，而不是乘系数变黑）。
    /// </summary>
    public static Color Dim(Color c, float factor = DimFactor) => c.Lerp(EmptySlotColor, factor);

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

    // ---------- 耐久状态配色（护甲 / 头盔 / 背包里的备用甲共用一套阈值） ----------

    /// <summary>耐久 &gt; 60%：完好（深陆军绿，浅底上够沉）。</summary>
    public static Color DurabilityGood { get; } = new Color(0.22f, 0.42f, 0.16f);

    /// <summary>耐久 25%~60%：受损（深琥珀橙）。</summary>
    public static Color DurabilityWorn { get; } = new Color(0.62f, 0.36f, 0.08f);

    /// <summary>耐久 ≤ 25%：濒危（深铁锈红）。</summary>
    public static Color DurabilityCritical { get; } = new Color(0.66f, 0.16f, 0.13f);

    /// <summary>耐久归零（破甲）：暗血红。</summary>
    public static Color DurabilityBroken { get; } = new Color(0.56f, 0.12f, 0.10f);

    /// <summary>按耐久比例 / 是否破损取文字与耐久条颜色（阈值只此一处）。</summary>
    public static Color DurabilityColor(float ratio, bool broken)
    {
        if (broken || ratio <= 0.0f) return DurabilityBroken;
        if (ratio <= 0.25f) return DurabilityCritical;
        if (ratio <= 0.60f) return DurabilityWorn;
        return DurabilityGood;
    }

    /// <summary>该耐久比例对应的进度条填充贴图（需要换档时调用 <see cref="StyleDurabilityBar"/>）。</summary>
    public static string DurabilityFill(float ratio)
        => ratio <= 0.25f ? "bar_hp.png" : (ratio <= 0.60f ? "bar_amber.png" : "bar_armor.png");

    /// <summary>按耐久比例切换进度条填充（会重建 StyleBox，**只在档位变化时调**）。</summary>
    public static void StyleDurabilityBar(ProgressBar bar, float ratio)
        => StylePixelBar(bar, DurabilityFill(ratio));

    // ---------- 末日像素风九宫格（StyleBoxTexture） ----------

    private const string UiDir = "res://assets/sprites/ui/";

    public static StyleBox MakeNineSlice(string file, int margin = 6, int contentPad = -1)
    {
        var tex = GD.Load<Texture2D>(UiDir + file);
        if (tex == null) return new StyleBoxFlat { BgColor = new Color(0.10f, 0.11f, 0.13f, 0.95f) };

        var sb = new StyleBoxTexture
        {
            Texture = tex,
            TextureMarginLeft = margin,
            TextureMarginRight = margin,
            TextureMarginTop = margin,
            TextureMarginBottom = margin,
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile,
            AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Tile,
        };
        if (contentPad >= 0)
        {
            sb.ContentMarginLeft = contentPad;
            sb.ContentMarginRight = contentPad;
            sb.ContentMarginTop = contentPad;
            sb.ContentMarginBottom = contentPad;
        }
        return sb;
    }

    public static StyleBox PanelOuter() => MakeNineSlice("panel_outer.png", 6, 10);
    public static StyleBox PanelRegion() => MakeNineSlice("panel_region.png", 6, 8);
    public static StyleBox SlotNormal() => MakeNineSlice("slot_normal.png", 5, 6);
    public static StyleBox SlotHover() => MakeNineSlice("slot_hover.png", 5, 6);
    public static StyleBox SlotPressed() => MakeNineSlice("slot_pressed.png", 5, 6);

    /// <summary>
    /// HUD 角落悬浮块：比 <see cref="PanelOuter"/> 更紧凑、更透，
    /// 左边缘带 2px 琥珀装饰竖条（`tools/gen_hud_icons.py` 生成）。
    /// </summary>
    public static StyleBox HudBox() => MakeNineSlice("hud_box.png", 5, 8);

    /// <summary>加载 HUD 小图标（12×12 原生，1:1 显示）；Godot 资源缓存已负责复用。</summary>
    public static Texture2D LoadUiIcon(string name) => GD.Load<Texture2D>(UiDir + name + ".png");

    /// <summary>给按钮套上像素末日三态槽位皮肤。</summary>
    public static void StylePixelButton(Button btn)
    {
        if (btn == null) return;
        var normal = SlotNormal();
        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("focus", normal);
        btn.AddThemeStyleboxOverride("hover", SlotHover());
        btn.AddThemeStyleboxOverride("pressed", SlotPressed());
    }

    /// <summary>给进度条套上像素风凹槽底框与分节填充条（bar_hp / bar_armor / bar_amber）。</summary>
    public static void StylePixelBar(ProgressBar bar, string fillFile)
    {
        if (bar == null) return;
        bar.AddThemeStyleboxOverride("background", MakeNineSlice("bar_bg.png", 3, 0));
        bar.AddThemeStyleboxOverride("fill", MakeNineSlice(fillFile, 3, 0));
    }
}
