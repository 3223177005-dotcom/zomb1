using Godot;

/// <summary>
/// 用**真实的 Player 场景**验算护甲：装上 IV 级甲，分别吃低穿透 / 高穿透一拳，核对血量与耐久。
/// Godot --headless --path . res://tests/ArmorPlayerProbe.tscn
/// </summary>
public partial class ArmorPlayerProbe : Node
{
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

        var armorData = ItemDatabase.Instance?.GetItem("armor_arman");
        if (armorData == null)
        {
            GD.PrintErr("物品库没有 armor_arman");
            GetTree().Quit(1);
            return;
        }

        var armor = new ItemStack(armorData, 1);
        player.Equipment.Equip(Player.ArmorSlot, armor);
        GD.Print("装上护甲：class=", armorData.ArmorClass, " 耐久=", armor.Durability);

        // ① 低穿透：应被挡住（只吃 15%）
        float hp0 = player.Health;
        player.TakeHit(20.0f, 0.0f);
        float dmg0 = hp0 - player.Health;
        GD.Print($"低穿透 20 伤害 → 实际扣血 {dmg0:F1}  耐久 {armor.Durability:F1}");
        bool blocked = dmg0 < 6.0f;

        // ② 高穿透：应打穿（接近全额）
        float hp1 = player.Health;
        player.TakeHit(20.0f, 60.0f);
        float dmg1 = hp1 - player.Health;
        GD.Print($"高穿透 20 伤害 → 实际扣血 {dmg1:F1}  耐久 {armor.Durability:F1}");
        bool pierced = dmg1 > 10.0f;

        // ③ 破甲后：全额伤害
        armor.Durability = 0.0f;
        float hp2 = player.Health;
        player.TakeHit(20.0f, 0.0f);
        float dmg2 = hp2 - player.Health;
        GD.Print($"破甲后 20 伤害 → 实际扣血 {dmg2:F1}");
        bool broken = Mathf.IsEqualApprox(dmg2, 20.0f);

        bool ok = blocked && pierced && broken;
        GD.Print(ok ? "PASS 护甲在真实 Player 上生效" : "FAIL 护甲结算不符合预期");
        GetTree().Quit(ok ? 0 : 1);
    }
}
