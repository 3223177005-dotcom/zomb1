using Godot;

/// <summary>
/// 局内 HUD 的**四角战术布局**（《Mini DAYZ》式纯图标 / 条带，**不写物品名**）：
/// 左上生存状态 · 右下武器弹药 · 底部快捷栏 · 右上消息/查看浮窗 · 左下极简按键提示。
///
/// 设计约定（别退回"网页侧边栏"）：
/// - **能靠图标 + 条看懂的一律不写字**：护甲只留 `100%`，负重只留 `18.6kg`，
///   武器只有贴图 + 弹药数，快捷栏只有格子数字 + 图标；
/// - 平时不挡视野：消息 / 查看浮窗没有内容时整块 `Visible = false`。
///
/// 为什么拆两个文件：搭建（本文件）与喂数据（`HudSidebar.Data.cs`）分开，
/// 各自守在 400 行以内（AGENTS 规则 1）。
/// </summary>
public partial class HudSidebar : Control
{
    private Label _healthText;
    private Label _staminaText;
    private Label _weightText;
    private ProgressBar _healthBar;
    private ProgressBar _staminaBar;
    private ProgressBar _weightBar;

    private TextureRect _armorIcon;
    private TextureRect _helmetIcon;
    private Label _armorText;
    private Label _helmetText;
    private ProgressBar _armorBar;
    private ProgressBar _helmetBar;

    private TextureRect _weaponIcon;
    private Label _ammoText;
    private Label _ammoTotal;
    private Label _modeTag;

    private readonly PanelContainer[] _quickSlots = new PanelContainer[3];
    private readonly TextureRect[] _quickIcons = new TextureRect[3];
    private readonly Label[] _quickKeys = new Label[3];
    private readonly bool[] _quickActive = new bool[3];

    private PanelContainer _feedBox;
    private PanelContainer _vitalsBox;
    private RichTextLabel _messageText;
    private RichTextLabel _inspectText;

    /// <summary>右侧小数值（`100%` / `18.6kg`）的固定宽度。</summary>
    private const float ValueWidth = 42.0f;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        BuildVitals();
        BuildWeaponBox();
        BuildQuickBar();
        BuildFeed();
        BuildHint();
    }

    // ---------- 左上：生存状态（全图标化） ----------

    private void BuildVitals()
    {
        _vitalsBox = HudWidgets.Corner(LayoutPreset.TopLeft,
            UiMetrics.HudVitalsWidth, UiMetrics.HudVitalsHeight, UiMetrics.HudCorner);
        AddChild(_vitalsBox);

        var col = HudWidgets.Column(UiMetrics.GapXs);
        _vitalsBox.AddChild(col);

        col.AddChild(BuildHealthRow());
        col.AddChild(BuildStaminaRow());
        col.AddChild(BuildArmorRow("icon_armor", out _armorIcon, out _armorBar, out _armorText));
        col.AddChild(BuildArmorRow("icon_helmet", out _helmetIcon, out _helmetBar, out _helmetText));
        col.AddChild(BuildWeightRow());

        // 高度跟着内容走：护甲/头盔的条是显隐切换的，固定高度会溢出或留空
        FitVitalsBox();
    }

    /// <summary>把左上盒的高度收成内容高度（宽度固定），四角锚点不变。</summary>
    private void FitVitalsBox()
    {
        if (_vitalsBox == null) return;

        _vitalsBox.CustomMinimumSize = new Vector2(UiMetrics.HudVitalsWidth, 0.0f);
        _vitalsBox.ResetSize();

        _vitalsBox.OffsetRight = _vitalsBox.OffsetLeft + _vitalsBox.Size.X;
        _vitalsBox.OffsetBottom = _vitalsBox.OffsetTop + _vitalsBox.Size.Y;
    }

    /// <summary>生命：红十字 + 分节血条 + **条内压字**（省掉单独一行数值）。</summary>
    private Control BuildHealthRow()
    {
        var row = HudWidgets.Row(UiMetrics.GapSm);
        row.AddChild(HudWidgets.Icon("icon_hp"));

        _healthBar = HudWidgets.Bar(UiMetrics.BarHp, "bar_hp.png");
        _healthBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _healthBar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(_healthBar);

        // 条内压字：压在深红血条上，用亮米白 + 深描边
        _healthText = HudWidgets.TextInside(_healthBar, UiMetrics.FontTiny, HudWidgets.BarTextColor);
        return row;
    }

    /// <summary>耐力：闪电标 + 细琥珀条 + 纯数字。</summary>
    private Control BuildStaminaRow()
    {
        var row = HudWidgets.Row(UiMetrics.GapSm);
        row.AddChild(HudWidgets.Icon("icon_stamina"));

        _staminaBar = HudWidgets.Bar(UiMetrics.BarMicro, "bar_amber.png");
        _staminaBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _staminaBar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(_staminaBar);

        _staminaText = HudWidgets.Text("100", UiMetrics.FontTiny, HudWidgets.HintColor);
        _staminaText.CustomMinimumSize = new Vector2(ValueWidth, 0.0f);
        _staminaText.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(_staminaText);
        return row;
    }

    /// <summary>护甲 / 头盔：槽位图标（空槽半透明）+ 耐久条 + 百分比，**不印名字**。</summary>
    private Control BuildArmorRow(string kindIcon, out TextureRect icon, out ProgressBar bar, out Label value)
    {
        var row = HudWidgets.Row(UiMetrics.GapSm);

        icon = new TextureRect
        {
            Texture = UiStyle.LoadUiIcon(kindIcon),
            CustomMinimumSize = new Vector2(UiMetrics.HudIcon, UiMetrics.HudIcon),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddChild(icon);

        bar = HudWidgets.Bar(UiMetrics.BarMicro, "bar_armor.png");
        bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        bar.Visible = false;
        row.AddChild(bar);

        value = HudWidgets.Text("—", UiMetrics.FontTiny, UiStyle.EmptySlotColor);
        value.CustomMinimumSize = new Vector2(ValueWidth, 0.0f);
        value.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(value);
        return row;
    }

    /// <summary>负重：砝码图标 + 条 + `18.6kg`（上限交给条的占比表达）。</summary>
    private Control BuildWeightRow()
    {
        var row = HudWidgets.Row(UiMetrics.GapSm);
        row.AddChild(HudWidgets.Icon("icon_weight"));

        _weightBar = HudWidgets.Bar(UiMetrics.BarMicro, "bar_armor.png");
        _weightBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _weightBar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(_weightBar);

        _weightText = HudWidgets.Text("—", UiMetrics.FontTiny, HudWidgets.BodyColor);
        _weightText.CustomMinimumSize = new Vector2(ValueWidth, 0.0f);
        _weightText.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(_weightText);
        return row;
    }

    // ---------- 右下：武器与弹药（不印武器名） ----------

    private void BuildWeaponBox()
    {
        var box = HudWidgets.Corner(LayoutPreset.BottomRight,
            UiMetrics.HudWeaponWidth, UiMetrics.HudWeaponHeight, UiMetrics.HudCorner);
        AddChild(box);

        var row = HudWidgets.Row(UiMetrics.GapSm);
        box.AddChild(row);

        _weaponIcon = new TextureRect
        {
            CustomMinimumSize = new Vector2(UiMetrics.HudWeaponIconW, UiMetrics.HudWeaponIconH),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        row.AddChild(_weaponIcon);

        var col = HudWidgets.Column(0);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(col);

        var ammoRow = HudWidgets.Row(UiMetrics.GapXs);
        col.AddChild(ammoRow);

        _ammoText = HudWidgets.Text("—", UiMetrics.FontTitle, HudWidgets.AmmoColor);
        ammoRow.AddChild(_ammoText);

        _ammoTotal = HudWidgets.Text("", UiMetrics.FontTiny, HudWidgets.HintColor);
        _ammoTotal.VerticalAlignment = VerticalAlignment.Bottom;
        ammoRow.AddChild(_ammoTotal);

        _modeTag = HudWidgets.Text("", UiMetrics.FontTiny, HudWidgets.ModeColor);
        col.AddChild(_modeTag);
    }

    // ---------- 底部居中：纯图标快捷栏 ----------

    private void BuildQuickBar()
    {
        int width = UiMetrics.HudQuickSlotWidth * 3 + UiMetrics.GapSm * 2;
        var box = HudWidgets.Corner(LayoutPreset.CenterBottom,
            width, UiMetrics.HudQuickBarHeight, UiMetrics.HudCorner);
        AddChild(box);

        var row = HudWidgets.Row(UiMetrics.GapSm);
        box.AddChild(row);

        for (int i = 0; i < _quickSlots.Length; i++)
            row.AddChild(BuildQuickSlot(i));
    }

    /// <summary>一个快捷槽：左上角格号 + 占满槽位的物品图标（**不印名字**）。</summary>
    private Control BuildQuickSlot(int index)
    {
        var slot = new PanelContainer
        {
            CustomMinimumSize = new Vector2(UiMetrics.HudQuickSlotWidth, UiMetrics.HudQuickBarHeight - 10),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        slot.AddThemeStyleboxOverride("panel", UiStyle.SlotNormal());
        _quickSlots[index] = slot;

        _quickIcons[index] = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        slot.AddChild(_quickIcons[index]);
        _quickIcons[index].SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _quickIcons[index].OffsetLeft = 3.0f;
        _quickIcons[index].OffsetTop = 3.0f;
        _quickIcons[index].OffsetRight = -3.0f;
        _quickIcons[index].OffsetBottom = -3.0f;

        _quickKeys[index] = HudWidgets.Text($"{index + 1}", UiMetrics.FontTiny, UiStyle.CaptionColor);
        slot.AddChild(_quickKeys[index]);
        _quickKeys[index].SetAnchorsPreset(LayoutPreset.TopLeft);
        _quickKeys[index].OffsetLeft = 4.0f;
        _quickKeys[index].OffsetTop = 0.0f;
        _quickKeys[index].OffsetRight = 20.0f;
        _quickKeys[index].OffsetBottom = 14.0f;

        return slot;
    }

    // ---------- 右上：消息 / 查看浮窗 ----------

    private void BuildFeed()
    {
        _feedBox = HudWidgets.Corner(LayoutPreset.TopRight,
            UiMetrics.HudFeedWidth, UiMetrics.HudFeedHeight, UiMetrics.HudCorner);
        _feedBox.Visible = false;          // 没内容时完全不挡视野
        AddChild(_feedBox);

        var col = HudWidgets.Column(UiMetrics.GapXs);
        _feedBox.AddChild(col);

        _messageText = HudWidgets.Rich(UiMetrics.FontSmall);
        _messageText.Visible = false;
        col.AddChild(_messageText);

        _inspectText = HudWidgets.Rich(UiMetrics.FontSmall);
        _inspectText.Visible = false;
        col.AddChild(_inspectText);
    }

    // ---------- 左下：极简按键提示 ----------

    private void BuildHint()
    {
        // 直接压在世界地图上（不是浅色面板），所以用亮米白 + 深描边；只留两个最常用的键
        var hint = HudWidgets.Text("Tab 背包     F 互动",
            UiMetrics.FontTiny, HudWidgets.BarTextColor, outline: true);
        hint.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.55f);

        hint.SetAnchorsPreset(LayoutPreset.BottomLeft);
        hint.OffsetLeft = UiMetrics.HudCorner;
        hint.OffsetTop = -UiMetrics.HudCorner - 16;
        hint.OffsetRight = UiMetrics.HudCorner + 200;
        hint.OffsetBottom = -UiMetrics.HudCorner;
        AddChild(hint);
    }
}
