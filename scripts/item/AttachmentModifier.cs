using Godot;

/// <summary>
/// 配件上的**一条属性修正**（数据层壳子）。
///
/// 为什么要这个壳：Godot 的 <c>[Export]</c> **不支持 Dictionary**，
/// 所以"属性名 → 修正器"的映射只能导出成数组，每项一个本类实例。
/// </summary>
[GlobalClass]
public partial class AttachmentModifier : Resource
{
    /// <summary>属性名，必须是 <see cref="StatNames"/> 里的 7 个之一。</summary>
    [Export] public string StatName { get; set; } = StatNames.Inaccuracy;

    /// <summary>加数（默认 0）。</summary>
    [Export] public float Addend { get; set; } = 0.0f;

    /// <summary>乘数（默认 1）。</summary>
    [Export] public float Multiplier { get; set; } = 1.0f;

    /// <summary>转成计算用的修正器。</summary>
    public StatModifier ToModifier() => new(Addend, Multiplier);

    public bool IsValid => StatNames.IsValid(StatName);

    public override string ToString() => $"{StatName} {ToModifier()}";
}
