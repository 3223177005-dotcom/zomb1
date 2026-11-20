using Godot;

/// <summary>
/// 护甲结算（照 ZS <c>bullet_hit_player.gml</c> 里那段公式，原样移植）。
///
/// ZS 的算法（dmg=伤害，pen=子弹穿透，cls=护甲等级，dur=耐久 0~100，rel=耐久损耗系数）：
/// <code>
///   relK = (rel + 100) / 100
///   chance = pen &lt;= cls*10
///          ? 0.9 + (pen - 10*cls)/20 - log10(dur/100) * 2
///          : 0.9 + (pen - 10*cls)/20 - log10(dur/100) * 2 * 0.15
///   chance = clamp(chance, 0, 1)          // ★ 这是"被打穿"的概率
///
///   打穿 (roll &lt; chance)：耐久 -= dmg * 0.08 * relK，伤害 *= (chance/2 + 0.5)
///   挡住 (!roll &lt; chance)：耐久 -= dmg * 0.10 * relK，伤害 *= 0.15
/// </code>
///
/// 注意 <c>dur</c> 用对数参与：耐久掉到 0 时 log10(0) → -∞，chance 必为 1 → 破甲等于没穿。
/// 这里对 dur=0 直接短路，避免浮点 -∞。
/// </summary>
public static class ArmorService
{
    /// <summary>一次结算的结果。</summary>
    public readonly struct Result
    {
        public readonly float Damage;          // 实际吃到的伤害
        public readonly float DurabilityLoss;  // 护甲掉的耐久
        public readonly bool Blocked;          // 是否被挡住（伤害被压到 15%）

        public Result(float damage, float durabilityLoss, bool blocked)
        {
            Damage = damage;
            DurabilityLoss = durabilityLoss;
            Blocked = blocked;
        }
    }

    /// <summary>子弹被打穿的几率（0~1）。破甲（耐久 ≤ 0）时恒为 1。</summary>
    public static float PenetrateChance(float penetration, int armorClass, float durability)
    {
        if (durability <= 0.0f) return 1.0f;

        float cls = Mathf.Max(1, armorClass);
        float durK = Mathf.Log(durability / 100.0f) * 2.0f;   // log10(dur/100)*2
        // Godot 没有 log10，Mathf.Log 是自然对数 → 除以 ln10 换算
        durK = Mathf.Log(durability / 100.0f) / Mathf.Log(10.0f) * 2.0f;

        float baseChance = 0.9f + (penetration - 10.0f * cls) / 20.0f;
        float chance = penetration <= cls * 10.0f
            ? baseChance - durK
            : baseChance - durK * 0.15f;

        return Mathf.Clamp(chance, 0.0f, 1.0f);
    }

    /// <summary>
    /// 结算一次伤害。<paramref name="roll01"/> 是 0~1 的随机数（测试里可固定）。
    /// 返回实际伤害 / 耐久损耗 / 是否挡住。
    /// </summary>
    public static Result Absorb(float damage, float penetration, int armorClass, float durDamage,
        ref float durability, float roll01)
    {
        if (damage <= 0.0f) return new Result(0.0f, 0.0f, false);
        if (armorClass < 1 || durability <= 0.0f)
            return new Result(damage, 0.0f, false);      // 没甲 / 破甲 → 全额吃伤害

        float chance = PenetrateChance(penetration, armorClass, durability);
        float relK = (durDamage + 100.0f) / 100.0f;

        if (roll01 < chance)
        {
            // 被打穿：伤害几乎全吃，耐久掉得少一些
            float loss = damage * 0.08f * relK;
            durability = Mathf.Max(0.0f, durability - loss);
            return new Result(damage * (chance / 2.0f + 0.5f), loss, false);
        }

        // 被挡住：只吃 15%，但耐久掉得更多
        float blockedLoss = damage * 0.10f * relK;
        durability = Mathf.Max(0.0f, durability - blockedLoss);
        return new Result(damage * 0.15f, blockedLoss, true);
    }
}

