using Godot;

/// <summary>
/// 近战武器类型 —— 决定「基础最大命中目标数」。
///
/// 将来改装系统会给某类型**增加**目标数，所以这里只定义基础值，
/// 实际目标数 = 类型基础值 + 改装加成（目前没有改装，等于基础值）。
/// </summary>
public enum MeleeWeaponType
{
    /// <summary>匕首类：单体，只能打到 1 个目标。</summary>
    Dagger,

    /// <summary>砍刀类：横扫，范围内目标不限。</summary>
    Machete,

    /// <summary>撬棍类：一次打 3 个。</summary>
    Crowbar,
}

/// <summary>近战武器类型的查表与显示名。</summary>
public static class MeleeWeaponTypeInfo
{
    /// <summary>「不限目标数」的表示值。</summary>
    public const int Unlimited = 0;

    /// <summary>该类型的基础最大命中目标数（<see cref="Unlimited"/> = 不限）。</summary>
    public static int GetBaseMaxTargets(MeleeWeaponType type) => type switch
    {
        MeleeWeaponType.Dagger => 1,
        MeleeWeaponType.Machete => Unlimited,
        MeleeWeaponType.Crowbar => 3,
        _ => 1,
    };

    /// <summary>类型中文名（UI 直接用）。</summary>
    public static string GetDisplayName(MeleeWeaponType type) => type switch
    {
        MeleeWeaponType.Dagger => "匕首类",
        MeleeWeaponType.Machete => "砍刀类",
        MeleeWeaponType.Crowbar => "撬棍类",
        _ => "未知",
    };
}

/// <summary>
/// 近战武器数据（与 GunData 对称）。
///
/// 只放「战斗属性」，不含背包/价值等物品信息 ——
/// 由物品层的 MeleeWeaponData 引用（类似 RangedWeaponData 引用 GunData），
/// 所以调近战手感只需要动这里。
/// </summary>
[GlobalClass]
public partial class MeleeData : Resource
{
    [Export] public string WeaponName { get; set; } = "匕首";

    /// <summary>攻击力（单次挥砍伤害）。</summary>
    [Export] public float Damage { get; set; } = 40.0f;

    /// <summary>攻击速度（次/秒）。与枪械的 FireRate 单位保持一致。</summary>
    [Export] public float AttackSpeed { get; set; } = 2.2f;

    /// <summary>
    /// 武器类型 —— 决定基础最大命中目标数。
    /// 匕首类打 1 个 / 砍刀类不限 / 撬棍类打 3 个。
    /// </summary>
    [Export] public MeleeWeaponType WeaponType { get; set; } = MeleeWeaponType.Dagger;

    /// <summary>攻击距离（扇形半径，像素）。</summary>
    [Export] public float Reach { get; set; } = 40.0f;

    /// <summary>扇形张角（度）。</summary>
    [Export] public float ArcDegrees { get; set; } = 100.0f;

    /// <summary>基础最大命中目标数，由武器类型决定（0 = 不限）。</summary>
    public int BaseMaxTargets => MeleeWeaponTypeInfo.GetBaseMaxTargets(WeaponType);

    /// <summary>
    /// 实际最大命中目标数。
    /// 目前 = 类型基础值；将来改装系统会在这里叠加加成。
    /// </summary>
    public int MaxTargets => BaseMaxTargets;

    /// <summary>武器类型中文名。</summary>
    public string WeaponTypeName => MeleeWeaponTypeInfo.GetDisplayName(WeaponType);

    /// <summary>攻击间隔（秒），由攻击速度换算 —— 供内部计时使用。</summary>
    public float Cooldown => AttackSpeed > 0.001f ? 1.0f / AttackSpeed : 0.0f;

    /// <summary>扇形半角（弧度）。</summary>
    public float HalfArcRadians => Mathf.DegToRad(ArcDegrees * 0.5f);
}
