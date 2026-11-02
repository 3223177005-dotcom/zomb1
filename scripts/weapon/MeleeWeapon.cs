using Godot;
using System.Collections.Generic;

/// <summary>
/// 近战武器：朝鼠标方向挥出扇形，命中范围内所有敌人（群攻）。
///
/// 测试版简化点（后续再完善）：
/// - 即时判定，没有前摇 / 后摇分段；
/// - 没有体力消耗、击退、耐久消耗；
/// - 表现为一个短暂淡出的扇形轮廓（没有美术资源）。
/// </summary>
public partial class MeleeWeapon : Node2D
{
    /// <summary>挥砍完成时发出（方向为归一化朝向，hitCount 为本次命中数）。</summary>
    [Signal] public delegate void SwungEventHandler(Vector2 origin, Vector2 direction, int hitCount);

    [Export] public MeleeData MeleeData { get; set; }

    /// <summary>可命中的物理层（默认 4 = 第 3 层，即丧尸所在层）。</summary>
    [Export] public uint TargetMask { get; set; } = 4;

    /// <summary>扇形轮廓的显示时长（秒）。</summary>
    [Export] public float SwingVisualTime { get; set; } = 0.15f;

    private float _cooldown = 0.0f;
    private float _swingTimer = 0.0f;
    private Vector2 _swingDir = Vector2.Right;

    /// <summary>是否已冷却完毕、可以挥砍。</summary>
    public bool IsReady => _cooldown <= 0.0f && MeleeData != null;

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (_cooldown > 0.0f)
            _cooldown = Mathf.Max(_cooldown - dt, 0.0f);

        if (_swingTimer > 0.0f)
        {
            _swingTimer = Mathf.Max(_swingTimer - dt, 0.0f);
            QueueRedraw();
        }
    }

    /// <summary>尝试挥砍一次，返回本次实际命中的目标数。</summary>
    public int TryAttack(Vector2 origin, Vector2 aimDir)
    {
        if (MeleeData == null || _cooldown > 0.0f)
            return 0;

        var dir = aimDir.LengthSquared() < 0.001f ? Vector2.Right : aimDir.Normalized();

        _cooldown = MeleeData.Cooldown;
        _swingDir = dir;
        _swingTimer = SwingVisualTime;
        QueueRedraw();

        var targets = CollectTargets(origin, dir);
        foreach (var target in targets)
            target.TakeDamage(MeleeData.Damage);

        EmitSignal(SignalName.Swung, origin, dir, targets.Count);
        return targets.Count;
    }

    /// <summary>
    /// 扇形范围检测：距离 &lt;= Reach 且与瞄准方向夹角 &lt;= 半角。
    /// 用圆形物理查询先粗筛，再按角度过滤。
    /// </summary>
    private List<IDamageable> CollectTargets(Vector2 origin, Vector2 aimDir)
    {
        var result = new List<IDamageable>();
        if (MeleeData == null || MeleeData.Reach <= 0.0f)
            return result;

        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = new CircleShape2D { Radius = MeleeData.Reach },
            Transform = new Transform2D(0.0f, origin),
            CollisionMask = TargetMask,
            CollideWithBodies = true,
            CollideWithAreas = false,
        };

        var space = GetWorld2D().DirectSpaceState;
        float halfArc = MeleeData.HalfArcRadians;
        var candidates = new List<(float Distance, IDamageable Target)>();

        foreach (var hit in space.IntersectShape(query, 32))
        {
            var collider = hit["collider"].AsGodotObject();
            if (collider is not Node2D node) continue;
            if (node is not IDamageable damageable) continue;

            var toTarget = node.GlobalPosition - origin;
            float distance = toTarget.Length();
            if (distance > MeleeData.Reach) continue;

            // 贴脸时不判角度，避免方向抖动导致打空
            if (distance > 1.0f && Mathf.Abs(aimDir.AngleTo(toTarget)) > halfArc) continue;

            candidates.Add((distance, damageable));
        }

        // 近的优先：受 MaxTargets 限制时先打最近的
        candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        int limit = MeleeData.MaxTargets > 0 ? MeleeData.MaxTargets : candidates.Count;
        for (int i = 0; i < candidates.Count && i < limit; i++)
            result.Add(candidates[i].Target);

        return result;
    }

    /// <summary>绘制扇形轮廓（短暂淡出）。</summary>
    public override void _Draw()
    {
        if (MeleeData == null || _swingTimer <= 0.0f)
            return;

        float alpha = _swingTimer / Mathf.Max(SwingVisualTime, 0.001f);
        var color = new Color(1.0f, 1.0f, 0.9f, alpha * 0.85f);
        float halfArc = MeleeData.HalfArcRadians;
        float angle = _swingDir.Angle();

        DrawArc(Vector2.Zero, MeleeData.Reach, angle - halfArc, angle + halfArc, 20, color, 1.5f);
        DrawLine(Vector2.Zero, new Vector2(MeleeData.Reach, 0.0f).Rotated(angle - halfArc), color, 1.0f);
        DrawLine(Vector2.Zero, new Vector2(MeleeData.Reach, 0.0f).Rotated(angle + halfArc), color, 1.0f);
    }
}
