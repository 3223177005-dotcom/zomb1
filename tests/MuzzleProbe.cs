using Godot;

/// <summary>
/// 枪口位置探针：验证子弹出膛点 = 枪贴图的 brake 槽位置。
/// Godot --headless --path . res://tests/MuzzleProbe.tscn
///
/// 判据：外层节点在 (400,300)、AimAt 会把视觉层移到 HandAnchor(0,9)、缩放 0.8、支点 (18,16)、brake 挂点 (62,14)
/// 那么朝右时枪口 = (400 + (62−18)*0.8, 309 + (14−16)*0.8) = (435.2, 307.4)。
/// </summary>
public partial class MuzzleProbe : Node2D
{
    public override void _Ready()
    {
        // WeaponVisual 会把自身 Position 覆盖成 HandAnchor，所以用外层节点摆位
        var holder = new Node2D { Name = "Holder", Position = new Vector2(400, 300) };
        AddChild(holder);
        var visual = new WeaponVisual { Name = "Probe" };
        holder.AddChild(visual);
        visual.SetGunItem("zs:svd");
        visual.AimAt(Vector2.Right);

        var muzzle = visual.MuzzleGlobal;
        GD.Print("gun global = ", visual.GlobalPosition);
        GD.Print("muzzle global = ", muzzle, "  期望 (435.2, 307.4)");

        bool okRight = Mathf.Abs(muzzle.X - 435.2f) < 0.6f && Mathf.Abs(muzzle.Y - 307.4f) < 0.6f;

        // 朝上（-90°）：枪口应转到支点上方
        visual.AimAt(Vector2.Up);
        Vector2 up = visual.MuzzleGlobal;
        GD.Print("aim up muzzle = ", up, "  期望 (398.4, 273.8)");
        bool okUp = Mathf.Abs(up.X - 398.4f) < 0.6f && Mathf.Abs(up.Y - 273.8f) < 0.6f;

        GD.Print(okRight && okUp ? "PASS 枪口位置正确" : "FAIL 枪口位置不对");
        GetTree().Quit(okRight && okUp ? 0 : 1);
    }
}


