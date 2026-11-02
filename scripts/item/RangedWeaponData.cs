using Godot;

/// <summary>
/// 远程武器数据。
///
/// 战斗属性直接复用现有的 <see cref="GunData"/>（伤害 / 射速 / 散布 / 弹匣 / 换弹 / 噪音），
/// 不重复定义 —— 这样改成枪系统时仍然只动 GunData。
/// </summary>
[GlobalClass]
public partial class RangedWeaponData : WeaponData
{
    public override WeaponKind Kind => WeaponKind.Ranged;

    /// <summary>枪械战斗属性（复用 GunData）。</summary>
    [Export] public GunData GunStats { get; set; }

    // TODO(需求待补)：口径、配件槽位、后坐力曲线、弹种兼容…
}
