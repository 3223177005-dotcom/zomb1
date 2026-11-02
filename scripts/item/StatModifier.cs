using Godot;
using System.Collections.Generic;

/// <summary>
/// 八个**配件槽类型**（学 TaCZ 的 `allow_attachment_types`，但去掉了枪托，
/// 并把激光拆成左右两个、另外补了枪管和枪身）。
///
/// 枪用 <c>WeaponData.Slots</c> 声明自己开放哪几个；**没写的一律关闭**。
/// </summary>
public static class SlotTypes
{
    /// <summary>枪口：消音器 / 制退器 / 补偿器。</summary>
    public const string Muzzle = "muzzle";

    /// <summary>枪管：长短枪管、材质。影响散布 / 瞄准速度。</summary>
    public const string Barrel = "barrel";

    /// <summary>瞄具：影响瞄准散布与开镜时间。</summary>
    public const string Scope = "scope";

    /// <summary>握把：影响腰射 / 瞄准散布。</summary>
    public const string Grip = "grip";

    /// <summary>扩容弹匣：影响弹匣容量。</summary>
    public const string ExtendedMag = "extended_mag";

    /// <summary>枪侧配件 1（原 laser）。</summary>
    public const string Laser1 = "laser_1";

    /// <summary>枪侧配件 2（原 laser）。</summary>
    public const string Laser2 = "laser_2";

    /// <summary>枪身：机匣 / 扳机组。影响换弹与散布。</summary>
    public const string Body = "body";

    /// <summary>全部合法槽类型（数据校验用）。</summary>
    public static readonly string[] All =
    {
        Muzzle, Barrel, Scope, Grip, ExtendedMag, Laser1, Laser2, Body,
    };

    /// <summary>是不是合法槽类型。</summary>
    public static bool IsValid(string slotType)
    {
        if (string.IsNullOrWhiteSpace(slotType)) return false;
        foreach (var t in All)
            if (t == slotType) return true;
        return false;
    }

    /// <summary>给 UI 显示的中文名。</summary>
    public static string DisplayName(string slotType) => slotType switch
    {
        Muzzle => "枪口",
        Barrel => "枪管",
        Scope => "瞄具",
        Grip => "握把",
        ExtendedMag => "扩容弹匣",
        Laser1 => "枪侧配件1",
        Laser2 => "枪侧配件2",
        Body => "枪身",
        _ => slotType,
    };
}

/// <summary>
/// 属性修正器 —— 枪和配件改数值的**唯一**手段（学 TaCZ）。
///
/// 合成规则：
/// <code>
///   最终值 = (基础值 + Σ所有修正器的 Addend) × Π所有修正器的 Multiplier
/// </code>
///
/// - <see cref="Addend"/> 全部**先相加**，当成一个乘区；
/// - <see cref="Multiplier"/> 全部**相乘**；
/// - 两个乘区再相乘。
///
/// 所以多个配件天然可叠加、可冲突，不需要任何"A 装了就改 B"的特判。
/// </summary>
public struct StatModifier
{
    /// <summary>加数（默认 0）。</summary>
    public float Addend;

    /// <summary>乘数（默认 1）。</summary>
    public float Multiplier;

    public StatModifier(float addend = 0.0f, float multiplier = 1.0f)
    {
        Addend = addend;
        Multiplier = multiplier;
    }

    /// <summary>只加不减的快捷构造。</summary>
    public static StatModifier Add(float value) => new(value, 1.0f);

    /// <summary>只乘不加的快捷构造。</summary>
    public static StatModifier Mul(float value) => new(0.0f, value);

    /// <summary>什么都不改。</summary>
    public static readonly StatModifier Identity = new(0.0f, 1.0f);

    /// <summary>是不是没有任何效果。</summary>
    public bool IsIdentity => Mathf.IsZeroApprox(Addend) && Mathf.IsZeroApprox(Multiplier - 1.0f);

    public override string ToString()
        => $"{(Addend >= 0.0f ? "+" : "")}{Addend:0.##} ×{Multiplier:0.###}";
}

/// <summary>修正器的合成工具。</summary>
public static class StatMath
{
    /// <summary>
    /// 把一组修正器作用到基础值上：
    /// <c>(baseValue + Σaddend) × Πmultiplier</c>。
    /// </summary>
    public static float Apply(float baseValue, IEnumerable<StatModifier> modifiers)
    {
        if (modifiers == null) return baseValue;

        float addend = 0.0f;
        float multiplier = 1.0f;

        foreach (var m in modifiers)
        {
            addend += m.Addend;
            multiplier *= m.Multiplier;
        }
        return (baseValue + addend) * multiplier;
    }

    /// <summary>把一组修正器塌缩成**一个等价修正器**（便于缓存 / 显示）。</summary>
    public static StatModifier Combine(IEnumerable<StatModifier> modifiers)
    {
        if (modifiers == null) return StatModifier.Identity;

        float addend = 0.0f;
        float multiplier = 1.0f;

        foreach (var m in modifiers)
        {
            addend += m.Addend;
            multiplier *= m.Multiplier;
        }
        return new StatModifier(addend, multiplier);
    }

    /// <summary>对<b>基础值 0</b> 的情况做一次作用（纯乘区效果，用来给 UI 显示）。</summary>
    public static float ApplyToUnit(IEnumerable<StatModifier> modifiers)
        => Apply(1.0f, modifiers) - 1.0f;
}
