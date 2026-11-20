using Godot;

/// <summary>
/// 战斗与移动的集中调参表（AGENTS 规则 11：魔法数字集中在这里，改一处生效）。
///
/// 数值全部来自《ZERO Sievert》反编译实测（GML 毫秒/帧单位已换算成秒）：
///   scr_player_movement.gml、player_step_personal_stats.gml、player_action_shoot.gml
/// 只放只读常量，不放可变状态（规则 4）。
/// </summary>
public static class CombatTuning
{
    // ---- 移动：ZS 的 16px 世界走 0.75 px/帧、跑 1.2 px/帧；本项目世界格 32px，等比换算 ----
    public const float ZsWalkSpeed = 96.0f;      // ≈ 0.75 * 60 * 2
    public const float ZsSprintSpeed = 152.0f;   // ≈ 1.2  * 60 * 2
    public const float HubSprintBonus = 1.5f;    // 掩体内跑速加成

    // ---- 耐力：ZS 每帧 0.18 回复、跑步每帧 lerp(0.18,0.75,负重比) 消耗 ----
    public const float StaminaMax = 100.0f;
    public const float StaminaRegenPerSec = 10.8f;   // 0.18 * 60
    public const float StaminaDrainMinPerSec = 10.8f; // 0.18 * 60
    public const float StaminaDrainMaxPerSec = 45.0f; // 0.75 * 60
    public const float SprintMinStamina = 5.0f;      // 低于此值禁止起跑

    // ---- 负重：超过上限降级 ----
    public const float OverweightWalkMultiplier = 0.5f; // 超上限：不能跑、走速减半
    public const float OverweightBlockExtra = 5.0f;     // 超上限 +5：不能走

    // ---- 后坐：两个池（移动 / 开火）----
    public const float RecoilShotCapFactor = 2.0f;      // 开火池上限 = now_recoil * 2
    public const float RecoilShotDecayBase = 2.0f;      // 每帧衰减 2 + 5%
    public const float RecoilShotDecayRatio = 0.05f;
    public const float RecoilMoveAddBase = 250.0f;      // 移动时 += 250 + (100 - ergo)
    public const float RecoilMoveErgoWeight = 100.0f;
    public const float RecoilIdleRegenBase = 150.0f;    // 静止时 -= 150 + 2*ergo
    public const float RecoilIdleRegenErgo = 2.0f;
    public const float RecoilCapNumerator = 500000.0f;  // 移动池上限 min(500000/(ergo*重量), 5500)
    public const float RecoilCapCeiling = 5500.0f;
    public const float RecoilMouseGain = 100.0f;        // 鼠标位移 × 100

    // ---- 散布：ZS 的 total_precision 是弧度角锥，这里换算成「度」直接喂给 Weapon.HandleTrigger ----
    // ZS: precision_deg = (100 - accuracy/k)*0.04 + movement*0.001
    // 本项目 ExistingSpread 已经是度，后坐附加按 0.02 度/点 起步，可在此调
    public const float RecoilSpreadDegPerPoint = 0.02f;
    public const float RecoilMaxSpreadDeg = 12.0f;      // 后坐最多额外加 12 度，防爆表

    // ---- 手持武器姿态：换弹抬枪 / 切枪甩枪（ZS class_player_weapon.gml 的三阶段）----
    public const float ReloadTiltDeg = 25.0f;   // 换弹时枪口下压角度
    public const float ReloadDropPx = 5.0f;     // 换弹时枪身下移
    public const float SwitchTiltDeg = 90.0f;   // 切枪甩枪角度（ZS 是 90°→180° 往返，这里取 90°）
    public const float SwitchDropPx = 10.0f;    // 切枪时枪身下移
    public const float SwitchBaseTime = 0.7f;   // 切枪基准时长（秒），实际被 ergo 缩短
    public const float SwitchErgoDivisor = 1.5f; // duration = base * (100 - ergo/1.5)/100
}
