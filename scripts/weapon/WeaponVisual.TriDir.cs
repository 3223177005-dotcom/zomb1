using Godot;

/// <summary>
/// <see cref="WeaponVisual"/> 的「三方向旧贴图」部分：逐帧动画、枪/枪焰的朝向偏移。
/// 从 WeaponVisual.cs 拆出来只为守住 400 行上限（AGENTS 规则 1）。
/// </summary>
public partial class WeaponVisual
{
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
}
