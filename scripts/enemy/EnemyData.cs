using Godot;

/// <summary>
/// 敌人数据（未来：多种丧尸/怪物通过不同 .tres 配置数值；特殊行为用 Enemy 子类覆盖）。
/// </summary>
[GlobalClass]
public partial class EnemyData : Resource
{
    [Export] public string EnemyName { get; set; } = "丧尸";
    [Export] public float MaxHealth { get; set; } = 60.0f;
    [Export] public float MoveSpeed { get; set; } = 60.0f;       // 移动速度（像素/秒）
    [Export] public float Damage { get; set; } = 10.0f;          // 每次攻击伤害
    [Export] public float AttackRange { get; set; } = 20.0f;     // 攻击距离（像素）
    [Export] public float AttackCooldown { get; set; } = 1.0f;   // 攻击间隔（秒）
    [Export] public float VisionRange { get; set; } = 300.0f;    // 视野范围（像素）
    [Export] public float HearingRange { get; set; } = 250.0f;   // 听觉范围（像素，听枪声）
}
