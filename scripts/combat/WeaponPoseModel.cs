using Godot;

/// <summary>
/// 手持武器的「姿态」：换弹抬枪、切枪甩枪。移植自 ZS 的
/// <c>class_player_weapon.gml</c>（三阶段换弹 + 90° 甩枪，时长被 ergo 缩放）。
///
/// 纯数学，不碰节点；由 <see cref="WeaponVisual"/> 每帧驱动，
/// 输出「额外角度」与「额外位移」，叠加到正常瞄准姿态上（AGENTS 规则 3 / 4）。
/// </summary>
public sealed class WeaponPoseModel
{
    private float _reloadTotal;
    private float _reloadLeft;
    private float _switchTotal;
    private float _switchLeft;

    /// <summary>额外俯仰角（度，正值 = 枪口往下压）。</summary>
    public float AngleOffsetDeg { get; private set; }

    /// <summary>额外位移（枪的局部像素，正值 = 向右/向下）。</summary>
    public Vector2 LocalOffset { get; private set; }

    public bool Switching => _switchLeft > 0.0f;

    public void StartReload(float seconds)
    {
        _reloadTotal = Mathf.Max(0.1f, seconds);
        _reloadLeft = _reloadTotal;
    }

    public void StartSwitch(float seconds)
    {
        _switchTotal = Mathf.Max(0.1f, seconds);
        _switchLeft = _switchTotal;
    }

    /// <summary>每帧推进；结束时角度/位移回到 0。</summary>
    public void Update(float delta)
    {
        float reloadAngle = 0.0f;
        float reloadDrop = 0.0f;
        if (_reloadLeft > 0.0f)
        {
            _reloadLeft = Mathf.Max(0.0f, _reloadLeft - delta);
            float t = 1.0f - _reloadLeft / _reloadTotal;      // 0 → 1
            float phase = ReloadPhase(t);                     // 0 → 1 → 0
            reloadAngle = CombatTuning.ReloadTiltDeg * phase;
            reloadDrop = CombatTuning.ReloadDropPx * phase;
        }

        float switchAngle = 0.0f;
        float switchDrop = 0.0f;
        if (_switchLeft > 0.0f)
        {
            _switchLeft = Mathf.Max(0.0f, _switchLeft - delta);
            float t = 1.0f - _switchLeft / _switchTotal;
            float phase = t < 0.5f ? Ease(t * 2.0f) : Ease(2.0f - t * 2.0f);
            switchAngle = CombatTuning.SwitchTiltDeg * phase;
            switchDrop = CombatTuning.SwitchDropPx * phase;
        }

        AngleOffsetDeg = switchAngle > 0.0f ? switchAngle : reloadAngle;
        LocalOffset = new Vector2(0.0f, switchAngle > 0.0f ? switchDrop : reloadDrop);
    }

    /// <summary>换弹曲线：前 40% 抬起（0→1），中间保持，后 30% 放回（1→0）。</summary>
    private static float ReloadPhase(float t)
    {
        if (t < 0.4f) return Ease(t / 0.4f);
        if (t < 0.7f) return 1.0f;
        return Ease((1.0f - t) / 0.3f);
    }

    /// <summary>平滑曲线（替代 ZS 的 power(k, 1-k)，形状接近且无奇点）。</summary>
    private static float Ease(float k)
    {
        k = Mathf.Clamp(k, 0.0f, 1.0f);
        return k * k * (3.0f - 2.0f * k);
    }
}
