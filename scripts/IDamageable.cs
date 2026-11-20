/// <summary>可受击的对象（玩家、敌人、可破坏物等）。</summary>
public interface IDamageable
{
    void TakeDamage(float amount);
}

/// <summary>
/// 能按"穿透值"结算伤害的对象（带护甲的玩家）。
/// 单独一个接口是为了让攻击方**不必知道 Player 类型**（AGENTS 规则 3：下层不引用上层）。
/// </summary>
public interface IArmoredDamageable : IDamageable
{
    /// <summary>带穿透的受伤结算：穿透越高越容易打穿护甲。</summary>
    void TakeHit(float amount, float penetration);
}
