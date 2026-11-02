using Godot;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// **撤离结算面板**：撤离成功后盖在最上层，列出带出来的东西和总价值。
///
/// 最简框架：
/// - 标题「撤离成功」
/// - 带出物品清单（名称 ×数量 → 价值）
/// - 总计：件数 / 总价值 / 负重
/// - 「返回主菜单」按钮
/// </summary>
public partial class ResultPanel : Control
{
    private PanelContainer _card;
    private VBoxContainer _list;
    private Label _summary;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        ZIndex = 200;

        AddChild(new ColorRect { Color = new Color(0.02f, 0.03f, 0.02f, 0.82f) });
        GetChild<ColorRect>(0).SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Pass };
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _card = new PanelContainer { CustomMinimumSize = new Vector2(560, 420) };
        _card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.09f, 0.07f, 0.99f),
            BorderColor = new Color(0.35f, 0.85f, 0.50f, 0.95f),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        });
        center.AddChild(_card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        _card.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);

        var title = new Label { Text = "撤离成功" };
        title.AddThemeFontSizeOverride("font_size", 26);
        title.AddThemeColorOverride("font_color", new Color(0.45f, 1.0f, 0.62f));
        box.AddChild(title);

        box.AddChild(new HSeparator());

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        box.AddChild(scroll);

        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(_list);

        box.AddChild(new HSeparator());

        _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _summary.AddThemeFontSizeOverride("font_size", 16);
        _summary.AddThemeColorOverride("font_color", new Color(0.85f, 0.90f, 0.86f));
        box.AddChild(_summary);

        var back = new Button { Text = "返回主菜单" };
        back.AddThemeFontSizeOverride("font_size", 17);
        back.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
        box.AddChild(back);
    }

    /// <summary>结算：列出玩家身上所有东西 + 总价值。</summary>
    public void Show(Player player)
    {
        if (player == null) return;

        foreach (var child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }

        int totalValue = 0;
        int totalStacks = 0;
        float totalWeight = 0.0f;

        void AddFrom(Inventory pocket, string label)
        {
            if (pocket == null || pocket.IsEmpty) return;

            var head = new Label { Text = label };
            head.AddThemeFontSizeOverride("font_size", 14);
            head.AddThemeColorOverride("font_color", new Color(0.55f, 0.60f, 0.56f));
            _list.AddChild(head);

            foreach (var stack in pocket.Stacks)
            {
                if (stack?.Data == null) continue;

                int value = stack.TotalValue;
                totalValue += value;
                totalStacks++;
                totalWeight += stack.TotalWeight;

                var row = new Label
                {
                    Text = $"  {stack.Data.Name}" + (stack.Count > 1 ? $" ×{stack.Count}" : "") +
                           $"    {stack.TotalWeight:0.##} kg    价值 {value}",
                };
                row.AddThemeFontSizeOverride("font_size", 15);
                row.AddThemeColorOverride("font_color", UiStyle.ItemText(stack.Data));
                _list.AddChild(row);
            }
        }

        AddFrom(player.Pocket, "基础口袋");
        for (int i = 0; i < player.Equipment.SlotCount; i++)
        {
            var slot = player.Equipment.Get(i);
            if (slot?.Data == null) continue;

            // 武器本身也算带出来的
            totalValue += slot.TotalValue;
            totalWeight += slot.TotalWeight;

            var row = new Label { Text = $"  [装备{i + 1}] {slot.Data.Name}    价值 {slot.TotalValue}" };
            row.AddThemeFontSizeOverride("font_size", 15);
            row.AddThemeColorOverride("font_color", UiStyle.ItemText(slot.Data));
            _list.AddChild(row);

            AddFrom(slot.Contents, $"  {slot.Data.Name} 里面");
        }

        var sb = new StringBuilder();
        sb.Append($"共 {totalStacks} 件东西    总价值 {totalValue}    总负重 {totalWeight:0.##} kg");
        _summary.Text = sb.ToString();

        Visible = true;
        MoveToFront();
    }
}
