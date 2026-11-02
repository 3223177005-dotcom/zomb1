using Godot;

/// <summary>
/// **近战武器挥动表现**（泰拉瑞亚式）：只在一次挥动期间显示武器贴图，
/// 绕角色出手点扫过「攻击判定」的那段圆弧；挥完立即隐藏。
///
/// 与判定的一致性：角度范围直接取 <see cref="MeleeData.HalfArcRadians"/>，
/// 半径直接取 <see cref="MeleeData.Reach"/>，所以视觉扫过的扇形 == 逻辑判定的扇形。
/// 判定本身仍在 <see cref="MeleeWeapon.CollectTargets"/>（瞬间判定），本类只负责画。
/// </summary>
public partial class MeleeVisual : Node2D
{
    /// <summary>一次挥动的时长（秒）。默认与 <see cref="MeleeWeapon.SwingVisualTime"/> 一致。</summary>
    [Export] public float SwingTime { get; set; } = 0.15f;

    /// <summary>刀身贴图相对目标长度的缩放（32px 图标 → 世界像素）。</summary>
    [Export] public float BladeScale { get; set; } = 1.5f;

    /// <summary>图标本身的朝向补偿：MSX 物品图标是 45° 斜放，+45° 转到「朝右」。</summary>
    [Export] public float BladeAngleOffset { get; set; } = Mathf.Pi * 0.25f;

    private Sprite2D _blade;
    private float _left;
    private float _from;
    private float _to;
    private float _radius;
    private bool _mirror;

    /// <summary>是否正在挥。</summary>
    public bool Swinging => _left > 0.0f;

    public override void _Ready()
    {
        _blade = new Sprite2D
        {
            Name = "Blade",
            Texture = GD.Load<Texture2D>("res://assets/sprites/items32/dagger.png"),
            Centered = true,
            Visible = false,
        };
        AddChild(_blade);
        Visible = false;
    }

    /// <summary>开始一次挥动：从 aimDir-半弧 扫到 aimDir+半弧。</summary>
    public void PlaySwing(Vector2 aimDir, float reach, float halfArcRadians)
    {
        if (_blade?.Texture == null) return;

        var dir = aimDir.LengthSquared() < 0.0001f ? Vector2.Right : aimDir.Normalized();
        float center = dir.Angle();

        _from = center - halfArcRadians;
        _to = center + halfArcRadians;
        _mirror = dir.X < 0.0f;
        _left = Mathf.Max(SwingTime, 0.01f);

        // 刀尖落在 reach 上：中心距 = reach - 半个刀长
        float halfLen = _blade.Texture.GetSize().X * BladeScale * 0.5f;
        _radius = Mathf.Max(reach - halfLen, reach * 0.35f);

        _blade.Scale = new Vector2(BladeScale, BladeScale);
        Visible = true;
        _blade.Visible = true;
        Apply(0.0f);
    }

    /// <summary>立刻收刀（换武器 / 死亡 / 打断时调用）。</summary>
    public void Cancel()
    {
        _left = 0.0f;
        Visible = false;
        if (_blade != null) _blade.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (_left <= 0.0f) return;

        _left -= (float)delta;
        if (_left <= 0.0f)
        {
            Cancel();
            return;
        }
        Apply(1.0f - _left / SwingTime);
    }

    private void Apply(float t)
    {
        float a = Mathf.Lerp(_from, _to, t);
        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));

        _blade.Position = dir * _radius;
        _blade.Rotation = a + BladeAngleOffset;
        // 朝左时竖直镜像，避免刀背朝上/刀刃朝上颠倒
        _blade.Scale = new Vector2(BladeScale, _mirror ? -BladeScale : BladeScale);
        _blade.ZIndex = 1;
    }
}
