using Godot;
using System.Collections.Generic;

/// <summary>
/// 状态栏面板：按 Tab 打开 / 关闭。
///
/// 面板布局：
///   左上 —— 血量 / 健康信息（**预留区域，尚未实现**）
///   右侧 —— 身上带的物品（基础口袋 + 装备容器，可**拖动换口袋**、可**右键丢弃**）
///   左下 —— 装备武器槽（可用鼠标左键点选切换武器）
///
/// 打开时会显示鼠标，并通知角色屏蔽开火（点槽位也是左键，不屏蔽会同时开枪）。
/// 不做的事：不暂停游戏。
/// </summary>
public partial class EquipmentPanel : CanvasLayer
{
    /// <summary>右键菜单里「丢弃」的条目 Id。</summary>
    private const int MenuIdDrop = 1;

    /// <summary>右键菜单里「拆分」的条目 Id。</summary>
    private const int MenuIdSplit = 2;

    /// <summary>右键菜单：「改装」（只对枪有效）。</summary>
    private const int MenuIdModify = 3;

    /// <summary>改装面板（建在 _Ready 里，平时隐藏）。</summary>
    private ModifyPanel _modifyPanel;

    /// <summary>「关闭自己」的回调，放进 <see cref="PanelStack"/>，让 Esc 关最新打开的那层。</summary>
    private readonly System.Action _closeSelf = null;

    public EquipmentPanel() { _closeSelf = () => SetOpen(false); }

    /// <summary>Esc：关掉最新打开的面板（栈由 <see cref="PanelStack"/> 维护）。</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel")) return;
        if (PanelStack.CloseTop()) GetViewport().SetInputAsHandled();
    }

    private Control _root;
    private Button[] _slotButtons;
    private Label _bagTotal;
    private VBoxContainer _bagList;
    private PopupMenu _contextMenu;
    private SplitRow _splitRow;              // 行内拆分（正在拆的那一叠）
    private ItemStack _splitStack;
    private Inventory _splitPocket;

    // ---------- 搜索战利品容器 ----------
    private LootSpot _searchSpot;
    private float _searchProgress;           // 0..1（整箱总进度，给外部看）
    private readonly List<ItemStack> _searchOrder = new();   // 逐件搜索的顺序
    private int _searchIndex;                // 正在搜第几件
    private float _searchItemElapsed;        // **当前这一件**已经搜了多久 —— 进度条用这个
    private int _searchRevealedCount = -1;   // 上一次刷新的"已揭示件数"（用来判断要不要重建列表）
    private bool _searchDone;
    private VBoxContainer _searchColumn;
    private Label _searchHint;
    private VBoxContainer _searchList;
    private GridPanel _searchGrid;           // 战利品容器本身的格子（阶段 4）

    /// <summary>当前是否正在查看某个战利品容器。</summary>
    public bool Searching => _searchSpot != null;

    /// <summary>搜索进度 0..1（给外部/测试看）。</summary>
    public float SearchProgress => _searchProgress;

    /// <summary>是否已经搜索完成（物品已揭示）。</summary>
    public bool SearchDone => _searchDone;

    /// <summary>搜索列当前显示了几行（调试 / 自检用）。</summary>
    public int SearchRowCount => _searchList?.GetChildCount() ?? 0;
    private ItemSlot _contextSlot;
    private Player _player;
    private bool _open;

    /// <summary>面板是否处于打开状态。</summary>
    public bool IsOpen => _open;

    public override void _Ready()
    {
        _root = GetNode<Control>("Root");
        _slotButtons = new[]
        {
            GetNode<Button>("%Slot1"),
            GetNode<Button>("%Slot2"),
            GetNode<Button>("%Slot3"),
        };
        // 用「场景唯一名」取节点：版面是容器化的，嵌套路径会随布局改动而变
        _bagList = GetNode<VBoxContainer>("%BagList");
        _bagTotal = GetNode<Label>("%BagTotal");

        for (int i = 0; i < _slotButtons.Length; i++)
        {
            int slotIndex = i;   // 闭包捕获，必须用局部变量
            _slotButtons[i].Pressed += () => OnSlotPressed(slotIndex);
            _slotButtons[i].GuiInput += e => OnEquipSlotGuiInput(slotIndex, e);

            // 拖放接收层：把背包里的武器 / 容器拖到装备槽上
            var dropTarget = new EquipDropTarget
            {
                Name = "DropTarget",
                SlotIndex = slotIndex,
                Dropped = OnEquipSlotDropped,
            };
            _slotButtons[i].AddChild(dropTarget);
            dropTarget.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }

        EventBus.Instance.InventoryChanged += OnInventoryChanged;

        BuildContextMenu();
        BuildSearchColumn();

        SetOpen(false);
    }

    /// <summary>建右键菜单（丢弃 / 拆分）和拆分对话框。</summary>
    private void BuildContextMenu()
    {
        _contextMenu = new PopupMenu { Name = "ItemContextMenu" };
        _contextMenu.AddItem("丢弃", MenuIdDrop);
        _contextMenu.AddItem("拆分", MenuIdSplit);
        _contextMenu.AddItem("改装", MenuIdModify);

        // 改装面板：铺满整个面板，平时隐藏
        _modifyPanel = new ModifyPanel { Name = "ModifyPanel", Visible = false };
        AddChild(_modifyPanel);
        _contextMenu.IdPressed += OnContextMenuPressed;

        // 字号跟主 UI 对齐（UI 尺寸 ×2 后统一 22）
        _contextMenu.AddThemeFontSizeOverride("font_size", 22);

        AddChild(_contextMenu);
    }

    /// <summary>右键点某一行 → 在鼠标位置弹出菜单。</summary>
    private void OnSlotContextRequested(ItemSlot slot, Vector2 screenPosition)
    {
        if (slot?.Items == null || slot.Items.Count == 0) return;

        _contextSlot = slot;

        // 「拆分」只在**存在数量 > 1 的堆叠**时才可用（否则灰掉）
        int splitIndex = _contextMenu.GetItemIndex(MenuIdSplit);
        if (splitIndex >= 0) _contextMenu.SetItemDisabled(splitIndex, FindSplittable(slot) == null);

        // 「改装」只对**有槽位的武器**可用
        int modifyIndex = _contextMenu.GetItemIndex(MenuIdModify);
        if (modifyIndex >= 0)
            _contextMenu.SetItemDisabled(modifyIndex,
                slot.Items == null || slot.Items.Count == 0 || !WeaponMods.CanModify(slot.Items[0]));

        // 用 Popup(Rect2I) 定位：只设 Position 再调 Popup() 会被 PopupMenu 内部重算掉，
        // 菜单会跑到左上角。尺寸给 0 表示"按内容自适应"。
        _contextMenu.Popup(new Rect2I(
            new Vector2I(Mathf.RoundToInt(screenPosition.X), Mathf.RoundToInt(screenPosition.Y)),
            Vector2I.Zero));
    }

    /// <summary>找这一行里**第一个可拆的堆叠**（数量 &gt; 1）；没有就返回 null。</summary>
    private static ItemStack FindSplittable(ItemSlot slot)
    {
        if (slot?.Items == null) return null;

        foreach (var stack in slot.Items)
            if (stack.Count > 1) return stack;

        return null;
    }

    private void OnContextMenuPressed(long id)
    {
        var slot = _contextSlot;
        _contextSlot = null;

        if (slot == null) return;

        if (id == MenuIdModify)
        {
            _modifyPanel?.Open(slot.Items[0]);
            return;
        }

        if (id == MenuIdDrop)
        {
            DropSlot(slot);
        }
        else if (id == MenuIdSplit)
        {
            // 行内拆分：不弹窗口，让那一行自己变成滑块行
            var stack = FindSplittable(slot);
            if (stack != null)
            {
                _splitStack = stack;
                _splitPocket = slot.Pocket;
                Refresh();
            }
        }
    }

    /// <summary>
    /// 把背包里的东西**拖进装备栏**：装上新的，旧的放回身上。
    /// </summary>
    private void OnEquipSlotDropped(int slotIndex, ItemStack incoming)
    {
        if (_player == null || incoming?.Data == null) return;

        // 拖拽来源（一般是基础口袋或某个容器）
        var source = ItemDrag.Source ?? _player.Pocket;

        // 先装备，再移除 —— 顺序反了 incoming 就被销毁了
        var previous = _player.Equipment.Equip(slotIndex, incoming);
        source?.RemoveFrom(incoming, 1);

        // 兜底：来源不是拖拽记录的容器（或没扣掉）时，从身上再扫一遍
        if (incoming.Count > 0 && _player.CountItem(incoming.Data.Id) > 0)
            _player.ConsumeItem(incoming.Data.Id, 1);

        // 被换下来的旧物品放回身上（剩余空间最大的口袋）
        if (previous != null && !ReferenceEquals(previous, incoming))
            _player.TryTake(previous, out _);

        // 装到哪个槽就顺手切到那个槽
        _player.TrySetActiveSlot(slotIndex);

        Refresh();
    }

    /// <summary>
    /// **右键装备栏的槽** → 弹出和背包一样的菜单（丢弃 / 拆分 / 改装）。
    ///
    /// 做法：造一个**不在场景树里**的 <see cref="ItemSlot"/> 当数据载体
    /// （带 <see cref="ItemSlot.EquipSlotIndex"/>），复用同一套菜单处理逻辑。
    /// </summary>
    private void OnEquipSlotGuiInput(int slotIndex, InputEvent @event)
    {
        if (@event is not InputEventMouseButton mb) return;
        if (!mb.Pressed || mb.ButtonIndex != MouseButton.Right) return;

        var equipped = _player?.Equipment.Get(slotIndex);
        if (equipped == null) return;          // 空槽没有菜单

        var proxy = new ItemSlot
        {
            Items = new List<ItemStack> { equipped },
            EquipSlotIndex = slotIndex,
        };

        OnSlotContextRequested(proxy, mb.GlobalPosition);
    }

    /// <summary>
    /// 把某一行涉及的东西**全丢在离人物最近的一格**
    /// （就是人物所在那一格；那一格放不下时 <see cref="WorldItems.Drop"/> 会自动顺延到相邻格）。
    /// </summary>
    private void DropSlot(ItemSlot slot)
    {
        var game = GetTree().GetFirstNodeInGroup("game") as Game;
        if (_player == null || game?.Items == null || slot?.Items == null) return;

        // ★ 装备栏的槽没有 Pocket —— 先卸下，再丢地上
        if (slot.EquipSlotIndex >= 0)
        {
            var equipped = _player.Equipment.Unequip(slot.EquipSlotIndex);
            if (equipped != null) game.Items.Drop(_player.GlobalPosition, equipped);

            // 丢掉的可能正是手上那把 → 重算手上状态（枪的贴图 / 开火数据）
            _player.RefreshActiveWeapon();
            Refresh();
            return;
        }

        if (slot.Pocket == null) return;

        // 先拷贝一份：丢弃过程中源列表会被改动
        var stacks = new List<ItemStack>(slot.Items);

        foreach (var stack in stacks)
        {
            int count = stack.Count;
            if (count <= 0) continue;

            var leftover = game.Items.Drop(_player.GlobalPosition, stack);
            int dropped = count - (leftover?.Count ?? 0);

            if (dropped > 0) slot.Pocket.RemoveFrom(stack, dropped);
        }

        Refresh();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null) EventBus.Instance.InventoryChanged -= OnInventoryChanged;
    }

    public override void _Process(double delta)
    {
        // 注意：找不到玩家时**不能提前 return**，否则会吞掉这一帧的按键
        // （IsActionJustPressed 只在按下那一帧为真，丢掉就永久丢失）。
        if (_player == null)
            _player = GetTree().GetFirstNodeInGroup("player") as Player;

        if (Input.IsActionJustPressed("inventory"))
            SetOpen(!_open);

        // 搜索读条：按内容最高稀有度算出的时长推进，满了才揭示物品
        if (Searching && !_searchDone)
            AdvanceSearch((float)delta);

        // ★★ 绝对不能在这里每帧 Refresh()！
        //   列表里每一行都是可拖放的 ItemSlot，每帧重建会把鼠标底下的控件换掉，
        //   而 Godot 的拖放系统要求**同一个控件持续接收鼠标移动**才能启动拖拽 ——
        //   每帧重建 = 拖拽永远启动不了。
        //   改成事件驱动：开面板时刷一次（SetOpen），之后靠 ItemSlot.Changed 回调、
        //   按装备槽（OnSlotPressed）、以及 EventBus.InventoryChanged。
    }

    /// <summary>身上东西变了（捡到 / 丢掉）→ 面板开着就刷新。</summary>
    private void OnInventoryChanged()
    {
        if (_open) Refresh();
    }

    /// <summary>面板里用左键点某个装备槽 → 切换当前武器（等价于按 1 / 2）。</summary>
    private void OnSlotPressed(int slotIndex)
    {
        if (_player == null) return;
        _player.TrySetActiveSlot(slotIndex);
        Refresh();
    }

    /// <summary>打开 / 关闭面板。</summary>
    public void SetOpen(bool open)
    {
        _open = open;
        PanelStack.Set(_closeSelf, open);          // 打开入栈 / 关闭出栈（Esc 按顺序关）
        if (!open) _modifyPanel?.Close();          // 关主面板时连带关掉改装面板

        if (_root != null) _root.Visible = open;

        // 关面板时退出行内拆分 / 搜索状态
        if (!open) { CancelSplit(); CloseSearch(); }
        ApplyPanelLayout();

        // 面板打开时显示鼠标（否则点不了），关闭时交还准星
        if (_player == null || !_player.IsDead)
            Input.MouseMode = open ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Hidden;

        // 通知角色屏蔽开火
        EventBus.Instance.EmitSignal(EventBus.SignalName.UiPanelToggled, open);

        if (open) Refresh();
    }

    /// <summary>刷新所有槽位与背包列表的显示。</summary>
    public void Refresh()
    {
        // ★ 搜索列也要跟着刷：从尸体拖走 / 双击拿走之后，东西**不能再留在搜索栏里**
        RefreshSearch();

        if (_player == null || _slotButtons == null) return;

        for (int i = 0; i < _slotButtons.Length; i++)
        {
            var item = _player.Equipment.Get(i);
            bool selected = _player.ActiveSlot == i;

            _slotButtons[i].Text = $"[{i + 1}] {item?.Data?.Name ?? "—"}";

            // ★ 物品名颜色一律取自**稀有度**；选中与否只改明暗（保留色相）
            var color = item?.Data != null
                ? (selected ? UiStyle.ItemText(item.Data) : UiStyle.ItemTextDimmed(item.Data))
                : UiStyle.EmptySlotColor;

            _slotButtons[i].AddThemeColorOverride("font_color", color);
        }

        RefreshBag();
    }

    // ---------- 右侧：身上带的物品（模仿 CDDA 的物品列表） ----------

    /// <summary>
    /// 列出**身上带的全部东西**：基础口袋 + 所有装备容器。
    ///
    /// 每个口袋一个小标题（名字 + 占格/总格 + 网格尺寸），下面是一块**格子面板**
    /// （<see cref="GridPanel"/>）：物品按 W×H 摆在自己的格子里，可拖着换格子、换口袋。
    ///
    /// ★ 必须包含**基础口袋** —— 否则捡起来的容器进了基础口袋就"看不见"了。
    /// </summary>
    private void RefreshBag()
    {
        if (_bagList == null) return;

        // 清掉旧行（先 RemoveChild 再 QueueFree，否则同一帧还会被 GetChildren 取到）
        foreach (var child in _bagList.GetChildren())
        {
            _bagList.RemoveChild(child);
            child.QueueFree();
        }

        int sections = 0;

        // ① 基础口袋
        sections += AddPocketSection("基础口袋", _player.Pocket);

        // ② 各装备容器
        for (int i = 0; i < _player.Equipment.SlotCount; i++)
        {
            var equipped = _player.Equipment.Get(i);
            if (equipped?.Contents == null) continue;

            sections += AddPocketSection(equipped.Data.Name, equipped.Contents);
        }

        if (sections == 0) _bagList.AddChild(MakeCaptionRow("（什么都没带）"));

        // 合计两行：总占格（不含装备栏物品自身）/ 总负重（含装备栏）
        _bagTotal.Text =
            $"占用格子 {_player.TotalUsedCells} / {_player.TotalCellCapacity} 格\n" +
            $"总负重   {_player.TotalWeight:0.00} / {_player.MaxCarryWeight:0.00} kg";
    }

    /// <summary>列出一个口袋：小标题 + 格子面板（+ 正在拆的那一叠的滑块行）。返回是否有内容可显示。</summary>
    private int AddPocketSection(string name, Inventory pocket)
    {
        if (pocket == null) return 0;

        var header = new Label
        {
            Text = $"{name}      {pocket.UsedCells} / {pocket.CellCount} 格  {pocket.GridW}×{pocket.GridH}",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        header.AddThemeFontSizeOverride("font_size", 15);
        header.AddThemeColorOverride("font_color", UiStyle.CaptionColor);
        _bagList.AddChild(header);

        var grid = new GridPanel
        {
            Pocket = pocket,
            CellPx = 40,
            Changed = Refresh,
            ContextRequested = OnGridContextRequested,
            Activated = OnGridActivated,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };
        _bagList.AddChild(grid);
        grid.Rebuild();

        // 正在拆的那一叠 → 网格下面接一行滑块（不占据格子），并滚到看得见的位置
        if (_splitStack != null && ReferenceEquals(_splitPocket, pocket) && pocket.Contains(_splitStack))
            ShowSplitRow(_bagList, pocket, _splitStack, _bagList.GetParent() as ScrollContainer);

        // 口袋之间留点空隙
        _bagList.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        return 1;
    }

    /// <summary>格子面板右键某个物品 → 复用原来的右键菜单（造一个代理行）。</summary>
    private void OnGridContextRequested(ItemStack stack, Inventory pocket, Vector2 screenPosition)
    {
        if (stack?.Data == null) return;

        var proxy = new ItemSlot
        {
            Items = new List<ItemStack> { stack },
            Pocket = pocket,
        };
        OnSlotContextRequested(proxy, screenPosition);
    }

    /// <summary>格子面板双击某个物品 → 和原来的双击行为一致（枪 = 卸第一个配件）。</summary>
    private void OnGridActivated(ItemStack stack, Inventory pocket)
    {
        if (stack?.Data?.WeaponStats != null)
            UninstallFirstAttachment(pocket, stack);
    }

    /// <summary>
    /// 在 <paramref name="parent"/> 里放一行**行内拆分**控件，并（如果传了滚动容器）滚到看得见。
    ///
    /// 拆的时候如果没有空格子摆新的一叠，就**留在拆分状态**并在控件上提示 ——
    /// 以前是静默失败，看着像"容器里的东西不能拆"。
    /// </summary>
    private void ShowSplitRow(VBoxContainer parent, Inventory pocket, ItemStack stack, ScrollContainer scroll)
    {
        SplitRow row = null;

        row = new SplitRow
        {
            Confirmed = amount =>
            {
                var split = pocket.SplitStack(stack, amount);
                if (split == null)
                {
                    row?.ShowError("没有空格子，先腾一格");
                    return;
                }

                _splitStack = null;
                _splitPocket = null;
                _splitRow = null;
                Refresh();
            },
            Cancelled = () =>
            {
                _splitStack = null;
                _splitPocket = null;
                _splitRow = null;
                Refresh();
            },
        };

        parent.AddChild(row);       // 先加进树：_Ready 会把子节点建好
        row.Setup(pocket, stack);   // 再填数据
        _splitRow = row;

        // ★ 拆分行长在网格下面：口袋靠下时它会落在可视区外，滚过去。
        ScrollRowIntoView(scroll, row);
    }

    /// <summary>
    /// 把滚动区滚到能看见 <paramref name="row"/> 的位置。
    ///
    /// 不能用 <c>ScrollContainer.EnsureControlVisible</c>：它要求控件是滚动容器的**直接子节点**，
    /// 而我们的行挂在中间的 VBox 里（会直接报错、什么都不滚）。这里等一帧让布局算完，
    /// 再按行在 VBox 里的 y 自己算滚动量。
    /// </summary>
    private async void ScrollRowIntoView(ScrollContainer scroll, Control row)
    {
        if (scroll == null || row == null) return;

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInstanceValid(scroll) || !IsInstanceValid(row)) return;

        float bottom = row.Position.Y + row.Size.Y + 6.0f;
        scroll.ScrollVertical = Mathf.RoundToInt(bottom - scroll.Size.Y);
    }

    /// <summary>退出拆分状态（关闭面板 / 物品消失时用）。</summary>
    private void CancelSplit()
    {
        _splitStack = null;
        _splitPocket = null;
        _splitRow = null;
    }

    // ---------- 搜索战利品容器（风格参考三角洲行动：左=自己的东西，右=正在搜的容器） ----------

    /// <summary>
    /// 打开某个战利品容器的搜索界面：**整个 Tab 面板一起弹出来**，
    /// 右侧多出一列显示这个容器；先读条，读完才显示物品。
    /// </summary>
    public void OpenSearch(LootSpot spot)
    {
        if (spot == null) return;

        _searchSpot = spot;
        _searchDone = spot.Searched;                  // 搜过的箱子不用重读条
        // 逐件搜索的顺序 = 容器里的叠（搜过的直接算全部搜完）
        _searchOrder.Clear();
        foreach (var s in spot.Contents.Stacks)
            if (s?.Data != null) _searchOrder.Add(s);

        _searchIndex = spot.Searched ? _searchOrder.Count : 0;
        _searchItemElapsed = 0.0f;
        _searchProgress = spot.Searched ? 1.0f : 0.0f;
        _searchRevealedCount = -1;

        SetOpen(true);
        ApplyPanelLayout();
        RefreshSearch();
    }

    /// <summary>退出搜索（保持 Tab 面板开着）。</summary>
    private void CloseSearch()
    {
        // 正在拆的是这个容器里的东西 → 一起退出，否则背包区会找不到这一叠
        if (_splitPocket != null && _searchSpot != null && ReferenceEquals(_splitPocket, _searchSpot.Contents))
            CancelSplit();

        _searchSpot = null;
        _searchProgress = 0.0f;
        _searchOrder.Clear();
        _searchIndex = 0;
        _searchItemElapsed = 0.0f;
        _searchRevealedCount = -1;
        _searchDone = false;
    }

    /// <summary>
    /// 这一件是不是已经搜出来了（逐件搜索：排在当前项之前的都已经揭示）。
    ///
    /// 不在搜索序列里的（例如**刚拆出来的一叠**）直接算可见 —— 它是从已揭示的堆叠里拆的，
    /// 再藏起来就永远不会被搜到。
    /// </summary>
    private bool IsRevealed(ItemStack stack)
    {
        if (stack?.Data == null) return false;
        if (_searchDone) return true;

        int index = _searchOrder.IndexOf(stack);
        if (index < 0) return true;
        return index < _searchIndex;
    }

    /// <summary>已经揭示了几叠。</summary>
    private int CountRevealed()
    {
        if (_searchSpot?.Contents == null) return 0;

        int count = 0;
        foreach (var stack in _searchSpot.Contents.Stacks)
            if (IsRevealed(stack)) count++;
        return count;
    }

    /// <summary>
    /// 面板宽度随状态切换：
    /// **不搜索时偏左且窄**（0.03~0.66），搜索时向右扩出第三列（0.03~0.97）。
    /// </summary>
    private void ApplyPanelLayout()
    {
        if (_root != null) _root.AnchorRight = Searching ? 0.97f : 0.66f;
        if (_searchColumn != null) _searchColumn.Visible = Searching;
    }

    /// <summary>在代码里搭出「搜索」第三列（加进 Columns，默认隐藏）。</summary>
    private void BuildSearchColumn()
    {
        var columns = GetNodeOrNull<HBoxContainer>("Root/Margin/Columns");
        if (columns == null) return;

        _searchColumn = new VBoxContainer
        {
            Visible = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 1.15f,
        };
        _searchColumn.AddThemeConstantOverride("separation", 8);
        columns.AddChild(_searchColumn);

        var panel = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.11f, 0.09f, 0.15f, 0.92f),   // 略带紫，呼应尸体的紫色
            BorderColor = new Color(0.42f, 0.30f, 0.58f, 1.0f),
            BorderWidthTop = 2, BorderWidthBottom = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
        });
        _searchColumn.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        margin.AddChild(box);

        var title = new Label { Text = "正在搜索" };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color(0.78f, 0.72f, 0.88f));
        box.AddChild(title);

        // ★ 没有"整箱总进度条"：每件物品的格子自己显示搜到哪了（从下往上填的紫色底纹）
        _searchHint = new Label { Text = "" };
        _searchHint.AddThemeFontSizeOverride("font_size", 14);
        _searchHint.AddThemeColorOverride("font_color", UiStyle.CaptionColor);
        box.AddChild(_searchHint);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        box.AddChild(scroll);

        _searchList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _searchList.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(_searchList);
    }

    /// <summary>
    /// 刷新「搜索」列：读条中未摸出来的那几格显示「？ + 自己的进度条」，
    /// 摸出来的格子才显示图标和名字（格子位置**一开始就是真实的**，只是内容藏着）。
    /// </summary>
    private void RefreshSearch()
    {
        if (_searchList == null) return;

        foreach (var child in _searchList.GetChildren())
        {
            _searchList.RemoveChild(child);
            child.QueueFree();
        }
        _searchGrid = null;

        if (_searchSpot == null) return;

        UpdateSearchProgress();

        var grid = new GridPanel
        {
            Pocket = _searchSpot.Contents,
            CellPx = 40,
            IsRevealed = IsRevealed,
            RevealProgress = RevealProgressOf,
            Changed = Refresh,
            ContextRequested = OnGridContextRequested,
            Activated = (stack, pocket) => OnItemActivated(new ItemSlot
            {
                Items = new List<ItemStack> { stack },
                Pocket = pocket,
            }),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };
        _searchList.AddChild(grid);
        grid.Rebuild();
        _searchGrid = grid;

        // 正在拆的那一叠在**这个容器里** → 在格子下面接一行滑块（搜刮来的东西也能拆）
        if (_splitStack != null && ReferenceEquals(_splitPocket, _searchSpot.Contents)
            && _searchSpot.Contents.Contains(_splitStack))
        {
            ShowSplitRow(_searchList, _searchSpot.Contents, _splitStack,
                         _searchList.GetParent() as ScrollContainer);
        }

        if (_searchSpot.IsEmpty) _searchHint.Text = "（空的）";
        else if (_searchDone) _searchHint.Text = "已搜完  可直接拖走";
    }

    /// <summary>某一格搜到哪了（0~1）：只有"当前正在搜的那一件"才有进度。</summary>
    private float RevealProgressOf(ItemStack stack)
    {
        if (stack?.Data == null) return 0.0f;
        if (IsRevealed(stack)) return 1.0f;
        if (_searchSpot == null || _searchIndex >= _searchOrder.Count) return 1.0f;
        if (!ReferenceEquals(_searchOrder[_searchIndex], stack)) return 0.0f;

        float own = Mathf.Max(0.1f, LootSpot.TimeForRarity(stack.Data.Rarity));
        return Mathf.Clamp(_searchItemElapsed / own, 0.0f, 1.0f);
    }

    /// <summary>
    /// **双击**物品行 → 直接拿走（战利品搜索栏用）。
    /// 和普通拾取不同：**优先放进空格子最多的口袋**，不是"基础口袋优先"。
    /// </summary>
    private void OnItemActivated(ItemSlot slot)
    {
        if (_player == null || slot?.Items == null || slot.Pocket == null) return;

        // 先拷一份：搬运过程中源列表会被改动
        foreach (var stack in new List<ItemStack>(slot.Items))
        {
            int count = stack.Count;
            if (count <= 0) continue;

            var code = _player.TryTake(stack, out var stored);
            if (code != ContainCode.Success || stored == null) break;

            slot.Pocket.RemoveFrom(stack, count);
        }

        Refresh();   // 搜索列 + 身上的东西一起刷
    }

    /// <summary>
    /// **逐件搜索**：每一件按**它自己的**时长读条，读完揭示，再开始搜下一件。
    /// 所以进度条反映的是"当前这一件搜到哪了"，不是整箱总进度。
    /// </summary>
    private void AdvanceSearch(float delta)
    {
        if (_searchIndex >= _searchOrder.Count) { FinishSearch(); return; }

        var current = _searchOrder[_searchIndex];
        if (current?.Data == null) { _searchIndex++; return; }

        float own = Mathf.Max(0.1f, LootSpot.TimeForRarity(current.Data.Rarity));
        _searchItemElapsed += delta;

        // 整箱总进度（只给外部 / 自检看）
        float spent = 0.0f, whole = 0.0f;
        for (int i = 0; i < _searchOrder.Count; i++)
        {
            float t = Mathf.Max(0.1f, LootSpot.TimeForRarity(_searchOrder[i].Data.Rarity));
            whole += t;
            if (i < _searchIndex) spent += t;
        }
        _searchProgress = Mathf.Clamp((spent + _searchItemElapsed) / Mathf.Max(0.1f, whole), 0.0f, 1.0f);

        if (_searchItemElapsed >= own)
        {
            _searchIndex++;
            _searchItemElapsed = 0.0f;

            if (_searchIndex >= _searchOrder.Count) FinishSearch();
            else RefreshSearch();
            return;
        }

        UpdateSearchProgress();
    }

    private void FinishSearch()
    {
        _searchProgress = 1.0f;
        _searchDone = true;
        if (_searchSpot != null) _searchSpot.Searched = true;
        RefreshSearch();
    }

    private void UpdateSearchProgress()
    {
        if (_searchSpot == null) return;

        // ★ 只有"每件物品自己的进度"，没有整箱总进度
        _searchGrid?.RefreshRevealProgress();

        if (!_searchDone && _searchHint != null && _searchIndex < _searchOrder.Count)
            _searchHint.Text = "搜索中…";
    }

    /// <summary>把枪上装着的**第一个**配件取回这个口袋（双击格子里的枪触发，可连按卸多个）。</summary>
    private void UninstallFirstAttachment(Inventory pocket, ItemStack gun)
    {
        foreach (var weaponSlot in WeaponMods.Slots(gun))
        {
            if (weaponSlot.IsEmpty) continue;

            var removed = WeaponMods.TryUninstall(weaponSlot);
            if (removed != null) pocket.Add(removed);
            break;
        }
        Refresh();
    }

    /// <summary>一行说明文字（例如「（空）」）。</summary>
    private Control MakeCaptionRow(string text)
    {
        var label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(0, ItemRow.RowHeight),
            // ★ 必须 Ignore：否则这个 Label 会吞掉鼠标事件，空口袋就接不住拖拽了
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", UiStyle.EmptySlotColor);
        return label;
    }
}
