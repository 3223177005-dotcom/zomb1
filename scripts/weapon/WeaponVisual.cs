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

    /// <summary>配件叠加层（枪 Sprite 的子节点，自动继承缩放/旋转/后坐）。</summary>
    private WeaponModOverlay _mods;

    /// <summary>换弹 / 切枪的姿态曲线（纯数学，见 scripts/combat/WeaponPoseModel.cs）。</summary>
    private readonly WeaponPoseModel _pose = new();

    /// <summary>本帧的基础朝向角（不含姿态偏移），由 AimAt / ApplyCdda 写入。</summary>
    private float _baseAngle;

    /// <summary>枪精灵的世界缩放：ZS 素材 0.8（= ZS 的 0.4 × 本项目 16→32px 的 2 倍），CDDA 素材 2.0。</summary>
    private float _gunScale = 2.0f;

    private const float ZsWeaponScale = 0.8f;

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

        _mods = new WeaponModOverlay { Name = "Mods" };
        _gun.AddChild(_mods);      // 挂在枪下面：跟着枪一起 2×、旋转、后坐

        _flash = new Sprite2D { Name = "Flash", Centered = true, Visible = false, Scale = new Vector2(2.0f, 2.0f) };
        AddChild(_flash);

        SetAnim("idle");
        ApplyGunOffset();
    }

    /// <summary>
    /// 握把在枪贴图上的横向位置比例（0~1）。CDDA 素材按长度 35%；导入 ZS 素材时填 ZS 的
    /// 原点比例（例如 SVD 是 18/80 = 0.225），这样 ZS 的像素挂点就能直接用。
    /// </summary>
    [Export] public float GripFractionX { get; set; } = 0.35f;

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

        // ZS 素材（id 带 zs: 前缀）：贴图在 weapons_cdda/zs/<名>_base.png，
        // 握把点 = ZS 精灵原点，挂点用 ZsMountTable 里的像素值。
        ZsMountTable.TryGet(itemId, out var zsEntry);
        string path = zsEntry != null
            ? ZsMountTable.ArtPath(itemId)
            : $"res://assets/sprites/weapons_cdda/{itemId}.png";

        var tex = GD.Load<Texture2D>(path);
        if (tex == null || tex == _cddaTex) return;

        _cddaTex = tex;
        var size = tex.GetSize();
        var centre = new Vector2(size.X * 0.5f, size.Y * 0.5f);
        // 非 ZS 素材（CDDA）：握把取长度 35% 处、垂直居中，世界缩放 2×
        var grip = new Vector2(size.X * GripFractionX, size.Y * 0.5f);

        // ZS 素材按 ZS 的写法（class_player_weapon.func_draw）：
        //   缩放 0.4 是 16px 世界的值 → 本项目 32px 世界 = 0.8
        //   旋转支点 = sprite_ingame 的原点（不是 inv 的原点）
        //   组合图自身以 sprite_inv（原点 0,0）为坐标原点
        _gunScale = zsEntry != null ? ZsWeaponScale : 2.0f;
        _cddaOffset = zsEntry != null ? centre - zsEntry.Pivot : centre - grip;

        // 枪口 = ZS 的 brake 槽位置（没有 brake 数据就用贴图右端）；
        // 换算到节点局部：贴图像素 − 支点，再乘枪的缩放。CDDA 素材沿用"贴图右端"。
        Vector2 muzzleTex = size;
        if (zsEntry != null)
        {
            muzzleTex = zsEntry.Slots.TryGetValue("brake", out var brake)
                ? brake.Offset
                : new Vector2(size.X, zsEntry.Pivot.Y);
        }
        _cddaMuzzle = zsEntry != null
            ? (muzzleTex - zsEntry.Pivot) * _gunScale
            : new Vector2(size.X - grip.X, 0.0f) * _gunScale;

        // 配件叠加层：ZS 素材用像素挂点（原点 0,0 即组合图坐标原点），其它按比例
        // ZS：节点原点 = 旋转支点（ingame 原点），组合图坐标原点 = inv 原点 (0,0)
        _mods?.Setup(tex, zsEntry != null ? zsEntry.Pivot : grip, zsEntry != null ? Vector2.Zero : grip);
        _mods?.Refresh(WeaponMods.Active);   // 换枪后重建配件叠加层
    }

    /// <summary>枪口的世界坐标（子弹从这个点出膛；没有贴图时退回自身原点）。</summary>
    public Vector2 MuzzleGlobal => ToGlobal(_cddaMuzzle);

    /// <summary>配件叠加层（自测 / 调试用）。</summary>
    public WeaponModOverlay Mods => _mods;

    /// <summary>配件增删后手动刷新叠加层（改枪面板装/卸时调用）。</summary>
    public void RefreshMods() => _mods?.Refresh(WeaponMods.Active);

    /// <summary>切枪甩枪：时长由 ergo 缩短（ZS 的 switching_weapon_max 同款公式）。</summary>
    public void PlaySwitch(float seconds) => _pose.StartSwitch(seconds);

    /// <summary>CDDA 贴图的统一摆法：给定角度 + 是否竖直镜像。</summary>
    private void ApplyCdda(float angle, bool mirror)
    {
        _baseAngle = angle;
        Rotation = angle;
        Scale = new Vector2(1.0f, mirror ? -1.0f : 1.0f);

        if (_gun.Texture != _cddaTex)
        {
            _gun.Texture = _cddaTex;
            _gun.Hframes = 1;
            _gun.Frame = 0;
        }
        _gun.FlipH = false;
        _gun.Scale = new Vector2(_gunScale, _gunScale);
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
        _baseAngle = Rotation;

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

    public override void _Process(double delta)
    {
        _pose.Update((float)delta);

        // ---- CDDA 贴图：没有逐帧动画，只处理后坐回弹 ----
        if (UseCddaGun && _cddaTex != null)
        {
            float dtc = (float)delta;
            if (_shootLeft > 0.0f) _shootLeft = Mathf.Max(0.0f, _shootLeft - dtc);
            if (_reloadLeft > 0.0f && _anim == "reload") _reloadLeft = Mathf.Max(0.0f, _reloadLeft - dtc);
            _recoil = Mathf.MoveToward(_recoil, 0.0f, dtc * 60.0f);

            if (_gun != null)
            {
                // 姿态（换弹抬枪 / 切枪甩枪）叠加在正常瞄准角与后坐位移上
                Rotation = _baseAngle + Mathf.DegToRad(_pose.AngleOffsetDeg);
                _gun.Position = new Vector2(-_recoil, 0.0f) + _pose.LocalOffset;
                // 火焰贴图前几帧的火根不在最左侧，往枪管里压 3px 才贴住枪口
                _flash.Position = _cddaMuzzle - new Vector2(3.0f + _recoil, 0.0f) + _pose.LocalOffset;
            }
            return;
        }

        if (_gun?.Texture == null) return;

        // 三方向旧贴图同样叠姿态（位置由 ApplyGunOffset 决定，这里只叠角度）
        Rotation = _baseAngle + Mathf.DegToRad(_pose.AngleOffsetDeg);

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
