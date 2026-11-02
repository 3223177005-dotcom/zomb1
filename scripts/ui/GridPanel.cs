using Godot;
using System.Collections.Generic;

/// <summary>
/// **格子面板**：把一个 <see cref="Inventory"/> 画成二维格子，物品按 W×H 摆成块，
/// 并负责**按格坐标**的拖放（阶段 4 的核心控件）。
///
/// 用法（见 EquipmentPanel / ModifyPanel）：
/// <code>
/// var grid = new GridPanel { Pocket = pocket, CellPx = 34, Changed = Refresh };
/// parent.AddChild(grid);
/// grid.Rebuild();          // 内容变了就再调一次
/// </code>
///
/// 交互：
/// - **拖出**：物品块本身就是拖拽源，载荷是**这一份堆叠**（单叠），
///   同时记下来源格坐标（<see cref="ItemDrag.SourceX"/>）。
/// - **拖入**：本控件按鼠标位置算目标格；落在空格就摆在那儿（放不下会**自动试 90° 旋转**），
///   落在**同类堆叠**上就合并。拖拽悬停时把 W×H 范围染色（绿=能放，红=不能放）。
/// - **右键**：回调 <see cref="ContextRequested"/>（丢弃 / 拆分 / 改装，由面板复用原菜单）。
/// - **双击**：回调 <see cref="Activated"/>。
/// </summary>
public partial class GridPanel : Control
{
    /// <summary>格子四周留白（像素）。</summary>
    public const int Pad = 4;

    private static readonly Color BgColor = new(0.05f, 0.06f, 0.08f, 0.85f);
    private static readonly Color CellColor = new(0.11f, 0.12f, 0.15f, 0.95f);
    private static readonly Color CellBorder = new(0.20f, 0.22f, 0.27f, 1.0f);
    private static readonly Color OkFill = new(0.28f, 0.85f, 0.45f, 0.30f);
    private static readonly Color OkLine = new(0.35f, 0.95f, 0.55f, 0.95f);
    private static readonly Color BadFill = new(0.95f, 0.30f, 0.30f, 0.28f);
    private static readonly Color BadLine = new(1.0f, 0.40f, 0.40f, 0.95f);

    private readonly List<GridItemBlock> _blocks = new();

    private int _hoverX = -1;
    private int _hoverY = -1;
    private int _hoverW = 1;
    private int _hoverH = 1;
    private bool _hoverOk;

    /// <summary>悬停高亮层（画在**物品块上面**，否则被块盖住看不见）。</summary>
    private Panel _hoverCell;
    private StyleBoxFlat _hoverOkStyle;
    private StyleBoxFlat _hoverBadStyle;

    /// <summary>要显示的口袋（必须设置）。</summary>
    public Inventory Pocket { get; set; }

    /// <summary>一格多少像素。</summary>
    public int CellPx { get; set; } = 40;

    /// <summary>内容变了（拖放成功）后回调，通常指向面板的 Refresh。</summary>
    public System.Action Changed { get; set; }

    /// <summary>右键某个物品（物品、所属口袋、屏幕坐标）。</summary>
    public System.Action<ItemStack, Inventory, Vector2> ContextRequested { get; set; }

    /// <summary>双击某个物品（物品、所属口袋）。</summary>
    public System.Action<ItemStack, Inventory> Activated { get; set; }

    /// <summary>拖拽划过放不下的位置时，给一句人话（null = 恢复正常）。</summary>
    public System.Action<string> Message { get; set; }

    /// <summary>这一份是不是已经揭示（搜刮容器用；null = 全部可见）。</summary>
    public System.Func<ItemStack, bool> IsRevealed { get; set; }

    /// <summary>未揭示物品的进度 0~1（搜刮容器用）。</summary>
    public System.Func<ItemStack, float> RevealProgress { get; set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;   // 空格子也要接住拖放
        ClipContents = false;

        _hoverOkStyle = MakeHoverStyle(OkFill, OkLine);
        _hoverBadStyle = MakeHoverStyle(BadFill, BadLine);

        _hoverCell = new Panel { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _hoverCell.AddThemeStyleboxOverride("panel", _hoverOkStyle);
        AddChild(_hoverCell);
    }

    private static StyleBoxFlat MakeHoverStyle(Color fill, Color line) => new()
    {
        BgColor = fill,
        BorderColor = line,
        BorderWidthTop = 2,
        BorderWidthBottom = 2,
        BorderWidthLeft = 2,
        BorderWidthRight = 2,
    };

    // ---------- 构建 ----------

    /// <summary>按当前 <see cref="Pocket"/> 重建所有物品块（调用前本控件应在场景树里）。</summary>
    public void Rebuild()
    {
        foreach (var block in _blocks)
        {
            RemoveChild(block);
            block.QueueFree();
        }
        _blocks.Clear();
        ClearHover();

        if (Pocket == null)
        {
            CustomMinimumSize = Vector2.Zero;
            QueueRedraw();
            return;
        }

        int cell = Mathf.Max(16, CellPx);
        CustomMinimumSize = new Vector2(Pocket.GridW * cell + Pad * 2, Pocket.GridH * cell + Pad * 2);

        foreach (var stack in Pocket.Stacks)
        {
            if (stack?.Data == null) continue;
            if (stack.GridX < 0 || stack.GridY < 0) continue;   // 没摆进格子的不画

            var size = GridInventory.SizeOf(stack, stack.Rotated);
            var block = new GridItemBlock
            {
                Name = $"Item_{stack.Data.Id}",
                Grid = this,
                Stack = stack,
                Pocket = Pocket,
                Position = new Vector2(Pad + stack.GridX * cell + 1, Pad + stack.GridY * cell + 1),
                Size = new Vector2(size.X * cell - 2, size.Y * cell - 2),
            };
            _blocks.Add(block);
            AddChild(block);
        }

        // 高亮层必须留在**最后**（Godot 的绘制顺序 = 子节点顺序）
        if (_hoverCell != null) MoveChild(_hoverCell, GetChildCount() - 1);

        QueueRedraw();
    }

    /// <summary>只刷新未揭示物品的进度条（搜刮读条每帧调，不重建节点）。</summary>
    public void RefreshRevealProgress()
    {
        foreach (var block in _blocks) block.RefreshProgress();
    }

    // ---------- 绘制 ----------

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), BgColor, true);

        if (Pocket == null) return;

        int cell = Mathf.Max(16, CellPx);
        for (int y = 0; y < Pocket.GridH; y++)
        {
            for (int x = 0; x < Pocket.GridW; x++)
            {
                var rect = new Rect2(Pad + x * cell + 1, Pad + y * cell + 1, cell - 2, cell - 2);
                DrawRect(rect, CellColor, true);
                DrawRect(rect, CellBorder, false, 1);
            }
        }
    }

    public override void _Notification(int what)
    {
        // 尺寸变了要重画（格子按 Size 之外的地方是背景）
        if (what == (int)NotificationResized) QueueRedraw();

        // 鼠标离开 / 拖拽结束 → 取消高亮
        if (what == (int)NotificationMouseExit || what == (int)NotificationDragEnd) ClearHover();
    }

    // ---------- 坐标 ----------

    /// <summary>本地坐标 → 格子坐标（可能越界，调用方自己判断）。</summary>
    public Vector2I CellAt(Vector2 local)
        => new(Mathf.FloorToInt((local.X - Pad) / Mathf.Max(1, CellPx)),
               Mathf.FloorToInt((local.Y - Pad) / Mathf.Max(1, CellPx)));

    // ---------- 拖放：由 GridPanel 自己接，也由物品块转发 ----------

    public override bool _CanDropData(Vector2 atPosition, Variant data) => CanDropAt(atPosition);

    public override void _DropData(Vector2 atPosition, Variant data) => DropAt(atPosition);

    /// <summary>拖拽悬停判定（<paramref name="local"/> 是相对本控件的坐标）。</summary>
    public bool CanDropAt(Vector2 local)
    {
        bool ok = Evaluate(local, out string reason);

        // 拖拽中划过放不下的位置 → 告诉玩家原因
        if (ItemDrag.Active) Message?.Invoke(ok ? null : reason);

        return ok;
    }

    /// <summary>放下（<paramref name="local"/> 是相对本控件的坐标）。</summary>
    public void DropAt(Vector2 local)
    {
        var stack = ItemDrag.Single;
        if (!ItemDrag.Active || Pocket == null) { ClearHover(); ItemDrag.End(); return; }

        if (!Evaluate(local, out _)) { ClearHover(); ItemDrag.End(); return; }

        if (stack == null)
        {
            // 多叠载荷 → 整行自动排布
            ItemDrag.TryMove(Pocket);
            ClearHover();
            ItemDrag.End();
            Changed?.Invoke();
            return;
        }

        var occupant = Pocket.At(_hoverX, _hoverY);
        bool merged = false;

        if (occupant != null && !ReferenceEquals(occupant, stack))
        {
            // 同类堆叠 → 合并
            merged = ItemDrag.TryMergeInto(occupant) > 0;
        }

        if (!merged)
            ItemDrag.TryPlaceAt(Pocket, _hoverX, _hoverY);

        ClearHover();
        ItemDrag.End();
        Changed?.Invoke();
    }

    // ---------- 判定 ----------

    private bool Evaluate(Vector2 local, out string reason)
    {
        reason = "";

        var stack = ItemDrag.Single;
        if (!ItemDrag.Active || Pocket == null || stack?.Data == null)
        {
            reason = "没有可放下的物品";
            return false;
        }

        var hover = CellAt(local);
        if (hover.X < 0 || hover.Y < 0 || hover.X >= Pocket.GridW || hover.Y >= Pocket.GridH)
        {
            reason = "放到格子外面了";
            return false;
        }

        // ⓪ 多叠载荷（例如地面物品树里"3 个绷带"那一行）：没有单一格位，
        //    整行交给口袋**自动排布**（能放几叠放几叠）
        if (ItemDrag.Single == null)
        {
            if (ItemDrag.CanDrop(Pocket))
            {
                SetHover(hover.X, hover.Y, Vector2I.One, true);
                return true;
            }
            reason = "这些物品放不进这个口袋";
            SetHover(hover.X, hover.Y, Vector2I.One, false);
            return false;
        }

        var occupant = Pocket.At(hover.X, hover.Y);

        // ① 同类堆叠 → 合并（高亮那一叠的大小）
        if (occupant != null && !ReferenceEquals(occupant, stack))
        {
            var occupantSize = GridInventory.SizeOf(occupant, occupant.Rotated);
            if (occupant.Data == stack.Data && occupant.RemainingStackSpace > 0)
            {
                SetHover(occupant.GridX, occupant.GridY, occupantSize, true);
                return true;
            }

            reason = $"这一格被「{occupant.Data?.Name}」占着";
            SetHover(occupant.GridX, occupant.GridY, occupantSize, false);
            return false;
        }

        // ② 空格 → 找一个「占位能包含鼠标那一格」的落点（鼠标不用精确对准左上角）
        if (!ResolveCell(hover, stack, out int x, out int y, out bool rotated))
        {
            var size = GridInventory.SizeOf(stack, stack.Rotated);
            SetHover(hover.X, hover.Y, size, false);
            reason = Pocket.FitsWhenEmpty(stack)
                ? $"「{stack.Data.Name}」放不进这个位置"
                : $"这个口袋放不下「{stack.Data.Name}」";
            return false;
        }

        var fit = GridInventory.SizeOf(stack, rotated);
        SetHover(x, y, fit, true);
        return true;
    }

    /// <summary>
    /// 把「鼠标所在格」换算成能放下的左上角：
    /// 先试正对准，再按 dx/dy 让物品块把鼠标那格包进去（更宽容的手感）。
    /// 朝向优先保持原样，放不下才试旋转。
    /// </summary>
    private bool ResolveCell(Vector2I hover, ItemStack stack, out int x, out int y, out bool rotated)
    {
        x = y = 0;
        rotated = false;

        foreach (bool rot in new[] { stack.Rotated, !stack.Rotated })
        {
            var size = GridInventory.SizeOf(stack, rot);
            if (size.X > Pocket.GridW || size.Y > Pocket.GridH) continue;

            for (int dy = 0; dy < size.Y; dy++)
            {
                for (int dx = 0; dx < size.X; dx++)
                {
                    int cx = hover.X - dx;
                    int cy = hover.Y - dy;

                    if (!Pocket.CanPlace(stack, cx, cy, rot)) continue;

                    x = cx;
                    y = cy;
                    rotated = rot;
                    return true;
                }
            }
        }
        return false;
    }

    private void SetHover(int x, int y, Vector2I size, bool ok)
    {
        _hoverX = x;
        _hoverY = y;
        _hoverW = Mathf.Max(1, size.X);
        _hoverH = Mathf.Max(1, size.Y);
        _hoverOk = ok;

        if (_hoverCell == null) return;

        int cell = Mathf.Max(16, CellPx);
        _hoverCell.Position = new Vector2(Pad + x * cell + 1, Pad + y * cell + 1);
        _hoverCell.Size = new Vector2(_hoverW * cell - 2, _hoverH * cell - 2);
        _hoverCell.AddThemeStyleboxOverride("panel", ok ? _hoverOkStyle : _hoverBadStyle);
        _hoverCell.Visible = true;
        MoveChild(_hoverCell, GetChildCount() - 1);
    }

    private void ClearHover()
    {
        _hoverX = _hoverY = -1;
        if (_hoverCell == null) return;
        _hoverCell.Visible = false;
    }
}
