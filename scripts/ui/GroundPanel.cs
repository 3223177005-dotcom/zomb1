using Godot;
using System.Collections.Generic;

/// <summary>
/// 地面拾取面板。
///
/// 按 <c>interact</c>（F / E）时：
/// - 目标格里**只有一个堆叠** → 直接捡起，不开面板；
/// - 有**多个堆叠** → 弹出本面板，把这一格的内容按**物品类型**列出来让你挑
///   （对应 CDDA 在 `g` 时的拾取菜单）。
///
/// 打开时显示鼠标并通知角色屏蔽开火（复用 <see cref="EventBus.UiPanelToggled"/>）。
/// </summary>
public partial class GroundPanel : CanvasLayer
{
    private Control _root;
    private Label _title;
    private VBoxContainer _list;
    private Player _player;
    private Game _game;
    private bool _open;

    /// <summary>当前查看的格子。</summary>
    private Vector2I _cell = new Vector2I(-1, -1);

    public bool IsOpen => _open;

    /// <summary>当前查看的格子。</summary>
    public Vector2I Cell => _cell;

    public override void _Ready()
    {
        _root = GetNode<Control>("Root");
        // 用「场景唯一名」取节点：版面是容器化的，嵌套路径会随布局改动而变
        _title = GetNode<Label>("%Title");
        _list = GetNode<VBoxContainer>("%List");

        SetOpen(false);
    }

    public override void _Process(double delta)
    {
        // 注意：找不到节点时**不能提前 return**，否则会吞掉这一帧的按键
        if (_player == null) _player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (_game == null) _game = GetTree().GetFirstNodeInGroup("game") as Game;

        if (!Input.IsActionJustPressed("interact")) return;
        if (_player == null || _game?.Items == null) return;

        if (_open)
        {
            SetOpen(false);
            return;
        }

        // ★★ **战利品容器（尸体）优先**：按 F 不捡东西，而是打开 Tab 面板开始搜索
        var spot = FindLootSpotInReach();
        if (spot != null)
        {
            var equip = GetParent()?.GetNodeOrNull<EquipmentPanel>("EquipmentPanel");
            if (equip != null)
            {
                equip.OpenSearch(spot);
                return;
            }
        }

        var cell = _player.FindPickupTargetCell(_game.Items);
        if (cell.X < 0) return;                       // 附近没东西

        var inv = _game.Items.GetCell(cell);
        if (inv == null) return;

        // ★ 这一格里有容器 → 一定开面板：要让你进容器里挑东西
        bool hasContainer = false;
        foreach (var stack in inv.Stacks)
        {
            if (stack.IsContainer) { hasContainer = true; break; }
        }

        // 只有 1 叠、捡得起来、又不是容器 → 直接捡，不开面板
        if (!hasContainer && inv.StackCount == 1
            && _player.CanPickUp(inv.Stacks[0]) == ContainCode.Success)
        {
            _player.TryPickupFromGround(_game.Items, out _);
            return;
        }

        // 其它情况（有容器 / 多叠 / 唯一那叠捡不起来）→ 开面板
        Open(cell);
    }

    /// <summary>
    /// 找**在交互距离内**的战利品容器（尸体）——取最近的那个。
    /// 战利品容器不在露天物品的格子里，所以必须单独找。
    /// </summary>
    private LootSpot FindLootSpotInReach()
    {
        if (_game?.Items == null || _player == null) return null;

        const float reach = 40.0f;
        var origin = _player.GlobalPosition;

        LootSpot best = null;
        float bestDistance = float.MaxValue;

        foreach (var cell in _game.Items.LootSpotCells)
        {
            var spot = _game.Items.GetLootSpot(cell);
            if (spot == null || spot.IsEmpty) continue;    // 搜空的不再触发

            float distance = origin.DistanceTo(_game.Grid.CellCenter(cell));
            if (distance > reach || distance >= bestDistance) continue;

            best = spot;
            bestDistance = distance;
        }
        return best;
    }

    /// <summary>打开面板并查看指定格子。</summary>
    public void Open(Vector2I cell)
    {
        _cell = cell;
        SetOpen(true);
    }

    /// <summary>打开 / 关闭面板。</summary>
    /// <summary>「关闭自己」的回调（放进 <see cref="PanelStack"/>，Esc 按顺序关）。</summary>
    private readonly System.Action _closeSelf = null;

    public GroundPanel() { _closeSelf = () => SetOpen(false); }

    public void SetOpen(bool open)
    {
        PanelStack.Set(_closeSelf, open);
        _open = open;
        if (_root != null) _root.Visible = open;

        // 面板打开时显示鼠标，关闭时交还准星
        if (_player == null || !_player.IsDead)
            Input.MouseMode = open ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Hidden;

        EventBus.Instance.EmitSignal(EventBus.SignalName.UiPanelToggled, open);

        if (open) Refresh();
    }

    /// <summary>重建列表。显示**物品树**：容器行下面缩进列出它的内容物，每行都能单独捡。</summary>
    public void Refresh()
    {
        if (_list == null || _game?.Items == null) return;

        // 清掉旧行（先 RemoveChild 再 QueueFree，否则同一帧内还会被 GetChildren 取到）
        foreach (var child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }

        var nodes = GroundTree.Flatten(_game.Items.GetCell(_cell));
        if (_title != null) _title.Text = $"地上的物品  格 ({_cell.X}, {_cell.Y})";

        if (nodes.Count == 0)
        {
            SetOpen(false);      // 已经拿光了
            return;
        }

        foreach (var node in nodes) _list.AddChild(MakeNodeRow(node));
    }

    /// <summary>当前格子本身就是一个 <see cref="Inventory"/>（地面格 = 一个容器），直接当拖动来源。</summary>
    private Inventory GroundSource => _game?.Items?.GetCell(_cell);

    /// <summary>
    /// 把树里的一行做成 <see cref="ItemSlot"/>（和背包 / 搜索面板同一种行）：
    /// **悬停有属性面板、单击捡走、拖拽高亮样式一致**。
    /// </summary>
    private Control MakeNodeRow(GroundNode node)
    {
        // 预检：这一行能不能捡起来（按「一整叠」的量判断）
        int probe = Mathf.Min(node.Count, Mathf.Max(1, node.Data.StackLimit));
        var reason = _player != null
            ? _player.CanPickUp(new ItemStack(node.Data, probe))
            : ContainCode.Success;
        bool pickable = reason == ContainCode.Success;

        var summary = new ItemSummary(node.Data.Id, node.Data, node.Count, node.Weight, node.Stacks.Count);

        var content = ItemRow.Build(
            summary,
            pickable ? null : $"（{ContainCodeText.Short(reason)}）",
            dimmed: !pickable,
            indent: node.Depth,
            marker: node.IsContainer ? "▾" : null);      // 容器加个标记，一眼看出能进去

        var row = new ItemSlot
        {
            Name = "GroundRow",
            // ★ 载荷用**真实堆叠引用**（不是临时克隆）：
            //   否则拖到别处时"从源里扣掉"会扣错，地上的那份会留下来变成复制，
            //   容器搬走时内容物也会丢。
            Items = new List<ItemStack>(node.Stacks),
            Pocket = GroundSource,          // ★ 伪容器：拖拽来源（拖走后会写回地面）
            Changed = Refresh,
            TooltipText = ItemTooltip.Build(
                new ItemStack(node.Data, Mathf.Max(1, node.Count)),
                pickable ? "" : $"捡不起来：{ContainCodeText.Short(reason)}"),
        };

        // 左键单击 = 捡（保持原来的手感；双击留给别处）
        if (pickable) row.Clicked = _ => OnNodePressed(node);

        row.AddChild(content);
        ItemRow.IgnoreMouse(content);                    // 鼠标事件留给 ItemSlot
        return row;
    }

    /// <summary>左键点树里的一行 → 把这一行涉及的东西尽量捡起（可以在容器内部）。</summary>
    private void OnNodePressed(GroundNode node)
    {
        if (_player == null || _game?.Items == null) return;

        _player.TryPickupNode(_game.Items, node);   // 内部会清空地面格
        Refresh();
    }
}
