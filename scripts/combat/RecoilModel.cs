using Godot;

/// <summary>
/// 两个后坐池，移植自 ZS 的 <c>player_action_shoot.gml</c>：
///   · <see cref="FromMovement"/>：移动 / 转视角累积，静止时按 ergo 回复，上限与 ergo×武器重量 成反比
///   · <see cref="FromShooting"/>：每开一枪累积，随后按「2 + 自身 5%」衰减，上限 = now_recoil×2
/// 只做数值，不碰节点；由 <see cref="Player"/> 持有并按帧驱动（不新增全局状态 —— AGENTS 规则 4）。
/// </summary>
public sealed class RecoilModel
{
    /// <summary>移动 / 转视角累积的晃动（ZS 的 recoil_from_movement）。</summary>
    public float FromMovement { get; private set; }

    /// <summary>开火累积的后坐（ZS 的 recoil_from_shooting）。</summary>
    public float FromShooting { get; private set; }

    /// <summary>合计后坐，供 HUD / 准星推开使用。</summary>
    public float Total => FromMovement + FromShooting;

    /// <summary>把后坐换算成额外的散布（度），叠加到项目原有的 CurrentSpread 上。</summary>
    public float ExtraSpreadDegrees =>
        Mathf.Clamp(Total * CombatTuning.RecoilSpreadDegPerPoint, 0.0f, CombatTuning.RecoilMaxSpreadDeg);

    /// <summary>每帧推进：移动时累积、静止时回复、开火池持续衰减。</summary>
    public void Tick(float delta, float ergo, float weaponWeight, bool moving, float mouseDelta)
    {
        float e = Mathf.Max(ergo, 1.0f);
        if (moving)
        {
            FromMovement += CombatTuning.RecoilMoveAddBase + (CombatTuning.RecoilMoveErgoWeight - e);
            FromMovement += Mathf.Abs(mouseDelta) * CombatTuning.RecoilMouseGain;
        }
        else
        {
            FromMovement -= (CombatTuning.RecoilIdleRegenBase + CombatTuning.RecoilIdleRegenErgo * e) * delta;
        }

        float cap = Mathf.Min(
            CombatTuning.RecoilCapNumerator / (e * Mathf.Max(weaponWeight, 0.1f)),
            CombatTuning.RecoilCapCeiling);
        FromMovement = Mathf.Clamp(FromMovement, 0.0f, cap);

        float decay = (CombatTuning.RecoilShotDecayBase + FromShooting * CombatTuning.RecoilShotDecayRatio) * 60.0f * delta;
        FromShooting = Mathf.Max(0.0f, FromShooting - decay);
    }

    /// <summary>真的射出一发时调用（由 Weapon 的 Fired 信号驱动，空仓/冷却不会触发）。</summary>
    public void OnShot(float nowRecoil, float effectiveRangeFactor = 1.0f)
    {
        FromShooting += Mathf.Max(nowRecoil, 0.0f) * effectiveRangeFactor;
        FromShooting = Mathf.Min(FromShooting, nowRecoil * CombatTuning.RecoilShotCapFactor);
    }

    /// <summary>切枪 / 换弹结束后把开火池清掉一半，避免余波穿到下一把枪。</summary>
    public void ResetShotRecoil(float keepRatio = 0.5f)
        => FromShooting *= Mathf.Clamp(keepRatio, 0.0f, 1.0f);
}
