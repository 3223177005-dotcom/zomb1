using Godot;

/// <summary>
/// Tab 面板左上「防具」区：护甲 / 头盔**两行**，每行 = 槽位图标 + 耐久条 + 百分比。
///
/// ★ 减字约定：**不写槽位小标题、不写物品全名、不写操作说明** ——
///   空槽用半透明槽位剪影图标表示，穿上后换成物品贴图，右侧只留 `100%` / `破损`；
///   物品全名与精确耐久在悬停提示里。
///
/// ★ 整行就是一个 Button，拖放接收层**盖满整行**（含耐久条区域），没有死区。
/// ★ 不引用 Player 的属性（只借槽位常量）—— 状态靠订阅
///   <see cref="EventBus.PlayerArmorChanged"/> 与面板主动 <see cref="Sync"/>（AGENTS 规则 3/5）。
/// </summary>
public partial class EquipArmorRows : MarginContainer
{
    /// <summary>行高（像素）。</summary>
    private const float RowHeight = UiMetrics.SlotMinHeight;

    /// <summary>槽位图标框边长（原生物品图标 32px，这里给 44 的框居中）。</summary>
    private const float IconBox = UiMetrics.IconBox;

    /// <summary>耐久条高度（像素）——主操作面板，比 HUD 的迷你条粗。</summary>
    private const float BarHeight = 10.0f;

    /// <summary>右侧百分比文字的固定宽度。</summary>
    private const float ValueWidth = 44.0f;

    private static readonly string[] Titles = { "护甲", "头盔" };

    /// <summary>空槽时显示的槽位剪影图标（`assets/sprites/ui/icon_*.png`）。</summary>
    private static readonly string[] KindIcons = { "icon_armor", "icon_helmet" };

    private readonly Button[] _cells = new Button[2];
    private readonly TextureRect[] _icons = new TextureRect[2];
    private readonly ProgressBar[] _bars = new ProgressBar[2];
    private readonly Label[] _values = new Label[2];

    /// <summary>装备槽里的**实例**：Sync 时记住，信号回调直接读它的最新耐久。</summary>
    private readonly ItemStack[] _stacks = new ItemStack[2];

    /// <summary>上一次用过的填充贴图（换档才重建 StyleBox，避免每帧分配）。</summary>
    private readonly string[] _lastFill = { "", "" };

    private readonly string[] _lastName = { "", "" };
    private readonly float[] _lastDur = { float.NaN, float.NaN };

    public override void _Ready()
    {
        AddThemeConstantOverride("margin_left", UiMetrics.PadRegion);
        AddThemeConstantOverride("margin_top", UiMetrics.GapMd);
        AddThemeConstantOverride("margin_right", UiMetrics.PadRegion);
        AddThemeConstantOverride("margin_bottom", UiMetrics.GapMd);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", UiMetrics.GapSm);
        AddChild(column);

        var header = new Label { Text = "防具", MouseFilter = MouseFilterEnum.Ignore };
        header.AddThemeFontSizeOverride("font_size", UiMetrics.FontSmall);
        header.AddThemeColorOverride("font_color", UiStyle.AmberHeader);
        column.AddChild(header);

        column.AddChild(BuildRow(0));
        column.AddChild(BuildRow(1));

        // 吸收 ArmorRegion 剩余高度，别在下面留一大块空帆布
        column.AddChild(new Control
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        });

        EventBus.Instance.PlayerArmorChanged += OnArmorChanged;
        Refresh();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
            EventBus.Instance.PlayerArmorChanged -= OnArmorChanged;
    }

    /// <summary>一行 = 整宽 Button（图标 + 耐久条 + 百分比）+ 盖满整行的拖放层。</summary>
    private Control BuildRow(int index)
    {
        var row = new Button
        {
            Name = index == 0 ? "ArmorCell" : "HelmetCell",
            CustomMinimumSize = new Vector2(0.0f, RowHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None,
            Text = "",
            ClipText = true,
        };
        UiStyle.StylePixelButton(row);
        _cells[index] = row;

        var body = BuildBody(index);
        row.AddChild(body);
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        body.OffsetLeft = 10.0f;
        body.OffsetRight = -10.0f;
        body.OffsetTop = 6.0f;
        body.OffsetBottom = -6.0f;

        AddDropTarget(row, index);
        return row;
    }

    /// <summary>行内容：槽位图标 + 耐久条 + 右侧百分比。</summary>
    private Control BuildBody(int index)
    {
        var body = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        body.AddThemeConstantOverride("separation", UiMetrics.GapMd);

        _icons[index] = new TextureRect
        {
            Texture = UiStyle.LoadUiIcon(KindIcons[index]),
            CustomMinimumSize = new Vector2(IconBox, IconBox),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        body.AddChild(_icons[index]);

        _bars[index] = new ProgressBar
        {
            MinValue = 0.0,
            MaxValue = 100.0,
            Value = 0.0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0.0f, BarHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        UiStyle.StylePixelBar(_bars[index], "bar_armor.png");
        body.AddChild(_bars[index]);

        _values[index] = new Label
        {
            Text = "—",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(ValueWidth, 0.0f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _values[index].AddThemeFontSizeOverride("font_size", UiMetrics.FontTiny);
        body.AddChild(_values[index]);

        return body;
    }

    /// <summary>拖放接收层盖满整行（含耐久条区域），不再有死区。</summary>
    private static void AddDropTarget(Button row, int index)
    {
        int slotIndex = index == 0 ? Player.ArmorSlot : Player.HelmetSlot;
        var drop = new EquipDropTarget
        {
            Name = "DropTarget",
            SlotIndex = slotIndex,
            KindProvider = () => slotIndex == Player.ArmorSlot ? EquipSlotKind.Armor : EquipSlotKind.Helmet,
        };
        row.AddChild(drop);
        drop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    /// <summary>面板每帧把自己的两个装备槽传进来（装上 / 脱下 / 耐久变化都会立刻反映）。</summary>
    public void Sync(ItemStack body, ItemStack helmet)
    {
        _stacks[0] = body;
        _stacks[1] = helmet;
        Refresh();
    }

    private void OnArmorChanged(string bodyName, float bodyDur, float bodyMax,
        string helmetName, float helmetDur, float helmetMax) => Refresh();

    private void Refresh()
    {
        Apply(0);
        Apply(1);
    }

    /// <summary>把一格的状态画出来（数据没变就跳过，避免每帧重绘）。</summary>
    private void Apply(int index)
    {
        var stack = _stacks[index];
        var data = stack?.Data;
        float durability = data != null ? stack.Durability : -1.0f;

        if (_lastName[index] == (data?.Name ?? "") && Mathf.IsEqualApprox(_lastDur[index], durability)) return;
        _lastName[index] = data?.Name ?? "";
        _lastDur[index] = durability;

        if (data == null)
        {
            ApplyEmpty(index);
            return;
        }

        ApplyEquipped(index, stack);
    }

    /// <summary>空槽：半透明槽位剪影图标 + 淡横杠，不写任何字。</summary>
    private void ApplyEmpty(int index)
    {
        _icons[index].Texture = UiStyle.LoadUiIcon(KindIcons[index]);
        _icons[index].Modulate = new Color(1.0f, 1.0f, 1.0f, 0.32f);

        _bars[index].Visible = false;
        _values[index].Text = "—";
        _values[index].AddThemeColorOverride("font_color", UiStyle.EmptySlotColor);
        _cells[index].TooltipText = $"把{Titles[index]}拖到这一行";
    }

    /// <summary>已装备：物品贴图 + 耐久条（按比例换色）+ 右侧百分比。</summary>
    private void ApplyEquipped(int index, ItemStack stack)
    {
        var data = stack.Data;
        _icons[index].Texture = data.Icon;
        _icons[index].Modulate = Colors.White;

        float max = Mathf.Max(1.0f, data.MaxDurability);
        float ratio = data.HasDurability ? Mathf.Clamp(stack.Durability / max, 0.0f, 1.0f) : 1.0f;
        bool broken = data.HasDurability && ratio <= 0.0f;
        var color = UiStyle.DurabilityColor(ratio, broken);

        _bars[index].Visible = data.HasDurability;
        if (data.HasDurability)
        {
            _bars[index].MaxValue = max;
            _bars[index].Value = stack.Durability;
            ApplyFill(index, ratio);
        }

        if (!data.HasDurability) _values[index].Text = "";
        else _values[index].Text = broken ? "破损" : $"{ratio * 100.0f:0}%";

        _values[index].AddThemeColorOverride("font_color", color);

        _cells[index].TooltipText = broken
            ? $"{data.Name}  已破损（耐久 0/{max:F0}）：不再提供防护"
            : $"{data.Name}  耐久 {stack.Durability:F0}/{max:F0}";
    }

    /// <summary>只在耐久档位变化时重建进度条填充贴图。</summary>
    private void ApplyFill(int index, float ratio)
    {
        string fill = UiStyle.DurabilityFill(ratio);
        if (_lastFill[index] == fill) return;

        _lastFill[index] = fill;
        UiStyle.StyleDurabilityBar(_bars[index], ratio);
    }
}
