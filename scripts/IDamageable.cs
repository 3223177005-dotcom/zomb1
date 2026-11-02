/// <summary>可受击的对象（玩家、敌人、可破坏物等）。</summary>
public interface IDamageable
{
    void TakeDamage(float amount);
}
