using Godot;

/// <summary>
/// 回归测试：**装备栏一变就必须广播 <see cref="EventBus.InventoryChanged"/>**。
///
/// 复现的 bug：把护甲拖到 Tab 面板左上「防具模块」那一行，
/// 护甲当场进了护甲位，但背包格子还画着那一件，要再拖一次别的东西才消失。
/// 根因是 <see cref="EquipService"/> 改完装备栏没通知 UI ——
/// 防具行走的 <see cref="EquipDropTarget"/> 默认分支也不会自己调面板 Refresh()，
/// 于是格子只能等下一次别处拖放触发 Changed 才重建。
///
/// Godot --headless --path . res://tests/EquipNotifySelfTest.tscn
/// </summary>
public partial class EquipNotifySelfTest : Node
{
    private int _pass;
    private int _fail;

    public override async void _Ready()
    {
        var scene = GD.Load<PackedScene>("res://scenes/player.tscn");
        if (scene == null)
        {
            GD.PrintErr("加载 player.tscn 失败");
            GetTree().Quit(1);
            return;
        }

        var player = scene.Instantiate<Player>();
        AddChild(player);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        int notified = 0;
        EventBus.Instance.InventoryChanged += () => notified++;

        var armor = FindInPocket(player, "armor_arman");
        if (armor == null)
        {
            GD.PrintErr("口袋里没有 armor_arman，无法复现");
            GetTree().Quit(1);
            return;
        }

        // ① 装备：护甲位拿到，背包立刻少一件，期间必须广播
        int before = player.Pocket.CountOf("armor_arman");
        bool ok = EquipService.TryEquip(player.Equipment, Player.ArmorSlot, armor,
            player.Pocket, player.Pocket, out _, out string reason);
        int after = player.Pocket.CountOf("armor_arman");

        Check("装备成功", ok, reason);
        Check("护甲位拿到了这一件",
            player.Equipment.Get(Player.ArmorSlot)?.Data?.Id == "armor_arman",
            player.Equipment.Get(Player.ArmorSlot)?.Data?.Id ?? "(空)");
        Check("背包里立刻少一件", after == before - 1, $"{before} → {after}");
        Check("装备广播了 InventoryChanged", notified >= 1, $"收到 {notified} 次");

        // ② 卸下：同一条信号，UI 同一个重建入口
        int beforeOff = notified;
        bool off = EquipService.TryUnequip(player.Equipment, Player.ArmorSlot, player.Pocket,
            out _, out string offReason);

        Check("卸下成功", off, offReason);
        Check("卸下也广播了 InventoryChanged", notified > beforeOff, $"收到 {notified - beforeOff} 次");

        await CheckPanelSurvivesReparent(player);

        GD.Print($"=== 结果：通过 {_pass} / 失败 {_fail} ===");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    /// <summary>
    /// 面板**被 Reparent 之后**还必须收得到 InventoryChanged。
    ///
    /// 真实游戏里 <see cref="Game.SetupWorldZoom"/> 会把 EquipmentPanel 搬到 UiLayer，
    /// 那会触发一次 _ExitTree（取消订阅）；订阅若写在 _Ready（只跑一次）里就永久失联 ——
    /// 表现正是「护甲装上了，但背包格子还画着那一件」。
    /// </summary>
    private async System.Threading.Tasks.Task CheckPanelSurvivesReparent(Player player)
    {
        var panelScene = GD.Load<PackedScene>("res://scenes/equipment_panel.tscn");
        if (panelScene == null)
        {
            Check("加载 equipment_panel.tscn", false, "加载失败");
            return;
        }

        var panel = panelScene.Instantiate<EquipmentPanel>();
        var uiLayer = new CanvasLayer();
        AddChild(uiLayer);
        AddChild(panel);

        panel.Reparent(uiLayer);          // ← 模拟 SetupWorldZoom：会触发 _ExitTree

        panel.SetOpen(true);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        panel.Refresh();                  // 面板 _Process 解析到玩家之后重建一次

        int blocksBefore = CountBlocks(panel, "armor_arman");
        Check("搬家后面板先画出背包里的护甲", blocksBefore == 1, $"{blocksBefore} 块");

        var armor = FindInPocket(player, "armor_arman");
        string reason = "口袋里没有护甲";
        bool ok = false;
        if (armor != null)
            ok = EquipService.TryEquip(player.Equipment, Player.ArmorSlot, armor,
                player.Pocket, player.Pocket, out _, out reason);

        int blocksAfter = CountBlocks(panel, "armor_arman");
        Check("搬家后仍能装备", ok, reason);
        Check("搬家后背包格子立刻少一块", blocksAfter == 0, $"{blocksBefore} → {blocksAfter}");

        uiLayer.QueueFree();
    }

    /// <summary>数一数面板里画了几个该物品的格子块（格子重建成功 = 旧块消失）。</summary>
    private static int CountBlocks(Node root, string itemId)
    {
        int count = 0;
        foreach (var node in AllNodes(root))
            if (node is GridItemBlock block && block.Stack?.Data?.Id == itemId) count++;
        return count;
    }

    private static System.Collections.Generic.IEnumerable<Node> AllNodes(Node root)
    {
        yield return root;
        foreach (var child in root.GetChildren())
            foreach (var node in AllNodes(child))
                yield return node;
    }

    private static ItemStack FindInPocket(Player player, string itemId)
    {
        foreach (var stack in player.Pocket.Stacks)
            if (stack?.Data?.Id == itemId) return stack;
        return null;
    }

    private void Check(string name, bool ok, string detail)
    {
        if (ok) _pass++;
        else _fail++;
        GD.Print(ok ? $"  [PASS] {name}" : $"  [FAIL] {name}  （{detail}）");
    }
}
