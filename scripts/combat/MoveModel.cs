using Godot;

/// <summary>移动模型的一帧输入（收进结构体，避免方法参数超 5 个 —— AGENTS 规则 2）。</summary>
public struct MoveInputs
{
    public float WalkSpeed;
    public float SprintSpeed;
    public float Weight;
    public float MaxWeight;
    public float Stamina;
    public bool SprintHeld;
    public bool Aiming;
    public bool MoveHeld;
    public bool InHub;
}

/// <summary>移动模型的一帧输出。</summary>
public readonly struct MoveResult
{
    public readonly float Speed;
    public readonly bool CanSprint;
    public readonly bool CanWalk;
    public readonly float StaminaDelta;

    public MoveResult(float speed, bool canSprint, bool canWalk, float staminaDelta)
    {
        Speed = speed;
        CanSprint = canSprint;
        CanWalk = canWalk;
        StaminaDelta = staminaDelta;
    }
}

/// <summary>
/// 移动规则，移植自 ZS 的 <c>scr_player_movement.gml</c>：
///   · 负重两档降级（超上限不能跑、超上限 +5 不能走）
///   · 冲刺耐力：跑动按负重比消耗，不跑时按固定速率回复，低于 5 禁止起跑
///   · 瞄准 ×0.5（沿用项目原有的 AimSpeedMultiplier 语义）
///   · 掩体内跑速 ×1.5 且不消耗耐力
/// 对角归一由 Godot 的 <see cref="Input.GetVector"/> 负责（ZS 是 ÷1.4，等价）。
/// 纯计算、无节点引用、无状态（AGENTS 规则 3 / 4）。
/// </summary>
public static class MoveModel
{
    public static MoveResult Compute(in MoveInputs i, float delta, float aimMultiplier)
    {
        bool canSprint = i.Weight <= i.MaxWeight && i.Stamina > CombatTuning.SprintMinStamina;
        float walkMul = 1.0f;
        bool canWalk = true;

        if (i.Weight > i.MaxWeight)
        {
            canSprint = false;
            walkMul = CombatTuning.OverweightWalkMultiplier;
        }
        if (i.Weight > i.MaxWeight + CombatTuning.OverweightBlockExtra)
        {
            canWalk = false;
            walkMul = 0.0f;
        }
        if (i.InHub)
        {
            canSprint = i.Stamina > CombatTuning.SprintMinStamina;
            canWalk = true;
            walkMul = 1.0f;
        }

        bool sprinting = i.SprintHeld && canSprint && i.MoveHeld;
        float speed = sprinting ? i.SprintSpeed : i.WalkSpeed * walkMul;
        if (sprinting && i.InHub) speed *= CombatTuning.HubSprintBonus;
        if (i.Aiming) speed *= aimMultiplier;
        if (!canWalk || !i.MoveHeld) speed = 0.0f;

        float drain = Mathf.Lerp(
            CombatTuning.StaminaDrainMinPerSec,
            CombatTuning.StaminaDrainMaxPerSec,
            Mathf.Clamp(i.MaxWeight > 0.0f ? i.Weight / i.MaxWeight : 0.0f, 0.0f, 1.0f));
        float staminaDelta = sprinting ? -drain : CombatTuning.StaminaRegenPerSec;
        if (i.InHub && sprinting) staminaDelta = CombatTuning.StaminaRegenPerSec;

        return new MoveResult(speed, canSprint, canWalk, staminaDelta * delta);
    }
}
