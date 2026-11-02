using Godot;
using System.Collections.Generic;

/// <summary>
/// HUD —— **模仿 CDDA 的右侧竖排侧栏**：
/// 分区块、区块之间横线分隔、文字为主、信息密度高。
///
/// 区块（自上而下）：标题 / 生命 / 负重 / 手持武器 / 装备栏 / 消息 / 查看格子 / 操作提示。
///
/// 只放**有真实数据**的区块 —— CDDA 里的饥饿、口渴、体温、疼痛、部位血量
/// 我们都没有对应系统，硬做出来只是假数据。
///
/// 动态内容每 <see cref="RefreshInterval"/> 秒轮询刷新一次（读玩家属性，无副作用），
/// 比给每个变化都挂信号更不容易漏。
/// </summary>
public partial class Hud : CanvasLayer
{
    /// <summary>侧栏宽度（像素）。</summary>
    private const float SidebarWidth = 280.0f;

    /// <summary>动态内容刷新间隔（秒）。</summary>
    private const float RefreshInterval = 0.15f;

    /// <summary>消息区最多同时显示几条。</summary>
    private const int MaxVisibleMessages = 3;

    /// <summary>查看区最多显示几行物品。</summary>
    private const int MaxInspectLines = 4;

    private static readonly Color SectionColor = new(0.52f, 0.60f, 0.70f);
    private static readonly Color BodyColor = new(0.86f, 0.88f, 0.92f);
    private static readonly Color HintColor = new(0.42f, 0.46f, 0.54f);

    private Label _hpText;
    private ProgressBar _healthBar;
    private Label _weightText;
    private RichTextLabel _weaponText;
    private RichTextLabel _equipText;
    private RichTextLabel _messageText;
    private RichTextLabel _inspectText;

    private Player _player;
    private Weapon _weapon;
    private Game _game;

    private float _refreshTimer;
    private readonly List<(string text, float time)> _messages = new();
    private Vector2I? _inspectedCell;

    public override void _Ready()
    {
        BuildSidebar();

        EventBus.Instance.PlayerHealthChanged += OnPlayerHealthChanged;
        EventBus.Instance.PlayerDied += OnPlayerDied;

        RefreshDynamic();
        RefreshMessages();
        RefreshInspect();
    }

    /// <summary>离开场景树时断开 EventBus（常驻跨场景，不断开会调用到已释放对象）。</summary>
    public override void _ExitTree()
    {
        if (EventBus.Instance == null) return;
        EventBus.Instance.PlayerHealthChanged -= OnPlayerHealthChanged;
        EventBus.Instance.PlayerDied -= OnPlayerDied;
    }

    public override void _Process(double delta)
    {
        _refreshTimer -= (float)delta;
        if (_refreshTimer <= 0.0f)
        {
            _refreshTimer = RefreshInterval;
            RefreshDynamic();
        }

        UpdateMessages((float)delta);
        UpdateInspect();
    }

    // ---------- 侧栏搭建 ----------

    /// <summary>整个侧栏在代码里搭，保证字号 / 配色和物品 UI 一套。</summary>
    private void BuildSidebar()
    {
        var panel = new PanelContainer { Name = "Sidebar" };
        panel.AnchorLeft = 1; panel.AnchorRight = 1;
        panel.AnchorTop = 0; panel.AnchorBottom = 1;
        panel.OffsetLeft = -SidebarWidth;
        panel.OffsetRight = 0;
        panel.OffsetTop = 0;
        panel.OffsetBottom = 0;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.07f, 0.09f, 0.92f),
            BorderColor = new Color(0.28f, 0.31f, 0.36f, 1.0f),
            BorderWidthLeft = 2,
        });
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 3);
        margin.AddChild(content);

        // 标题
        var title = MakeLabel("余日无多", 19, new Color(0.78f, 0.82f, 0.88f));
        content.AddChild(title);
        content.AddChild(MakeSeparator());

        // 生命
        content.AddChild(MakeHeader("生命"));
        _healthBar = new ProgressBar { MaxValue = 100, Value = 100, ShowPercentage = false };
        _healthBar.CustomMinimumSize = new Vector2(0, 16);
        _healthBar.AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color(0.14f, 0.10f, 0.10f, 1.0f),
            BorderColor = new Color(0.30f, 0.22f, 0.22f, 1.0f),
            BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
        });
        _healthBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
        {
            BgColor = new Color(0.72f, 0.24f, 0.24f, 1.0f),
        });
        content.AddChild(_healthBar);
        _hpText = MakeLabel("100 / 100", 15, BodyColor);
        content.AddChild(_hpText);
        content.AddChild(MakeSeparator());

        // 负重
        content.AddChild(MakeHeader("负重"));
        _weightText = MakeLabel("—", 15, BodyColor);
        content.AddChild(_weightText);
        content.AddChild(MakeSeparator());

        // 手持
        content.AddChild(MakeHeader("手持"));
        _weaponText = MakeRich(16);
        content.AddChild(_weaponText);
        content.AddChild(MakeSeparator());

        // 装备栏
        content.AddChild(MakeHeader("装备"));
        _equipText = MakeRich(15);
        content.AddChild(_equipText);
        content.AddChild(MakeSeparator());

        // 消息
        content.AddChild(MakeHeader("消息"));
        _messageText = MakeRich(14);
        content.AddChild(_messageText);
        content.AddChild(MakeSeparator());

        // 查看格子（中键）
        content.AddChild(MakeHeader("查看"));
        _inspectText = MakeRich(14);
        content.AddChild(_inspectText);
        content.AddChild(MakeSeparator());

        // 操作提示
        content.AddChild(MakeLabel("Tab 装备  F 拾取  中键查看格子", 13, HintColor));
    }

    private static Label MakeLabel(string text, int fontSize, Color color)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Label MakeHeader(string text) => MakeLabel(text, 14, SectionColor);

    private static RichTextLabel MakeRich(int fontSize)
    {
        var rich = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.Off,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        rich.AddThemeFontSizeOverride("normal_font_size", fontSize);
        rich.AddThemeColorOverride("default_color", BodyColor);
        return rich;
    }

    private static HSeparator MakeSeparator()
    {
        var separator = new HSeparator();
        separator.AddThemeColorOverride("separator", new Color(0.26f, 0.29f, 0.34f, 1.0f));
        return separator;
    }

    // ---------- 动态内容 ----------

    /// <summary>把玩家 / 武器 / 装备的状态刷新到侧栏（纯读取，不改游戏状态）。</summary>
    private void RefreshDynamic()
    {
        ResolvePlayer();
        if (_player == null) return;

        // 生命
        _healthBar.MaxValue = _player.MaxHealth;
        _healthBar.Value = _player.Health;
        _hpText.Text = $"{Mathf.Max(0.0f, _player.Health):0} / {_player.MaxHealth:0}";

        // 负重（格子按「不含装备栏物品自身」的口径，与 Tab 面板一致）
        _weightText.Text =
            $"负重   {_player.TotalWeight:0.00} / {_player.MaxCarryWeight:0.00} kg";

        RefreshWeapon();
        RefreshEquipment();
    }

    private void RefreshWeapon()
    {
        if (_player == null) { _weaponText.Text = ""; return; }

        string name = _player.CurrentWeaponName;
        if (string.IsNullOrEmpty(name)) { _weaponText.Text = UiStyle.BbText("（空手）", HintColor); return; }

        var item = _player.ActiveWeaponItem;
        var sb = new System.Text.StringBuilder();
        sb.Append(UiStyle.BbItemName(item?.Data));

        // 近战 / 空手不显示开火模式，也**不显示弹药**
        if (_player.IsRangedActive)
        {
            sb.Append(UiStyle.BbText($"  {FireModeInfo.GetDisplayName(_player.CurrentFireMode)}",
                new Color(0.62f, 0.66f, 0.72f)));
            sb.Append('\n');

            if (_weapon != null)
            {
                string ammo = _weapon.InfiniteReserve
                    ? $"{_weapon.Magazine} / ∞"
                    : $"{_weapon.Magazine} / {_weapon.ReserveAmmo}";
                sb.Append(UiStyle.BbText($"弹药   {ammo}", new Color(0.72f, 0.76f, 0.84f)));
            }
        }

        _weaponText.Text = sb.ToString();
    }

    private void RefreshEquipment()
    {
        if (_player == null) { _equipText.Text = ""; return; }

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < _player.Equipment.SlotCount; i++)
        {
            var item = _player.Equipment.Get(i);
            bool active = _player.ActiveSlot == i;

            sb.Append(UiStyle.BbText(active ? "▸ " : "  ", UiStyle.CaptionColor));
            sb.Append(UiStyle.BbText($"[{i + 1}] ", UiStyle.CaptionColor));

            if (item?.Data == null) sb.Append(UiStyle.BbText("—", UiStyle.EmptySlotColor));
            else if (active) sb.Append(UiStyle.BbItemName(item.Data));          // 激活：亮
            else sb.Append(UiStyle.BbItemNameDimmed(item.Data));                 // 未激活：暗

            if (i < _player.Equipment.SlotCount - 1) sb.Append('\n');
        }

        _equipText.Text = sb.ToString();
    }

    private void ResolvePlayer()
    {
        if (_player != null) return;

        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (_player == null) return;

        _weapon = _player.GetNodeOrNull<Weapon>("Weapon");
        if (_weapon != null) _weapon.AmmoEmpty += OnAmmoEmpty;
        _refreshTimer = 0.0f;   // 立刻刷一次
    }

    private void OnPlayerHealthChanged(float current, float maximum)
    {
        _healthBar.MaxValue = maximum;
        _healthBar.Value = current;
        _hpText.Text = $"{Mathf.Max(0.0f, current):0} / {maximum:0}";
    }

    private void OnAmmoEmpty() => AddMessage("弹药耗尽！", 3.0f);

    private void OnPlayerDied() => AddMessage("你死了！", 5.0f);

    // ---------- 中键查看格子 ----------

    /// <summary>中键按下的那一刻，查看**鼠标所在那一格**的物品。</summary>
    private void UpdateInspect()
    {
        // 中键 = inspect 动作（和瞄准用的右键分开，避免看东西时误瞄）
        if (!Input.IsActionJustPressed("inspect")) return;

        _game ??= GetTree().GetFirstNodeInGroup("game") as Game;
        if (_game?.Grid == null) return;

        // 用玩家节点换算鼠标的世界坐标（HUD 是 CanvasLayer，没有 GetGlobalMousePosition）
        var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        if (player == null) return;

        ShowCellContents(_game.Grid.WorldToCell(player.GetGlobalMousePosition()));
    }

    /// <summary>
    /// 把某一格的物品名列到侧栏的「查看」区。
    /// ★ 物品名颜色一律走 <see cref="UiStyle"/>（取自稀有度），不在这里写死。
    /// </summary>
    public void ShowCellContents(Vector2I cell)
    {
        _game ??= GetTree().GetFirstNodeInGroup("game") as Game;
        if (_game?.Items == null) return;

        _inspectedCell = cell;
        RefreshInspect();
    }

    private void RefreshInspect()
    {
        if (_inspectText == null) return;

        if (_inspectedCell == null)
        {
            _inspectText.Text = UiStyle.BbText("（中键点一格）", HintColor);
            return;
        }

        var cell = _inspectedCell.Value;
        var rows = _game?.Items?.SummarizeCell(cell) ?? new List<ItemSummary>();

        var sb = new System.Text.StringBuilder();
        sb.Append(UiStyle.BbText($"格 ({cell.X}, {cell.Y})", UiStyle.CaptionColor)).Append('\n');

        if (rows.Count == 0)
        {
            sb.Append(UiStyle.BbText("（空的）", UiStyle.EmptySlotColor));
        }
        else
        {
            int shown = Mathf.Min(rows.Count, MaxInspectLines);
            for (int i = 0; i < shown; i++)
            {
                sb.Append(UiStyle.BbItemName(rows[i].Data, $" ×{rows[i].Count}"));
                if (i < shown - 1) sb.Append('\n');
            }
            if (rows.Count > shown)
                sb.Append('\n').Append(UiStyle.BbText($"…还有 {rows.Count - shown} 种", HintColor));
        }

        _inspectText.Text = sb.ToString();
    }

    // ---------- 消息 ----------

    /// <summary>添加一条信息，duration 秒后自动消失。</summary>
    public void AddMessage(string text, float duration = 3.0f)
    {
        _messages.Add((text, duration));
        RefreshMessages();
    }

    private void UpdateMessages(float delta)
    {
        bool changed = false;
        for (int i = _messages.Count - 1; i >= 0; i--)
        {
            var (text, time) = _messages[i];
            time -= delta;
            if (time <= 0.0f)
            {
                _messages.RemoveAt(i);
                changed = true;
            }
            else
            {
                _messages[i] = (text, time);
            }
        }
        if (changed) RefreshMessages();
    }

    private void RefreshMessages()
    {
        if (_messageText == null) return;

        if (_messages.Count == 0)
        {
            _messageText.Text = UiStyle.BbText("—", HintColor);
            return;
        }

        var sb = new System.Text.StringBuilder();
        int start = Mathf.Max(0, _messages.Count - MaxVisibleMessages);
        for (int i = start; i < _messages.Count; i++)
        {
            if (i > start) sb.Append('\n');
            sb.Append(UiStyle.BbText("▸ ", UiStyle.CaptionColor)).Append(_messages[i].text);
        }
        _messageText.Text = sb.ToString();
    }
}
