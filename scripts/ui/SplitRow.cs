using Godot;

/// <summary>
/// **行内拆分控件** —— 直接长在物品列表里，不弹独立窗口。
///
/// 点「拆分」后，那一行原地变成：
/// <code>
///   绷带 ×10                        拆 3  留 7
///   [━━━━━━●━━━━━━━━━━]   [ 确定 ] [ 取消 ]
/// </code>
///
/// 和 <see cref="ItemRow"/> 共用字号与稀有度配色。
/// </summary>
public partial class SplitRow : PanelContainer
{
    private Label _title;
    private Label _hint;
    private HSlider _slider;
    private Button _ok;
    private Button _cancel;

    /// <summary>要拆的那一叠。</summary>
    public ItemStack Stack { get; private set; }

    /// <summary>它所在的口袋。</summary>
    public Inventory Pocket { get; private set; }

    /// <summary>点「确定」（参数 = 拆出的数量）。</summary>
    public System.Action<int> Confirmed { get; set; }

    /// <summary>点「取消」。</summary>
    public System.Action Cancelled { get; set; }

    /// <summary>当前滑块选中的拆分数量。</summary>
    public int Amount => _slider != null ? Mathf.RoundToInt((float)_slider.Value) : 0;

    /// <summary>右侧那行提示文字（自测 / 错误提示用）。</summary>
    public string HintText => _hint?.Text ?? "";

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;

        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.22f, 0.32f, 0.95f),
            BorderColor = new Color(0.42f, 0.62f, 0.92f, 1.0f),
            BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
        });

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);
        margin.AddChild(box);

        // 第一行：物品名 + 拆分/保留 提示
        var head = new HBoxContainer();
        box.AddChild(head);

        _title = MakeLabel("", 16, Colors.White);
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(_title);

        _hint = MakeLabel("", 14, UiStyle.CaptionColor);
        _hint.HorizontalAlignment = HorizontalAlignment.Right;
        head.AddChild(_hint);

        // 第二行：滑块 + 确定 / 取消
        var controls = new HBoxContainer();
        controls.AddThemeConstantOverride("separation", 6);
        box.AddChild(controls);

        _slider = new HSlider
        {
            CustomMinimumSize = new Vector2(0, 24),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            Step = 1,
            MinValue = 1,
            MaxValue = 1,
        };
        _slider.ValueChanged += _ => UpdateHint();
        controls.AddChild(_slider);

        _ok = MakeButton("确定");
        _ok.Pressed += () =>
        {
            int amount = Amount;
            if (amount > 0) Confirmed?.Invoke(amount);
        };
        controls.AddChild(_ok);

        _cancel = MakeButton("取消");
        _cancel.Pressed += () => Cancelled?.Invoke();
        controls.AddChild(_cancel);
    }

    /// <summary>绑定到某一叠（必须在加入场景树之后调，子节点已就绪）。</summary>
    public void Setup(Inventory pocket, ItemStack stack)
    {
        Pocket = pocket;
        Stack = stack;
        if (stack?.Data == null || _slider == null) return;

        _title.Text = $"{stack.Data.Name} ×{stack.Count}";
        _title.AddThemeColorOverride("font_color", UiStyle.ItemText(stack.Data));   // ★ 稀有度色

        _slider.MinValue = 1;
        _slider.MaxValue = Mathf.Max(1, stack.Count - 1);
        _slider.Value = Mathf.Max(1, stack.Count / 2);   // 默认拆一半

        UpdateHint();
    }

    private void UpdateHint()
    {
        if (Stack == null) return;

        int amount = Amount;
        _hint.Text = $"拆 {amount}  留 {Stack.Count - amount}";
        _hint.AddThemeColorOverride("font_color", UiStyle.CaptionColor);
    }

    /// <summary>
    /// 显示一条错误（不退出拆分状态）。
    /// 例如：口袋没有空格子摆新拆出来的一叠 —— 以前这种情况是静默失败。
    /// </summary>
    public void ShowError(string text)
    {
        if (_hint == null) return;

        _hint.Text = text;
        _hint.AddThemeColorOverride("font_color", new Color(1.0f, 0.45f, 0.45f));
    }

    private static Label MakeLabel(string text, int fontSize, Color color)
    {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Button MakeButton(string text)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(52, 26),
            FocusMode = FocusModeEnum.None,
        };
        button.AddThemeFontSizeOverride("font_size", 15);
        return button;
    }
}
