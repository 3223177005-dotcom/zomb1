using Godot;

/// <summary>
/// **武器叠层**：挂在玩家身上，在角色贴图之上再画一把枪 + 枪口火焰。
///
/// 资源包的枪是**独立贴图**（`Gun_*` / `Fire_*`），角色本身手上是空的，
/// 所以必须用这一层把枪"贴"到手上。
///
/// 支持的动画：idle(持枪待机/跑) · shoot(开枪后坐) · reload(换弹) · death(掉枪) · fire(枪口火焰)
/// </summary>
public partial class WeaponVisual : Node2D
{
    /// <summary>武器的摆放方式。</summary>
    public enum AimStyle
    {
        /// <summary>旧的：按 down / up / side 三方向贴图 + 固定偏移。</summary>
        Directional,

        /// <summary>新的：绕握持点 360° 连续旋转（泰拉瑞亚式）。</summary>
        Rotate360,
    }

    /// <summary>当前摆放方式（由 <c>Player.WeaponFollowAim360</c> 决定）。</summary>
    public AimStyle Style { get; set; } = AimStyle.Rotate360;

    /// <summary>握持点相对玩家原点（脚底中心）的偏移，单位是世界像素。</summary>
    public Vector2 HandAnchor { get; set; } = new(0.0f, 9.0f);

    // 枪贴图是横向画的（side_*.png，单帧 20×11，2× 缩放）：后手握把在贴图 (6.5, 7.5)，
    // 中心在 (10, 5.5)。Offset 让握把正好落在节点原点上 —— 这样旋转中心就是"手"。
    private static readonly Vector2 GripOffset = new(3.5f, -2.0f);

    // 枪口（枪管前端）在节点局部坐标里的位置：((17.5,4.5) - 中心 + GripOffset) × 2× 缩放
    private static readonly Vector2 MuzzleLocal = new(22.0f, -6.0f);

    private Sprite2D _gun;
    private Sprite2D _flash;

    /// <summary>true = 用 CDDA 素材（`assets/sprites/weapons_cdda/<id>.png`，纯枪不带手）；
    /// false = 退回旧的 6 帧「手握枪」贴图。</summary>
    [Export] public bool UseCddaGun { get; set; } = true;

    private Texture2D _cddaTex;
    private Vector2 _cddaOffset;     // _gun.Offset：让握把落在节点原点
    private Vector2 _cddaMuzzle;     // 枪口（节点局部坐标，已含 2× 缩放）
    private float _recoil;           // 后坐位移（局部 -X），没有开枪帧就用它顶替

    private string _dir = "down";
    private string _anim = "idle";
    private int _frames;

    private float _animTime;
    private float _shootLeft;
    private float _reloadLeft;
    private float _reloadTotal;

    /// <summary>换弹动画是否正在播。</summary>
    public bool Reloading => _reloadLeft > 0.0f;

    /// <summary>开枪后坐 / 枪口火焰是否正在播（自测用：只有真射出子弹才会是 true）。</summary>
    public bool Shooting => _shootLeft > 0.0f;

    public override void _Ready()
    {
        _gun = new Sprite2D { Name = "Gun", Centered = true, Scale = new Vector2(2.0f, 2.0f) };
        AddChild(_gun);

        _flash = new Sprite2D { Name = "Flash", Centered = true, Visible = false, Scale = new Vector2(2.0f, 2.0f) };
        AddChild(_flash);

        SetAnim("idle");
        ApplyGunOffset();
    }

    /// <summary>
    /// 按物品 id 换 CDDA 枪贴图（`weapons_cdda/<id>.png`）。找不到就退回旧的手握枪贴图。
    /// </summary>
    public void SetGunItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            _cddaTex = null;
            return;
        }
        var tex = GD.Load<Texture2D>($"res://assets/sprites/weapons_cdda/{itemId}.png");
        if (tex == null || tex == _cddaTex) return;

        _cddaTex = tex;
        var size = tex.GetSize();
        // 贴图已在工具里裁到枪身外框：握把取长度 35% 处、垂直居中；枪口取右端
        var centre = new Vector2(size.X * 0.5f, size.Y * 0.5f);
        var grip = new Vector2(size.X * 0.35f, size.Y * 0.5f);
        _cddaOffset = centre - grip;
        _cddaMuzzle = new Vector2(size.X - grip.X, 0.0f) * 2.0f;   // 2× 缩放后的局部坐标
    }

    /// <summary>CDDA 贴图的统一摆法：给定角度 + 是否竖直镜像。</summary>
    private void ApplyCdda(float angle, bool mirror)
    {
        Rotation = angle;
        Scale = new Vector2(1.0f, mirror ? -1.0f : 1.0f);

        if (_gun.Texture != _cddaTex)
        {
            _gun.Texture = _cddaTex;
            _gun.Hframes = 1;
            _gun.Frame = 0;
        }
        _gun.FlipH = false;
        _gun.Scale = new Vector2(2.0f, 2.0f);
        _gun.Offset = _cddaOffset;
        _gun.Position = new Vector2(-_recoil, 0.0f);        // 后坐：沿枪身往回缩

        _flash.FlipH = false;
        _flash.Offset = new Vector2(6.0f, 0.0f);
        // 火焰贴图前几帧的火根不在最左侧，往枪管里压 3px 才贴住枪口
        _flash.Position = _cddaMuzzle - new Vector2(3.0f + _recoil, 0.0f);
    }

    /// <summary>
    /// 360° 模式下：把武器转到 <paramref name="aimDir"/> 方向。
    /// 只影响表现，不参与任何射击 / 命中判定（那些仍用 <c>Player.GetAimDirection()</c>）。
    /// </summary>
    public void AimAt(Vector2 aimDir)
    {
        if (_gun == null) return;
        if (aimDir.LengthSquared() < 0.000001f) return;

        aimDir = aimDir.Normalized();

        if (UseCddaGun && _cddaTex != null)
        {
            Position = HandAnchor;
            ApplyCdda(aimDir.Angle(), aimDir.X < 0.0f);
            return;
        }

        if (Style != AimStyle.Rotate360 || _gun.Texture == null) return;

        // 360° 只保留 side 那套贴图（枪是横向画的），其余角度全靠旋转
        if (_dir != "side")
        {
            _dir = "side";
            SetAnim(_anim);
        }

        Rotation = aimDir.Angle();

        // 朝左时竖直镜像：否则枪会「倒着拿」（180° 旋转后枪的上面朝下）
        Scale = new Vector2(1.0f, aimDir.X < 0.0f ? -1.0f : 1.0f);

        Position = HandAnchor;

        _gun.FlipH = false;
        _flash.FlipH = false;
        _gun.Position = Vector2.Zero;
        _gun.Offset = GripOffset;        // 握把 = 旋转中心
        // 火焰贴图里"火根"在单帧 (4,5.5)，帧中心是 (10,5.5) —— 不补这 6 像素，
        // 火苗会缩在枪管里面（看起来没在枪口）。
        _flash.Offset = new Vector2(6.0f, 0.0f);
        _flash.Position = MuzzleLocal;   // 枪焰根部对齐枪管前端，跟着一起转
    }

    /// <summary>设置朝向（down / up / side）。side 用 <see cref="SetFlip"/> 区分左右。</summary>
    public void SetDirection(string direction)
    {
        if (direction == _dir) return;

        _dir = direction;
        SetAnim(_anim == "death" ? "death" : _anim);
        ApplyGunOffset();
        ApplyFlashOffset();      // ★ 转向后枪焰位置也要跟着变
    }

    /// <summary>朝左时水平翻转。</summary>
    public void SetFlip(bool flip)
    {
        _gun.FlipH = flip;
        _flash.FlipH = flip;
        ApplyGunOffset();
        ApplyFlashOffset();      // ★ 左右翻转后枪焰位置也要跟着变
    }

    /// <summary>开枪：播 shoot 3 帧 + 枪口火焰 3 帧（CDDA 贴图没有开枪帧，用后坐位移顶替）。</summary>
    public void PlayShoot()
    {
        if (_shootLeft > 0.0f) return;      // 一次开枪只播一遍

        _shootLeft = 3.0f / 20.0f;          // 3 帧 @ 20fps = 0.15 秒
        PlayFlash();

        if (UseCddaGun && _cddaTex != null)
        {
            _recoil = 3.0f;
            return;
        }
        SetAnim("shoot");
    }

    /// <summary>换弹：把 reload 的 8 帧**拉长到 duration 秒**，和换弹时间同步。</summary>
    public void PlayReload(float duration)
    {
        _reloadTotal = Mathf.Max(0.15f, duration);
        _reloadLeft = _reloadTotal;
        SetAnim("reload");
    }

    /// <summary>死亡：播掉枪动画。</summary>
    public void PlayDeath()
    {
        SetAnim("death");
        _flash.Visible = false;
    }

    private void PlayFlash()
    {
        // CDDA 贴图是横向的，火焰必须用 side 那套；否则会拿到 down_fire（竖向）贴在横枪上
        string dir = (UseCddaGun && _cddaTex != null) ? "side" : _dir;
        var tex = GD.Load<Texture2D>($"res://assets/sprites/characters/weapon/{dir}_fire.png");
        if (tex == null) return;

        _flash.Texture = tex;
        _flash.Hframes = 3;
        _flash.Frame = 0;
        _flash.Visible = true;
        ApplyFlashOffset();

        // 0.15 秒后自动隐藏
        var timer = GetTree().CreateTimer(0.15);
        timer.Timeout += () => { if (IsInstanceValid(_flash)) _flash.Visible = false; };
    }

    private void SetAnim(string action)
    {
        // death 只有 side 有 —— 其它方向退回 idle
        string dir = _dir;
        var tex = GD.Load<Texture2D>($"res://assets/sprites/characters/weapon/{dir}_{action}.png");
        if (tex == null && action == "death")
        {
            dir = "side";
            action = "death";
            tex = GD.Load<Texture2D>($"res://assets/sprites/characters/weapon/{dir}_{action}.png");
        }
        if (tex == null) return;

        int frames = action switch
        {
            "shoot" or "fire" => 3,
            "reload" => 8,
            "death" => 6,
            _ => 6,
        };

        _anim = action;
        _animTime = 0.0f;
        _frames = frames;

        _gun.Texture = tex;
        _gun.Hframes = frames;
        _gun.Frame = 0;
        _gun.FlipH = dir == "side" && _gun.FlipH;
    }

    /// <summary>枪相对角色中心的位置（按朝向不同）。</summary>
    private void ApplyGunOffset()
    {
        // CDDA 贴图：旧的三方向模式也用同一张图，只是角度吸附到 上/下/侧
        if (UseCddaGun && _cddaTex != null)
        {
            float angle = _dir switch
            {
                "up" => -Mathf.Pi * 0.5f,
                "down" => Mathf.Pi * 0.5f,
                _ => _gun.FlipH ? Mathf.Pi : 0.0f,
            };
            Position = HandAnchor;
            ApplyCdda(angle, _gun.FlipH);
            return;
        }

        if (Style == AimStyle.Rotate360) return;    // 由 AimAt 接管

        // 相对**角色可见中心**的手部位置
        _gun.Position = _dir switch
        {
            "up" => new Vector2(0, -4),
            "side" => new Vector2(_gun.FlipH ? -14 : 14, 6),
            _ => new Vector2(0, 8),
        };
        _gun.Offset = Vector2.Zero;
    }

    private void ApplyFlashOffset()
    {
        if (_flash == null || Style == AimStyle.Rotate360) return;   // 由 AimAt 接管

        // 枪口位置 = 枪的前端
        _flash.Position = _dir switch
        {
            "up" => new Vector2(0, -26),
            // 朝右时枪口稍高一点（实测偏下，上移 3px）
            "side" => new Vector2(_flash.FlipH ? -32 : 32, 0),   // 左右同高，都对齐枪管
            _ => new Vector2(0, 20),
        };
    }

    public override void _Process(double delta)
    {
        // ---- CDDA 贴图：没有逐帧动画，只处理后坐回弹 ----
        if (UseCddaGun && _cddaTex != null)
        {
            float dtc = (float)delta;
            if (_shootLeft > 0.0f) _shootLeft = Mathf.Max(0.0f, _shootLeft - dtc);
            if (_reloadLeft > 0.0f && _anim == "reload") _reloadLeft = Mathf.Max(0.0f, _reloadLeft - dtc);
            _recoil = Mathf.MoveToward(_recoil, 0.0f, dtc * 60.0f);

            if (_gun != null)
            {
                _gun.Position = new Vector2(-_recoil, 0.0f);
                // 火焰贴图前几帧的火根不在最左侧，往枪管里压 3px 才贴住枪口
        _flash.Position = _cddaMuzzle - new Vector2(3.0f + _recoil, 0.0f);
            }
            return;
        }

        if (_gun?.Texture == null) return;

        float dt = (float)delta;
        _animTime += dt;

        // 换弹：按总时长临时决定帧率，播完回 idle
        if (_reloadLeft > 0.0f && _anim == "reload")
        {
            _reloadLeft -= dt;
            float fps = _frames / _reloadTotal;
            _gun.Frame = Mathf.Min((int)(_animTime * fps), _frames - 1);

            if (_reloadLeft <= 0.0f) { SetAnim("idle"); ApplyGunOffset(); }
            return;
        }

        // 开枪后坐：3 帧播完回 idle
        if (_shootLeft > 0.0f && _anim == "shoot")
        {
            _shootLeft -= dt;
            _gun.Frame = Mathf.Min((int)(_animTime * 20.0f), _frames - 1);

            if (_shootLeft <= 0.0f) { SetAnim("idle"); ApplyGunOffset(); }
            return;
        }

        // idle：6 帧循环
        if (_anim == "idle")
            _gun.Frame = (int)(_animTime * 8.0f) % _frames;
    }
}
