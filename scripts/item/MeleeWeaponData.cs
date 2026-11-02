using Godot;

/// <summary>
/// 近战武器数据（物品层）。
///
/// 战斗属性复用现有的 <see cref="MeleeData"/>（伤害 / 攻击范围 / 扇形角度 / 冷却 / 目标数），
/// 与 RangedWeaponData 引用 GunData 对称 —— 这样调近战手感只动 MeleeData。
/// </summary>
[GlobalClass]
public partial class MeleeWeaponData : WeaponData
{
    public override WeaponKind Kind => WeaponKind.Melee;

    /// <summary>近战战斗属性（复用 MeleeData）。</summary>
    [Export] public MeleeData MeleeStats { get; set; }

    // TODO(需求待补)：近战武器在物品层还需要什么字段
}
