using Godot;

/// <summary>
/// 格子面板里的**一件物品块**：按 W×H × CellPx 定尺寸，画图标 + 名称，
/// 自己就是拖拽源，并把拖放事件转发给 <see cref="GridPanel"/>（按格坐标落点）。
///
/// 为什么不用 ItemRow：物品在格子里是**一块区域**而不是一行，
/// 尺寸和位置都由所在格子决定（阶段 4 的核心区别）。
/// </summary>
public partial class GridItemBlock : Control
{
    /// <summary>这一块代表的堆叠。</summary>
    public ItemStack Stack { get; set; }

    /// <summary>它在哪个口袋（拖拽源 + 右键菜单用）。</summary>
    public Inventory Pocket { get; set; }

    /// <summary>所属格子面板。</summary>
    public GridPanel Grid { get; set; }

    /// <summary>未揭示时的进度条（搜刮容器用；其余情况为 null）。</summary>
    public ColorRect Progress { get; private set; }

    private bool _revealed = true;

    private static readonly Color TextDim = new(0.45f, 0.46f, 0.50f);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;

        _revealed = Grid?.IsRevealed?.Invoke(Stack) ?? true;

        if (Stack?.Data != null && _revealed)
            TooltipText = ItemTooltip.Build(Stack);

        if (!_revealed)
        {
            BuildUnknown();
            return;
        }

        BuildItem();
    }

    // ---------- 外观 ----------

    private void BuildUnknown()
    {
        // 进度：从下往上填的紫色底纹（单格物品太小时，细条几乎看不见）
        Progress = new ColorRect
        {
            Color = new Color(0.55f, 0.38f, 0.82f, 0.45f),
            Position = new Vector2(1, Size.Y - 1),
            Size = new Vector2(Size.X - 2, 0),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(Progress);

        var question = new Label
        {
            Text = "？",
            Position = Vector2.Zero,
            Size = Size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        question.AddThemeFontSizeOverride("font_size", 20);
        question.AddThemeColorOverride("font_color", TextDim);
        AddChild(question);   // 画在进度底纹**上面**
        RefreshProgress();
    }

    private void BuildItem()
    {
        var data = Stack?.Data;
        if (data == null) return;

        // 够宽的块（≥ 2 格）图标靠左、名字靠右；单格块只放图标（名字看悬停提示）
        bool wide = Size.X >= 64.0f;
        var inner = new Rect2(Vector2.Zero, Size).Grow(-3);

        var icon = new TextureRect
        {
            Texture = data.Icon,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };

        Vector2 iconSize;
        if (wide)
        {
            float side = Mathf.Min(inner.Size.Y, 32.0f);   // 32 = 图标源尺寸，1:1 不缩放
            iconSize = new Vector2(side, side);
            icon.Position = new Vector2(inner.Position.X, inner.Position.Y + (inner.Size.Y - side) * 0.5f);
        }
        else
        {
            // 格子 40 - 边框 2 - 内边距 6 = 32，正好等于图标源尺寸
            float side = Mathf.Min(inner.Size.X, inner.Size.Y);
            side = Mathf.Max(8.0f, side);
            iconSize = new Vector2(side, side);
            icon.Position = inner.Position + (inner.Size - iconSize) * 0.5f;
        }

        icon.Size = iconSize;
        if (Stack.Rotated)
        {
            icon.PivotOffset = iconSize * 0.5f;
            icon.Rotation = Mathf.Pi * 0.5f;
        }
        AddChild(icon);

        if (!wide) return;

        string text = data.Name;
        if (Stack.Count > 1) text += $" ×{Stack.Count}";

        var label = new Label
        {
            Text = text,
            Position = new Vector2(icon.Position.X + iconSize.X + 4, 1),
            Size = new Vector2(Size.X - (icon.Position.X + iconSize.X + 4) - 3, Size.Y - 2),
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = true,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", UiStyle.ItemText(data));
        AddChild(label);
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);

        var fill = _revealed
            ? new Color(0.14f, 0.15f, 0.19f, 0.95f)
            : new Color(0.10f, 0.10f, 0.13f, 0.95f);
        DrawRect(rect, fill, true);

        Color line;
        if (!_revealed) line = new Color(0.28f, 0.26f, 0.34f, 1.0f);
        else if (Stack?.Data != null)
        {
            var c = UiStyle.ItemText(Stack.Data);
            line = new Color(c.R, c.G, c.B, 0.85f);
        }
        else line = new Color(0.3f, 0.3f, 0.34f, 1.0f);

        DrawRect(rect, line, false, 2);
    }

    /// <summary>按 <see cref="GridPanel.RevealProgress"/> 更新进度底纹（不重建节点）。</summary>
    public void RefreshProgress()
    {
        if (Progress == null || Grid == null) return;

        float value = Mathf.Clamp(Grid.RevealProgress?.Invoke(Stack) ?? 0.0f, 0.0f, 1.0f);
        float height = Mathf.Max(0.0f, Size.Y - 2.0f) * value;

        Progress.Position = new Vector2(1, Size.Y - 1 - height);
        Progress.Size = new Vector2(Size.X - 2, height);
    }

    // ---------- 拖出 ----------

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!_revealed || Stack?.Data == null || Pocket == null) return default;

        ItemDrag.Begin(Pocket, new System.Collections.Generic.List<ItemStack> { Stack },
                       Stack.GridX, Stack.GridY);

        string text = Stack.Data.Name + (Stack.Count > 1 ? $" ×{Stack.Count}" : "");
        var preview = new Label { Text = text };
        preview.AddThemeFontSizeOverride("font_size", 16);
        preview.AddThemeColorOverride("font_color", UiStyle.ItemText(Stack.Data));
        SetDragPreview(preview);

        return true;   // 非空 = 开始拖拽（真载荷在 ItemDrag 里）
    }

    // ---------- 拖入（转发给格子面板，按格坐标算） ----------

    public override bool _CanDropData(Vector2 atPosition, Variant data)
        => Grid != null && Grid.CanDropAt(Position + atPosition);

    public override void _DropData(Vector2 atPosition, Variant data)
        => Grid?.DropAt(Position + atPosition);

    // ---------- 点击 ----------

    public override void _GuiInput(InputEvent @event)
    {
        if (!_revealed) return;
        if (@event is not InputEventMouseButton mb || !mb.Pressed) return;

        if (mb.ButtonIndex == MouseButton.Right)
        {
            Grid?.ContextRequested?.Invoke(Stack, Pocket, mb.GlobalPosition);
            AcceptEvent();
            return;
        }

        if (mb.ButtonIndex == MouseButton.Left && mb.DoubleClick)
        {
            Grid?.Activated?.Invoke(Stack, Pocket);
            AcceptEvent();
        }
    }
}
