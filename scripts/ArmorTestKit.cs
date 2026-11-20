using Godot;

/// <summary>
/// **护甲测试工具**（临时内容，护甲机制验收用；正式版删掉）。
///
/// 快捷键（游戏内）：
///   F6 → 打自己 20 伤害，穿透 0（应被护甲挡住，只吃 15%，耐久下降）
///   F7 → 打自己 20 伤害，穿透 60（应打穿，几乎全吃）
///   F8 → 把护甲耐久打成 1（测破甲：之后任何伤害都全额）
///   F9 → 治疗满血（方便反复测）
///
/// 每次按键都会把「护甲等级 / 耐久 / 实际伤害」打到控制台，可直接对照
/// <see cref="ArmorService"/> 的公式验算。
/// </summary>
public partial class ArmorTestKit : Node
{
    /// <summary>挂在 Player 下面，直接把父节点当玩家（不用 group 查询，也不跨层引用）。</summary>
    private Player _player;

    public override void _Ready()
    {
        _player = GetParent() as Player;
        GD.Print("[ArmorTest] 已就绪：F6=低穿透  F7=高穿透  F8=打成破甲  F9=回血");
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

        switch (key.Keycode)
        {
            case Key.F6: Hit(20.0f, 0.0f, "低穿透（应被挡）"); break;
            case Key.F7: Hit(20.0f, 60.0f, "高穿透（应打穿）"); break;
            case Key.F8: Break(); break;
            case Key.F9: Heal(); break;
        }
    }

    private void Hit(float amount, float penetration, string label)
    {
        if (_player == null) return;

        var armor = _player.Equipment.Get(Player.ArmorSlot);
        float before = armor?.Durability ?? -1.0f;
        float hpBefore = _player.Health;

        _player.TakeHit(amount, penetration);

        float after = _player.Equipment.Get(Player.ArmorSlot)?.Durability ?? -1.0f;
        GD.Print($"[ArmorTest] {label}  伤害 {amount} 穿透 {penetration} → " +
                 $"实际扣血 {(hpBefore - _player.Health):F1}  护甲耐久 {before:F1} → {after:F1}  " +
                 $"血量 {_player.Health:F1}/{_player.MaxHealth:F1}");
    }

    private void Break()
    {
        var armor = _player?.Equipment.Get(Player.ArmorSlot);
        if (armor == null)
        {
            GD.Print("[ArmorTest] 没穿护甲，先拖一件到左上角护甲位");
            return;
        }
        armor.Durability = 1.0f;
        _player?.RefreshActiveWeapon();     // 触发 HUD 护甲栏刷新
        GD.Print("[ArmorTest] 护甲耐久打成 1 —— 下次受伤应全额（破甲无防护）");
    }

    private void Heal()
    {
        if (_player == null) return;
        _player.Heal(_player.MaxHealth);
        GD.Print("[ArmorTest] 已回满血");
    }
}
