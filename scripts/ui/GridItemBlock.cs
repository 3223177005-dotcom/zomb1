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

    private static readonly Color TextDim = new(0.38f, 0.34f, 0.28f);

    /// <summary>底部每条带（名字 / 数量角标）的高度（像素）。</summary>
    private const float BandHeight = 16.0f;

    /// <summary>底部给耐久细条留的高度（像素）。</summary>
    private const float StripZone = 8.0f;

    /// <summary>数量角标占的宽度（像素）。</summary>
    private const float BadgeWidth = 34.0f;

    /// <summary>数量角标：浅底上用深炭墨字。</summary>
    private static readonly Color BadgeColor = new(0.16f, 0.13f, 0.10f);

    /// <summary>**浅色**描边：深色字压在深色武器图标上时，靠它衬出一圈羊皮纸白边。</summary>
    private static readonly Color OutlineColor = new(0.95f, 0.92f, 0.84f, 0.92f);

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
        // 进度：从下往上填的暗琥珀铜底纹（单格物品太小时，细条几乎看不见）
        Progress = new ColorRect
        {
            Color = new Color(0.52f, 0.38f, 0.18f, 0.38f),
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
        question.AddThemeFontSizeOverride("font_size", UiMetrics.FontSubtitle);
        question.AddThemeColorOverride("font_color", TextDim);
        AddChild(question);   // 画在进度底纹**上面**
        RefreshProgress();
    }

    /// <summary>
    /// 图标框：**占满格块主体并居中**（等比，不固定 32）—— ZS 的枪图标是 80×32、
    /// 配件是 32×16，固定 32 的方框会把它们缩成一条细缝（旧 bug：背包图标过小）。
    /// 名字 / 角标 / 耐久条各占一条底部带，由 <see cref="BuildItem"/> 扣掉高度后传进来。
    /// </summary>
    private TextureRect BuildIcon(ItemData data, Rect2 inner)
    {
        var icon = new TextureRect
        {
            Texture = data.Icon,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore,
            Position = inner.Position,
            Size = inner.Size,
        };

        // 旋转过的物品：图标框按**宽高交换**后居中，再绕自身中心转 90°，
        // 正好覆盖回格块内区（否则整块会被转出格位外面）。
        if (Stack.Rotated)
        {
            var swapped = new Vector2(inner.Size.Y, inner.Size.X);
            icon.Size = swapped;
            icon.Position = inner.Position + (inner.Size - swapped) * 0.5f;
            icon.PivotOffset = swapped * 0.5f;
            icon.Rotation = Mathf.Pi * 0.5f;
        }
        return icon;
    }

    private void BuildItem()
    {
        var data = Stack?.Data;
        if (data == null) return;

        bool hasBadge = Stack.Count > 1;

        // 底部只留「耐久条区 + 数量角标区」：格块内**不印物品名**（名字看悬停提示）
        float stripZone = Stack.HasDurability ? StripZone : 3.0f;
        float bandBottom = Size.Y - stripZone;
        float bandTop = bandBottom - BandHeight;

        // 图标占满格块（角标会压在图标上，带浅色描边所以仍清晰）
        var inner = new Rect2(
            new Vector2(3.0f, 3.0f),
            new Vector2(Size.X - 6.0f, Mathf.Max(8.0f, bandBottom - 3.0f)));

        AddChild(BuildIcon(data, inner));
        if (hasBadge) AddChild(BuildCountBadge(Stack.Count, bandTop));
        if (Stack.HasDurability) AddDurabilityStrip();
    }

    /// <summary>数量角标：**单格物品（绷带 / 弹药）也要显示**，否则看不出叠了几个。</summary>
    private Label BuildCountBadge(int count, float top)
    {
        var badge = new Label
        {
            Text = $"×{count}",
            Position = new Vector2(Size.X - BadgeWidth - 4.0f, top),
            Size = new Vector2(BadgeWidth, BandHeight),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        badge.AddThemeFontSizeOverride("font_size", UiMetrics.FontTiny);
        badge.AddThemeColorOverride("font_color", BadgeColor);
        badge.AddThemeColorOverride("font_outline_color", OutlineColor);
        badge.AddThemeConstantOverride("outline_size", 3);
        return badge;
    }

    /// <summary>底部耐久细条（凹槽 + 按耐久比例填色），背包里的备用甲一眼看出损耗。</summary>
    private void AddDurabilityStrip()
    {
        float width = Mathf.Max(4.0f, Size.X - 16.0f);
        float top = Size.Y - 7.0f;
        float ratio = Mathf.Clamp(Stack.DurabilityRatio, 0.0f, 1.0f);

        AddChild(new ColorRect
        {
            Color = new Color(0.42f, 0.38f, 0.31f, 0.90f),
            Position = new Vector2(8.0f, top),
            Size = new Vector2(width, 3.0f),
            MouseFilter = MouseFilterEnum.Ignore,
        });

        AddChild(new ColorRect
        {
            Color = UiStyle.DurabilityColor(ratio, Stack.IsBroken),
            Position = new Vector2(8.0f, top),
            Size = new Vector2(Mathf.Max(1.0f, width * ratio), 3.0f),
            MouseFilter = MouseFilterEnum.Ignore,
        });
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);

        var fill = _revealed
            ? new Color(0.88f, 0.84f, 0.75f, 0.96f)
            : new Color(0.64f, 0.59f, 0.50f, 0.96f);
        DrawRect(rect, fill, true);

        Color line;
        if (!_revealed) line = new Color(0.38f, 0.34f, 0.28f, 1.0f);
        else if (Stack?.Data != null)
        {
            var c = UiStyle.ItemText(Stack.Data);
            line = new Color(c.R, c.G, c.B, 0.85f);
        }
        else line = new Color(0.42f, 0.38f, 0.31f, 1.0f);

        DrawRect(rect, line, false, 2);
        // 像素战术角标（左上与右下各画 L 型亮色折角）
        var corner = new Color(Mathf.Min(1f, line.R * 1.25f), Mathf.Min(1f, line.G * 1.25f), Mathf.Min(1f, line.B * 1.25f), 0.95f);
        DrawRect(new Rect2(2, 2, 5, 2), corner, true);
        DrawRect(new Rect2(2, 2, 2, 5), corner, true);
        DrawRect(new Rect2(Size.X - 7, Size.Y - 4, 5, 2), corner, true);
        DrawRect(new Rect2(Size.X - 4, Size.Y - 7, 2, 5), corner, true);
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
