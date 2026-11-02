using Godot;

/// <summary>
/// 枪械数据（未来：不同枪械与改枪系统通过替换/修改这些字段实现）。
///
/// 射速只有一个 <see cref="FireRate"/>，所有开火模式共用 ——
/// 模式只决定「怎么扣扳机」（按住 / 单击 / 打几发）。
/// 栓动的"拉栓慢"直接用低 FireRate 表达，不需要单独字段。
/// </summary>
[GlobalClass]
public partial class GunData : Resource
{
    [Export] public string GunName { get; set; } = "突击步枪";
    [Export] public float Damage { get; set; } = 25.0f;
    [Export] public float FireRate { get; set; } = 8.0f;             // 射速（发/秒），所有模式共用
    [Export] public int MagazineSize { get; set; } = 30;
    [Export] public float ReloadTime { get; set; } = 2.0f;           // 换弹时间（秒）
    [Export] public float BulletRange { get; set; } = 600.0f;        // 射程（像素）
    [Export] public float NoiseRadius { get; set; } = 250.0f;        // 开火噪声半径（像素，吸引丧尸）

    /// <summary>
    /// 对应口径的**弹药物品 Id**（例：SVD = <c>ammo_762x54r</c>）。
    /// 空字符串 = 这把枪不消耗具体弹药（走无限备弹）。
    /// </summary>
    [Export] public string Caliber { get; set; } = "";

    // ---------- 开火模式 ----------
    /// <summary>支持哪些开火模式（可多种，用 | 组合）。</summary>
    [Export] public FireMode SupportedModes { get; set; } = FireMode.Auto;

    /// <summary>默认开火模式（必须是 SupportedModes 里的一种）。</summary>
    [Export] public FireMode DefaultMode { get; set; } = FireMode.Auto;

    /// <summary>点射一次打几发。</summary>
    [Export] public int BurstCount { get; set; } = 3;

    /// <summary>点射结束后的额外延迟（秒，0 = 不需要）。</summary>
    [Export] public float BurstCooldown { get; set; } = 0.0f;

    // ---------- 散布（单位：度，准星半角）----------
    /// <summary>
    /// 瞄准时间（秒）：**静止状态下**从按下右键到准星收至最小的时间。
    /// 移动时准星收到的最小值还要叠加移动附加散布。
    /// </summary>
    [Export] public float AimTime { get; set; } = 0.3f;

    [Export] public float HipSpread { get; set; } = 6.0f;            // 腰射基础散布
    [Export] public float AimSpread { get; set; } = 1.5f;            // 瞄准散布
    [Export] public float MoveSpreadBonus { get; set; } = 4.0f;      // 移动时额外散布
    [Export] public float SprintSpreadBonus { get; set; } = 10.0f;   // 冲刺时额外散布
    /// <summary>【暂未使用】保留给将来的「连续射击散布累积」后的恢复速度。</summary>
    [Export] public float SpreadRecovery { get; set; } = 10.0f;
}
