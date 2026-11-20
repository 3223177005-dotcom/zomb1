using Godot;
using System.Collections.Generic;

/// <summary>
/// 局内 HUD 的**数据侧**：解析玩家 / 武器，每 <see cref="RefreshInterval"/> 秒轮询一次，
/// 把数值喂给四角战术布局的 <see cref="HudSidebar"/>（左上生存 / 右下武器 / 底部快捷栏 / 右上浮窗）。
///
/// 控件搭建在 `HudSidebar.cs`，喂数据在 `HudSidebar.Data.cs` ——
/// 这里只做「取数据 → 调用」，所以文件守在 200 行出头（AGENTS 规则 1）。
/// </summary>
public partial class Hud : CanvasLayer
{
    /// <summary>动态内容刷新间隔（秒）。</summary>
    private const float RefreshInterval = 0.15f;

    /// <summary>查看区最多显示几行物品。</summary>
    private const int MaxInspectLines = 4;

    private static readonly Color HintColor = new(0.42f, 0.37f, 0.30f);

    private HudSidebar _sidebar;

    private Player _player;
    private Weapon _weapon;
    private Game _game;

    private float _refreshTimer;
    private Vector2I? _inspectedCell;

    public override void _Ready()
    {
        _sidebar = new HudSidebar { Name = "Hud" };
        AddChild(_sidebar);

        EventBus.Instance.PlayerHealthChanged += OnPlayerHealthChanged;
        EventBus.Instance.PlayerDied += OnPlayerDied;
        EventBus.Instance.HudVisibleRequested += OnHudVisibleRequested;

        RefreshDynamic();
    }

    /// <summary>全屏面板（Tab 背包）打开时让位：整块 HUD 收起。</summary>
    private void OnHudVisibleRequested(bool visible)
    {
        if (_sidebar != null) _sidebar.Visible = visible;
    }

    /// <summary>离开场景树时断开 EventBus（常驻跨场景，不断开会调用到已释放对象）。</summary>
    public override void _ExitTree()
    {
        if (EventBus.Instance == null) return;
        EventBus.Instance.PlayerHealthChanged -= OnPlayerHealthChanged;
        EventBus.Instance.PlayerDied -= OnPlayerDied;
        EventBus.Instance.HudVisibleRequested -= OnHudVisibleRequested;
    }

    public override void _Process(double delta)
    {
        _refreshTimer -= (float)delta;
        if (_refreshTimer <= 0.0f)
        {
            _refreshTimer = RefreshInterval;
            RefreshDynamic();
        }

        _sidebar?.TickMessages((float)delta);
        UpdateInspect();
    }

    // ---------- 刷新 ----------

    /// <summary>把玩家 / 武器状态喂给 HUD（纯读取，不改游戏状态）。</summary>
    private void RefreshDynamic()
    {
        ResolvePlayer();
        if (_player == null || _sidebar == null) return;

        _sidebar.ApplyVitals(_player.Health, _player.MaxHealth, _player.Stamina, CombatTuning.StaminaMax);
        _sidebar.ApplyArmor(_player.Equipment.Get(Player.ArmorSlot), _player.Equipment.Get(Player.HelmetSlot));
        _sidebar.ApplyWeight(_player.TotalWeight, _player.MaxCarryWeight);
        RefreshWeapon();
        _sidebar.ApplyQuickSlots(_player.Equipment, _player.ActiveSlot);
    }

    private void RefreshWeapon()
    {
        var item = _player.ActiveWeaponItem;

        if (!_player.IsRangedActive)
        {
            _sidebar.ApplyWeapon(item, false, "", 0, 0, false);
            return;
        }

        _sidebar.ApplyWeapon(
            item,
            true,
            FireModeInfo.GetDisplayName(_player.CurrentFireMode),
            _weapon?.Magazine ?? 0,
            _weapon?.ReserveAmmo ?? 0,
            _weapon?.InfiniteReserve ?? false);
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

    /// <summary>血量变化不需要立刻重算全部——把计时归零，下一帧统一刷新。</summary>
    private void OnPlayerHealthChanged(float current, float maximum) => _refreshTimer = 0.0f;

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
    /// 把某一格的物品名列到右上「查看」浮窗。
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
        if (_inspectedCell == null)
        {
            _sidebar?.SetInspect("");
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

        _sidebar?.SetInspect(sb.ToString());
    }

    // ---------- 消息 ----------

    /// <summary>添加一条信息，duration 秒后自动消失（消息状态与显示都由 <see cref="HudSidebar"/> 管）。</summary>
    public void AddMessage(string text, float duration = 3.0f) => _sidebar?.AddMessage(text, duration);
}
