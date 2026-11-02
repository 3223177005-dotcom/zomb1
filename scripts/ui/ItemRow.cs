using Godot;

/// <summary>
/// 物品列表的一行：**［贴图预留区］ 名称 ×数量   占格   重量**
///
/// 背包列表（Tab 面板右侧）与地面拾取面板共用同一套构造，保证两边外观一致。
///
/// ★ 贴图预留区：现在是一块固定尺寸的空位（<see cref="IconSize"/> 见方）。
///   它直接绑定 <see cref="ItemData.Icon"/> —— 现在是 null 所以留白，
///   将来物品补了图标（items.json 的 IconPath）**会自动显示，不用改这里**。
/// </summary>
public static class ItemRow
{
    /// <summary>物品图标显示边长（像素）。必须等于图标源尺寸（32），否则最近邻放大/缩小时像素块大小不一。</summary>
    public const float IconSize = 32.0f;

    /// <summary>一行的高度。</summary>
    public const float RowHeight = 38.0f;

    /// <summary>树里缩进一层的像素数。</summary>
    public const float IndentStep = 12.0f;

    /// <summary>
    /// 造一行。返回的 HBox 可以直接塞进 VBoxContainer；
    /// 若要塞进 Button，记得对整棵子树调 <see cref="IgnoreMouse"/>，否则点不到按钮。
    /// </summary>
    /// <param name="note">名称后面的补充说明（例如「空间不够」），可空。</param>
    /// <param name="dimmed">整行调暗（表示不可用）。</param>
    /// <param name="indent">树的缩进层数（0 = 最外层）。</param>
    /// <param name="marker">名称前缀标记（容器用「▾」）。</param>
    public static HBoxContainer Build(ItemSummary row, string note = null, bool dimmed = false,
                                      int indent = 0, string marker = null)
    {
        var box = new HBoxContainer { CustomMinimumSize = new Vector2(0, RowHeight) };
        box.AddThemeConstantOverride("separation", 6);

        // 树的缩进
        if (indent > 0)
        {
            box.AddChild(new Control
            {
                CustomMinimumSize = new Vector2(indent * IndentStep, 0),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        }

        // ★ 贴图预留区
        box.AddChild(BuildIconSlot(row.Data, dimmed));

        // 名称（稀有度色）
        string prefix = string.IsNullOrEmpty(marker) ? "" : marker + " ";
        var name = new Label
        {
            Text = $"{prefix}{row.Data.Name} ×{row.Count}",
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        name.AddThemeFontSizeOverride("font_size", 17);
        name.AddThemeColorOverride("font_color",
            dimmed ? UiStyle.ItemTextDimmed(row.Data) : UiStyle.ItemText(row.Data));
        box.AddChild(name);

        // 补充说明（自己占宽度，因为名称是 ExpandFill，固定列仍然钉在右边）
        if (!string.IsNullOrEmpty(note))
        {
            var noteLabel = new Label
            {
                Text = note,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            noteLabel.AddThemeFontSizeOverride("font_size", 15);
            noteLabel.AddThemeColorOverride("font_color", UiStyle.CaptionColor);
            box.AddChild(noteLabel);
        }

        box.AddChild(MakeNumberCell($"{row.Data?.GridW ?? 1}×{row.Data?.GridH ?? 1}", 34));
        box.AddChild(MakeNumberCell($"{row.Weight:0.00} kg", 58));
        return box;
    }

    /// <summary>
    /// 贴图预留区。现在 <see cref="ItemData.Icon"/> 都是 null，所以是一块空白；
    /// 将来物品补了图标，这里会自动显示出来。
    /// </summary>
    public static Control BuildIconSlot(ItemData data, bool dimmed = false)
    {
        var slot = new TextureRect
        {
            Texture = data?.Icon,                       // ← 有图标就自动显示
            CustomMinimumSize = new Vector2(IconSize, IconSize),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        if (dimmed) slot.Modulate = UiStyle.Dim(Colors.White, 0.5f);
        return slot;
    }

    /// <summary>把整棵子树的鼠标过滤设为 Ignore，让点击落到外层 Button 上。</summary>
    public static void IgnoreMouse(Node node)
    {
        if (node is Control control) control.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (var child in node.GetChildren()) IgnoreMouse(child);
    }

    /// <summary>右对齐的数值格。</summary>
    private static Label MakeNumberCell(string text, float width)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(width, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", UiStyle.CaptionColor);
        return label;
    }
}
