using Godot;

/// <summary>
/// 实际游戏画面截图（窗口模式跑，headless 无渲染）：
///   Godot --path . res://tests/GameShot.tscn
/// 产物：user://game_shot.png —— 用来量人物 / UI 在屏幕上的真实占比。
/// </summary>
public partial class GameShot : Node
{
    [Export] public int WaitFrames { get; set; } = 40;

    public override async void _Ready()
    {
        var scene = GD.Load<PackedScene>("res://scenes/game.tscn");
        if (scene == null)
        {
            GD.PrintErr("加载 scenes/game.tscn 失败");
            GetTree().Quit(1);
            return;
        }
        var root = scene.Instantiate();
        AddChild(root);

        // 把人物挪到第一间房子门口（地图原点居中：WorldBounds.Position = -960,-640）
        var player = root.GetNodeOrNull<Node2D>("Player");
        if (player != null)
            player.GlobalPosition = new Vector2(-960.0f + 12 * 32.0f, -640.0f + 10 * 32.0f);

        for (int i = 0; i < WaitFrames; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var img = GetViewport().GetTexture().GetImage();
        img.SavePng("user://game_shot.png");

        // 再截一张「打开背包/装备面板」的样子，检查缩放后 UI 有没有溢出
        Input.ActionPress("inventory");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ActionRelease("inventory");
        for (int i = 0; i < 10; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        VerifyArmorDrop();      // 先把护甲穿上，截图里才看得到耐久条
        for (int i = 0; i < 5; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetViewport().GetTexture().GetImage().SavePng("user://game_shot_ui.png");

        var size = GetViewport().GetVisibleRect().Size;
        GD.Print("viewport=", size, " window=", DisplayServer.WindowGetSize());
        GD.Print("saved ", ProjectSettings.GlobalizePath("user://game_shot.png"));
        GetTree().Quit();
    }

    /// <summary>模拟"把护甲拖到左上角那一行"：直接调放下处理器，确认真能穿上。</summary>
    private async void VerifyArmorDrop()
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Player;

        // EquipmentPanel 的根是 CanvasLayer，按类型在整棵树里找
        Node panel = null;
        foreach (var node in AllNodes(GetTree().Root))
            if (node is EquipmentPanel ep) { panel = ep; break; }

        if (player == null || panel == null)
        {
            GD.Print($"[DropTest] 找不到玩家或面板 player={player != null} panel={panel != null}");
            return;
        }

        ItemStack armor = null;
        foreach (var s in player.Pocket.Stacks)
            if (s.Data?.Id == "armor_arman") { armor = s; break; }
        if (armor == null)
        {
            GD.Print("[DropTest] 口袋里没有 armor_arman");
            return;
        }

        EquipDropTarget target = null;
        foreach (var node in AllNodes(panel))
            if (node is EquipDropTarget t && t.SlotIndex == Player.ArmorSlot) target = t;

        if (target == null)
        {
            GD.Print("[DropTest] 面板里没有护甲行的拖放层");
            return;
        }

        GD.Print($"[DropTest] 护甲行 rect={target.GetGlobalRect()} 可见={target.IsVisibleInTree()}");

        ItemDrag.Begin(player.Pocket, new System.Collections.Generic.List<ItemStack> { armor });
        bool can = target._CanDropData(Vector2.Zero, default);
        GD.Print($"[DropTest] _CanDropData = {can}");
        if (!can) { ItemDrag.End(); return; }

        target._DropData(Vector2.Zero, default);
        var after = player.Equipment.Get(Player.ArmorSlot);
        GD.Print($"[DropTest] 放下后护甲位 = {after?.Data?.Id ?? "(空)"} 耐久={after?.Durability}");
        ItemDrag.End();

        // 手动再推一次状态，判断是"信号没发"还是"信号发了但订阅端没收到"
        EventBus.Instance.PlayerArmorChanged += (bn, bd, bm, hn, hd, hm)
            => GD.Print($"[DropTest] 收到信号 body={bn} dur={bd}");
        int rows = 0;
        foreach (var node in AllNodes(GetTree().Root))
            if (node is EquipArmorRows) rows++;
        GD.Print($"[DropTest] 护甲栏实例数 = {rows}");
        player.RefreshActiveWeapon();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        foreach (var node in AllNodes(panel))
            if (node is Button b && (b.Name == "ArmorCell" || b.Name == "HelmetCell"))
                GD.Print($"[DropTest] 行 {b.Name} 文本 = 「{b.Text}」");
    }

    private static System.Collections.Generic.IEnumerable<Node> AllNodes(Node root)
    {
        yield return root;
        foreach (var child in root.GetChildren())
            foreach (var n in AllNodes(child))
                yield return n;
    }
}