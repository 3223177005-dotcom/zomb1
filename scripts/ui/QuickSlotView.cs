using Godot;

/// <summary>
/// Tab 面板左下角「快捷装备位」按钮里的**内容层**：`[1]` 键位角标 + 居中物品图标 + 激活标 `▸`。
///
/// ★ 减字约定：**槽里不写物品名**（名字看悬停提示），空槽只留一个半透明压痕横杠。
/// ★ 整层 MouseFilter = Ignore —— 点击要落到按钮本身、拖放要落到
///   <see cref="EquipDropTarget"/>（同时不新增任何跨层 group 查询，AGENTS 规则 5）。
/// </summary>
public partial class QuickSlotView : Control
{
    /// <summary>槽位索引（0 基），画键位角标用。</summary>
    public int SlotIndex { get; set; }

    private Label _key;
    private Label _marker;
    private Label _empty;
    private TextureRect _icon;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _key = MakeLabel($"[{SlotIndex + 1}]", UiMetrics.FontTiny, UiStyle.CaptionColor);
        _key.HorizontalAlignment = HorizontalAlignment.Left;
        AddChild(_key);
        Place(_key, UiMetrics.GapSm + 1, UiMetrics.GapXs, 34.0f, 18.0f);

        _marker = MakeLabel("▸", UiMetrics.FontSmall, UiStyle.AmberHeader);
        _marker.HorizontalAlignment = HorizontalAlignment.Right;
        AddChild(_marker);
        PlaceFromRight(_marker, UiMetrics.GapSm + 1, UiMetrics.GapXs, 34.0f, 18.0f);

        // 空槽压痕：一个半透明的横杠，暗示"这里可以放东西"
        _empty = MakeLabel("—", UiMetrics.FontSubtitle, UiStyle.EmptySlotColor);
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.VerticalAlignment = VerticalAlignment.Center;
        _empty.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.35f);
        AddChild(_empty);
        StretchVertically(_empty, 14.0f, 12.0f);

        _icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        AddChild(_icon);
        StretchHorizontally(_icon, 8.0f, 10.0f, 66.0f);

        ShowItem(null, false, "—");
    }

    /// <summary>按槽里的东西刷新：图标 + 激活标记（**不印名字**）。</summary>
    public void ShowItem(ItemStack item, bool active, string emptyText)
    {
        var data = item?.Data;
        bool hasIcon = data?.Icon != null;

        _icon.Texture = hasIcon ? data.Icon : null;
        _icon.Visible = hasIcon;
        _empty.Visible = !hasIcon;

        _marker.Visible = active;
        _key.AddThemeColorOverride("font_color", active ? UiStyle.AmberHeader : UiStyle.CaptionColor);

        TooltipText = data != null
            ? ItemTooltip.Build(item)
            : $"快捷装备位 [{SlotIndex + 1}]：{emptyText}";
    }

    // ---------- 布局小工具（都按本层尺寸算锚点 / 偏移） ----------

    private static Label MakeLabel(string text, int fontSize, Color color)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    /// <summary>左上角锚定，宽高固定。</summary>
    private static void Place(Control control, float x, float y, float w, float h)
    {
        control.AnchorLeft = 0.0f;
        control.AnchorRight = 0.0f;
        control.AnchorTop = 0.0f;
        control.AnchorBottom = 0.0f;
        control.OffsetLeft = x;
        control.OffsetTop = y;
        control.OffsetRight = x + w;
        control.OffsetBottom = y + h;
    }

    /// <summary>右上角锚定，宽高固定。</summary>
    private static void PlaceFromRight(Control control, float right, float y, float w, float h)
    {
        control.AnchorLeft = 1.0f;
        control.AnchorRight = 1.0f;
        control.AnchorTop = 0.0f;
        control.AnchorBottom = 0.0f;
        control.OffsetLeft = -right - w;
        control.OffsetTop = y;
        control.OffsetRight = -right;
        control.OffsetBottom = y + h;
    }

    /// <summary>横向拉伸（左右各留边距）、纵向固定区间 —— 枪这种 80×32 的宽图标也占得满。</summary>
    private static void StretchHorizontally(Control control, float margin, float top, float bottom)
    {
        control.AnchorLeft = 0.0f;
        control.AnchorRight = 1.0f;
        control.AnchorTop = 0.0f;
        control.AnchorBottom = 0.0f;
        control.OffsetLeft = margin;
        control.OffsetTop = top;
        control.OffsetRight = -margin;
        control.OffsetBottom = bottom;
    }

    /// <summary>横向拉伸、纵向按上下边距内缩（空槽压痕用）。</summary>
    private static void StretchVertically(Control control, float margin, float top)
    {
        control.AnchorLeft = 0.0f;
        control.AnchorRight = 1.0f;
        control.AnchorTop = 0.0f;
        control.AnchorBottom = 1.0f;
        control.OffsetLeft = margin;
        control.OffsetTop = top;
        control.OffsetRight = -margin;
        control.OffsetBottom = 0.0f;
    }
}
